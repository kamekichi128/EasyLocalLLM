using System;
using System.Collections;
using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Manager;
using EasyLocalLLM.LLM.Ollama;
using EasyLocalLLM.LLM.Factory;

/// <summary>
/// ReferenceOnlyDeveloping の ADVSceneController パターンめE
/// 新しい Runtime ライブラリで完�Eに再現するチE��チE
/// </summary>
public class LibraryComparisonTest : MonoBehaviour
{
    private OllamaClient _client;
    private string _testOutput = "";
    private bool _testInProgress = false;

    // 前�Eコード�E ChatMessage 相彁E
    private class LegacyChatMessage
    {
        public string role;
        public string content;
    };

    void Start()
    {
        Debug.Log("=== ReferenceOnlyDeveloping vs New Library Comparison ===");

        // 新ライブラリの初期匁E
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            MaxRetries = 3,
            RetryDelaySeconds = 1.0f,
            DefaultSeed = 17254,
            DebugMode = true
        };

        _client = LLMClientFactory.CreateOllamaClient(config);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

        GUILayout.Label("Comparison: Legacy vs New Library", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });

        GUILayout.Space(10);

        if (GUILayout.Button("Test: ADVSceneController Pattern (Non-Streaming)", GUILayout.Height(40)))
        {
            StartCoroutine(TestADVScenePattern());
        }

        if (GUILayout.Button("Test: ADVSceneController Pattern (Streaming)", GUILayout.Height(40)))
        {
            StartCoroutine(TestADVSceneStreamingPattern());
        }

        if (GUILayout.Button("Test: MainSceneController Pattern", GUILayout.Height(40)))
        {
            StartCoroutine(TestMainScenePattern());
        }

        if (GUILayout.Button("Test: TitleScene Initialization Pattern", GUILayout.Height(40)))
        {
            StartCoroutine(TestTitleSceneInitPattern());
        }

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
    /// ADVSceneController の非ストリーミングパターンを�E現
    /// </summary>
    private IEnumerator TestADVScenePattern()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[ADVScene Non-Streaming] Legacy Pattern Recreation\n";
        _testOutput += "Legacy: SendMessageToChatbotAtOnce\n";
        _testOutput += "New: SendMessageAsync\n";
        _testOutput += "---\n";

        string model = "mistral";
        var messageHistory = new System.Collections.Generic.List<LegacyChatMessage>();

        // Legacy pattern: まずシスチE��メチE��ージと初期メチE��ージ
        messageHistory.Add(new LegacyChatMessage { role = "system", content = "You are a helpful assistant." });

        // New library pattern: ChatRequestOptions で管琁E
        var options = new ChatRequestOptions
        {
            ChatId = "adv-scene-session",
            SystemPrompt = "You are a helpful assistant.",
            Temperature = 0.0f,
            Seed = 17254
        };

        _testOutput += "Sending: \"Hello, I'm starting an adventure game.\"\n";

        bool isComplete = false;

        yield return _client.SendMessageAsync(
            "Hello, I'm starting an adventure game.",
            (response, error) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.ErrorType} - {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (response.IsFinal)
                {
                    _testOutput += $"\nAssistant: {response.Content}\n";

                    // Legacy: messageHistory に追加してぁE��
                    // New: 自動的に冁E��で管琁E
                    _testOutput += "\n[Internal History Management]\n";
                    _testOutput += "Legacy: Manual messageHistory.Add(...)\n";
                    _testOutput += "New: Automatic via ChatHistoryManager\n";

                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        yield return new WaitForSeconds(1.0f);

        // 2番目のメチE��ージ�E�履歴を含む�E�E
        _testOutput += "\nSending: \"What should I do first?\"\n";

        bool isComplete2 = false;

        yield return _client.SendMessageAsync(
            "What should I do first?",
            (response, error) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.Message}\n";
                    isComplete2 = true;
                    return;
                }

                if (response.IsFinal)
                {
                    _testOutput += $"\nAssistant: {response.Content}\n";
                    isComplete2 = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete2);

        _testOutput += "\n---\n";
        _testInProgress = false;
        _testOutput += "[ADVScene Pattern] Complete\n";
    }

    /// <summary>
    /// ADVSceneController のストリーミングパターンを�E現
    /// </summary>
    private IEnumerator TestADVSceneStreamingPattern()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[ADVScene Streaming] Legacy Pattern Recreation\n";
        _testOutput += "Legacy: SendMessageToChatbotStreaming\n";
        _testOutput += "New: SendMessageStreamingAsync\n";
        _testOutput += "---\n";

        var options = new ChatRequestOptions
        {
            ChatId = "adv-scene-streaming",
            Temperature = 0.7f,
            Seed = 17254
        };

        _testOutput += "Sending: \"Tell me an epic story about a dragon.\"\n";
        _testOutput += "Streaming chunks: ";

        bool isComplete = false;
        int chunkCount = 0;

        yield return _client.SendMessageStreamingAsync(
            "Tell me an epic story about a dragon.",
            (response, error) =>
            {
                if (error != null)
                {
                    _testOutput += $"\nERROR: {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (!response.IsFinal)
                {
                    chunkCount++;
                    _testOutput += "*";  // Progress indicator
                }
                else
                {
                    _testOutput += $"\n\nTotal chunks: {chunkCount}\n";
                    _testOutput += $"Final response length: {response.Content.Length} chars\n";

                    // Legacy: Translate メソチE��の参老E
                    _testOutput += "\n[Streaming Behavior]\n";
                    _testOutput += "Legacy: Manual chunk parsing & concat\n";
                    _testOutput += "New: Automatic via HttpRequestHelper\n";

                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[ADVScene Streaming] Complete\n";
    }

    /// <summary>
    /// MainSceneController パターンを�E現
    /// </summary>
    private IEnumerator TestMainScenePattern()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[MainScene] Pattern Recreation\n";
        _testOutput += "Legacy: Language-specific system prompts\n";
        _testOutput += "New: ChatRequestOptions.SystemPrompt\n";
        _testOutput += "---\n";

        // Legacy: inEnglish フラグで日本誁E英語を刁E��替ぁE
        bool inEnglish = false;

        string systemPromptEn = "You are a capable AI. Carefully read the given text. Also, please pay the utmost attention to consistency between your statements.";
        string systemPromptJa = "あなた�E有�EなAIです。与えられた文章を注意深く読み解ぁE��ください。また、�E身の発言間での整合性に最大限�E注意を払ってください、E;

        string selectedPrompt = inEnglish ? systemPromptEn : systemPromptJa;

        var options = new ChatRequestOptions
        {
            ChatId = "main-scene-session",
            SystemPrompt = selectedPrompt,
            Temperature = 0.0f,
            Seed = 17254
        };

        _testOutput += $"Language: {(inEnglish ? "English" : "Japanese")}\n";
        _testOutput += "Sending: \"Please analyze this text.\"\n";

        bool isComplete = false;

        yield return _client.SendMessageAsync(
            "Please analyze this text.",
            (response, error) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.Message}\n";
                    isComplete = true;
                    return;
                }

                if (response.IsFinal)
                {
                    _testOutput += $"\nResponse: {response.Content}\n";
                    _testOutput += "\n[Improvements]\n";
                    _testOutput += "Legacy: Manual isRunning flag\n";
                    _testOutput += "New: Automatic via OllamaClient._isRunning\n";
                    _testOutput += "Legacy: Static system prompts\n";
                    _testOutput += "New: Dynamic via ChatRequestOptions\n";

                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[MainScene Pattern] Complete\n";
    }

    /// <summary>
    /// TitleSceneController 初期化パターンを�E現
    /// </summary>
    private IEnumerator TestTitleSceneInitPattern()
    {
        if (_testInProgress)
            yield break;

        _testInProgress = true;
        _testOutput += "\n[TitleScene] Initialization Pattern\n";
        _testOutput += "Legacy: AwakeChatbot with retry loop\n";
        _testOutput += "New: HttpRequestHelper with auto-retry\n";
        _testOutput += "---\n";

        var options = new ChatRequestOptions
        {
            ChatId = "title-init-test",
            SystemPrompt = "you are a chatbot",
            Temperature = 0.0f,
            Seed = 17254
        };

        _testOutput += "Sending initialization message: \"hello\"\n";
        _testOutput += "Automatic retry enabled (MaxRetries=3)\n";

        bool isComplete = false;

        yield return _client.SendMessageAsync(
            "hello",
            (response, error) =>
            {
                if (error != null)
                {
                    _testOutput += $"ERROR: {error.ErrorType}\n";
                    _testOutput += $"Message: {error.Message}\n";
                    _testOutput += $"IsRetryable: {error.IsRetryable}\n";
                    isComplete = true;
                    return;
                }

                if (response.IsFinal)
                {
                    _testOutput += $"\nSuccess! Response: {response.Content}\n";
                    _testOutput += "\n[Initialization Improvements]\n";
                    _testOutput += "Legacy: try-catch + manual retry loop\n";
                    _testOutput += "New: HttpRequestHelper handles retries\n";
                    _testOutput += "Legacy: tryAttempt variable\n";
                    _testOutput += "New: MaxRetries in config\n";

                    isComplete = true;
                }
            },
            options
        );

        yield return new WaitUntil(() => isComplete);

        _testOutput += "---\n";
        _testInProgress = false;
        _testOutput += "[TitleScene Pattern] Complete\n";
    }
}

