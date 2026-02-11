using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Manager;

namespace EasyLocalLLM.LLM.Ollama
{
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
        /// Pending request information
        /// </summary>
        private class PendingRequest(string sessionId, int priority, long order)
        {
            public string SessionId { get; } = sessionId;
            public int Priority { get; } = priority;
            public long Order { get; } = order;
        }

        /// <summary>
        /// Insert pending request in sorted order
        /// the list is sorted by priority (desc) and order (asc)
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
            options ??= new ChatRequestOptions();
            var token = PrepareCancellationToken(options, cancellationToken, out var linkedSource);

            var tcs = new TaskCompletionSource<ChatResponse>();
            var registration = token.CanBeCanceled
                ? token.Register(() => tcs.TrySetCanceled(token))
                : default;

            CoroutineRunner.Run(SendMessageAsync(
                message,
                (response, error) =>
                {
                    if (error != null)
                    {
                        tcs.TrySetException(new ChatLLMException(error));
                        return;
                    }

                    tcs.TrySetResult(response);
                },
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
        /// <param name="message">Message to send</param>
        /// <param name="callback">Response: (response, error)</param>
        /// <param name="options">Request options</param>
        /// </summary>
        public IEnumerator SendMessageAsync(
            string message,
            Action<ChatResponse, ChatError> callback,
            ChatRequestOptions options = null)
        {
            options ??= new ChatRequestOptions();
            string sessionId = options.SessionId ?? Guid.NewGuid().ToString();

            bool waitFailed = false;
            yield return WaitForTurn(sessionId, options, error =>
            {
                waitFailed = true;
                callback?.Invoke(null, error);
            });
            if (waitFailed)
            {
                yield break;
            }

            _runningSessions.Add(sessionId);

            try
            {
                var session = _historyManager.GetOrCreateSession(sessionId, options.SystemPrompt);
                var history = session.History;

                // Add system prompt if history is empty (first message)
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

                // Add user message
                history.Add(new ChatMessage { Role = "user", Content = message });

                // Tool call handling loop
                int toolIterations = 0;
                int maxIterations = options.MaxToolIterations;
                bool hasToolCalls = true;

                while (hasToolCalls && toolIterations < maxIterations)
                {
                    if (options.CancellationToken.IsCancellationRequested)
                    {
                        callback?.Invoke(null, new ChatError
                        {
                            ErrorType = LLMErrorType.Cancelled,
                            Message = "Request cancelled"
                        });
                        yield break;
                    }

                    // Get tools to include
                    var tools = _toolManager.GetAllTools();
                    if (options.Tools != null && options.Tools.Count > 0)
                    {
                        tools = tools.Where(t => options.Tools.Contains(t.Name)).ToList();
                    }

                    // Decide format value based on options
                    object formatValue = options.FormatSchema ?? (string.IsNullOrEmpty(options.Format) ? null : options.Format);

                    // Make request content
                    object requestContent;
                    if (tools != null && tools.Count > 0)
                    {
                        // Request with registered tools
                        if (formatValue != null)
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = false,
                                format = formatValue,
                                tools = tools.Select(t => t.ToOllamaFormat()).ToArray(),
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                        else
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = false,
                                tools = tools.Select(t => t.ToOllamaFormat()).ToArray(),
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                    }
                    else
                    {
                        // Request without tools
                        if (formatValue != null)
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = false,
                                format = formatValue,
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                        else
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = false,
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                    }

                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                    string url = _config.ServerUrl + "/api/chat";

                    ChatError errorInfo = null;
                    ChatResponse finalResponse = null;
                    bool requestComplete = false;

                    yield return _httpHelper.ExecuteWithRetry(
                        url,
                        json,
                        responseBody =>
                        {
                            try
                            {
                                var chatResponse = JObject.Parse(responseBody);
                                var chatMessage = chatResponse["message"];

                                var response = new ChatResponse
                                {
                                    SessionId = sessionId,
                                    Content = chatMessage?["content"]?.ToString() ?? "",
                                    Role = chatMessage?["role"]?.ToString() ?? "assistant",
                                    IsFinal = true,
                                    RawResponse = responseBody
                                };

                                // Extract tool calls
                                var toolCallsArray = chatMessage?["tool_calls"] as JArray;
                                if (toolCallsArray != null && toolCallsArray.Count > 0)
                                {
                                    response.ToolCalls = new List<Core.ToolCall>();
                                    foreach (var tc in toolCallsArray)
                                    {
                                        var toolCall = new Core.ToolCall
                                        {
                                            ToolName = tc["function"]?["name"]?.ToString(),
                                            Arguments = tc["function"]?["arguments"]
                                        };
                                        response.ToolCalls.Add(toolCall);
                                    }
                                }

                                finalResponse = response;
                                requestComplete = true;
                            }
                            catch (Exception ex)
                            {
                                errorInfo = new ChatError
                                {
                                    ErrorType = LLMErrorType.InvalidResponse,
                                    Message = $"Failed to parse response: {ex.Message}",
                                    Exception = ex
                                };
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
                        callback?.Invoke(null, errorInfo);
                        yield break;
                    }

                    if (!requestComplete || finalResponse == null)
                    {
                        callback?.Invoke(null, new ChatError
                        {
                            ErrorType = LLMErrorType.Unknown,
                            Message = "Request incomplete"
                        });
                        yield break;
                    }

                    // Tool calls handling
                    if (finalResponse.ToolCalls != null && finalResponse.ToolCalls.Count > 0)
                    {
                        toolIterations++;

                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Detected {finalResponse.ToolCalls.Count} tool calls (iteration {toolIterations}/{maxIterations})");
                        }

                        // Add assistant message with tool calls to history
                        history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = finalResponse.Content,
                            ToolCalls = finalResponse.ToolCalls
                        });

                        // Execute each tool call
                        foreach (var toolCall in finalResponse.ToolCalls)
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

                            // Add tool result message to history
                            history.Add(new ChatMessage
                            {
                                Role = "tool",
                                Content = toolResult
                            });
                        }

                        // Resend including tool results in the next loop
                        hasToolCalls = true;
                    }
                    else
                    {
                        // No tool calls, end loop
                        hasToolCalls = false;

                        // Add to history
                        _historyManager.AddMessage(sessionId, new ChatMessage
                        {
                            Role = finalResponse.Role,
                            Content = finalResponse.Content
                        }, options.MaxHistory);

                        callback?.Invoke(finalResponse, null);
                    }
                }

                // Max iterations reached
                if (toolIterations >= maxIterations && hasToolCalls)
                {
                    if (_config.DebugMode)
                    {
                        UnityEngine.Debug.LogWarning($"[Ollama] Max tool iterations ({maxIterations}) reached");
                    }

                    callback?.Invoke(null, new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = $"Max tool iterations ({maxIterations}) reached"
                    });
                }
            }
            finally
            {
                _runningSessions.Remove(sessionId);
            }
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
                        content = msg.Content
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
                        content = msg.Content
                    });
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Send message asynchronously with IEnumerator (get response in streaming)
        /// <param name="message">The message to send</param>
        /// <param name="callback">Response callback: (response, error)</param>
        /// <param name="options">Request options</param>
        /// </summary>
        public IEnumerator SendMessageStreamingAsync(
            string message,
            Action<ChatResponse, ChatError> callback,
            ChatRequestOptions options = null)
        {
            options ??= new ChatRequestOptions();
            string sessionId = options.SessionId ?? Guid.NewGuid().ToString();

            bool waitFailed = false;
            yield return WaitForTurn(sessionId, options, error =>
            {
                waitFailed = true;
                callback?.Invoke(null, error);
            });
            if (waitFailed)
            {
                yield break;
            }

            _runningSessions.Add(sessionId);

            try
            {
                var session = _historyManager.GetOrCreateSession(sessionId, options.SystemPrompt);
                var history = session.History;

                // Add system prompt if not present
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

                // Add user message
                history.Add(new ChatMessage { Role = "user", Content = message });

                // Tool call handling loop
                int toolIterations = 0;
                int maxIterations = options.MaxToolIterations;
                bool hasToolCalls = true;

                while (hasToolCalls && toolIterations < maxIterations)
                {
                    if (options.CancellationToken.IsCancellationRequested)
                    {
                        callback?.Invoke(null, new ChatError
                        {
                            ErrorType = LLMErrorType.Cancelled,
                            Message = "Request cancelled"
                        });
                        yield break;
                    }

                    // Get tools to include
                    var tools = _toolManager.GetAllTools();
                    if (options.Tools != null && options.Tools.Count > 0)
                    {
                        tools = tools.Where(t => options.Tools.Contains(t.Name)).ToList();
                    }

                    // Determine format field
                    object formatValue = options.FormatSchema ?? (string.IsNullOrEmpty(options.Format) ? null : options.Format);

                    // Create request
                    object requestContent;
                    if (tools != null && tools.Count > 0)
                    {
                        if (formatValue != null)
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = true,
                                format = formatValue,
                                tools = tools.Select(t => t.ToOllamaFormat()).ToArray(),
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                        else
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = true,
                                tools = tools.Select(t => t.ToOllamaFormat()).ToArray(),
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                    }
                    else
                    {
                        if (formatValue != null)
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = true,
                                format = formatValue,
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                        else
                        {
                            requestContent = new
                            {
                                model = options.ModelName ?? _config.DefaultModelName,
                                messages = SerializeMessages(history),
                                stream = true,
                                options = new
                                {
                                    seed = options.Seed ?? _config.DefaultSeed,
                                    temperature = options.Temperature
                                }
                            };
                        }
                    }

                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                    string url = _config.ServerUrl + "/api/chat";

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
                            try
                            {
                                lastRawChunk = chunk;
                                var chunkJson = JObject.Parse(chunk);
                                var chunkMessage = chunkJson["message"];

                                if (chunkMessage != null)
                                {
                                    string content = chunkMessage["content"]?.ToString() ?? "";
                                    string role = chunkMessage["role"]?.ToString() ?? "assistant";

                                    fullResponse += content;
                                    fullRole = role;

                                    // Detect tool calls (final chunk)
                                    var toolCallsArray = chunkMessage["tool_calls"] as JArray;
                                    if (toolCallsArray != null && toolCallsArray.Count > 0)
                                    {
                                        detectedToolCalls = new List<Core.ToolCall>();
                                        foreach (var tc in toolCallsArray)
                                        {
                                            var toolCall = new Core.ToolCall
                                            {
                                                ToolName = tc["function"]?["name"]?.ToString(),
                                                Arguments = tc["function"]?["arguments"]
                                            };
                                            detectedToolCalls.Add(toolCall);
                                        }
                                    }

                                    // Progress callback (IsFinal = false)
                                    var response = new ChatResponse
                                    {
                                        SessionId = sessionId,
                                        Content = fullResponse,
                                        Role = fullRole,
                                        IsFinal = false,
                                        RawResponse = chunk
                                    };

                                    callback?.Invoke(response, null);
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
                            streamComplete = isSuccess;
                        },
                        error =>
                        {
                            callback?.Invoke(null, error);
                        },
                        options.CancellationToken
                    );

                    if (!streamComplete)
                    {
                        yield break;
                    }

                    // Tool call handling
                    if (detectedToolCalls != null && detectedToolCalls.Count > 0)
                    {
                        toolIterations++;

                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Detected {detectedToolCalls.Count} tool calls (iteration {toolIterations}/{maxIterations})");
                        }

                        // Assistant message with tool calls
                        history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = fullResponse,
                            ToolCalls = detectedToolCalls
                        });

                        // Execute tool calls
                        foreach (var toolCall in detectedToolCalls)
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

                            // Add tool result message to history
                            history.Add(new ChatMessage
                            {
                                Role = "tool",
                                Content = toolResult
                            });
                        }

                        // Resend including tool results in the next loop
                        hasToolCalls = true;
                    }
                    else
                    {
                        // No tool calls, end processing
                        hasToolCalls = false;

                        if (!string.IsNullOrEmpty(fullResponse))
                        {
                            // Add to history
                            _historyManager.AddMessage(sessionId, new ChatMessage
                            {
                                Role = fullRole,
                                Content = fullResponse
                            }, options.MaxHistory);

                            // Final chunk
                            var finalResponse = new ChatResponse
                            {
                                SessionId = sessionId,
                                Content = fullResponse,
                                Role = fullRole,
                                IsFinal = true,
                                RawResponse = lastRawChunk ?? fullResponse
                            };

                            callback?.Invoke(finalResponse, null);
                        }
                    }
                }

                // Max iterations reached
                if (toolIterations >= maxIterations && hasToolCalls)
                {
                    if (_config.DebugMode)
                    {
                        UnityEngine.Debug.LogWarning($"[Ollama] Max tool iterations ({maxIterations}) reached");
                    }

                    callback?.Invoke(null, new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = $"Max tool iterations ({maxIterations}) reached"
                    });
                }
            }
            finally
            {
                _runningSessions.Remove(sessionId);
            }
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
            options ??= new ChatRequestOptions();
            var token = PrepareCancellationToken(options, cancellationToken, out var linkedSource);

            var tcs = new TaskCompletionSource<ChatResponse>();
            var registration = token.CanBeCanceled
                ? token.Register(() => tcs.TrySetCanceled(token))
                : default;

            CoroutineRunner.Run(SendMessageStreamingAsync(
                message,
                (response, error) =>
                {
                    if (error != null)
                    {
                        tcs.TrySetException(new ChatLLMException(error));
                        return;
                    }

                    if (response != null)
                    {
                        onProgress?.Report(response);

                        if (response.IsFinal)
                        {
                            tcs.TrySetResult(response);
                        }
                    }
                },
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
