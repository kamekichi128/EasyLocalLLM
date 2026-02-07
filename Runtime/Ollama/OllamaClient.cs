using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Manager;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// Ollama LLM クライアントの実装
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

        private class PendingRequest
        {
            public string SessionId { get; }
            public int Priority { get; }
            public long Order { get; }

            public PendingRequest(string sessionId, int priority, long order)
            {
                SessionId = sessionId;
                Priority = priority;
                Order = order;
            }
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

        public string GlobalSystemPrompt { get; set; } = "You are a helpful AI assistant.";

        /// <summary>
        /// OllamaClient を初期化
        /// </summary>
        public OllamaClient(OllamaConfig config = null)
        {
            _config = config ?? new OllamaConfig();
            _historyManager = new ChatHistoryManager();
            _httpHelper = new HttpRequestHelper(_config);
            _toolManager = new ToolManager(_config.DebugMode);
        }

        public void ClearAllMessages() => _historyManager.ClearAll();
        public void ClearMessages(string sessionId) => _historyManager.Clear(sessionId);

        /// <summary>
        /// セッション情報を取得
        /// </summary>
        public ChatSession GetSession(string sessionId)
        {
            return _historyManager.GetOrCreateSession(sessionId);
        }

        /// <summary>
        /// すべてのセッションIDを取得
        /// </summary>
        public IEnumerable<string> GetAllSessionIds()
        {
            return _historyManager.GetAllSessionIds();
        }

        /// <summary>
        /// セッションが存在するか確認
        /// </summary>
        public bool HasSession(string sessionId)
        {
            return _historyManager.HasSession(sessionId);
        }

        /// <summary>
        /// セッションのメッセージ数を取得
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
        /// セッションのシステムプロンプトを設定
        /// </summary>
        /// <param name="sessionId">セッションID</param>
        /// <param name="systemPrompt">設定するシステムプロンプト</param>
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
        /// セッションのシステムプロンプトを取得
        /// </summary>
        /// <param name="sessionId">セッションID</param>
        /// <returns>システムプロンプト、セッションが存在しない場合はnull</returns>
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
        /// セッションのシステムプロンプトをリセット（グローバルプロンプトを使用するように）
        /// </summary>
        /// <param name="sessionId">セッションID</param>
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
        /// 複数のセッションに対してシステムプロンプトをバッチ設定
        /// </summary>
        /// <param name="sessionIds">セッションIDのリスト</param>
        /// <param name="systemPrompt">設定するシステムプロンプト</param>
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
        /// すべてのセッションのシステムプロンプトをリセット
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
        /// セッションのシステムプロンプトと履歴をリセット
        /// </summary>
        /// <param name="sessionId">セッションID</param>
        public void ClearSessionWithPrompt(string sessionId)
        {
            _historyManager.Clear(sessionId);
            ResetSessionSystemPrompt(sessionId);

            if (_config.DebugMode)
            {
                UnityEngine.Debug.Log($"[Ollama] Session '{sessionId}' cleared with prompt reset");
            }
        }

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
        /// メッセージを Task で送信（完全回答を取得）
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
        /// 非同期でメッセージを送信（一度に完全な回答を取得）
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

                // システムプロンプトがなければ追加
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

                // ユーザーメッセージを追加
                history.Add(new ChatMessage { Role = "user", Content = message });

                // Tool対応: ツールループ処理
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

                    // 使用するツール一覧を取得
                    var tools = options.Tools ?? _toolManager.GetAllTools();

                    // リクエスト作成
                    object requestContent;
                    if (tools != null && tools.Count > 0)
                    {
                        // ツールを含むリクエスト
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
                    else
                    {
                        // ツールなしのリクエスト
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

                                // Tool calls を抽出
                                var toolCallsArray = chatMessage?["tool_calls"] as JArray;
                                if (toolCallsArray != null && toolCallsArray.Count > 0)
                                {
                                    response.ToolCalls = new List<Core.ToolCall>();
                                    foreach (var tc in toolCallsArray)
                                    {
                                        var toolCall = new Core.ToolCall
                                        {
                                            ToolCallId = tc["id"]?.ToString(),
                                            ToolName = tc["function"]?["name"]?.ToString(),
                                            Arguments = tc["function"]?["arguments"]?.ToString()
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

                    // Tool calls の処理
                    if (finalResponse.ToolCalls != null && finalResponse.ToolCalls.Count > 0)
                    {
                        toolIterations++;

                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Detected {finalResponse.ToolCalls.Count} tool calls (iteration {toolIterations}/{maxIterations})");
                        }

                        // Assistant メッセージを履歴に追加（tool_calls 付き）
                        history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = finalResponse.Content,
                            ToolCalls = finalResponse.ToolCalls
                        });

                        // 各ツールを実行
                        foreach (var toolCall in finalResponse.ToolCalls)
                        {
                            string toolResult;
                            try
                            {
                                toolResult = _toolManager.ExecuteTool(toolCall.ToolName, toolCall.Arguments);
                            }
                            catch (Exception ex)
                            {
                                toolResult = $"Error: {ex.Message}";
                                if (_config.DebugMode)
                                {
                                    UnityEngine.Debug.LogError($"[Ollama] Tool execution failed: {ex.Message}");
                                }
                            }

                            // ツール実行結果を履歴に追加
                            history.Add(new ChatMessage
                            {
                                Role = "tool",
                                Content = toolResult,
                                ToolCallId = toolCall.ToolCallId
                            });
                        }

                        // 次のループでツール結果を含めて再送
                        hasToolCalls = true;
                    }
                    else
                    {
                        // Tool calls がない場合は終了
                        hasToolCalls = false;

                        // 履歴に追加
                        _historyManager.AddMessage(sessionId, new ChatMessage
                        {
                            Role = finalResponse.Role,
                            Content = finalResponse.Content
                        }, options.MaxHistory);

                        callback?.Invoke(finalResponse, null);
                    }
                }

                // 最大反復回数に達した場合
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
        /// メッセージをシリアライズ（Ollama API 形式）
        /// </summary>
        private object[] SerializeMessages(List<ChatMessage> messages)
        {
            var result = new List<object>();

            foreach (var msg in messages)
            {
                if (msg.Role == "tool")
                {
                    // Tool 結果メッセージ
                    result.Add(new
                    {
                        role = "tool",
                        content = msg.Content,
                        tool_call_id = msg.ToolCallId
                    });
                }
                else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    // Tool calls を含む Assistant メッセージ
                    result.Add(new
                    {
                        role = msg.Role,
                        content = msg.Content,
                        tool_calls = msg.ToolCalls.Select(tc => new
                        {
                            id = tc.ToolCallId,
                            type = "function",
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
                    // 通常のメッセージ
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
        /// ストリーミングでメッセージを送信（段階的に回答を受け取る）
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

                // システムプロンプトがなければ追加
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

                // ユーザーメッセージを追加
                history.Add(new ChatMessage { Role = "user", Content = message });

                // Tool対応: ツールループ処理
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

                    // 使用するツール一覧を取得
                    var tools = options.Tools ?? _toolManager.GetAllTools();

                    // リクエスト作成
                    object requestContent;
                    if (tools != null && tools.Count > 0)
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

                                    // Tool calls の検出（最終チャンク）
                                    var toolCallsArray = chunkMessage["tool_calls"] as JArray;
                                    if (toolCallsArray != null && toolCallsArray.Count > 0)
                                    {
                                        detectedToolCalls = new List<Core.ToolCall>();
                                        foreach (var tc in toolCallsArray)
                                        {
                                            var toolCall = new Core.ToolCall
                                            {
                                                ToolCallId = tc["id"]?.ToString(),
                                                ToolName = tc["function"]?["name"]?.ToString(),
                                                Arguments = tc["function"]?["arguments"]?.ToString()
                                            };
                                            detectedToolCalls.Add(toolCall);
                                        }
                                    }

                                    // 進捗コールバック（IsFinal = false）
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

                    // Tool calls の処理
                    if (detectedToolCalls != null && detectedToolCalls.Count > 0)
                    {
                        toolIterations++;

                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Detected {detectedToolCalls.Count} tool calls (iteration {toolIterations}/{maxIterations})");
                        }

                        // Assistant メッセージを履歴に追加（tool_calls 付き）
                        history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = fullResponse,
                            ToolCalls = detectedToolCalls
                        });

                        // 各ツールを実行
                        foreach (var toolCall in detectedToolCalls)
                        {
                            string toolResult;
                            try
                            {
                                toolResult = _toolManager.ExecuteTool(toolCall.ToolName, toolCall.Arguments);
                            }
                            catch (Exception ex)
                            {
                                toolResult = $"Error: {ex.Message}";
                                if (_config.DebugMode)
                                {
                                    UnityEngine.Debug.LogError($"[Ollama] Tool execution failed: {ex.Message}");
                                }
                            }

                            // ツール実行結果を履歴に追加
                            history.Add(new ChatMessage
                            {
                                Role = "tool",
                                Content = toolResult,
                                ToolCallId = toolCall.ToolCallId
                            });
                        }

                        // 次のループでツール結果を含めて再送
                        hasToolCalls = true;
                    }
                    else
                    {
                        // Tool calls がない場合は終了
                        hasToolCalls = false;

                        if (!string.IsNullOrEmpty(fullResponse))
                        {
                            // 履歴に追加
                            _historyManager.AddMessage(sessionId, new ChatMessage
                            {
                                Role = fullRole,
                                Content = fullResponse
                            }, options.MaxHistory);

                            // 最終チャンク
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

                // 最大反復回数に達した場合
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
        /// メッセージをストリーミングで送信（Task 版）
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
        /// 設定を更新
        /// </summary>
        public void UpdateConfig(OllamaConfig newConfig)
        {
            if (newConfig != null)
            {
                // 既存の設定を更新
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
        /// セッション履歴をファイルに保存
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
        /// ファイルからセッション履歴を復元
        /// </summary>
        public void LoadSession(string filePath, string sessionId, string encryptionKey = null)
        {
            var loadedSession = ChatSessionPersistence.LoadSession(filePath, encryptionKey);

            // セッションIDが異なる場合は指定されたIDで上書き
            if (loadedSession.Id != sessionId)
            {
                loadedSession.Id = sessionId;
            }

            // 既存のセッション履歴をクリア
            _historyManager.Clear(sessionId);

            // 読み込んだメッセージを復元
            foreach (var message in loadedSession.History)
            {
                _historyManager.AddMessage(sessionId, message, null);
            }

            // システムプロンプトを復元
            var session = _historyManager.GetOrCreateSession(sessionId);
            if (!string.IsNullOrEmpty(loadedSession.SystemPrompt))
            {
                session.SystemPrompt = loadedSession.SystemPrompt;
            }
        }

        /// <summary>
        /// すべてのセッション履歴をディレクトリに保存
        /// </summary>
        public void SaveAllSessions(string dirPath, string encryptionKey = null)
        {
            ChatSessionPersistence.SaveAllSessions(dirPath, _historyManager, encryptionKey);
        }

        /// <summary>
        /// ディレクトリからすべてのセッション履歴を復元
        /// </summary>
        public void LoadAllSessions(string dirPath, string encryptionKey = null)
        {
            ChatSessionPersistence.LoadAllSessions(dirPath, _historyManager, encryptionKey);
        }

        #region Tool Management

        /// <summary>
        /// ツールを登録（スキーマ自動生成）
        /// </summary>
        /// <param name="name">ツール名</param>
        /// <param name="description">ツール説明</param>
        /// <param name="callback">コールバック関数（任意のシグネチャ）</param>
        public void RegisterTool(string name, string description, Delegate callback)
        {
            _toolManager.RegisterTool(name, description, callback);
        }

        /// <summary>
        /// ツールを登録（手動スキーマ指定）
        /// </summary>
        /// <param name="name">ツール名</param>
        /// <param name="description">ツール説明</param>
        /// <param name="inputSchema">JSON Schema</param>
        /// <param name="callback">コールバック関数</param>
        public void RegisterTool(string name, string description, object inputSchema, Delegate callback)
        {
            _toolManager.RegisterTool(name, description, inputSchema, callback);
        }

        /// <summary>
        /// ツールを削除
        /// </summary>
        /// <param name="name">ツール名</param>
        /// <returns>削除に成功した場合 true</returns>
        public bool UnregisterTool(string name)
        {
            return _toolManager.UnregisterTool(name);
        }

        /// <summary>
        /// すべてのツールを削除
        /// </summary>
        public void RemoveAllTools()
        {
            _toolManager.RemoveAllTools();
        }

        /// <summary>
        /// 登録済みツール一覧を取得
        /// </summary>
        /// <returns>ツール定義のリスト</returns>
        public List<Core.ToolDefinition> GetRegisteredTools()
        {
            return _toolManager.GetAllTools();
        }

        /// <summary>
        /// ツールが登録されているか確認
        /// </summary>
        /// <param name="name">ツール名</param>
        /// <returns>登録されている場合 true</returns>
        public bool HasTool(string name)
        {
            return _toolManager.HasTool(name);
        }

        #endregion
    }
}
