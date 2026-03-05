using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Factory;
using EasyLocalLLM.LLM.WebGL;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace EasyLocalLLM.Samples
{
    /// <summary>
    /// WebGL Sample
    /// Simple demo for using EasyLocalLLM in WebGL build. 
    /// It initializes the client with a local model and provides a UI to send prompts and display responses.
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
            var config = new WebGLLlamaCppConfig
            {
                ModelUrl = Application.streamingAssetsPath + "/EasyLocalLLM/models/qwen2-0_5b-instruct-q4_k_m.gguf",
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

        public void OnDisable()
        {
            RemoveUIEvent();
        }
    }
}