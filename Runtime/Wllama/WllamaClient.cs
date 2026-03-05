using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Manager;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EasyLocalLLM.LLM.WebGL
{
    /// <summary>
    /// WebGL client implementation backed by llama.cpp WASM bridge.
    /// </summary>
    public class WllamaClient : IChatLLMClient
    {
        private readonly WllamaConfig _config;
        private readonly ChatHistoryManager _historyManager;
        private readonly ToolManager _toolManager;

        private readonly List<PendingRequest> _pendingRequests = new();
        private long _pendingSequence;
        private int _runningRequestCount;

        private readonly Dictionary<string, RequestState> _activeRequests = new();

        private bool _bridgeInitRequested;
        private bool _bridgeInitCompleted;
        private bool _bridgeInitSucceeded;
        private string _bridgeInitError;

        /// <summary>
        /// Global system prompt.
        /// </summary>
        public string GlobalSystemPrompt { get; set; } = "You are a helpful AI assistant.";

        private class PendingRequest
        {
            public string RequestId { get; }
            public int Priority { get; }
            public long Order { get; }

            public PendingRequest(string requestId, int priority, long order)
            {
                RequestId = requestId;
                Priority = priority;
                Order = order;
            }
        }

        private sealed class RequestState
        {
            public string RequestId { get; set; }
            public string SessionId { get; set; }
            public bool IsStreaming { get; set; }
            public bool IsDone { get; set; }
            public string ErrorMessage { get; set; }
            public object RawResponse { get; set; }
            public StringBuilder Builder { get; } = new();
            public Action<ChatResponse> OnResponse { get; set; }
            public Action<ChatError> OnError { get; set; }
        }

        private sealed class LlamaBridgeInitPayload
        {
            public string modelUrl;
            public int contextSize;
            public bool useWebGpu;
            public string wasmBaseUrl;
            public bool disableCache;
            public bool debugMode;
        }

        private sealed class LlamaGeneratePayload
        {
            public string requestId;
            public string sessionId;
            public bool stream;
            public List<HistoryMessagePayload> messages;
            public GenerationOptionsPayload options;
        }

        private sealed class HistoryMessagePayload
        {
            public string role;
            public string content;
        }

        private sealed class GenerationOptionsPayload
        {
            public float? temperature;
            public int? top_k;
            public float? top_p;
            public float? min_p;
            public int? seed;
            public int? n_ctx;
            public int? n_predict;
            public List<string> stop;
            public string format;
            public object format_schema;
            public bool think;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void EasyLocalLLM_Llama_Init(string gameObjectName, string callbackMethodName, string configJson);

        [DllImport("__Internal")]
        private static extern void EasyLocalLLM_Llama_Generate(string requestJson);

        [DllImport("__Internal")]
        private static extern void EasyLocalLLM_Llama_Abort(string requestId);

        [DllImport("__Internal")]
        private static extern void EasyLocalLLM_Llama_ResetSession(string sessionId);
#else
        private static void EasyLocalLLM_Llama_Init(string gameObjectName, string callbackMethodName, string configJson) { }
        private static void EasyLocalLLM_Llama_Generate(string requestJson) { }
        private static void EasyLocalLLM_Llama_Abort(string requestId) { }
        private static void EasyLocalLLM_Llama_ResetSession(string sessionId) { }
#endif

        /// <summary>
        /// Constructor.
        /// </summary>
        public WllamaClient(WllamaConfig config = null)
        {
            _config = config ?? new WllamaConfig();
            _historyManager = new ChatHistoryManager();
            _toolManager = new ToolManager(_config.DebugMode);
            WebGLLlamaCppBridgeReceiver.BridgeEventReceived += HandleBridgeEvent;
        }

        /// <summary>
        /// Clear all message history.
        /// </summary>
        public void ClearAllMessages()
        {
            _historyManager.ClearAll();
        }

        /// <summary>
        /// Clear message history for the specified session.
        /// </summary>
        public void ClearMessages(string sessionId)
        {
            _historyManager.Clear(sessionId);
            EasyLocalLLM_Llama_ResetSession(sessionId);
        }

        /// <summary>
        /// Send message asynchronously (non-streaming).
        /// </summary>
        public IEnumerator SendMessageAsync(
            string message,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null)
        {
            yield return SendMessageCoreAsync(
                message,
                stream: false,
                onResponse,
                onError,
                options);
        }

        /// <summary>
        /// Send message asynchronously (streaming).
        /// </summary>
        public IEnumerator SendMessageStreamingAsync(
            string message,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null)
        {
            yield return SendMessageCoreAsync(
                message,
                stream: true,
                onResponse,
                onError,
                options);
        }

        /// <summary>
        /// Send message with Task (non-streaming).
        /// </summary>
        public Task<ChatResponse> SendMessageTaskAsync(
            string message,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<ChatResponse>();
            options ??= new ChatRequestOptions();

            using var linkedSource = LinkCancellation(options.CancellationToken, cancellationToken);
            if (linkedSource != null)
            {
                options.CancellationToken = linkedSource.Token;
            }

            CoroutineRunner.Run(SendMessageAsync(
                message,
                response => tcs.TrySetResult(response),
                error =>
                {
                    if (error?.ErrorType == LLMErrorType.Cancelled)
                    {
                        tcs.TrySetCanceled();
                        return;
                    }

                    tcs.TrySetException(new ChatLLMException(new()
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = error?.Message ?? "Unknown error",
                        Exception = error.Exception,
                        HttpStatus = error.HttpStatus
                    }));
                },
                options));

            return tcs.Task;
        }

        /// <summary>
        /// Send message with Task (streaming).
        /// </summary>
        public Task<ChatResponse> SendMessageStreamingTaskAsync(
            string message,
            IProgress<ChatResponse> onProgress,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<ChatResponse>();
            options ??= new ChatRequestOptions();

            using var linkedSource = LinkCancellation(options.CancellationToken, cancellationToken);
            if (linkedSource != null)
            {
                options.CancellationToken = linkedSource.Token;
            }

            CoroutineRunner.Run(SendMessageStreamingAsync(
                message,
                response =>
                {
                    if (response.IsFinal)
                    {
                        tcs.TrySetResult(response);
                    }
                    else
                    {
                        onProgress?.Report(response);
                    }
                },
                error =>
                {
                    if (error?.ErrorType == LLMErrorType.Cancelled)
                    {
                        tcs.TrySetCanceled();
                        return;
                    }

                    tcs.TrySetException(new ChatLLMException(new()
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = error?.Message ?? "Unknown error",
                        Exception = error.Exception,
                        HttpStatus = error.HttpStatus
                    }));
                },
                options));

            return tcs.Task;
        }

        /// <summary>
        /// Save session history to file.
        /// </summary>
        public void SaveSession(string filePath, string sessionId, string encryptionKey = null)
        {
            if (!_historyManager.HasSession(sessionId))
            {
                throw new InvalidOperationException($"Session '{sessionId}' does not exist");
            }

            var session = _historyManager.GetOrCreateSession(sessionId);
            ChatSessionPersistence.SaveSession(filePath, session, encryptionKey);
        }

        /// <summary>
        /// Restore session history from file.
        /// </summary>
        public void LoadSession(string filePath, string sessionId, string encryptionKey = null)
        {
            var loaded = ChatSessionPersistence.LoadSession(filePath, encryptionKey);
            if (loaded.Id != sessionId)
            {
                throw new InvalidOperationException($"Loaded session id '{loaded.Id}' does not match requested id '{sessionId}'");
            }

            _historyManager.Clear(sessionId);
            foreach (var message in loaded.History)
            {
                _historyManager.AddMessage(sessionId, message, null);
            }

            if (!string.IsNullOrEmpty(loaded.SystemPrompt))
            {
                _historyManager.GetOrCreateSession(sessionId).SystemPrompt = loaded.SystemPrompt;
            }
        }

        /// <summary>
        /// Save all session history to directory.
        /// </summary>
        public void SaveAllSessions(string dirPath, string encryptionKey = null)
        {
            ChatSessionPersistence.SaveAllSessions(dirPath, _historyManager, encryptionKey);
        }

        /// <summary>
        /// Load all session history from directory.
        /// </summary>
        public void LoadAllSessions(string dirPath, string encryptionKey = null)
        {
            ChatSessionPersistence.LoadAllSessions(dirPath, _historyManager, encryptionKey);
        }

        /// <summary>
        /// Set system prompt for session.
        /// </summary>
        public void SetSessionSystemPrompt(string sessionId, string systemPrompt)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("sessionId cannot be null or empty", nameof(sessionId));
            }

            _historyManager.GetOrCreateSession(sessionId, systemPrompt).SystemPrompt = systemPrompt;
        }

        /// <summary>
        /// Get system prompt for session.
        /// </summary>
        public string GetSessionSystemPrompt(string sessionId)
        {
            if (!_historyManager.HasSession(sessionId))
            {
                return null;
            }

            return _historyManager.GetOrCreateSession(sessionId).SystemPrompt;
        }

        /// <summary>
        /// Reset session system prompt.
        /// </summary>
        public void ResetSessionSystemPrompt(string sessionId)
        {
            if (!_historyManager.HasSession(sessionId))
            {
                return;
            }

            _historyManager.GetOrCreateSession(sessionId).SystemPrompt = null;
        }

        /// <summary>
        /// Batch set system prompt for multiple sessions.
        /// </summary>
        public void SetSystemPromptForMultipleSessions(IEnumerable<string> sessionIds, string systemPrompt)
        {
            if (sessionIds == null)
            {
                throw new ArgumentNullException(nameof(sessionIds));
            }

            foreach (var sessionId in sessionIds)
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    continue;
                }

                _historyManager.GetOrCreateSession(sessionId, systemPrompt).SystemPrompt = systemPrompt;
            }
        }

        /// <summary>
        /// Reset all session system prompts.
        /// </summary>
        public void ResetAllSessionSystemPrompts()
        {
            foreach (var sessionId in _historyManager.GetAllSessionIds().ToList())
            {
                _historyManager.GetOrCreateSession(sessionId).SystemPrompt = null;
            }
        }

        /// <summary>
        /// Clear session and reset prompt.
        /// </summary>
        public void ClearSessionWithPrompt(string sessionId)
        {
            _historyManager.Clear(sessionId);
            EasyLocalLLM_Llama_ResetSession(sessionId);
        }

        /// <summary>
        /// Register tool (auto schema generation).
        /// </summary>
        public void RegisterTool(string name, string description, Delegate callback)
        {
            _toolManager.RegisterTool(name, description, callback);
        }

        /// <summary>
        /// Register tool (manual schema).
        /// </summary>
        public void RegisterTool(string name, string description, object inputSchema, Delegate callback)
        {
            _toolManager.RegisterTool(name, description, inputSchema, callback);
        }

        /// <summary>
        /// Unregister tool.
        /// </summary>
        public bool UnregisterTool(string name)
        {
            return _toolManager.UnregisterTool(name);
        }

        /// <summary>
        /// Remove all tools.
        /// </summary>
        public void RemoveAllTools()
        {
            _toolManager.RemoveAllTools();
        }

        /// <summary>
        /// Get registered tools.
        /// </summary>
        public List<ToolDefinition> GetRegisteredTools()
        {
            return _toolManager.GetAllTools();
        }

        /// <summary>
        /// Check if tool is registered.
        /// </summary>
        public bool HasTool(string name)
        {
            return _toolManager.HasTool(name);
        }

        private IEnumerator SendMessageCoreAsync(
            string message,
            bool stream,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError,
            ChatRequestOptions options)
        {
            options ??= new ChatRequestOptions();

            if (string.IsNullOrWhiteSpace(message))
            {
                onError?.Invoke(new ChatError
                {
                    ErrorType = LLMErrorType.Unknown,
                    Message = "Message cannot be empty."
                });
                yield break;
            }

            if (options.Tools != null && options.Tools.Count > 0)
            {
                onError?.Invoke(new ChatError
                {
                    ErrorType = LLMErrorType.Unknown,
                    Message = "Tool calling is currently disabled in WebGLLlamaCppClient."
                });
                yield break;
            }

            yield return EnsureBridgeInitialized(onError);
            if (!_bridgeInitSucceeded)
            {
                yield break;
            }

            if (options.CancellationToken.IsCancellationRequested)
            {
                onError?.Invoke(new ChatError
                {
                    ErrorType = LLMErrorType.Cancelled,
                    Message = "Request was cancelled before execution."
                });
                yield break;
            }

            string sessionId = string.IsNullOrEmpty(options.SessionId) ? _config.DefaultSessionId : options.SessionId;
            string requestId = Guid.NewGuid().ToString("N");

            bool acquired = false;
            yield return WaitForTurn(requestId, options, error =>
            {
                onError?.Invoke(error);
            }, success => acquired = success);

            if (!acquired)
            {
                yield break;
            }

            try
            {
                var history = PrepareHistoryForRequest(sessionId, message, options);

                var requestPayload = new LlamaGeneratePayload
                {
                    requestId = requestId,
                    sessionId = sessionId,
                    stream = stream,
                    messages = history.Select(m => new HistoryMessagePayload
                    {
                        role = m.Role,
                        content = m.Content
                    }).ToList(),
                    options = new GenerationOptionsPayload
                    {
                        temperature = options.Temperature,
                        top_k = options.TopK,
                        top_p = options.TopP,
                        min_p = options.MinP,
                        seed = options.Seed,
                        n_ctx = options.NumCtx ?? _config.ContextSize,
                        n_predict = options.NumPredict ?? _config.DefaultMaxTokens,
                        stop = options.Stop,
                        format = options.FormatSchema != null ? null : options.Format,
                        format_schema = options.FormatSchema,
                        think = options.Think
                    }
                };

                string requestJson = JsonConvert.SerializeObject(requestPayload);
                var state = new RequestState
                {
                    RequestId = requestId,
                    SessionId = sessionId,
                    IsStreaming = stream,
                    OnResponse = onResponse,
                    OnError = onError
                };

                _activeRequests[requestId] = state;
                EasyLocalLLM_Llama_Generate(requestJson);

                while (!state.IsDone && string.IsNullOrEmpty(state.ErrorMessage))
                {
                    if (options.CancellationToken.IsCancellationRequested)
                    {
                        EasyLocalLLM_Llama_Abort(requestId);
                        state.ErrorMessage = "Request cancelled.";
                        onError?.Invoke(new ChatError
                        {
                            ErrorType = LLMErrorType.Cancelled,
                            Message = "Request was cancelled."
                        });
                        break;
                    }

                    yield return null;
                }

                if (!string.IsNullOrEmpty(state.ErrorMessage))
                {
                    if (!options.CancellationToken.IsCancellationRequested)
                    {
                        onError?.Invoke(new ChatError
                        {
                            ErrorType = LLMErrorType.ServerError,
                            Message = state.ErrorMessage
                        });
                    }

                    _activeRequests.Remove(requestId);
                    yield break;
                }

                string finalText = state.Builder.ToString();
                if (!string.IsNullOrEmpty(finalText))
                {
                    _historyManager.AddMessage(sessionId, new ChatMessage
                    {
                        Role = "assistant",
                        Content = finalText
                    }, options.MaxHistory);
                }

                onResponse?.Invoke(new ChatResponse
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = finalText,
                    IsFinal = true,
                    RawResponse = state.RawResponse
                });

                _activeRequests.Remove(requestId);
            }
            finally
            {
                ReleaseTurn();
            }
        }

        private IEnumerator EnsureBridgeInitialized(Action<ChatError> onError)
        {
            if (_bridgeInitCompleted)
            {
                if (!_bridgeInitSucceeded)
                {
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.ConnectionFailed,
                        Message = _bridgeInitError ?? "WebGL llama.cpp bridge initialization failed."
                    });
                }

                yield break;
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            _bridgeInitCompleted = true;
            _bridgeInitSucceeded = false;
            _bridgeInitError = "WebGLLlamaCppClient is only supported in WebGL Player builds.";
            onError?.Invoke(new ChatError
            {
                ErrorType = LLMErrorType.ConnectionFailed,
                Message = _bridgeInitError
            });
            yield break;
