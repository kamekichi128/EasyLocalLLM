using System;
using System.Collections;
using UnityEngine;
using EasyLocalLLM.LLM;

/// <summary>
/// 新しい Runtime ライブラリの非ストリーミング機能のテスト
/// ReferenceOnlyDeveloping の ADVSceneController パターンを再現
/// </summary>
public class LibraryTestNonStreaming : MonoBehaviour
{
    private OllamaClient _client;
    private string _testOutput = "";
    private bool _testInProgress = false;

    void Start()
    {
        Debug.Log("=== EasyLocalLLM NonStreaming Test Start ===");

        // サーバマネージャーを初期化
        var serverConfig = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            ExecutablePath = Application.streamingAssetsPath + "/LLM/ollama.exe",
            AutoStartServer = false,  // テスト環境では手動管理
            DebugMode = true
        };

        // OllamaServerManager.Initialize(serverConfig);

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

        GUILayout.Label("EasyLocalLLM Library - NonStreaming Test", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

        GUILayout.Space(10);

        // Test 1: Simple Message
        if (GUILayout.Button("Test 1: Simple Message", GUILayout.Height(40)))
        {
            StartCoroutine(TestSimpleMessage());
        }

        // Test 2: Temperature Control
        if (GUILayout.Button("Test 2: Temperature 0 (Deterministic)", GUILayout.Height(40)))
        {
            StartCoroutine(TestDeterministicResponse());
        }

        // Test 3: Session Management
        if (GUILayout.Button("Test 3: Session History", GUILayout.Height(40)))
        {
            StartCoroutine(TestSessionHistory());
        }

        // Test 4: Error Handling
        if (GUILayout.Button("Test 4: Error Handling (Wrong Server)", GUILayout.Height(40)))
        {
            StartCoroutine(TestErrorHandling());
        }

        // Test 5: Multi-Session
        if (GUILayout.Button("Test 5: Multiple Sessions", GUILayout.Height(40)))
        {
            StartCoroutine(TestMultipleSessions());
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
    /// テスト 1: シンプルなメッセージ送信
    /// </summary>
    private IEnumerator TestSimpleMessage()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 1] Simple Message Test\n";
        _testOutput += "Sending: \"Hello, how are you?\"\n";
        _testOutput += "---\n";

        var options = new ChatRequestOptions
        {
            ChatId = "test-session-1",
            Temperature = 0.7f,
            Seed = 42
        };

        bool isComplete = false;

        yield return _client.SendMessageAsync(
            "Hello, how are you?",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.ErrorType} - {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (isFinal)
                {
                    _testOutput += $"Response: {response.Content}\n";
                    _testOutput += "---\n";
                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testInProgress = false;
        _testOutput += "[Test 1] Complete\n";
    }

    /// <summary>
    /// テスト 2: 温度 0（確定的な回答）
    /// </summary>
    private IEnumerator TestDeterministicResponse()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 2] Deterministic Response Test (Temperature=0, Seed=100)\n";
        _testOutput += "Sending: \"What is 2+2?\"\n";
        _testOutput += "---\n";

        var options = new ChatRequestOptions
        {
            ChatId = "test-deterministic",
            Temperature = 0.0f,
            Seed = 100
        };

        bool isComplete = false;

        yield return _client.SendMessageAsync(
            "What is 2+2?",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.ErrorType} - {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (isFinal)
                {
                    _testOutput += $"Response: {response.Content}\n";
                    _testOutput += "---\n";
                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testInProgress = false;
        _testOutput += "[Test 2] Complete\n";
    }

    /// <summary>
    /// テスト 3: セッション履歴管理
    /// </summary>
    private IEnumerator TestSessionHistory()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 3] Session History Test\n";
        _testOutput += "---\n";

        string sessionId = "test-history-session";

        // メッセージ 1
        _testOutput += "Message 1: \"My name is Alice\"\n";
        bool isComplete1 = false;

        yield return _client.SendMessageAsync(
            "My name is Alice",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.Message}\n";
                    isComplete1 = true;
                    return;
                }

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

