using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Ollama;
using EasyLocalLLM.LLM.Factory;
using UnityEngine.UIElements;

/// <summary>
/// シンプルなチャット画面のサンプル
/// ロードしたLLMモデルに対して、プロンプトを送り応答を受け取ります
/// </summary>
public class SimpleChat : MonoBehaviour
{
    public UIDocument UIDocument;

    private IChatLLMClient client;

    void Start()
    {
        Debug.Log("=== EasyLocalLLM Simple Chat Sample ===");

        InitializeEasyLocalLLMClient();
    }

    private void InitializeEasyLocalLLMClient()
    {
        // ステップ 1: クライアントの初期化
        // サーバーを自動起動するため、ollama.exeを立ち上げている場合は終了するか、
        // 立ち上げていないポートを指定してください。
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            DefaultModelName = "mistral",
            AutoStartServer = true,
            DebugMode = true,
        };
        OllamaServerManager.Initialize(config, OnOllamaServerInitialized);
        client = LLMClientFactory.CreateOllamaClient(config);
        Debug.Log("✓ Client initialized");
    }

    private void OnOllamaServerInitialized(bool successed)
    {
        if (successed)
        {
            Debug.Log("✓ Ollama server initialized successfully.");
            EnableUI();
        }
        else
        {
            Debug.LogError("✗ Failed to initialize Ollama server.");
        }
    }

    private void EnableUI()
    {
        var root = UIDocument.rootVisualElement;
        var sendAsync = root.Q<Button>("SendAsync");
        var sendStreaming = root.Q<Button>("SendStreaming");
        var promptInput = root.Q<TextField>("PromptInput");
        sendAsync.clicked += OnSendAsyncClicked;
        sendStreaming.clicked += OnSendStreamingClicked;
        sendAsync.SetEnabled(true);
        sendStreaming.SetEnabled(true);
        promptInput.SetEnabled(true);
    }

    private void RemoveUIEvent()
    {
        var root = UIDocument.rootVisualElement;
        var sendAsync = root.Q<Button>("SendAsync");
        var sendStreaming = root.Q<Button>("SendStreaming");
        sendAsync.clicked -= OnSendAsyncClicked;
        sendStreaming.clicked -= OnSendStreamingClicked;
    }

    private void OnSendAsyncClicked()
    {
        var root = UIDocument.rootVisualElement;
        var promptInput = root.Q<TextField>("PromptInput");
        var result = root.Q<Label>("Result");
        string prompt = promptInput.value;

        StartCoroutine(client.SendMessageAsync(
            prompt,
            (chatResponse, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.ErrorType} - {error.Message}");
                    Debug.LogError($"  HttpStatus: {error.HttpStatus}");
                    result.text = "error occured...";
                    return;
                }
                Debug.Log($"✓ Response received: {chatResponse.Content}");
                result.text = chatResponse.Content;
            },
            new ChatRequestOptions { SessionId = "simple-chat-session" }
        ));
    }

    private void OnSendStreamingClicked()
    {
        var root = UIDocument.rootVisualElement;
        var promptInput = root.Q<TextField>("PromptInput");
        var result = root.Q<Label>("Result");
        string prompt = promptInput.value;

        StartCoroutine(client.SendMessageStreamingAsync(
            prompt,
            (chatResponse, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.ErrorType} - {error.Message}");
                    Debug.LogError($"  HttpStatus: {error.HttpStatus}");
                    result.text = "error occured...";
                    return;
                }
                if (!chatResponse.IsFinal)
                {
                    result.text = chatResponse.Content;
                    Debug.Log($"...streaming chunk: {chatResponse.Content}");
                    return;
                }
                Debug.Log($"✓ Response received: {chatResponse.Content}");
                result.text = chatResponse.Content;
            },
            new ChatRequestOptions { SessionId = "simple-chat-session" }
        ));
    }

    public void OnDisable()
    {
        RemoveUIEvent();
    }
}
