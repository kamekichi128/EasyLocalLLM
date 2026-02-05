using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyLocalLLM.LLM.Core;
using UnityEngine;

namespace EasyLocalLLM.LLM.Tests
{
    /// <summary>
    /// IChatLLMClient のモック実装
    /// Ollama サーバーの実行有無に関わらず動作可能
    /// </summary>
    public class MockChatLLMClient : IChatLLMClient
    {
        private Dictionary<string, List<ChatMessage>> _sessions = new Dictionary<string, List<ChatMessage>>();
        private Dictionary<string, string> _sessionSystemPrompts = new Dictionary<string, string>();
        private bool _simulateError = false;
        private float _responseDelay = 0.1f;
        private string _mockResponse = "This is a mock response.";

        public string GlobalSystemPrompt { get; set; } = "You are a helpful assistant.";

        public MockChatLLMClient()
        {
        }

        /// <summary>
        /// エラーをシミュレートするかどうかを設定
        /// </summary>
        public void SetSimulateError(bool simulate)
        {
            _simulateError = simulate;
        }

        /// <summary>
        /// レスポンス遅延時間を設定
        /// </summary>
        public void SetResponseDelay(float delay)
        {
            _responseDelay = delay;
        }

        /// <summary>
        /// モックレスポンスの内容を設定
        /// </summary>
        public void SetMockResponse(string response)
        {
            _mockResponse = response;
        }

        /// <summary>
        /// セッション履歴をクリア
        /// </summary>
        public void ClearSession(string chatId)
        {
            if (_sessions.ContainsKey(chatId))
            {
                _sessions[chatId].Clear();
            }
        }

        /// <summary>
        /// 全セッションをクリア
        /// </summary>
        public void ClearAllSessions()
        {
            _sessions.Clear();
            _sessionSystemPrompts.Clear();
        }

        /// <summary>
        /// セッションのシステムプロンプトを設定
        /// </summary>
        public void SetSessionSystemPrompt(string sessionId, string systemPrompt)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
            }

