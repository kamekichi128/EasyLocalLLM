using EasyLocalLLM.LLM.Factory;
using EasyLocalLLM.LLM.Ollama;
using UnityEngine;

public class QuickStart : MonoBehaviour
{
    OllamaClient client;

    void Start()
    {
        InitializeEasyLocalLLMClient();
    }

    private void InitializeEasyLocalLLMClient()
    {
        // Initialize client
        // If you have ollama.exe running to automatically start the server, please stop it or specify a port that is not in use.
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            DefaultModelName = "kamekichi128/qwen3-4b-instruct-2507",
            AutoStartServer = true,
            DebugMode = true,
        };
        OllamaServerManager.Initialize(config, OnOllamaServerInitialized);
        client = LLMClientFactory.CreateOllamaClient(config);
        Debug.Log("? Client initialized");
    }

    private void OnOllamaServerInitialized(bool successed)
    {
        if (successed)
        {
            Debug.Log("? Ollama server initialized successfully.");
            StartCoroutine(client.LoadModelRunnable(client.GetConfig().DefaultModelName, true, OnModelRunnable));
        }
        else
        {
            Debug.LogError("? Failed to initialize Ollama server.");
        }
    }

    private void OnModelRunnable(LoadModelProgress progress)
    {
        if (progress.IsCompleted)
        {
            if (progress.IsSuccessed)
            {
                Debug.Log("? Model is runnable.");
                StartCoroutine(client.SendMessageAsync("Hello, world!", response =>
                {
                    Debug.Log($"Model response: {response}");
                }));
            }
            else
            {
                Debug.LogError($"? Model failed to load: {progress.Message}");
            }
        }
        Debug.Log($"Model loading progress: {progress.Progress * 100}% | {progress.Message}");
    }
}