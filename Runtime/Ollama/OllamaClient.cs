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
        private readonly HashSet<string> _runningChatIds = new();
        private readonly List<PendingRequest> _pendingRequests = new();
        private long _pendingSequence = 0;

        private class PendingRequest
        {
            public string ChatId { get; }
            public int Priority { get; }
            public long Order { get; }

            public PendingRequest(string chatId, int priority, long order)
            {
                ChatId = chatId;
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
            if (_runningChatIds.Count >= maxConcurrent)
            {
                return -1;
            }

            int bestIndex = -1;
            for (int i = 0; i < _pendingRequests.Count; i++)
            {
                var candidate = _pendingRequests[i];
                if (_runningChatIds.Contains(candidate.ChatId))
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

        private IEnumerator WaitForTurn(string chatId, ChatRequestOptions options, Action<ChatError> onError)
        {
            int maxConcurrent = Mathf.Max(1, _config.MaxConcurrentSessions);

            if (!options.WaitIfBusy)
            {
                if (_pendingRequests.Count > 0 || _runningChatIds.Contains(chatId) || _runningChatIds.Count >= maxConcurrent)
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

            var pending = new PendingRequest(chatId, options.Priority, ++_pendingSequence);
            InsertPendingSorted(pending);

            while (true)
            {
                if (options.CancellationToken.IsCancellationRequested)
                {
                    _pendingRequests.Remove(pending);
                    onError?.Invoke(new ChatError
                    {
                        ErrorType = LLMErrorType.Cancelled,
                        Message = $"Request cancelled for session '{chatId}' by user"
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
        }

        public void ClearAllMessages() => _historyManager.ClearAll();
        public void ClearMessages(string chatId) => _historyManager.Clear(chatId);

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
            string chatId = options.ChatId ?? Guid.NewGuid().ToString();

            bool waitFailed = false;
            yield return WaitForTurn(chatId, options, error =>
            {
                waitFailed = true;
                callback?.Invoke(null, error);
            });
            if (waitFailed)
            {
                yield break;
            }

            _runningChatIds.Add(chatId);

            try
            {
                var session = _historyManager.GetOrCreateSession(chatId, options.SystemPrompt);
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

                var requestContent = new
                {
                    model = options.ModelName ?? _config.DefaultModelName,
                    messages = history,
                    stream = false,
                    options = new
                    {
                        seed = options.Seed ?? _config.DefaultSeed,
                        temperature = options.Temperature
                    }
                };

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                string url = _config.ServerUrl + "/api/chat";

                ChatError errorInfo = null;

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
                                Content = chatMessage?["content"]?.ToString() ?? "",
                                Role = chatMessage?["role"]?.ToString() ?? "assistant",
                                IsFinal = true,
                                RawResponse = responseBody
                            };

                            // 履歴に追加
                            _historyManager.AddMessage(chatId, new ChatMessage
                            {
                                Role = response.Role,
                                Content = response.Content
                            }, options.MaxHistory);

                            callback?.Invoke(response, null);
                        }
                        catch (Exception ex)
                        {
                            errorInfo = new ChatError
                            {
                                ErrorType = LLMErrorType.InvalidResponse,
                                Message = $"Failed to parse response from model '{options.ModelName ?? _config.DefaultModelName}': {ex.Message}. Check Ollama version compatibility.",
                                Exception = ex
                            };
                            callback?.Invoke(null, errorInfo);
                        }
                    },
                    error =>
                    {
                        errorInfo = error;
                        callback?.Invoke(null, error);
                    },
                    options.CancellationToken
                );
            }
            finally
            {
                _runningChatIds.Remove(chatId);
            }
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
            string chatId = options.ChatId ?? Guid.NewGuid().ToString();

            bool waitFailed = false;
            yield return WaitForTurn(chatId, options, error =>
            {
                waitFailed = true;
                callback?.Invoke(null, error);
            });
            if (waitFailed)
            {
                yield break;
            }

            _runningChatIds.Add(chatId);

            try
            {
                var session = _historyManager.GetOrCreateSession(chatId, options.SystemPrompt);
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

                var requestContent = new
                {
                    model = options.ModelName ?? _config.DefaultModelName,
                    messages = history,
                    stream = true,
                    options = new
                    {
                        seed = options.Seed ?? _config.DefaultSeed,
                        temperature = options.Temperature
                    }
                };

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                string url = _config.ServerUrl + "/api/chat";

                string fullResponse = "";
                string fullRole = "assistant";
                string lastRawChunk = null;

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

                                var response = new ChatResponse
                                {
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
                        if (isSuccess && !string.IsNullOrEmpty(fullResponse))
                        {
                            // 履歴に追加
                            _historyManager.AddMessage(chatId, new ChatMessage
                            {
                                Role = fullRole,
                                Content = fullResponse
                            }, options.MaxHistory);

                            // 最終チャンク
                            var finalResponse = new ChatResponse
                            {
                                Content = fullResponse,
                                Role = fullRole,
                                IsFinal = true,
                                RawResponse = lastRawChunk ?? fullResponse
                            };

                            callback?.Invoke(finalResponse, null);
                        }
                    },
                    error =>
                    {
                        callback?.Invoke(null, error);
                    },
                    options.CancellationToken
                );
            }
            finally
            {
                _runningChatIds.Remove(chatId);
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
    }
}
