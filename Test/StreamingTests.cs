using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Tests;

namespace EasyLocalLLM.LLM.Tests
{
    /// <summary>
    /// ストリーミングAPIの単体テスト
    /// モックを使用してOllamaサーバーなしで動作
    /// </summary>
    public class StreamingTests
    {
        private MockChatLLMClient _mockClient;

        [SetUp]
        public void SetUp()
        {
            _mockClient = new MockChatLLMClient();
            _mockClient.GlobalSystemPrompt = "You are a helpful AI assistant.";
            _mockClient.SetResponseDelay(0.1f); // テストを高速化
        }

        [TearDown]
        public void TearDown()
        {
            _mockClient.ClearAllSessions();
        }

        /// <summary>
        /// Test 1: シンプルなストリーミング
        /// </summary>
        [UnityTest]
        public IEnumerator Test_SimpleStreaming_ReceivesMultipleChunks()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-stream-1",
                Temperature = 0.7f,
                Seed = 42
            };

            int chunkCount = 0;
            bool isComplete = false;
            ChatResponse finalResponse = null;

            // Act
            yield return _mockClient.SendMessageStreamingAsync(
                "Tell me a short joke",
                (response, error) =>
                {
                    if (error != null)
                    {
                        isComplete = true;
                        return;
                    }

                    if (!response.IsFinal)
                    {
                        chunkCount++;
                    }
                    else
                    {
                        finalResponse = response;
                        isComplete = true;
                    }
                },
                options
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNotNull(finalResponse, "最終レスポンスが返されること");
            Assert.IsTrue(finalResponse.IsFinal, "最終フラグが立っていること");
            Assert.Greater(chunkCount, 0, "ストリーミングチャンクが複数回送信されること");
            Assert.IsNotEmpty(finalResponse.Content, "レスポンス内容が空でないこと");
        }

