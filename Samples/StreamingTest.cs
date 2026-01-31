using System;
using System.Collections;
using UnityEngine;
using EasyLocalLLM.LLM;

/// <summary>
/// 新しい Runtime ライブラリのストリーミング機能のテスト
/// ReferenceOnlyDeveloping の ADVSceneController パターンを再現
/// </summary>
public class LibraryTestStreaming : MonoBehaviour
{
    private OllamaClient _client;
    private string _testOutput = "";
    private bool _testInProgress = false;

    void Start()
    {
        Debug.Log("=== EasyLocalLLM Streaming Test Start ===");

        // クライアントを初期化
        var clientConfig = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            MaxRetries = 3,
            RetryDelaySeconds = 1.0f,
            DefaultSeed = 17254,
            DebugMode = true
        };

        _client = LLMClientFactory.CreateOllamaClient(clientConfig);
        _client.GlobalSystemPrompt = "You are a helpful AI assistant.";
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

        GUILayout.Label("EasyLocalLLM Library - Streaming Test", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

        GUILayout.Space(10);

        // Test 1: Simple Streaming
        if (GUILayout.Button("Test 1: Simple Streaming", GUILayout.Height(40)))
        {
            StartCoroutine(TestSimpleStreaming());
        }

        // Test 2: Long Response Streaming
        if (GUILayout.Button("Test 2: Long Response Streaming", GUILayout.Height(40)))
        {
            StartCoroutine(TestLongResponseStreaming());
        }

        // Test 3: Real-time Display
        if (GUILayout.Button("Test 3: Real-time Display Simulation", GUILayout.Height(40)))
        {
            StartCoroutine(TestRealTimeDisplay());
        }

        // Test 4: Streaming with Session History
        if (GUILayout.Button("Test 4: Streaming with History", GUILayout.Height(40)))
        {
            StartCoroutine(TestStreamingWithHistory());
        }

        // Clear
        if (GUILayout.Button("Clear Output", GUILayout.Height(30)))
        {
            _testOutput = "";
        }

        GUILayout.Space(10);

        GUILayout.Label("Output:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        GUILayout.TextArea(_testOutput, GUILayout.ExpandHeight(true));

        GUILayout.EndArea();
    }

    /// <summary>
    /// テスト 1: シンプルなストリーミング
    /// </summary>
    private IEnumerator TestSimpleStreaming()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 1] Simple Streaming Test\n";
        _testOutput += "Sending: \"Tell me a short joke\"\n";
        _testOutput += "---\n";
        _testOutput += "Streaming: ";

        var options = new ChatRequestOptions
        {
            ChatId = "test-stream-1",
            Temperature = 0.7f,
            Seed = 42
        };

        bool isComplete = false;

        yield return _client.SendMessageStreamingAsync(
            "Tell me a short joke",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"\nERROR: {error.ErrorType} - {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (!isFinal)
                {
                    // ストリーミング中
                    _testOutput += ".";
                }
                else
                {
                    // 完了
                    _testOutput += $"\n\nFinal Response:\n{response.Content}\n";
                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[Test 1] Complete\n";
    }

    /// <summary>
    /// テスト 2: 長い回答のストリーミング
    /// </summary>
    private IEnumerator TestLongResponseStreaming()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 2] Long Response Streaming Test\n";
        _testOutput += "Sending: \"Explain machine learning in detail\"\n";
        _testOutput += "---\n";

        var options = new ChatRequestOptions
        {
            ChatId = "test-stream-long",
            Temperature = 0.7f,
            Seed = 43,
            MaxHistory = 10
        };

        int chunkCount = 0;
        bool isComplete = false;

        yield return _client.SendMessageStreamingAsync(
            "Explain machine learning in detail",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (!isFinal)
                {
                    chunkCount++;
                    // 部分応答を表示（最初の200文字のみ）
                    string preview = response.Content.Length > 200
                        ? response.Content.Substring(0, 200) + "..."
                        : response.Content;

                    if (chunkCount == 1)
                    {
                        _testOutput += $"First chunk: {preview}\n";
                    }
                }
                else
                {
                    // 完了
                    _testOutput += $"\n\nTotal chunks received: {chunkCount}\n";
                    _testOutput += $"Final length: {response.Content.Length} characters\n";
                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[Test 2] Complete\n";
    }

    /// <summary>
    /// テスト 3: リアルタイム表示シミュレーション
    /// </summary>
    private IEnumerator TestRealTimeDisplay()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 3] Real-time Display Simulation\n";
        _testOutput += "Sending: \"Write a haiku about spring\"\n";
        _testOutput += "---\n";
        _testOutput += "Display: \n";

        var options = new ChatRequestOptions
        {
            ChatId = "test-stream-display",
            Temperature = 0.8f,
            Seed = 44
        };

        bool isComplete = false;
        string displayContent = "";

        yield return _client.SendMessageStreamingAsync(
            "Write a haiku about spring",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (!isFinal)
                {
                    // リアルタイムで内容を更新
                    displayContent = response.Content;
                    // 最新の内容を表示（改行前後200文字）
                    int startIdx = Math.Max(0, displayContent.Length - 100);
                    string visible = displayContent.Substring(startIdx);
                    _testOutput = _testOutput.TrimEnd('\n') + "\rDisplay: " + visible;
                }
                else
                {
                    // 最終結果
                    _testOutput += $"\n\nFinal Haiku:\n{response.Content}\n";
                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[Test 3] Complete\n";
    }

    /// <summary>
    /// テスト 4: セッション履歴を含むストリーミング
    /// </summary>
    private IEnumerator TestStreamingWithHistory()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 4] Streaming with Session History Test\n";
        _testOutput += "---\n";

        string sessionId = "test-stream-history-session";

        // メッセージ 1
        _testOutput += "Message 1 (Streaming): \"My favorite color is blue\"\n";
        bool isComplete1 = false;

        yield return _client.SendMessageStreamingAsync(
            "My favorite color is blue",
            (response, error, isFinal) =>
            {
                if (error != null) { isComplete1 = true; return; }
                if (isFinal)
                {
                    _testOutput += $"Response: {response.Content}\n";
                    isComplete1 = true;
                }
            },
            new ChatRequestOptions { ChatId = sessionId, Temperature = 0.0f, Seed = 50 }
        );

        yield return new WaitUntil(() => isComplete1);

        yield return new WaitForSeconds(1.0f);

        // メッセージ 2（履歴を含むストリーミング）
        _testOutput += "\nMessage 2 (Streaming): \"What color did I say I like?\"\n";
        bool isComplete2 = false;

        yield return _client.SendMessageStreamingAsync(
            "What color did I say I like?",
            (response, error, isFinal) =>
            {
                if (error != null) { isComplete2 = true; return; }
                if (isFinal)
                {
                    _testOutput += $"Response: {response.Content}\n";
                    _testOutput += "(Should mention 'blue')\n";
                    isComplete2 = true;
                }
            },
            new ChatRequestOptions { ChatId = sessionId, Temperature = 0.0f, Seed = 50 }
        );

        yield return new WaitUntil(() => isComplete2);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[Test 4] Complete\n";
    }
}
