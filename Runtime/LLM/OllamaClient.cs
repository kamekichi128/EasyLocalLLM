using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EasyLocalLLM.LLM
{
    /// <summary>
    /// Ollama LLM クライアントの実装
    /// </summary>
    public class OllamaClient : IChatLLMClient
    {
        private readonly OllamaConfig _config;
        private readonly ChatHistoryManager _historyManager;
        private readonly HttpRequestHelper _httpHelper;
        private bool _isRunning = false;

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
        /// 非同期でメッセージを送信（一度に完全な回答を取得）
        /// </summary>
        public IEnumerator SendMessageAsync(
            string message,
            Action<ChatResponse, ChatError, bool> callback,
            ChatRequestOptions options = null)
        {
            options ??= new ChatRequestOptions();
            string chatId = options.ChatId ?? Guid.NewGuid().ToString();

            if (_isRunning)
            {
                if (options.WaitIfBusy)
                {
                    while (_isRunning)
                        yield return null;
                }
                else
                {
                    callback?.Invoke(null, new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = "Client is busy",
                        IsRetryable = false
                    }, false);
                    yield break;
                }
            }

            _isRunning = true;

            try
            {
                var history = _historyManager.GetHistory(chatId);

                // システムプロンプトがなければ追加
                if (history.Count == 0)
                {
                    string systemPrompt = options.SystemPrompt ?? GlobalSystemPrompt;
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

                            callback?.Invoke(response, null, true);
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
                            callback?.Invoke(null, errorInfo, true);
                        }
                    },
                    error =>
                    {
                        hasError = true;
                        errorInfo = error;
                        callback?.Invoke(null, error, true);
                    }
                );
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// ストリーミングでメッセージを送信（段階的に回答を受け取る）
        /// </summary>
        public IEnumerator SendMessageStreamingAsync(
            string message,
            Action<ChatResponse, ChatError, bool> callback,
            ChatRequestOptions options = null)
        {
            options ??= new ChatRequestOptions();
            string chatId = options.ChatId ?? Guid.NewGuid().ToString();

            if (_isRunning)
            {
                if (options.WaitIfBusy)
                {
                    while (_isRunning)
                        yield return null;
                }
                else
                {
                    callback?.Invoke(null, new ChatError
                    {
                        ErrorType = LLMErrorType.Unknown,
                        Message = "Client is busy",
                        IsRetryable = false
                    }, false);
                    yield break;
                }
            }

            _isRunning = true;

            try
            {
                var history = _historyManager.GetHistory(chatId);

                // システムプロンプトがなければ追加
                if (history.Count == 0)
                {
                    string systemPrompt = options.SystemPrompt ?? GlobalSystemPrompt;
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

                yield return _httpHelper.ExecuteStreamingWithRetry(
                    url,
                    json,
                    chunk =>
                    {
                        try
                        {
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

                                callback?.Invoke(response, null, false);
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
                                RawResponse = fullResponse
                            };

                            callback?.Invoke(finalResponse, null, true);
                        }
                    },
                    error =>
                    {
                        callback?.Invoke(null, error, true);
                    }
                );
            }
            finally
            {
                _isRunning = false;
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