        // メッセージ 2（履歴を含む）
        _testOutput += "\nMessage 2: \"What is my name?\"\n";
        bool isComplete2 = false;

        yield return _client.SendMessageAsync(
            "What is my name?",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.Message}\n";
                    isComplete2 = true;
                    return;
                }

                if (isFinal)
                {
                    _testOutput += $"Response: {response.Content}\n";
                    _testOutput += "(Should remember 'Alice')\n";
                    isComplete2 = true;
                }
            },
            new ChatRequestOptions { ChatId = sessionId, Temperature = 0.0f, Seed = 50 }
        );

        yield return new WaitUntil(() => isComplete2);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[Test 3] Complete\n";
    }

    /// <summary>
    /// テスト 4: エラーハンドリング
    /// </summary>
    private IEnumerator TestErrorHandling()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 4] Error Handling Test\n";
        _testOutput += "Attempting to connect to wrong server...\n";
        _testOutput += "---\n";

        var wrongConfig = new OllamaConfig
        {
            ServerUrl = "http://localhost:9999",  // 存在しないサーバ
            DefaultModelName = "mistral",
            MaxRetries = 2,
            RetryDelaySeconds = 0.5f,
            DebugMode = true
        };

        var testClient = LLMClientFactory.CreateOllamaClient(wrongConfig);

        bool isComplete = false;

        yield return testClient.SendMessageAsync(
            "Test message",
            (response, error, isFinal) =>
            {
                if (error != null)
                {
                    _testOutput += $"ErrorType: {error.ErrorType}\n";
                    _testOutput += $"Message: {error.Message}\n";
                    _testOutput += $"HttpStatus: {error.HttpStatus}\n";
                    _testOutput += $"IsRetryable: {error.IsRetryable}\n";
                    isComplete = true;
                    return;
                }

                isComplete = true;
            },
            new ChatRequestOptions { ChatId = "error-test" }
        );

        yield return new WaitUntil(() => isComplete);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[Test 4] Complete\n";
    }

    /// <summary>
    /// テスト 5: 複数セッションの同時管理
    /// </summary>
    private IEnumerator TestMultipleSessions()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[Test 5] Multiple Sessions Test\n";
        _testOutput += "---\n";

        // セッション A
        _testOutput += "Session A: \"I like programming\"\n";
        bool completeA = false;

        yield return _client.SendMessageAsync(
            "I like programming",
            (response, error, isFinal) =>
            {
                if (error != null) { completeA = true; return; }
                if (isFinal)
                {
                    _testOutput += $"A Response: {response.Content}\n";
                    completeA = true;
                }
            },
            new ChatRequestOptions { ChatId = "session-a", Temperature = 0.0f, Seed = 60 }
        );

        yield return new WaitUntil(() => completeA);

        yield return new WaitForSeconds(0.5f);

        // セッション B
        _testOutput += "\nSession B: \"I like cooking\"\n";
        bool completeB = false;

        yield return _client.SendMessageAsync(
            "I like cooking",
            (response, error, isFinal) =>
            {
                if (error != null) { completeB = true; return; }
                if (isFinal)
                {
                    _testOutput += $"B Response: {response.Content}\n";
                    completeB = true;
                }
            },
            new ChatRequestOptions { ChatId = "session-b", Temperature = 0.0f, Seed = 60 }
        );

        yield return new WaitUntil(() => completeB);

        yield return new WaitForSeconds(0.5f);

        // セッション A に戻る
        _testOutput += "\nSession A Follow-up: \"What is my hobby?\"\n";
        bool completeA2 = false;

        yield return _client.SendMessageAsync(
            "What is my hobby?",
            (response, error, isFinal) =>
            {
                if (error != null) { completeA2 = true; return; }
                if (isFinal)
                {
                    _testOutput += $"A Response: {response.Content}\n";
                    _testOutput += "(Should remember 'programming')\n";
                    completeA2 = true;
                }
            },
            new ChatRequestOptions { ChatId = "session-a", Temperature = 0.0f, Seed = 60 }
        );

        yield return new WaitUntil(() => completeA2);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[Test 5] Complete\n";
    }
}