#else
            if (!_bridgeInitRequested)
            {
                _bridgeInitRequested = true;
                var payload = new LlamaBridgeInitPayload
                {
                    modelUrl = _config.ModelUrl,
                    contextSize = _config.ContextSize,
                    useWebGpu = _config.UseWebGpu,
                    wasmBaseUrl = _config.WasmBaseUrl,
                    disableCache = _config.DisableCache,
                    debugMode = _config.DebugMode
                };

                string json = JsonConvert.SerializeObject(payload);
                string gameObjectName = WebGLLlamaCppBridgeReceiver.EnsureReceiverGameObject();
                EasyLocalLLM_Llama_Init(gameObjectName, nameof(WebGLLlamaCppBridgeReceiver.OnBridgeEvent), json);
            }

            float startedAt = Time.realtimeSinceStartup;
            while (!_bridgeInitCompleted)
            {
                if (Time.realtimeSinceStartup - startedAt > _config.InitTimeoutSeconds)
                {
                    _bridgeInitCompleted = true;
                    _bridgeInitSucceeded = false;
                    _bridgeInitError = "WebGL llama.cpp bridge initialization timed out.";
                    break;
                }

                yield return null;
            }

            if (!_bridgeInitSucceeded)
            {
                onError?.Invoke(new ChatError
                {
                    ErrorType = LLMErrorType.ConnectionFailed,
                    Message = _bridgeInitError ?? "WebGL llama.cpp bridge initialization failed."
                });
            }
