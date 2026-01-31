using System;
using System.Collections;
using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Ollama;
using EasyLocalLLM.LLM.Factory;

/// <summary>
/// クイックスタート用テストスクリプト
/// 最小限の設定で新ライブラリを試せます
/// </summary>
public class QuickStartTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== EasyLocalLLM Quick Start ===");
        StartCoroutine(RunQuickTest());
    }

    private IEnumerator RunQuickTest()
    {
        // ステップ 1: クライアント初期化
        Debug.Log("[Step 1] Initializing OllamaClient...");

        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            DebugMode = true
        };

        var client = LLMClientFactory.CreateOllamaClient(config);
        Debug.Log("✓ Client initialized");

        // ステップ 2: シンプルなメッセージを送信
        Debug.Log("[Step 2] Sending simple message...");

        bool completed = false;
        string response = "";

        yield return client.SendMessageAsync(
            "Hello! What is your name?",
            (chatResponse, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.ErrorType} - {error.Message}");
                    Debug.LogError($"  HttpStatus: {error.HttpStatus}");
                    completed = true;
                    return;
                }

                if (chatResponse.IsFinal)
                {
                    response = chatResponse.Content;
                    Debug.Log($"✓ Response received: {response}");
                    completed = true;
                }
            },
            new ChatRequestOptions { ChatId = "quick-test-session" }
        );

        yield return new WaitUntil(() => completed);

        // ステップ 3: セッション履歴で再度メッセージを送信
        Debug.Log("[Step 3] Sending follow-up message (with history)...");

        completed = false;

        yield return client.SendMessageAsync(
            "Can you repeat your name again?",
            (chatResponse, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    completed = true;
                    return;
                }

                if (chatResponse.IsFinal)
                {
                    Debug.Log($"✓ Follow-up response: {chatResponse.Content}");
                    Debug.Log("✓ Session history is working correctly!");
                    completed = true;
                }
            },
            new ChatRequestOptions { ChatId = "quick-test-session" }
        );

        yield return new WaitUntil(() => completed);

        // ステップ 4: ストリーミングテスト
        Debug.Log("[Step 4] Testing streaming...");

        completed = false;
        int chunkCount = 0;

        yield return client.SendMessageStreamingAsync(
            "Tell me a very brief fact about space",
            (chatResponse, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    completed = true;
                    return;
                }

                if (!chatResponse.IsFinal)
                {
                    chunkCount++;
                }
                else
                {
                    Debug.Log($"✓ Streaming completed!");
                    Debug.Log($"  Total chunks: {chunkCount}");
                    Debug.Log($"  Response length: {chatResponse.Content.Length} chars");
                    completed = true;
                }
            },
            new ChatRequestOptions { ChatId = "quick-test-streaming" }
        );

        yield return new WaitUntil(() => completed);

        // 完了
        Debug.Log("=== All Quick Tests Passed ===");
        Debug.Log("The new EasyLocalLLM library is working correctly!");
        Debug.Log("Next step: Check TEST_GUIDE.md for comprehensive tests");
    }
}
