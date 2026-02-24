using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Ollama;
using EasyLocalLLM.LLM.Factory;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

namespace EasyLocalLLM.Samples
{
    /// <summary>
    /// Image understanding sample
    /// Sends prompts with image to the loaded LLM model and receives responses
    /// </summary>
    public class ImageUnderstanding : MonoBehaviour
    {
        public UIDocument UIDocument;

        private OllamaClient client;

        private Texture2D imageTexture;

        private readonly string SESSION_ID = "ImageUnderstandingSession";

        void Start()
        {
            Debug.Log("=== EasyLocalLLM Image Understanding Sample ===");

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
                DefaultModelName = "qwen3-vl:8b-instruct",
                AutoStartServer = true,
                DebugMode = true
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
                StartCoroutine(client.LoadModelRunnable(client.GetConfig().DefaultModelName, 180.0f, OnModelRunnable, true));
            }
            else
            {
                Debug.LogError("✗ Failed to initialize Ollama server.");
            }
        }

        private void OnModelRunnable(LoadModelProgress progress)
        {
            if (progress.IsCompleted)
            {
                if (progress.IsSuccessed)
                {
                    Debug.Log("✓ Model is runnable.");
                    EnableUI();
                }
                else
                {
                    Debug.LogError($"✗ Model failed to load: {progress.Message}");
                }
            }
            Debug.Log($"Model loading progress: {progress.Progress * 100}% | {progress.Message}");
        }

        private void EnableUI()
        {
            var root = UIDocument.rootVisualElement;
            var sendAsync = root.Q<Button>("SendAsync");
            var sendStreaming = root.Q<Button>("SendStreaming");
            var promptInput = root.Q<TextField>("PromptInput");
            var clearHistory = root.Q<Button>("ClearHistory");
            var targetImage = root.Q<VisualElement>("TargetImage");
            imageTexture = Resources.Load<Texture2D>("sample-picture");
            targetImage.style.backgroundImage = new StyleBackground(imageTexture);
            sendAsync.clicked += OnSendAsyncClicked;
            sendStreaming.clicked += OnSendStreamingClicked;
            clearHistory.clicked += OnClearHistoryClicked;
            sendAsync.SetEnabled(true);
            sendStreaming.SetEnabled(true);
            promptInput.SetEnabled(true);
            clearHistory.SetEnabled(true);
        }

        private void RemoveUIEvent()
        {
            var root = UIDocument.rootVisualElement;
            var sendAsync = root.Q<Button>("SendAsync");
            var sendStreaming = root.Q<Button>("SendStreaming");
            var clearHistory = root.Q<Button>("ClearHistory");
            sendAsync.clicked -= OnSendAsyncClicked;
            sendStreaming.clicked -= OnSendStreamingClicked;
            clearHistory.clicked -= OnClearHistoryClicked;
        }

        private void OnSendAsyncClicked()
        {
            var root = UIDocument.rootVisualElement;
            var promptInput = root.Q<TextField>("PromptInput");
            var result = root.Q<Label>("Result");
            var images = new List<Texture2D>();
            string prompt = promptInput.value;

            if (client.GetSessionMessageCount(SESSION_ID) == 0)
            {
                images.Add(imageTexture);
            }

            StartCoroutine(client.SendMessageAsync(
                prompt,
                images,
                chatResponse =>
                {
                    Debug.Log($"✓ Response received: {chatResponse.Content}");
                    result.text = chatResponse.Content;
                },
                error =>
                {
                    Debug.LogError($"✗ Error: {error.ErrorType} - {error.Message}");
                    Debug.LogError($"  HttpStatus: {error.HttpStatus}");
                    result.text = "error occured...";
                },
                new ChatRequestOptions
                {
                    SessionId = SESSION_ID
                }
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
                new List<Texture2D> { imageTexture },
                chatResponse =>
                {
                    if (!chatResponse.IsFinal)
                    {
                        result.text = chatResponse.Content;
                        Debug.Log($"...streaming chunk: {chatResponse.Content}");
                        return;
                    }
                    Debug.Log($"✓ Response received: {chatResponse.Content}");
                    result.text = chatResponse.Content;
                },
                error =>
                {
                    Debug.LogError($"✗ Error: {error.ErrorType} - {error.Message}");
                    Debug.LogError($"  HttpStatus: {error.HttpStatus}");
                    result.text = "error occured...";
                },
                new ChatRequestOptions
                {
                    SessionId = SESSION_ID
                }
            ));
        }

        private void OnClearHistoryClicked()
        {
            client.ClearAllMessages();
        }

        public void OnDisable()
        {
            RemoveUIEvent();
        }
    }
}