using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Tests;

namespace EasyLocalLLM.LLM.Tests
{
    /// <summary>
    /// 非ストリーミングAPIの単体テスト
    /// モックを使用してOllamaサーバーなしで動作
    /// </summary>
    public class NonStreamingTests
    {
        private MockChatLLMClient _mockClient;

        [SetUp]
        public void SetUp()
        {
            _mockClient = new MockChatLLMClient();
            _mockClient.GlobalSystemPrompt = "You are a helpful AI assistant.";
            _mockClient.SetResponseDelay(0.05f); // テストを高速化
        }

        [TearDown]
        public void TearDown()
        {
            _mockClient.ClearAllSessions();
        }

        /// <summary>
        /// Test 1: シンプルなメッセージ送信
        /// </summary>
        [UnityTest]
        public IEnumerator Test_SimpleMessage_ReturnsResponse()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-session-1",
                Temperature = 0.7f,
                Seed = 42
            };

            bool isComplete = false;
            ChatResponse receivedResponse = null;
            ChatError receivedError = null;

            // Act
            yield return _mockClient.SendMessageAsync(
                "Hello, how are you?",
                (response, error) =>
                {
                    receivedResponse = response;
                    receivedError = error;
                    isComplete = true;
                },
                options
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNull(receivedError, "エラーが発生しないこと");
            Assert.IsNotNull(receivedResponse, "レスポンスが返されること");
            Assert.IsTrue(receivedResponse.IsFinal, "最終レスポンスであること");
            Assert.IsNotEmpty(receivedResponse.Content, "レスポンス内容が空でないこと");
        }