            _sessionSystemPrompts[sessionId] = systemPrompt;
        }

        /// <summary>
        /// セッションのシステムプロンプトを取得
        /// </summary>
        public string GetSessionSystemPrompt(string sessionId)
        {
            return _sessionSystemPrompts.ContainsKey(sessionId) ? _sessionSystemPrompts[sessionId] : null;
        }

        /// <summary>
        /// セッションのシステムプロンプトをリセット
        /// </summary>
        public void ResetSessionSystemPrompt(string sessionId)
        {
            _sessionSystemPrompts.Remove(sessionId);
        }

        /// <summary>
        /// 複数のセッションに対してシステムプロンプトをバッチ設定
        /// </summary>
        public void SetSystemPromptForMultipleSessions(IEnumerable<string> sessionIds, string systemPrompt)
        {
            if (sessionIds == null)
            {
                throw new ArgumentNullException(nameof(sessionIds));
            }

            foreach (var sessionId in sessionIds)
            {
                SetSessionSystemPrompt(sessionId, systemPrompt);
            }
        }

        /// <summary>
        /// すべてのセッションのシステムプロンプトをリセット
        /// </summary>
        public void ResetAllSessionSystemPrompts()
        {
            _sessionSystemPrompts.Clear();
        }

        /// <summary>
        /// セッションのシステムプロンプトと履歴をリセット
        /// </summary>
        public void ClearSessionWithPrompt(string sessionId)
        {
            ClearSession(sessionId);
            ResetSessionSystemPrompt(sessionId);
        }

        public IEnumerator SendMessageAsync(
            string userMessage,
            Action<ChatResponse, ChatError> callback,
            ChatRequestOptions options = null)
        {
            options = options ?? new ChatRequestOptions();
            string chatId = options.ChatId ?? "default";

            // セッション履歴を取得または作成
            if (!_sessions.ContainsKey(chatId))
            {
                _sessions[chatId] = new List<ChatMessage>();
            }

            var history = _sessions[chatId];

            // ユーザーメッセージを履歴に追加
            history.Add(new ChatMessage("user", userMessage));

            // 履歴制限を適用
            int maxHistory = options.MaxHistory ?? 50;
            while (history.Count > maxHistory * 2)
            {
                history.RemoveAt(0);
            }

            // 遅延をシミュレート
            yield return new WaitForSeconds(_responseDelay);

            // エラーをシミュレート
            if (_simulateError)
            {
                var error = new ChatError(
                    ChatErrorType.ServerError,
                    "Mock error: Simulated server error",
                    500,
                    true
                );
                callback?.Invoke(null, error);
                yield break;
            }

            // レスポンスを生成（履歴を考慮）
            string responseContent = GenerateResponse(userMessage, history, options);

            // アシスタントのレスポンスを履歴に追加
            history.Add(new ChatMessage("assistant", responseContent));

            var response = new ChatResponse
            {
                Content = responseContent,
                IsFinal = true,
                TotalDuration = (long)(_responseDelay * 1000000000), // ナノ秒
                PromptEvalCount = userMessage.Split(' ').Length,
                EvalCount = responseContent.Split(' ').Length
            };

            callback?.Invoke(response, null);
        }

        public IEnumerator SendMessageStreamingAsync(
            string userMessage,
            Action<ChatResponse, ChatError> callback,
            ChatRequestOptions options = null)
        {
            options = options ?? new ChatRequestOptions();
            string chatId = options.ChatId ?? "default";

            // セッション履歴を取得または作成
            if (!_sessions.ContainsKey(chatId))
            {
                _sessions[chatId] = new List<ChatMessage>();
            }

            var history = _sessions[chatId];

            // ユーザーメッセージを履歴に追加
            history.Add(new ChatMessage("user", userMessage));

            // 履歴制限を適用
            int maxHistory = options.MaxHistory ?? 50;
            while (history.Count > maxHistory * 2)
            {
                history.RemoveAt(0);
            }

            // エラーをシミュレート
            if (_simulateError)
            {
                var error = new ChatError(
                    ChatErrorType.ServerError,
                    "Mock error: Simulated server error",
                    500,
                    true
                );
                callback?.Invoke(null, error);
                yield break;
            }

            // レスポンスを生成
            string responseContent = GenerateResponse(userMessage, history, options);

            // ストリーミングをシミュレート（単語ごとに分割）
            string[] words = responseContent.Split(' ');
            string accumulatedContent = "";

            for (int i = 0; i < words.Length; i++)
            {
                accumulatedContent += (i > 0 ? " " : "") + words[i];

                var partialResponse = new ChatResponse
                {
                    Content = accumulatedContent,
                    IsFinal = false,
                    TotalDuration = 0,
                    PromptEvalCount = 0,
                    EvalCount = i + 1
                };

                callback?.Invoke(partialResponse, null);

                // ストリーミングの遅延をシミュレート
                yield return new WaitForSeconds(_responseDelay / words.Length);
            }

            // アシスタントのレスポンスを履歴に追加
            history.Add(new ChatMessage("assistant", responseContent));

            // 最終レスポンス
            var finalResponse = new ChatResponse
            {
                Content = responseContent,
                IsFinal = true,
                TotalDuration = (long)(_responseDelay * 1000000000),
                PromptEvalCount = userMessage.Split(' ').Length,
                EvalCount = words.Length
            };

            callback?.Invoke(finalResponse, null);
        }

        public Task<ChatResponse> SendMessageTaskAsync(string userMessage, ChatRequestOptions options = null)
        {
            var tcs = new TaskCompletionSource<ChatResponse>();
            var runner = CoroutineRunner.Instance;

            runner.StartCoroutine(SendMessageAsync(
                userMessage,
                (response, error) =>
                {
                    if (error != null)
                    {
                        tcs.SetException(new ChatLLMException(error));
                    }
                    else
                    {
                        tcs.SetResult(response);
                    }
                },
                options
            ));

            return tcs.Task;
        }

        public Task<ChatResponse> SendMessageStreamingTaskAsync(
            string userMessage,
            Action<ChatResponse> onProgress,
            ChatRequestOptions options = null)
        {
            var tcs = new TaskCompletionSource<ChatResponse>();
            var runner = CoroutineRunner.Instance;

            runner.StartCoroutine(SendMessageStreamingAsync(
                userMessage,
                (response, error) =>
                {
                    if (error != null)
                    {
                        tcs.SetException(new ChatLLMException(error));
                    }
                    else if (response.IsFinal)
                    {
                        tcs.SetResult(response);
                    }
                    else
                    {
                        onProgress?.Invoke(response);
                    }
                },
                options
            ));

            return tcs.Task;
        }

        /// <summary>
        /// モックレスポンスを生成（履歴を考慮）
        /// </summary>
        private string GenerateResponse(string userMessage, List<ChatMessage> history, ChatRequestOptions options)
        {
            // カスタムレスポンスが設定されている場合はそれを使用
            if (_mockResponse != "This is a mock response.")
            {
                return _mockResponse;
            }

            // 簡単な履歴対応のロジック
            string lowerMessage = userMessage.ToLower();

            // 名前を覚えているかテスト
            if (lowerMessage.Contains("my name is"))
            {
                string name = ExtractName(userMessage);
                return $"Nice to meet you, {name}!";
            }

            if (lowerMessage.Contains("what is my name") || lowerMessage.Contains("my name"))
            {
                // 履歴から名前を探す
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    if (history[i].Role == "user" && history[i].Content.ToLower().Contains("my name is"))
                    {
                        string name = ExtractName(history[i].Content);
                        return $"Your name is {name}.";
                    }
                }
                return "I don't recall you telling me your name.";
            }

            // 好みを覚えているかテスト
            if (lowerMessage.Contains("i like") || lowerMessage.Contains("favorite"))
            {
                string preference = ExtractPreference(userMessage);
                return $"That's great! I'll remember that you like {preference}.";
            }

            if (lowerMessage.Contains("what") && (lowerMessage.Contains("like") || lowerMessage.Contains("favorite")))
            {
                // 履歴から好みを探す
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    if (history[i].Role == "user")
                    {
                        string content = history[i].Content.ToLower();
                        if (content.Contains("i like") || content.Contains("favorite"))
                        {
                            string preference = ExtractPreference(history[i].Content);
                            return $"You mentioned that you like {preference}.";
                        }
                    }
                }
                return "I don't recall you mentioning your preferences.";
            }

            // 計算テスト
            if (lowerMessage.Contains("2+2") || lowerMessage.Contains("2 + 2"))
            {
                return "2 + 2 = 4";
            }

            // ジョークのリクエスト
            if (lowerMessage.Contains("joke"))
            {
                return "Why did the programmer quit his job? Because he didn't get arrays!";
            }

            // 俳句のリクエスト
            if (lowerMessage.Contains("haiku"))
            {
                return "Cherry blossoms fall\nSoftly on the morning dew\nSpring whispers hello";
            }

            // 機械学習の説明
            if (lowerMessage.Contains("machine learning"))
            {
                return "Machine learning is a subset of artificial intelligence that enables systems to learn and improve from experience without being explicitly programmed. It focuses on developing algorithms that can access data and use it to learn for themselves.";
            }

            // 宇宙の事実
            if (lowerMessage.Contains("space") && lowerMessage.Contains("fact"))
            {
                return "The Sun accounts for about 99.86% of the total mass of the Solar System.";
            }

            // デフォルトレスポンス
            return $"Mock response to: '{userMessage}'. Temperature: {options?.Temperature ?? 0.7f}, Seed: {options?.Seed ?? 0}";
        }

        private string ExtractName(string message)
        {
            // "my name is X" から X を抽出
            int index = message.ToLower().IndexOf("my name is");
            if (index >= 0)
            {
                string namepart = message.Substring(index + "my name is".Length).Trim();
                string[] words = namepart.Split(new[] { ' ', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    return words[0];
                }
            }
            return "Unknown";
        }

        private string ExtractPreference(string message)
        {
            // "I like X" や "favorite X" から X を抽出
            string lower = message.ToLower();

            int likeIndex = lower.IndexOf("i like");
            if (likeIndex >= 0)
            {
                string pref = message.Substring(likeIndex + "i like".Length).Trim();
                string[] words = pref.Split(new[] { ' ', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    return words[0];
                }
            }

            int favIndex = lower.IndexOf("favorite");
            if (favIndex >= 0)
            {
                string pref = message.Substring(favIndex + "favorite".Length).Trim();
                // "favorite color is X" のようなパターンに対応
                if (pref.ToLower().StartsWith("color is"))
                {
                    pref = pref.Substring("color is".Length).Trim();
                }
                string[] words = pref.Split(new[] { ' ', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    return words[0];
                }
            }

            return "something";
        }
    }
}