        /// <summary>
        /// Test 2: 長文レスポンスのストリーミング
        /// </summary>
        [UnityTest]
        public IEnumerator Test_LongResponseStreaming_ReceivesProgressiveUpdates()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-stream-long",
                Temperature = 0.7f,
                Seed = 43,
                MaxHistory = 10
            };

            int chunkCount = 0;
            bool isComplete = false;
            string firstChunk = null;
            ChatResponse finalResponse = null;

            // Act
            yield return _mockClient.SendMessageStreamingAsync(
                "Explain machine learning in detail",
                (response, error) =>
                {
                    if (error != null)
                    {
                        isComplete = true;
                        return;
                    }

                    if (!response.IsFinal)
                    {
                        chunkCount++;
                        if (chunkCount == 1)
                        {
                            firstChunk = response.Content;
                        }
                    }
                    else
                    {
                        finalResponse = response;
                        isComplete = true;
                    }
                },
                options
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNotNull(finalResponse, "最終レスポンスが返されること");
            Assert.Greater(chunkCount, 0, "複数のチャンクが送信されること");
            Assert.IsNotEmpty(firstChunk, "最初のチャンクが空でないこと");
            Assert.Greater(finalResponse.Content.Length, firstChunk.Length, "最終レスポンスが最初のチャンクより長いこと");
        }

        /// <summary>
        /// Test 3: リアルタイム表示のシミュレーション
        /// </summary>
        [UnityTest]
        public IEnumerator Test_RealTimeDisplay_AccumulatesContent()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-stream-display",
                Temperature = 0.8f,
                Seed = 44
            };

            bool isComplete = false;
            string previousContent = "";
            bool contentGrowing = true;
            ChatResponse finalResponse = null;

            // Act
            yield return _mockClient.SendMessageStreamingAsync(
                "Write a haiku about spring",
                (response, error) =>
                {
                    if (error != null)
                    {
                        isComplete = true;
                        return;
                    }

                    if (!response.IsFinal)
                    {
                        // コンテンツが徐々に増えることを確認
                        if (response.Content.Length < previousContent.Length)
                        {
                            contentGrowing = false;
                        }
                        previousContent = response.Content;
                    }
                    else
                    {
                        finalResponse = response;
                        isComplete = true;
                    }
                },
                options
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNotNull(finalResponse, "最終レスポンスが返されること");
            Assert.IsTrue(contentGrowing, "コンテンツが段階的に増えること");
            Assert.IsNotEmpty(finalResponse.Content, "最終レスポンスが空でないこと");
        }

        /// <summary>
        /// Test 4: セッション履歴を含むストリーミング
        /// </summary>
        [UnityTest]
        public IEnumerator Test_StreamingWithHistory_RemembersContext()
        {
            // Arrange
            string sessionId = "test-stream-history-session";

            // Act & Assert - メッセージ 1
            bool isComplete1 = false;
            ChatResponse response1 = null;

            yield return _mockClient.SendMessageStreamingAsync(
                "My favorite color is blue",
                (response, error) =>
                {
                    if (response?.IsFinal == true)
                    {
                        response1 = response;
                        isComplete1 = true;
                    }
                },
                new ChatRequestOptions { ChatId = sessionId, Temperature = 0.0f, Seed = 50 }
            );
            yield return new WaitUntil(() => isComplete1);

            // Act & Assert - メッセージ 2（履歴を含む）
            bool isComplete2 = false;
            ChatResponse response2 = null;

            yield return _mockClient.SendMessageStreamingAsync(
                "What color did I say I like?",
                (response, error) =>
                {
                    if (response?.IsFinal == true)
                    {
                        response2 = response;
                        isComplete2 = true;
                    }
                },
                new ChatRequestOptions { ChatId = sessionId, Temperature = 0.0f, Seed = 50 }
            );
            yield return new WaitUntil(() => isComplete2);

            // Assert
            Assert.IsNotNull(response1, "最初のレスポンスが返されること");
            Assert.IsNotNull(response2, "2番目のレスポンスが返されること");
            Assert.IsTrue(
                response2.Content.ToLower().Contains("blue"),
                "色の情報を覚えていること"
            );
        }

        /// <summary>
        /// Test 5: エラーハンドリング
        /// </summary>
        [UnityTest]
        public IEnumerator Test_StreamingError_ReturnsError()
        {
            // Arrange
            _mockClient.SetSimulateError(true);

            bool isComplete = false;
            ChatError receivedError = null;

            // Act
            yield return _mockClient.SendMessageStreamingAsync(
                "Test message",
                (response, error) =>
                {
                    receivedError = error;
                    isComplete = true;
                },
                new ChatRequestOptions { ChatId = "error-test-stream" }
            );

            yield return new WaitUntil(() => isComplete);

            // Assert
            Assert.IsNotNull(receivedError, "エラーが返されること");
            Assert.AreEqual(ChatErrorType.ServerError, receivedError.ErrorType, "エラータイプが正しいこと");
        }

        /// <summary>
        /// Test 6: Task版ストリーミングAPIのテスト
        /// </summary>
        [UnityTest]
        public IEnumerator Test_TaskStreamingAPI_ReportsProgress()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-task-streaming",
                Temperature = 0.7f
            };

            int progressCount = 0;
            ChatResponse lastProgress = null;

            // Act
            var task = _mockClient.SendMessageStreamingTaskAsync(
                "Tell me about space",
                (progress) =>
                {
                    progressCount++;
                    lastProgress = progress;
                },
                options
            );

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.IsFalse(task.IsFaulted, "タスクがエラーにならないこと");
            Assert.IsNotNull(task.Result, "最終レスポンスが返されること");
            Assert.IsTrue(task.Result.IsFinal, "最終フラグが立っていること");
            Assert.Greater(progressCount, 0, "進捗が複数回報告されること");
            Assert.IsNotNull(lastProgress, "最後の進捗が記録されていること");
        }

        /// <summary>
        /// Test 7: ストリーミングの中断テスト
        /// </summary>
        [UnityTest]
        public IEnumerator Test_StreamingInterruption_HandlesGracefully()
        {
            // Arrange
            var options = new ChatRequestOptions
            {
                ChatId = "test-stream-interrupt",
                Temperature = 0.7f
            };

            bool receivedAtLeastOneChunk = false;
            bool isComplete = false;

            // Act
            var coroutine = _mockClient.SendMessageStreamingAsync(
                "Tell me a long story",
                (response, error) =>
                {
                    if (error != null)
                    {
                        isComplete = true;
                        return;
                    }

                    if (!response.IsFinal)
                    {
                        receivedAtLeastOneChunk = true;
                    }
                    else
                    {
                        isComplete = true;
                    }
                },
                options
            );

            // 少し待ってから完了を待つ
            yield return new WaitForSeconds(0.05f);
            yield return coroutine;

            // Assert
            Assert.IsTrue(receivedAtLeastOneChunk || isComplete, "チャンクまたは完了を受信すること");
        }
    }
}
