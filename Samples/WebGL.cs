using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Factory;
using EasyLocalLLM.LLM.WebGL;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace EasyLocalLLM.Samples
{
    /// <summary>
    /// Simple chat screen sample
    /// Sends prompts to the loaded LLM model and receives responses
    /// Provides multiple AI types that can be switched to change system prompts and tools
    /// </summary>
    public class WebGL : MonoBehaviour
    {
        public UIDocument UIDocument;

        private WebGLLlamaCppClient client;

        private readonly string SESSION_ID = "webgl_session";


        void Start()
        {
            Debug.Log("=== EasyLocalLLM WebGL Sample ===");

            InitializeEasyLocalLLMClient();
        }

        private void InitializeEasyLocalLLMClient()
        {
            // Initialize client
            // If you have ollama.exe running to automatically start the server, please stop it or specify a port that is not in use.
            var config = new WebGLLlamaCppConfig
            {
                ModelUrl = Application.streamingAssetsPath + "/models/qwen2-0_5b-instruct-q4_k_m.gguf",
                ContextSize = 2048,
                UseWebGpu = true,
                DebugMode = true
            };
            client = LLMClientFactory.CreateWebGLLlamaCppClient(config);
            EnableUI();
            Debug.Log("✓ Client initialized");
        }

        private void EnableUI()
        {
            var root = UIDocument.rootVisualElement;
            var sendAsync = root.Q<Button>("SendAsync");
            var sendStreaming = root.Q<Button>("SendStreaming");
            var promptInput = root.Q<TextField>("PromptInput");
            var clearHistory = root.Q<Button>("ClearHistory");
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
            string prompt = promptInput.value;

            StartCoroutine(client.SendMessageAsync(
                prompt,
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


        private void LoadHistory()
        {
            try
            {
                client.LoadAllSessions("webgl-histories");
                Debug.Log("✓ History loaded");
            }
            catch (Exception e)
            {
                Debug.LogWarning("✗ Failed to load history: " + e.Message);
            }
        }

        private void SaveHistory()
        {
            client.SaveAllSessions("webgl-histories");
            Debug.Log("✓ History saved");
        }

        public void OnDisable()
        {
            RemoveUIEvent();
            SaveHistory();
        }
    }
}