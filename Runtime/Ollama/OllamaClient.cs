using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Manager;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// Load model progress information
    /// </summary>
    public class LoadModelProgress
    {
        /// <summary>
        /// Progress value (0.0 to 1.0)
        /// </summary>
        public double Progress { get; private set; }
        /// <summary>
        /// true if loading is completed (even if failed)
        /// </summary>
        public bool IsCompleted { get; private set; }
        /// <summary>
        /// true if loading succeeded
        /// </summary>
        public bool IsSuccessed { get; private set; }
        /// <summary>
        /// Message about the loading status
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="progress">Progress value</param>
        /// <param name="isCompleted">true if loading is completed (even if failed)</param>
        /// <param name="isSuccessed">true if loading succeeded</param>
        /// <param name="message">Message about the loading status</param>
        public LoadModelProgress(double progress, bool isCompleted, bool isSuccessed, string message)
        {
            Progress = progress;
            IsCompleted = isCompleted;
            IsSuccessed = isSuccessed;
            Message = message;
        }
    }

    /// <summary>
    /// Implementation of Ollama LLM client
    /// </summary>
    public class OllamaClient : IChatLLMClient
    {
        private readonly OllamaConfig _config;
        private readonly ChatHistoryManager _historyManager;
        private readonly HttpRequestHelper _httpHelper;
        private readonly ToolManager _toolManager;
        private readonly HashSet<string> _runningSessions = new();
        private readonly List<PendingRequest> _pendingRequests = new();
        private long _pendingSequence = 0;

        /// <summary>
        /// Ollama chat endpoint URL.
        /// </summary>
        private string ChatApiUrl => _config.ServerUrl + "/api/chat";

        /// <summary>
        /// Pending request information
        /// </summary>
        private class PendingRequest
        {
            public string SessionId { get; private set; }
            public int Priority { get; private set; }
            public long Order { get; private set; }

            public PendingRequest(string sessionId, int priority, long order)
            {
                SessionId = sessionId;
                Priority = priority;
                Order = order;
            }
        }

        /// <summary>
        /// Container for the result of one request-loop iteration.
        /// </summary>
        private sealed class RequestLoopIterationData
        {
            /// <summary>
            /// Assistant content generated in this iteration.
            /// </summary>
            public string AssistantContent { get; private set; }

            /// <summary>
            /// Tool calls detected in this iteration.
            /// </summary>
            public List<Core.ToolCall> ToolCalls { get; private set; }

            /// <summary>
            /// Finalization action when no tool call is detected.
            /// </summary>
            public Action FinalizeAction { get; private set; }

            /// <summary>
            /// True when the outer loop should abort.
            /// </summary>
            public bool ShouldAbort { get; private set; }

            /// <summary>
            /// Set successful iteration result.
            /// </summary>
            /// <param name="assistantContent">Assistant content</param>
            /// <param name="toolCalls">Detected tool calls</param>
            /// <param name="finalizeAction">Action to finalize the response when no tool calls exist</param>
            public void SetResult(string assistantContent, List<Core.ToolCall> toolCalls, Action finalizeAction)
            {
                AssistantContent = assistantContent;
                ToolCalls = toolCalls;
                FinalizeAction = finalizeAction;
                ShouldAbort = false;
            }

            /// <summary>
            /// Mark this iteration as aborted.
            /// </summary>
            public void Abort()
            {
                ShouldAbort = true;
            }
        }

        /// <summary>
        /// Insert pending request in sorted order
        /// the list is sorted by priority (desc) and order (asc)
        /// <param name="request">Pending request to insert</param>"
        /// </summary>

        private void InsertPendingSorted(PendingRequest request)
        {
            int index = _pendingRequests.FindIndex(r =>
                r.Priority < request.Priority ||
                (r.Priority == request.Priority && r.Order > request.Order));

            if (index >= 0)
            {
                _pendingRequests.Insert(index, request);
            }
            else
            {
                _pendingRequests.Add(request);
            }
        }

        /// <summary>
        /// Get the index of the next runnable request
        /// <param name="maxConcurrent">Maximum concurrent sessions</param>
        /// </summary>
        private int GetNextRunnableIndex(int maxConcurrent)
        {
            if (_runningSessions.Count >= maxConcurrent)
            {
                return -1;
            }

            int bestIndex = -1;
            for (int i = 0; i < _pendingRequests.Count; i++)
            {
                var candidate = _pendingRequests[i];
                if (_runningSessions.Contains(candidate.SessionId))
                {
                    continue;
                }

                if (bestIndex < 0)
                {
                    bestIndex = i;
                    continue;
                }

                var best = _pendingRequests[bestIndex];
                if (candidate.Priority > best.Priority ||
                    (candidate.Priority == best.Priority && candidate.Order < best.Order))
                {
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Wait for turn to send request
        /// If the request is not allowed to wait and the client is busy, invoke onError and exit
        /// Otherwise wait until it's this request's turn or cancelled
        /// </summary>
        /// <param name="sessionId">Session ID of the request</param>
        /// <param name="options">Chat request options</param>
        /// <param name="onError">Error callback</param>
        /// <returns></returns>
        private IEnumerator WaitForTurn(string sessionId, ChatRequestOptions options, Action<ChatError> onError)
        {
            int maxConcurrent = Mathf.Max(1, _config.MaxConcurrentSessions);

            if (!options.WaitIfBusy)
            {
                if (_pendingRequests.Count > 0 || _runningSessions.Contains(sessionId) || _runningSessions.Count >= maxConcurrent)
                {
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = "Client is busy"
                    });
                    yield break;
                }

                yield break;
            }

            var pending = new PendingRequest(sessionId, options.Priority, ++_pendingSequence);
            InsertPendingSorted(pending);

            while (true)
            {
                if (options.CancellationToken.IsCancellationRequested)
                {
                    _pendingRequests.Remove(pending);
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.Cancelled,
                        Message = $"Request cancelled for session '{sessionId}' by user"
                    });
                    yield break;
                }

                maxConcurrent = Mathf.Max(1, _config.MaxConcurrentSessions);
                int nextIndex = GetNextRunnableIndex(maxConcurrent);
                if (nextIndex >= 0 && ReferenceEquals(_pendingRequests[nextIndex], pending))
                {
                    _pendingRequests.RemoveAt(nextIndex);
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Global system prompt
        /// </summary>
        public string GlobalSystemPrompt { get; set; } = "You are a helpful AI assistant.";

        /// <summary>
        /// Initialize OllamaClient
        /// If config is null, default settings will be used
        /// <param name="config">Ollama configuration</param>
        /// </summary>
        public OllamaClient(OllamaConfig config = null)
        {
            _config = config ?? new OllamaConfig();
            _historyManager = new ChatHistoryManager();
            _httpHelper = new HttpRequestHelper(_config);
            _toolManager = new ToolManager(_config.DebugMode);
        }

        /// <summary>
        /// Clear all message history
        /// </summary>
        public void ClearAllMessages() => _historyManager.ClearAll();

        /// <summary>
        /// Clear message history for the specified sessionId
        /// <param name="sessionId">Session ID</param>
        /// </summary>
        public void ClearMessages(string sessionId) => _historyManager.Clear(sessionId);

        /// <summary>
        /// Load model as runnable
        /// </summary>
        /// <param name="modelName">Name of model. e.g. mistral or kamekichi128/qwen4-4b-instruct-2507</param>
        /// <param name="timeoutSecondsForWarmup">Time out in seconds for model warmup (after model is available). The default value is ollamaConfigValue. If the model is not warmed up within the time out, it will be considered as failed.</param>
        /// <param name="progressCallback">Load progress callback</param>
        /// <param name="pullIfModelNotAvailable">pull model if not available</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public IEnumerator LoadModelRunnable(string modelName, float timeoutSecondsForWarmup = 0.0f, Action<LoadModelProgress> progressCallback = null, bool pullIfModelNotAvailable = false, CancellationToken cancellationToken = default)
        {
            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Load {modelName} started...");
            }

            // Check model availability via /api/show
            var showRequestContent = new
            {
                model = modelName
            };

            string showJson = Newtonsoft.Json.JsonConvert.SerializeObject(showRequestContent);
            string showUrl = _config.ServerUrl + "/api/show";

            bool modelAvailable = false;
            bool pullModel = false;

            yield return _httpHelper.ExecuteWithRetry(
                showUrl,
                showJson,
                responseBody =>
                {
                    // Model is available
                    if (_config.DebugMode)
                    {
                        UnityEngine.Debug.Log($"[Ollama] Model {modelName} is available.");
                    }
                    progressCallback?.Invoke(new(0.5, false, true, $"Model '{modelName}' is available."));
                    modelAvailable = true;
                },
                error =>
                {
                    // if error is 404, model is not available
                    // otherwise, some other error occurred
                    if (error.HttpStatus != 404)
                    {
                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Failed to check model {modelName}: {error.Message}");
                        }
                        progressCallback?.Invoke(new(1.0, true, false, $"Failed to check model '{modelName}': {error.Message}"));
                    }
                    else if (pullIfModelNotAvailable)
                    {
                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Model {modelName} is not available. Starting to pull...");
                        }
                        progressCallback?.Invoke(new(0.0, false, false, $"Model '{modelName}' is not available. Starting to pull..."));
                        pullModel = true;
                    }
                    else
                    {
                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Model {modelName} is not available. Failed to load.");
                        }
                        // Model not available and not pulling
                        progressCallback?.Invoke(new(1.0, true, false, $"Model '{modelName}' is not available. Failed to load."));
                    }
                },
                cancellationToken
            );

            if (modelAvailable)
            {
                // Warm up model by sending a short message
                yield return WarmupModel(modelName, timeoutSecondsForWarmup, progressCallback);
            }
            else if (pullModel)
            {
                // Pulling model in progress
                yield return PullModel(modelName, timeoutSecondsForWarmup, progressCallback, cancellationToken);
            }
        }

        /// <summary>
        /// Warm up model by sending a short message
        /// </summary>
        /// <param name="modelName">Name of model</param>
        /// <param name="timeoutSecondsForWarmup">Time out in seconds for model warmup</param>
        /// <param name="progressCallback">Progress callback</param>
        private IEnumerator WarmupModel(string modelName, float timeoutSecondsForWarmup, Action<LoadModelProgress> progressCallback)
        {
            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Model {modelName} warm up start.");
            }
            // temporay overwrite config to set timeout for warmup request
            float previousTimeout = _config.HttpTimeoutSeconds;
            if (timeoutSecondsForWarmup > 0)
            {
                _config.HttpTimeoutSeconds = timeoutSecondsForWarmup;
            }
            yield return SendMessageAsync(
                "Please say hello",
                response => progressCallback?.Invoke(new(1.0, true, true, $"Model '{modelName}' is loaded and runnable.")),
                error => progressCallback?.Invoke(new(1.0, true, false, $"Failed to run model '{modelName}': {error.Message}")),
                new ChatRequestOptions
                {
                    ModelName = modelName,
                }
            );
            // restore config
            _config.HttpTimeoutSeconds = previousTimeout;
        }

        /// <summary>
        /// Pull model via /api/pull
        /// </summary>
        /// <param name="modelName">Name of model</param>
        /// <param name="timeoutSecondsForWarmup">Time out in seconds for model warmup after pulling</param>
        /// <param name="progressCallback">Progress callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private IEnumerator PullModel(string modelName, float timeoutSecondsForWarmup, Action<LoadModelProgress> progressCallback, CancellationToken cancellationToken = default)
        {
            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Model {modelName} pull start.");
            }            // Pull model via /api/pull
            var pullRequestContent = new
            {
                model = modelName
            };
            string pullJson = Newtonsoft.Json.JsonConvert.SerializeObject(pullRequestContent);
            string pullUrl = _config.ServerUrl + "/api/pull";
            string lastRawChunk = "";
            double lastProgress = 0.0;
            bool pullSucceeded = false;
            yield return _httpHelper.ExecuteStreamingWithRetry(
                pullUrl,
                pullJson,
                chunk =>
                {
                    try
                    {
                        lastRawChunk = chunk;
                        var chunkJson = JObject.Parse(chunk);
                        var chunkStatus = chunkJson["status"];
                        var chunkTotal = chunkJson["total"];
                        var chunkCompleted = chunkJson["completed"];

                        if (chunkStatus != null && chunkTotal != null && chunkCompleted != null)
                        {
                            long total = chunkTotal.Value<long>();
                            long completed = chunkCompleted.Value<long>();
                            if (total == 0)
                            {
                                total = 1;
                            }
                            lastProgress = 0.9 * (double)completed / (double)total; // up to 90%, because of warmup later
                            progressCallback?.Invoke(new(lastProgress, false, true, $"Pulling model '{modelName}': {chunkStatus.Value<string>()} ({completed}/{total})"));
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.LogWarning($"[Ollama] Failed to parse chunk: {ex.Message}");
                        }
                    }
                },
                isSuccess =>
                {
                    progressCallback?.Invoke(new(lastProgress, false, true, $"Pulling model '{modelName}': finished."));
                    pullSucceeded = true;
                },
                error => progressCallback(new(lastProgress, true, false, $"Failed to pull model '{modelName}': {error.Message}")),
                cancellationToken
            );

            if (pullSucceeded)
            {
                yield return WarmupModel(modelName, timeoutSecondsForWarmup, progressCallback);
            }
        }

        /// <summary>
        /// Get session information
        /// <param name="sessionId">Session ID</param>
        /// </summary>
        public ChatSession GetSession(string sessionId)
        {
            return _historyManager.GetOrCreateSession(sessionId);
        }

        /// <summary>
        /// Get all session IDs
        /// </summary>
        public IEnumerable<string> GetAllSessionIds()
        {
            return _historyManager.GetAllSessionIds();
        }

        /// <summary>
        /// Check if a session exists
        /// <param name="sessionId">Session ID</param>
        /// </summary>
        public bool HasSession(string sessionId)
        {
            return _historyManager.HasSession(sessionId);
        }

        /// <summary>
        /// Get number of messages in the session
        /// <param name="sessionId">Session ID</param>
        /// </summary>
        public int GetSessionMessageCount(string sessionId)
        {
            if (!_historyManager.HasSession(sessionId))
            {
                return 0;
            }
            return _historyManager.GetHistory(sessionId).Count;
        }

        /// <summary>
        /// Set session's system prompt
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="systemPrompt">System prompt to set</param>
        public void SetSessionSystemPrompt(string sessionId, string systemPrompt)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
            }

            var session = _historyManager.GetOrCreateSession(sessionId);
            session.SystemPrompt = systemPrompt;

            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Session '{sessionId}' system prompt updated: {systemPrompt?.Substring(0, Math.Min(50, systemPrompt?.Length ?? 0))}...");
            }
        }

        /// <summary>
        /// Get session's system prompt
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <returns>System prompt, or null if the session does not exist</returns>
        public string GetSessionSystemPrompt(string sessionId)
        {
            if (!_historyManager.HasSession(sessionId))
            {
                return null;
            }

            var session = _historyManager.GetOrCreateSession(sessionId);
            return session.SystemPrompt;
        }

        /// <summary>
        /// Reset session's system prompt (to use the global prompt)
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        public void ResetSessionSystemPrompt(string sessionId)
        {
            if (!_historyManager.HasSession(sessionId))
            {
                if (_config.DebugMode)
                {
                    UnityEngine.Debug.LogWarning($"[Ollama] Session '{sessionId}' not found");
                }
                return;
            }

            var session = _historyManager.GetOrCreateSession(sessionId);
            session.SystemPrompt = null;

            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Session '{sessionId}' system prompt reset to global");
            }
        }

        /// <summary>
        /// Batch set system prompt for multiple sessions
        /// </summary>
        /// <param name="sessionIds">List of session IDs</param>
        /// <param name="systemPrompt">System prompt to set</param>
        public void SetSystemPromptForMultipleSessions(IEnumerable<string> sessionIds, string systemPrompt)
        {
            if (sessionIds == null)
            {
                throw new ArgumentNullException(nameof(sessionIds));
            }

            int count = 0;
            foreach (var sessionId in sessionIds)
            {
                SetSessionSystemPrompt(sessionId, systemPrompt);
                count++;
            }

            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] System prompt set for {count} sessions");
            }
        }

        /// <summary>
        /// Reset system prompt for all sessions
        /// </summary>
        public void ResetAllSessionSystemPrompts()
        {
            var sessionIds = _historyManager.GetAllSessionIds();
            int count = 0;

            foreach (var sessionId in sessionIds)
            {
                ResetSessionSystemPrompt(sessionId);
                count++;
            }

            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] System prompt reset for {count} sessions");
            }
        }

        /// <summary>
        /// Clear session messages and reset system prompt
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        public void ClearSessionWithPrompt(string sessionId)
        {
            _historyManager.Clear(sessionId);
            ResetSessionSystemPrompt(sessionId);

            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Session '{sessionId}' cleared with prompt reset");
            }
        }

        /// <summary>
        /// Prepare cancellation token by linking option and external tokens
        /// <param name="options">Chat request options</param>
        /// <param name="externalToken">External cancellation token</param>
        /// <param name="linkedSource">Output linked CancellationTokenSource (null if not linked)</param>
        /// </summary>
        private static CancellationToken PrepareCancellationToken(
            ChatRequestOptions options,
            CancellationToken externalToken,
            out CancellationTokenSource linkedSource)
        {
            linkedSource = null;

            var optionToken = options.CancellationToken;
            if (externalToken.CanBeCanceled)
            {
                if (optionToken.CanBeCanceled)
                {
                    linkedSource = CancellationTokenSource.CreateLinkedTokenSource(optionToken, externalToken);
                    options.CancellationToken = linkedSource.Token;
                }
                else
                {
                    options.CancellationToken = externalToken;
                }
            }

            return options.CancellationToken;
        }

        /// <summary>
        /// Build Ollama generation options payload from request options
        /// </summary>
        private object BuildOllamaGenerationOptions(ChatRequestOptions options)
        {
            var generationOptions = new Dictionary<string, object>
            {
                ["seed"] = options.Seed ?? _config.DefaultSeed
            };

            if (options.Temperature.HasValue)
            {
                generationOptions["temperature"] = options.Temperature.Value;
            }

            if (options.TopK.HasValue)
            {
                generationOptions["top_k"] = options.TopK.Value;
            }

            if (options.TopP.HasValue)
            {
                generationOptions["top_p"] = options.TopP.Value;
            }

            if (options.MinP.HasValue)
            {
                generationOptions["min_p"] = options.MinP.Value;
            }

            if (options.Stop != null && options.Stop.Count > 0)
            {
                generationOptions["stop"] = options.Stop;
            }

            if (options.NumCtx.HasValue)
            {
                generationOptions["num_ctx"] = options.NumCtx.Value;
            }

            if (options.NumPredict.HasValue)
            {
                generationOptions["num_predict"] = options.NumPredict.Value;
            }

            return generationOptions;
        }

        /// <summary>
        /// Prepare chat history for a new request.
        /// Adds system prompt (if needed) and user message.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="message">User message</param>
        /// <param name="images">Optional images</param>
        /// <param name="options">Chat request options</param>
        /// <returns>Mutable history list for this session</returns>
        private List<ChatMessage> PrepareHistoryForRequest(
            string sessionId,
            string message,
            List<Texture2D> images,
            ChatRequestOptions options)
        {
            var session = _historyManager.GetOrCreateSession(sessionId, options.SystemPrompt);
            var history = session.History;

            if (history.Count == 0)
            {
                string systemPrompt = options.SystemPrompt ?? session.SystemPrompt ?? GlobalSystemPrompt;
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    history.Add(new ChatMessage
                    {
                        Role = "system",
                        Content = systemPrompt
                    });
                }
            }

            history.Add(new ChatMessage
            {
                Role = "user",
                Content = message,
                Images = images?.Select(ConvertTexture2DToBase64).ToList()
            });

            return history;
        }

        /// <summary>
        /// Check cancellation and report cancellation error when requested.
        /// </summary>
        /// <param name="options">Chat request options</param>
        /// <param name="onError">Error callback</param>
        /// <returns>True if cancelled</returns>
        private static bool TryHandleCancellation(ChatRequestOptions options, Action<ChatError> onError)
        {
            if (!options.CancellationToken.IsCancellationRequested)
            {
                return false;
            }

            onError?.Invoke(new ChatError
            {
                ErrorType = LLMErrorType.Cancelled,
                Message = "Request cancelled"
            });
            return true;
        }

        /// <summary>
        /// Resolve the tool list to send with a request.
        /// </summary>
        /// <param name="options">Chat request options</param>
        /// <returns>Resolved tool definitions</returns>
        private List<Core.ToolDefinition> ResolveTools(ChatRequestOptions options)
        {
            var tools = _toolManager.GetAllTools();
            if (options.Tools != null && options.Tools.Count > 0)
            {
                tools = tools.Where(t => options.Tools.Contains(t.Name)).ToList();
            }

            return tools;
        }

        /// <summary>
        /// Build the payload object for Ollama /api/chat.
        /// </summary>
        /// <param name="history">Current message history</param>
        /// <param name="options">Chat request options</param>
        /// <param name="tools">Resolved tool definitions</param>
        /// <param name="stream">True for streaming mode</param>
        /// <returns>Serializable request payload</returns>
        private object BuildChatRequestContent(
            List<ChatMessage> history,
            ChatRequestOptions options,
            List<Core.ToolDefinition> tools,
            bool stream)
        {
            object formatValue = options.FormatSchema ?? (string.IsNullOrEmpty(options.Format) ? null : options.Format);

            var requestContent = new Dictionary<string, object>
            {
                ["model"] = options.ModelName ?? _config.DefaultModelName,
                ["messages"] = SerializeMessages(history),
                ["stream"] = stream,
                ["think"] = options.Think,
                ["options"] = BuildOllamaGenerationOptions(options)
            };

            if (formatValue != null)
            {
                requestContent["format"] = formatValue;
            }

            if (tools != null && tools.Count > 0)
            {
                requestContent["tools"] = tools.Select(t => t.ToOllamaFormat()).ToArray();
            }

            return requestContent;
        }

        /// <summary>
        /// Parse non-streaming /api/chat response.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="responseBody">Raw response body</param>
        /// <param name="response">Parsed chat response</param>
        /// <param name="error">Parse error when failed</param>
        /// <returns>True if parse succeeded</returns>
        private bool TryParseChatResponse(string sessionId, string responseBody, out ChatResponse response, out ChatError error)
        {
            response = null;
            error = null;

            try
            {
                var chatResponse = JObject.Parse(responseBody);
                var chatMessage = chatResponse["message"];

                response = new ChatResponse
                {
                    SessionId = sessionId,
                    Content = chatMessage?["content"]?.ToString() ?? "",
                    Role = chatMessage?["role"]?.ToString() ?? "assistant",
                    IsFinal = true,
                    RawResponse = responseBody
                };

                var parsedToolCalls = ParseToolCalls(chatMessage?["tool_calls"]);
                if (parsedToolCalls != null && parsedToolCalls.Count > 0)
                {
                    response.ToolCalls = parsedToolCalls;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = new ChatError
                {
                    ErrorType = LLMErrorType.InvalidResponse,
                    Message = $"Failed to parse response: {ex.Message}",
                    Exception = ex
                };
                return false;
            }
        }

        /// <summary>
        /// Parse tool call array from JSON token.
        /// </summary>
        /// <param name="toolCallsToken">tool_calls token</param>
        /// <returns>Parsed tool calls, or null when absent</returns>
        private List<Core.ToolCall> ParseToolCalls(JToken toolCallsToken)
        {
            var toolCallsArray = toolCallsToken as JArray;
            if (toolCallsArray == null || toolCallsArray.Count == 0)
            {
                return null;
            }

            var parsedCalls = new List<Core.ToolCall>();
            foreach (var tc in toolCallsArray)
            {
                var toolCall = new Core.ToolCall
                {
                    ToolName = tc["function"]?["name"]?.ToString(),
                    Arguments = tc["function"]?["arguments"]
                };
                parsedCalls.Add(toolCall);
            }

            return parsedCalls;
        }

        /// <summary>
        /// Process detected tool calls for one iteration.
        /// </summary>
        /// <param name="history">Session history</param>
        /// <param name="assistantContent">Assistant content</param>
        /// <param name="toolCalls">Detected tool calls</param>
        /// <param name="toolIterations">Current tool iteration count</param>
        /// <param name="maxIterations">Maximum allowed iterations</param>
        /// <returns>True if tool calls were processed</returns>
        private bool TryProcessToolCallsIteration(
            List<ChatMessage> history,
            string assistantContent,
            List<Core.ToolCall> toolCalls,
            ref int toolIterations,
            int maxIterations)
        {
            if (toolCalls == null || toolCalls.Count == 0)
            {
                return false;
            }

            toolIterations++;

            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Detected {toolCalls.Count} tool calls (iteration {toolIterations}/{maxIterations})");
            }

            history.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantContent,
                ToolCalls = toolCalls
            });

            ExecuteToolCallsAndAppendResults(history, toolCalls);
            return true;
        }

        /// <summary>
        /// Report error when max tool iterations are reached.
        /// </summary>
        /// <param name="toolIterations">Current iteration count</param>
        /// <param name="maxIterations">Maximum allowed iterations</param>
        /// <param name="hasToolCalls">Whether loop still had tool calls</param>
        /// <param name="onError">Error callback</param>
        private void ReportMaxToolIterationsReachedIfNeeded(
            int toolIterations,
            int maxIterations,
            bool hasToolCalls,
            Action<ChatError> onError)
        {
            if (toolIterations < maxIterations || !hasToolCalls)
            {
                return;
            }

            if (_config.DebugMode)
            {
                UnityEngine.Debug.LogWarning($"[Ollama] Max tool iterations ({maxIterations}) reached");
            }

            onError?.Invoke(new ChatError
            {
                ErrorType = LLMErrorType.Unknown,
                Message = $"Max tool iterations ({maxIterations}) reached"
            });
        }

        /// <summary>
        /// Shared loop for tool-aware request execution.
        /// </summary>
        /// <param name="history">Session history</param>
        /// <param name="options">Chat request options</param>
        /// <param name="onError">Error callback</param>
        /// <param name="executeIteration">Per-iteration request executor</param>
        /// <returns>Coroutine enumerator</returns>
        private IEnumerator ProcessToolAwareLoop(
            List<ChatMessage> history,
            ChatRequestOptions options,
            Action<ChatError> onError,
            Func<List<Core.ToolDefinition>, RequestLoopIterationData, IEnumerator> executeIteration)
        {
            int toolIterations = 0;
            int maxIterations = options.MaxToolIterations;
            bool hasToolCalls = true;

            while (hasToolCalls && toolIterations < maxIterations)
            {
                if (TryHandleCancellation(options, onError))
                {
                    yield break;
                }

                var tools = ResolveTools(options);
                var iterationData = new RequestLoopIterationData();

                yield return executeIteration(tools, iterationData);

                if (iterationData.ShouldAbort)
                {
                    yield break;
                }

                if (iterationData.AssistantContent == null)
                {
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = "Request iteration returned invalid state"
                    });
                    yield break;
                }

                hasToolCalls = TryProcessToolCallsIteration(
                    history,
                    iterationData.AssistantContent,
                    iterationData.ToolCalls,
                    ref toolIterations,
                    maxIterations);

                if (hasToolCalls)
                {
                    continue;
                }

                if (iterationData.FinalizeAction == null)
                {
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = "Request iteration finalize action is missing"
                    });
                    yield break;
                }

                iterationData.FinalizeAction();
            }

            ReportMaxToolIterationsReachedIfNeeded(toolIterations, maxIterations, hasToolCalls, onError);
        }

        /// <summary>
        /// Execute one session-bound request with queueing and running-session tracking.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="options">Chat request options</param>
        /// <param name="onError">Error callback</param>
        /// <param name="requestRoutine">Request routine coroutine factory</param>
        /// <returns>Coroutine enumerator</returns>
        private IEnumerator ExecuteSessionRequest(
            string sessionId,
            ChatRequestOptions options,
            Action<ChatError> onError,
            Func<IEnumerator> requestRoutine)
        {
            bool waitFailed = false;
            yield return WaitForTurn(sessionId, options, error =>
            {
                waitFailed = true;
                onError?.Invoke(error);
            });

            if (waitFailed)
            {
                yield break;
            }

            _runningSessions.Add(sessionId);

            try
            {
                yield return requestRoutine();
            }
            finally
            {
                _runningSessions.Remove(sessionId);
            }
        }

        /// <summary>
        /// Execute tool calls and append tool-result messages to history.
        /// </summary>
        /// <param name="history">Session history</param>
        /// <param name="toolCalls">Tool calls to execute</param>
        private void ExecuteToolCallsAndAppendResults(List<ChatMessage> history, List<Core.ToolCall> toolCalls)
        {
            foreach (var toolCall in toolCalls)
            {
                string toolResult;
                try
                {
                    toolResult = _toolManager.ExecuteTool(toolCall.ToolName, toolCall.Arguments?.ToString());
                }
                catch (Exception ex)
                {
                    toolResult = $"Error: {ex.Message}";
                    if (_config.DebugMode)
                    {
                        UnityEngine.Debug.LogError($"[Ollama] Tool execution failed: {ex.Message}");
                    }
                }

                history.Add(new ChatMessage
                {
                    Role = "tool",
                    Content = toolResult
                });
            }
        }

        /// <summary>
        /// Finalize non-streaming response when no tool call remains.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="finalResponse">Final response</param>
        /// <param name="options">Chat request options</param>
        /// <param name="onResponse">Response callback</param>
        private void FinalizeNonToolResponse(
            string sessionId,
            ChatResponse finalResponse,
            ChatRequestOptions options,
            Action<ChatResponse> onResponse)
        {
            _historyManager.AddMessage(sessionId, new ChatMessage
            {
                Role = finalResponse.Role,
                Content = finalResponse.Content
            }, options.MaxHistory);

            onResponse?.Invoke(finalResponse);
        }

        /// <summary>
        /// Execute one non-streaming request iteration.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="history">Session history</param>
        /// <param name="options">Chat request options</param>
        /// <param name="tools">Resolved tools</param>
        /// <param name="onResponse">Response callback</param>
        /// <param name="onError">Error callback</param>
        /// <param name="iterationData">Iteration result container</param>
        /// <returns>Coroutine enumerator</returns>
        private IEnumerator ExecuteNonStreamingIteration(
            string sessionId,
            List<ChatMessage> history,
            ChatRequestOptions options,
            List<Core.ToolDefinition> tools,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError,
            RequestLoopIterationData iterationData)
        {
            object requestContent = BuildChatRequestContent(history, options, tools, false);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
            string url = ChatApiUrl;

            ChatError errorInfo = null;
            ChatResponse finalResponse = null;
            bool requestComplete = false;

            yield return _httpHelper.ExecuteWithRetry(
                url,
                json,
                responseBody =>
                {
                    requestComplete = true;
                    if (TryParseChatResponse(sessionId, responseBody, out var parsedResponse, out var parseError))
                    {
                        finalResponse = parsedResponse;
                    }
                    else
                    {
                        errorInfo = parseError;
                    }
                },
                error =>
                {
                    errorInfo = error;
                    requestComplete = true;
                },
                options.CancellationToken
            );

            if (errorInfo != null)
            {
                onError?.Invoke(errorInfo);
                iterationData.Abort();
                yield break;
            }

            if (!requestComplete || finalResponse == null)
            {
                onError?.Invoke(new ChatError
                {
                    ErrorType = LLMErrorType.Unknown,
                    Message = "Request incomplete"
                });
                iterationData.Abort();
                yield break;
            }

            iterationData.SetResult(
                finalResponse.Content,
                finalResponse.ToolCalls,
                () => FinalizeNonToolResponse(sessionId, finalResponse, options, onResponse));
        }

        /// <summary>
        /// Process tool-aware loop for non-streaming requests.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="history">Session history</param>
        /// <param name="options">Chat request options</param>
        /// <param name="onResponse">Response callback</param>
        /// <param name="onError">Error callback</param>
        /// <returns>Coroutine enumerator</returns>
        private IEnumerator ProcessNonStreamingRequestLoop(
            string sessionId,
            List<ChatMessage> history,
            ChatRequestOptions options,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError)
        {
            yield return ProcessToolAwareLoop(
                history,
                options,
                onError,
                (tools, iterationData) => ExecuteNonStreamingIteration(
                    sessionId,
                    history,
                    options,
                    tools,
                    onResponse,
                    onError,
                    iterationData));
        }

        /// <summary>
        /// Handle one streaming chunk and update accumulated state.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="chunk">Raw chunk</param>
        /// <param name="onResponse">Response callback</param>
        /// <param name="fullResponse">Accumulated response content</param>
        /// <param name="fullRole">Current role</param>
        /// <param name="lastRawChunk">Last raw chunk</param>
        /// <param name="detectedToolCalls">Detected tool calls</param>
        private void HandleStreamingChunk(
            string sessionId,
            string chunk,
            Action<ChatResponse> onResponse,
            ref string fullResponse,
            ref string fullRole,
            ref string lastRawChunk,
            ref List<Core.ToolCall> detectedToolCalls)
        {
            try
            {
                lastRawChunk = chunk;
                var chunkJson = JObject.Parse(chunk);
                var chunkMessage = chunkJson["message"];

                if (chunkMessage == null)
                {
                    return;
                }

                string content = chunkMessage["content"]?.ToString() ?? "";
                string role = chunkMessage["role"]?.ToString() ?? "assistant";

                fullResponse += content;
                fullRole = role;

                var parsedToolCalls = ParseToolCalls(chunkMessage["tool_calls"]);
                if (parsedToolCalls != null && parsedToolCalls.Count > 0)
                {
                    detectedToolCalls = parsedToolCalls;
                }

                onResponse?.Invoke(new ChatResponse
                {
                    SessionId = sessionId,
                    Content = fullResponse,
                    Role = fullRole,
                    IsFinal = false,
                    RawResponse = chunk
                });
            }
            catch (Exception ex)
            {
                if (_config.DebugMode)
                {
                    UnityEngine.Debug.LogWarning($"[Ollama] Failed to parse chunk: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Finalize streaming response when no tool call remains.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="fullResponse">Accumulated response content</param>
        /// <param name="fullRole">Assistant role</param>
        /// <param name="lastRawChunk">Last raw chunk</param>
        /// <param name="options">Chat request options</param>
        /// <param name="onResponse">Response callback</param>
        private void FinalizeStreamingNonToolResponse(
            string sessionId,
            string fullResponse,
            string fullRole,
            string lastRawChunk,
            ChatRequestOptions options,
            Action<ChatResponse> onResponse)
        {
            if (string.IsNullOrEmpty(fullResponse))
            {
                return;
            }

            _historyManager.AddMessage(sessionId, new ChatMessage
            {
                Role = fullRole,
                Content = fullResponse
            }, options.MaxHistory);

            var finalResponse = new ChatResponse
            {
                SessionId = sessionId,
                Content = fullResponse,
                Role = fullRole,
                IsFinal = true,
                RawResponse = lastRawChunk ?? fullResponse
            };

            onResponse?.Invoke(finalResponse);
        }

        /// <summary>
        /// Execute one streaming request iteration.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="history">Session history</param>
        /// <param name="options">Chat request options</param>
        /// <param name="tools">Resolved tools</param>
        /// <param name="onResponse">Response callback</param>
        /// <param name="onError">Error callback</param>
        /// <param name="iterationData">Iteration result container</param>
        /// <returns>Coroutine enumerator</returns>
        private IEnumerator ExecuteStreamingIteration(
            string sessionId,
            List<ChatMessage> history,
            ChatRequestOptions options,
            List<Core.ToolDefinition> tools,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError,
            RequestLoopIterationData iterationData)
        {
            object requestContent = BuildChatRequestContent(history, options, tools, true);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
            string url = ChatApiUrl;

            string fullResponse = "";
            string fullRole = "assistant";
            string lastRawChunk = null;
            List<Core.ToolCall> detectedToolCalls = null;
            bool streamComplete = false;

            yield return _httpHelper.ExecuteStreamingWithRetry(
                url,
                json,
                chunk =>
                {
                    HandleStreamingChunk(
                        sessionId,
                        chunk,
                        onResponse,
                        ref fullResponse,
                        ref fullRole,
                        ref lastRawChunk,
                        ref detectedToolCalls);
                },
                isSuccess =>
                {
                    streamComplete = isSuccess;
                },
                error =>
                {
                    onError?.Invoke(error);
                },
                options.CancellationToken
            );

            if (!streamComplete)
            {
                iterationData.Abort();
                yield break;
            }

            iterationData.SetResult(
                fullResponse,
                detectedToolCalls,
                () => FinalizeStreamingNonToolResponse(sessionId, fullResponse, fullRole, lastRawChunk, options, onResponse));
        }

        /// <summary>
        /// Process tool-aware loop for streaming requests.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="history">Session history</param>
        /// <param name="options">Chat request options</param>
        /// <param name="onResponse">Response callback</param>
        /// <param name="onError">Error callback</param>
        /// <returns>Coroutine enumerator</returns>
        private IEnumerator ProcessStreamingRequestLoop(
            string sessionId,
            List<ChatMessage> history,
            ChatRequestOptions options,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError)
        {
            yield return ProcessToolAwareLoop(
                history,
                options,
                onError,
                (tools, iterationData) => ExecuteStreamingIteration(
                    sessionId,
                    history,
                    options,
                    tools,
                    onResponse,
                    onError,
                    iterationData));
        }

        /// <summary>
        /// Send message asynchronously with Task (get complete response at once)
        /// <param name="message">Message to send</param>
        /// <param name="options">Request options</param>
        /// <param name="cancellationToken">External cancellation token</param>
        /// </summary>
        public Task<ChatResponse> SendMessageTaskAsync(
            string message,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return SendMessageTaskAsync(message, null, options, cancellationToken);
        }

        /// <summary>
        /// Send message asynchronously with images with Task (get complete response at once)
        /// <param name="message">Message to send</param>
        /// <param name="images">List of images to send (if supported by the model)</param>
        /// <param name="options">Request options</param>
        /// <param name="cancellationToken">External cancellation token</param>
        /// </summary>
        public Task<ChatResponse> SendMessageTaskAsync(
            string message,
            List<Texture2D> images,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ChatRequestOptions();
            var token = PrepareCancellationToken(options, cancellationToken, out var linkedSource);

            var tcs = new TaskCompletionSource<ChatResponse>();
            var registration = token.CanBeCanceled
                ? token.Register(() => tcs.TrySetCanceled(token))
                : default;

            CoroutineRunner.Run(SendMessageAsync(
                message,
                images,
                response => tcs.TrySetResult(response),
                error => tcs.TrySetException(new ChatLLMException(error)),
                options
            ));

            return tcs.Task.ContinueWith(task =>
            {
                registration.Dispose();
                linkedSource?.Dispose();
                return task;
            }, TaskScheduler.Default).Unwrap();
        }


        /// <summary>
        /// Send message asynchronously with IEnumerator (get complete response at once)
        /// Execution flow:
        /// 1) Delegate to image-aware overload.
        /// 2) Queue/session control and main request loop are handled there.
        /// <param name="message">Message to send</param>
        /// <param name="onResponse">Response: (response)</param>
        /// <param name="onError">Response: (error)</param>
        /// <param name="options">Request options</param>
        /// </summary>
        public IEnumerator SendMessageAsync(
            string message,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null)
        {
            return SendMessageAsync(message, null, onResponse, onError, options);
        }

        /// <summary>
        /// Send message asynchronously with images with IEnumerator (get complete response at once)
        /// Execution structure:
        /// 1) Resolve/Create sessionId from options.
        /// 2) Enter ExecuteSessionRequest (wait turn + running-session tracking).
        /// 3) Build history for this request (system prompt/user message).
        /// 4) Run ProcessNonStreamingRequestLoop (request/response + tool-call iterations).
        /// <param name="message">Message to send</param>
        /// <param name="images">List of images to send (if supported by the model)</param>
        /// <param name="onResponse">Response: (response)</param>
        /// <param name="onError">Response: (error)</param>
        /// <param name="options">Request options</param>
        /// </summary>
        public IEnumerator SendMessageAsync(
            string message,
            List<Texture2D> images,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null)
        {
            options ??= new ChatRequestOptions();
            string sessionId = options.SessionId ?? Guid.NewGuid().ToString();

            // High-level orchestration:
            // - ExecuteSessionRequest: queue wait + running session enter/leave
            // - lambda body: per-request preparation + non-streaming processing loop
            yield return ExecuteSessionRequest(
                sessionId,
                options,
                onError,
                () =>
                {
                    // Prepare initial request context for this session.
                    var history = PrepareHistoryForRequest(sessionId, message, images, options);

                    // Execute non-streaming request loop (includes tool call iterations).
                    return ProcessNonStreamingRequestLoop(sessionId, history, options, onResponse, onError);
                });
        }

        /// <summary>
        /// Serialize messages (Ollama API format)
        /// <param name="messages">List of chat messages</param>
        /// </summary>
        private object[] SerializeMessages(List<ChatMessage> messages)
        {
            var result = new List<object>();

            foreach (var msg in messages)
            {
                if (msg.Role == "tool")
                {
                    // Tool Result
                    result.Add(new
                    {
                        role = "tool",
                        content = msg.Content,
                    });
                }
                else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    // Assistant message with tool calls
                    result.Add(new
                    {
                        role = msg.Role,
                        content = msg.Content,
                        tool_calls = msg.ToolCalls.Select(tc => new
                        {
                            function = new
                            {
                                name = tc.ToolName,
                                arguments = tc.Arguments
                            }
                        }).ToArray()
                    });
                }
                else
                {
                    // Regular message
                    result.Add(new
                    {
                        role = msg.Role,
                        content = msg.Content,
                        images = msg.Images != null && msg.Images.Count > 0 ? msg.Images.ToArray() : null
                    });
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Convert a Texture2D image into Base64 PNG string.
        /// </summary>
        /// <param name="texture">Texture to convert</param>
        /// <returns>Base64 encoded PNG</returns>
        private string ConvertTexture2DToBase64(Texture2D texture)
        {
            byte[] imageBytes = texture.EncodeToPNG();
            return Convert.ToBase64String(imageBytes);
        }

        /// <summary>
        /// Send message asynchronously with IEnumerator (get response in streaming)
        /// <param name="message">The message to send</param>
        /// <param name="onResponse">Successed callback</param>
        /// <param name="onError">Error callback</param>
        /// <param name="options">Request options</param>
        /// </summary>
        public IEnumerator SendMessageStreamingAsync(
            string message,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null)
        {
            return SendMessageStreamingAsync(message, null, onResponse, onError, options);
        }

        /// <summary>
        /// Send message asynchronously with IEnumerator (get response in streaming)
        /// Execution structure:
        /// 1) Resolve/Create sessionId from options.
        /// 2) Enter ExecuteSessionRequest (wait turn + running-session tracking).
        /// 3) Build history for this request (system prompt/user message).
        /// 4) Run ProcessStreamingRequestLoop (streaming chunks + tool-call iterations).
        /// <param name="message">The message to send</param>
        /// <param name="images">List of images to send (if supported by the model)</param>
        /// <param name="onResponse">Successed callback</param>
        /// <param name="onError">Error callback</param>
        /// <param name="options">Request options</param>
        /// </summary>
        public IEnumerator SendMessageStreamingAsync(
            string message,
            List<Texture2D> images,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null)
        {
            options ??= new ChatRequestOptions();
            string sessionId = options.SessionId ?? Guid.NewGuid().ToString();

            // High-level orchestration:
            // - ExecuteSessionRequest: queue wait + running session enter/leave
            // - lambda body: per-request preparation + streaming processing loop
            yield return ExecuteSessionRequest(
                sessionId,
                options,
                onError,
                () =>
                {
                    // Prepare initial request context for this session.
                    var history = PrepareHistoryForRequest(sessionId, message, images, options);

                    // Execute streaming request loop (includes tool call iterations).
                    return ProcessStreamingRequestLoop(sessionId, history, options, onResponse, onError);
                });
        }

        /// <summary>
        /// Send message with streaming (Task version)
        /// <param name="message">Message to send</param>
        /// <param name="onProgress">Progress during streaming reception</param>
        /// <param name="options">Request options</param>
        /// <param name="cancellationToken">External cancellation token</param>
        /// </summary>
        public Task<ChatResponse> SendMessageStreamingTaskAsync(
            string message,
            IProgress<ChatResponse> onProgress,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return SendMessageStreamingTaskAsync(message, null, onProgress, options, cancellationToken);
        }

        /// <summary>
        /// Send message with streaming (Task version)
        /// <param name="message">Message to send</param>
        /// <param name="images">List of images to send (if supported by the model)</param>
        /// <param name="onProgress">Progress during streaming reception</param>
        /// <param name="options">Request options</param>
        /// <param name="cancellationToken">External cancellation token</param>
        /// </summary>
        public Task<ChatResponse> SendMessageStreamingTaskAsync(
            string message,
            List<Texture2D> images,
            IProgress<ChatResponse> onProgress,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ChatRequestOptions();
            var token = PrepareCancellationToken(options, cancellationToken, out var linkedSource);

            var tcs = new TaskCompletionSource<ChatResponse>();
            var registration = token.CanBeCanceled
                ? token.Register(() => tcs.TrySetCanceled(token))
                : default;

            CoroutineRunner.Run(SendMessageStreamingAsync(
                message,
                images,
                response =>
                {
                    onProgress?.Report(response);

                    if (response.IsFinal)
                    {
                        tcs.TrySetResult(response);
                    }
                },
                error => tcs.TrySetException(new ChatLLMException(error)),
                options
            ));

            return tcs.Task.ContinueWith(task =>
            {
                registration.Dispose();
                linkedSource?.Dispose();
                return task;
            }, TaskScheduler.Default).Unwrap();
        }

        /// <summary>
        /// Update configuration
        /// <param name="newConfig">New configuration</param>
        /// </summary>
        public void UpdateConfig(OllamaConfig newConfig)
        {
            if (newConfig != null)
            {
                _config.ServerUrl = newConfig.ServerUrl;
                _config.DefaultModelName = newConfig.DefaultModelName;
                _config.MaxRetries = newConfig.MaxRetries;
                _config.RetryDelaySeconds = newConfig.RetryDelaySeconds;
                _config.DefaultSeed = newConfig.DefaultSeed;
                _config.HttpTimeoutSeconds = newConfig.HttpTimeoutSeconds;
                _config.DebugMode = newConfig.DebugMode;
            }
        }

        /// <summary>
        /// Save session to file
        /// <param name="filePath">File path to save</param>
        /// <param name="sessionId">Session ID to save</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        /// </summary>
        public void SaveSession(string filePath, string sessionId, string encryptionKey = null)
        {
            if (!_historyManager.HasSession(sessionId))
            {
                throw new System.InvalidOperationException($"Session '{sessionId}' not found");
            }

            var session = _historyManager.GetOrCreateSession(sessionId);
            ChatSessionPersistence.SaveSession(filePath, session, encryptionKey);
        }

        /// <summary>
        /// Load session history from file
        /// <param name="filePath">File path to load</param>
        /// <param name="sessionId">Session ID to load</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        /// </summary>
        public void LoadSession(string filePath, string sessionId, string encryptionKey = null)
        {
            var loadedSession = ChatSessionPersistence.LoadSession(filePath, encryptionKey);

            // If the session ID is different, overwrite it with the specified ID
            if (loadedSession.Id != sessionId)
            {
                loadedSession.Id = sessionId;
            }

            // Clear existing session history
            _historyManager.Clear(sessionId);

            // Restore loaded messages
            foreach (var message in loadedSession.History)
            {
                _historyManager.AddMessage(sessionId, message, null);
            }

            // Restore system prompt
            var session = _historyManager.GetOrCreateSession(sessionId);
            if (!string.IsNullOrEmpty(loadedSession.SystemPrompt))
            {
                session.SystemPrompt = loadedSession.SystemPrompt;
            }
        }

        /// <summary>
        /// Save all sessions to directory
        /// <param name="dirPath">Directory path to save sessions</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        /// </summary>
        public void SaveAllSessions(string dirPath, string encryptionKey = null)
        {
            ChatSessionPersistence.SaveAllSessions(dirPath, _historyManager, encryptionKey);
        }

        /// <summary>
        /// Load all sessions from directory
        /// <param name="dirPath">Directory path to load sessions</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        /// </summary>
        public void LoadAllSessions(string dirPath, string encryptionKey = null)
        {
            ChatSessionPersistence.LoadAllSessions(dirPath, _historyManager, encryptionKey);
        }

        public OllamaConfig GetConfig()
        {
            return new()
            {
                ServerUrl = _config.ServerUrl,
                DefaultModelName = _config.DefaultModelName,
                MaxRetries = _config.MaxRetries,
                RetryDelaySeconds = _config.RetryDelaySeconds,
                DefaultSeed = _config.DefaultSeed,
                HttpTimeoutSeconds = _config.HttpTimeoutSeconds,
                DebugMode = _config.DebugMode
            };
        }

        #region Tool Management

        /// <summary>
        /// Register tool (with automatic schema inference)
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <param name="description">Tool description</param>
        /// <param name="callback">Callback function (any signature)</param>
        public void RegisterTool(string name, string description, Delegate callback)
        {
            _toolManager.RegisterTool(name, description, callback);
        }

        /// <summary>
        /// Register tool (with manual schema specification)
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <param name="description">Tool description</param>
        /// <param name="inputSchema">JSON Schema</param>
        /// <param name="callback">Callback function</param>
        public void RegisterTool(string name, string description, object inputSchema, Delegate callback)
        {
            _toolManager.RegisterTool(name, description, inputSchema, callback);
        }

        /// <summary>
        /// Unregister tool
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if successfully unregistered</returns>
        public bool UnregisterTool(string name)
        {
            return _toolManager.UnregisterTool(name);
        }

        /// <summary>
        /// Unregister all tools
        /// </summary>
        public void RemoveAllTools()
        {
            _toolManager.RemoveAllTools();
        }

        /// <summary>
        /// Get all registered tools
        /// </summary>
        /// <returns>List of tool definitions</returns>
        public List<Core.ToolDefinition> GetRegisteredTools()
        {
            return _toolManager.GetAllTools();
        }

        /// <summary>
        /// Check if a tool is registered
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if registered</returns>
        public bool HasTool(string name)
        {
            return _toolManager.HasTool(name);
        }

        #endregion
    }
}