        /// <summary>
        /// Test 2: 温度0での確定的な回答
        /// </summary>
        [UnityTest]
        public IEnumerator Test_DeterministicResponse_WithTemperatureZero()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-deterministic",
                Temperature = 0.0f,
                Seed = 100
            };

            bool isComplete = false;
            ChatResponse receivedResponse = null;

            // Act
            yield return _mockClient.SendMessageAsync(
                "What is 2+2?",
                (response, error) =>
                {
                    if (response?.IsFinal == true)
                    {
                        receivedResponse = response;
                        isComplete = true;
                    }
                },
                options
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNotNull(receivedResponse, "レスポンスが返されること");
            Assert.IsTrue(receivedResponse.Content.Contains("4"), "正しい計算結果が含まれること");
        }

        /// <summary>
        /// Test 3: セッション履歴管理
        /// </summary>
        [UnityTest]
        public IEnumerator Test_SessionHistory_RemembersContext()
        {
            // Arrange
            string sessionId = "test-history-session";

            // Act & Assert - メッセージ 1
            bool isComplete1 = false;
            yield return _mockClient.SendMessageAsync(
                "My name is Alice",
                (response, error) =>
                {
                    if (response?.IsFinal == true)
                    {
                        isComplete1 = true;
                    }
                },
                new ChatRequestOptions { ChatId = sessionId, Temperature = 0.0f, Seed = 50 }
            );
            yield return new WaitUntil(() => isComplete1);

            // Act & Assert - メッセージ 2（履歴を含む）
            bool isComplete2 = false;
            ChatResponse finalResponse = null;

            yield return _mockClient.SendMessageAsync(
                "What is my name?",
                (response, error) =>
                {
                    if (response?.IsFinal == true)
                    {
                        finalResponse = response;
                        isComplete2 = true;
                    }
                },
                new ChatRequestOptions { ChatId = sessionId, Temperature = 0.0f, Seed = 50 }
            );
            yield return new WaitUntil(() => isComplete2);

            // Assert
            Assert.IsNotNull(finalResponse, "レスポンスが返されること");
            Assert.IsTrue(
                finalResponse.Content.ToLower().Contains("alice"),
                "名前を覚えていること"
            );
        }

        /// <summary>
        /// Test 4: エラーハンドリング
        /// </summary>
        [UnityTest]
        public IEnumerator Test_ErrorHandling_ReturnsError()
        {
            // Arrange
            _mockClient.SetSimulateError(true);

            bool isComplete = false;
            ChatError receivedError = null;

            // Act
            yield return _mockClient.SendMessageAsync(
                "Test message",
                (response, error) =>
                {
                    receivedError = error;
                    isComplete = true;
                },
                new ChatRequestOptions { ChatId = "error-test" }
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNotNull(receivedError, "エラーが返されること");
            Assert.AreEqual(ChatErrorType.ServerError, receivedError.ErrorType, "エラータイプが正しいこと");
            Assert.AreEqual(500, receivedError.HttpStatus, "HTTPステータスが正しいこと");
            Assert.IsTrue(receivedError.IsRetryable, "リトライ可能であること");
        }

        /// <summary>
        /// Test 5: 複数セッションの同時管理
        /// </summary>
        [UnityTest]
        public IEnumerator Test_MultipleSessions_MaintainSeparateHistory()
        {
            // Arrange & Act - セッション A
            bool completeA = false;
            yield return _mockClient.SendMessageAsync(
                "I like programming",
                (response, error) =>
                {
                    if (response?.IsFinal == true) completeA = true;
                },
                new ChatRequestOptions { ChatId = "session-a", Temperature = 0.0f, Seed = 60 }
            );
            yield return new WaitUntil(() => completeA);

            // Arrange & Act - セッション B
            bool completeB = false;
            yield return _mockClient.SendMessageAsync(
                "I like cooking",
                (response, error) =>
                {
                    if (response?.IsFinal == true) completeB = true;
                },
                new ChatRequestOptions { ChatId = "session-b", Temperature = 0.0f, Seed = 60 }
            );
            yield return new WaitUntil(() => completeB);

            // Act - セッション A に戻る
            bool completeA2 = false;
            ChatResponse responseA = null;

            yield return _mockClient.SendMessageAsync(
                "What is my hobby?",
                (response, error) =>
                {
                    if (response?.IsFinal == true)
                    {
                        responseA = response;
                        completeA2 = true;
                    }
                },
                new ChatRequestOptions { ChatId = "session-a", Temperature = 0.0f, Seed = 60 }
            );
            yield return new WaitUntil(() => completeA2);

            // Assert
            Assert.IsNotNull(responseA, "レスポンスが返されること");
            Assert.IsTrue(
                responseA.Content.ToLower().Contains("programming"),
                "セッションAの履歴を覚えていること"
            );
        }

        /// <summary>
        /// Test 6: Task版APIのテスト
        /// </summary>
        [UnityTest]
        public IEnumerator Test_TaskAPI_ReturnsResponse()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-task-api",
                Temperature = 0.7f
            };

            // Act
            var task = _mockClient.SendMessageTaskAsync("Hello", options);

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.IsFalse(task.IsFaulted, "タスクがエラーにならないこと");
            Assert.IsNotNull(task.Result, "レスポンスが返されること");
            Assert.IsTrue(task.Result.IsFinal, "最終レスポンスであること");
            Assert.IsNotEmpty(task.Result.Content, "レスポンス内容が空でないこと");
        }

        /// <summary>
        /// Test 7: カスタムレスポンスのテスト
        /// </summary>
        [UnityTest]
        public IEnumerator Test_CustomMockResponse_ReturnsCustomContent()
        {
            // Arrange
            string customResponse = "This is a custom mock response for testing.";
            _mockClient.SetMockResponse(customResponse);

            bool isComplete = false;
            ChatResponse receivedResponse = null;

            // Act
            yield return _mockClient.SendMessageAsync(
                "Any message",
                (response, error) =>
                {
                    if (response?.IsFinal == true)
                    {
                        receivedResponse = response;
                        isComplete = true;
                    }
                },
                new ChatRequestOptions { ChatId = "test-custom" }
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNotNull(receivedResponse, "レスポンスが返されること");
            Assert.AreEqual(customResponse, receivedResponse.Content, "カスタムレスポンスが返されること");
        }

        /// <summary>
        /// Test 8: セッション固有のシステムプロンプト設定
        /// </summary>
        [UnityTest]
        public IEnumerator Test_SessionSystemPrompt_SetAndRetrieve()
        {
            // Arrange
            string sessionId = "test-prompt-session";
            string customPrompt = "You are a helpful assistant specialized in programming.";

            // Act
            _mockClient.SetSessionSystemPrompt(sessionId, customPrompt);

            // Assert
            string retrievedPrompt = _mockClient.GetSessionSystemPrompt(sessionId);
            Assert.IsNotNull(retrievedPrompt, "プロンプトが設定されていること");
            Assert.AreEqual(customPrompt, retrievedPrompt, "設定したプロンプトが返されること");
        }

        /// <summary>
        /// Test 9: セッションプロンプトのリセット
        /// </summary>
        [UnityTest]
        public IEnumerator Test_SessionSystemPrompt_Reset()
        {
            // Arrange
            string sessionId = "test-prompt-reset";
            string customPrompt = "Custom prompt";

            // Act & Assert - 設定
            _mockClient.SetSessionSystemPrompt(sessionId, customPrompt);
            Assert.IsNotNull(_mockClient.GetSessionSystemPrompt(sessionId), "プロンプトが設定されていること");

            // Act & Assert - リセット
            _mockClient.ResetSessionSystemPrompt(sessionId);
            Assert.IsNull(_mockClient.GetSessionSystemPrompt(sessionId), "リセット後、プロンプトがnullであること");

            yield return null;
        }

        /// <summary>
        /// Test 10: 複数セッションへの一括プロンプト設定
        /// </summary>
        [UnityTest]
        public IEnumerator Test_SetSystemPromptForMultipleSessions()
        {
            // Arrange
            var sessionIds = new[] { "batch-session-1", "batch-session-2", "batch-session-3" };
            string commonPrompt = "You are a helpful assistant.";

            // Act
            _mockClient.SetSystemPromptForMultipleSessions(sessionIds, commonPrompt);

            // Assert
            foreach (var sessionId in sessionIds)
            {
                string prompt = _mockClient.GetSessionSystemPrompt(sessionId);
                Assert.AreEqual(commonPrompt, prompt, $"セッション {sessionId} にプロンプトが設定されていること");
            }

            yield return null;
        }

        /// <summary>
        /// Test 11: すべてのセッションプロンプトをリセット
        /// </summary>
        [UnityTest]
        public IEnumerator Test_ResetAllSessionSystemPrompts()
        {
            // Arrange
            var sessionIds = new[] { "reset-session-1", "reset-session-2", "reset-session-3" };
            foreach (var sessionId in sessionIds)
            {
                _mockClient.SetSessionSystemPrompt(sessionId, "Custom prompt");
            }

            // Act
            _mockClient.ResetAllSessionSystemPrompts();

            // Assert
            foreach (var sessionId in sessionIds)
            {
                Assert.IsNull(_mockClient.GetSessionSystemPrompt(sessionId), $"セッション {sessionId} のプロンプトがリセットされていること");
            }

            yield return null;
        }

        /// <summary>
        /// Test 12: セッション履歴とプロンプトをクリア
        /// </summary>
        [UnityTest]
        public IEnumerator Test_ClearSessionWithPrompt()
        {
            // Arrange
            string sessionId = "test-clear-with-prompt";
            string customPrompt = "Custom prompt";

            // 履歴とプロンプトを設定
            _mockClient.SetSessionSystemPrompt(sessionId, customPrompt);
            bool isComplete = false;
            yield return _mockClient.SendMessageAsync(
                "Test message",
                (response, error) =>
                {
                    if (response?.IsFinal == true) isComplete = true;
                },
                new ChatRequestOptions { ChatId = sessionId }
            );
            yield return new WaitUntil(() => isComplete);

            // Act
            _mockClient.ClearSessionWithPrompt(sessionId);

            // Assert - 履歴がクリアされていることを確認
            // セッションが完全にリセットされているので、新しいメッセージ後は履歴が少ないはず
            // (システムプロンプトもリセットされているため)
            string prompt = _mockClient.GetSessionSystemPrompt(sessionId);
            Assert.IsNull(prompt, "セッションプロンプトがリセットされていること");

            yield return null;
        }
    }
}

