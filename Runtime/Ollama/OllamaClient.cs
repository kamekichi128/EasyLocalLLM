using System;
using System.Collections;
using System.Collections.Generic;
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
                        Message = "Client is busy",
                        IsRetryable = false
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
                        Message = "Request cancelled",
                        IsRetryable = false
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

                bool hasError = false;
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
                            hasError = true;
                            errorInfo = new ChatError
                            {
                                ErrorType = LLMErrorType.InvalidResponse,
                                Message = $"Failed to parse response: {ex.Message}",
                                Exception = ex,
                                IsRetryable = false
                            };
                            callback?.Invoke(null, errorInfo);
                        }
                    },
                    error =>
                    {
                        hasError = true;
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
    }
}