#endif
        }

        private List<ChatMessage> PrepareHistoryForRequest(string sessionId, string message, ChatRequestOptions options)
        {
            var history = _historyManager.GetHistory(sessionId);

            if (history.Count == 0)
            {
                string systemPrompt = !string.IsNullOrEmpty(options.SystemPrompt)
                    ? options.SystemPrompt
                    : _historyManager.GetOrCreateSession(sessionId).SystemPrompt ?? GlobalSystemPrompt;

                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    history.Add(new ChatMessage
                    {
                        Role = "system",
                        Content = systemPrompt
                    });
                }
            }

            _historyManager.AddMessage(sessionId, new ChatMessage
            {
                Role = "user",
                Content = message
            }, options.MaxHistory);

            return _historyManager.GetHistory(sessionId);
        }

        private IEnumerator WaitForTurn(string requestId, ChatRequestOptions options, Action<ChatError> onError, Action<bool> onComplete)
        {
            int maxConcurrent = Mathf.Max(1, _config.MaxConcurrentRequests);

            if (!options.WaitIfBusy)
            {
                if (_runningRequestCount >= maxConcurrent || _pendingRequests.Count > 0)
                {
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = "Client is busy. Set WaitIfBusy=true to queue the request."
                    });
                    onComplete?.Invoke(false);
                    yield break;
                }

                _runningRequestCount++;
                onComplete?.Invoke(true);
                yield break;
            }

            var pending = new PendingRequest(requestId, options.Priority, Interlocked.Increment(ref _pendingSequence));
            InsertPendingSorted(pending);

            while (true)
            {
                if (options.CancellationToken.IsCancellationRequested)
                {
                    _pendingRequests.Remove(pending);
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.Cancelled,
                        Message = "Request was cancelled while waiting in queue."
                    });
                    onComplete?.Invoke(false);
                    yield break;
                }

                if (_runningRequestCount < maxConcurrent && _pendingRequests.Count > 0 && ReferenceEquals(_pendingRequests[0], pending))
                {
                    _pendingRequests.RemoveAt(0);
                    _runningRequestCount++;
                    onComplete?.Invoke(true);
                    yield break;
                }

                yield return null;
            }
        }

        private void ReleaseTurn()
        {
            _runningRequestCount = Mathf.Max(0, _runningRequestCount - 1);
        }

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

        private void HandleBridgeEvent(string eventJson)
        {
            if (string.IsNullOrEmpty(eventJson))
            {
                return;
            }

            try
            {
                var token = JObject.Parse(eventJson);
                string type = token["type"]?.ToString();

                if (type == "init")
                {
                    _bridgeInitCompleted = true;
                    _bridgeInitSucceeded = token["success"]?.Value<bool>() == true;
                    _bridgeInitError = token["error"]?.ToString();
                    return;
                }

                string requestId = token["requestId"]?.ToString();
                if (string.IsNullOrEmpty(requestId))
                {
                    return;
                }

                if (!_activeRequests.TryGetValue(requestId, out var state))
                {
                    return;
                }

                switch (type)
                {
                    case "chunk":
                        {
                            string chunk = token["content"]?.ToString() ?? string.Empty;
                            if (chunk.Length > 0)
                            {
                                state.Builder.Append(chunk);

                                if (state.IsStreaming)
                                {
                                    state.OnResponse?.Invoke(new ChatResponse
                                    {
                                        SessionId = state.SessionId,
                                        Role = "assistant",
                                        Content = state.Builder.ToString(),
                                        IsFinal = false,
                                        RawResponse = token
                                    });
                                }
                            }
                            break;
                        }
                    case "done":
                        {
                            string content = token["content"]?.ToString();
                            if (!string.IsNullOrEmpty(content) && state.Builder.Length == 0)
                            {
                                state.Builder.Append(content);
                            }

                            state.RawResponse = token["raw"];
                            state.IsDone = true;
                            break;
                        }
                    case "error":
                        {
                            state.ErrorMessage = token["error"]?.ToString() ?? "Unknown bridge error.";
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                if (_config.DebugMode)
                {
                    Debug.LogError($"[WebGLLlamaCppClient] Failed to process bridge event: {ex.Message}");
                }
            }
        }

        private static CancellationTokenSource LinkCancellation(CancellationToken optionToken, CancellationToken externalToken)
        {
            if (!optionToken.CanBeCanceled && !externalToken.CanBeCanceled)
            {
                return null;
            }

            if (optionToken.CanBeCanceled && externalToken.CanBeCanceled)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(optionToken, externalToken);
            }

            return optionToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(optionToken)
                : CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        }
    }

    internal sealed class WebGLLlamaCppBridgeReceiver : MonoBehaviour
    {
        private static WebGLLlamaCppBridgeReceiver _instance;

        public static event Action<string> BridgeEventReceived;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            BridgeEventReceived = null;
        }

        public static string EnsureReceiverGameObject()
        {
            if (_instance != null)
            {
                return _instance.gameObject.name;
            }

            var gameObject = new GameObject("EasyLocalLLM.WebGLLlamaBridge");
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<WebGLLlamaCppBridgeReceiver>();
            return gameObject.name;
        }

        public void OnBridgeEvent(string eventJson)
        {
            BridgeEventReceived?.Invoke(eventJson);
        }
    }
}
