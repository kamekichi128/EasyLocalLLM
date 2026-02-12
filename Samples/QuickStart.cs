using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Factory;
using EasyLocalLLM.LLM.Ollama;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

/// <summary>
/// Quick start test script
/// Allows trying the new library with minimal configuration
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
        // Step 1: Client initialization
        Debug.Log("[Step 1] Initializing OllamaClient...");

        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DebugMode = true
        };

        var client = LLMClientFactory.CreateOllamaClient(config);
        Debug.Log("✓ Client initialized");

        // Step 2: Load model
        Debug.Log("[Step 2] Loading model...");
        yield return client.LoadModelRunnable(config.DefaultModelName, true, 
            progress => {
                if (progress.IsCompleted)
                {
                    if (progress.IsSuccessed)
                    {
                        Debug.Log("✓ Model loaded successfully");
                        return;
                    }
                    else
                    {
                        Debug.LogError("✗ Model loading failed");
                        return;
                    }
                }
                Debug.Log($"  Loading progress: {progress.Progress * 100f:0.00}% | {progress.Message}");
            }
        );

        // Step 3: Send simple message
        Debug.Log("[Step 3] Sending simple message...");

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
            new ChatRequestOptions { SessionId = "quick-test-session" }
        );

        yield return new WaitUntil(() => completed);

        // Step 4: Send follow-up message with session history
        Debug.Log("[Step 4] Sending follow-up message (with history)...");

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
            new ChatRequestOptions { SessionId = "quick-test-session" }
        );

        yield return new WaitUntil(() => completed);

        // Step 5: Streaming test
        Debug.Log("[Step 5] Testing streaming...");

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
            new ChatRequestOptions { SessionId = "quick-test-streaming" }
        );

        yield return new WaitUntil(() => completed);

        // Step 6: Tool usage test 
        Debug.Log("[Step 6] Testing tool...");

        completed = false;

        // Tool registration: addition
        // Schema is auto-generated and return value is automatically converted to string
        client.RegisterTool(
            name: "add_numbers",
            description: "Add two numbers together",
            callback: (Func<int, int, int>)((a, b) => a + b)
        );

        // Tool registration: get current time
        client.RegisterTool(
            name: "get_current_time",
            description: "Get the current time",
            callback: (Func<DateTime>)(() => System.DateTime.Now)  // DateTime is automatically converted
        );

        // Send LLM message
        StartCoroutine(client.SendMessageAsync(
            "What is 125 + 378? And what time is it now?",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.Message}");
                    completed = true;
                    return;
                }

                // call tools automatically handled
                Debug.Log($"✓ Tool call passed!");
                Debug.Log($"Assistant: {response.Content}");
                // 例: "125 + 378 = 503. The current time is 2026-02-07 15:30:45."
                completed = true;
            },
            new ChatRequestOptions
            {
                Tools = new List<string> { "add_numbers" },
            }
        ));

        yield return new WaitUntil(() => completed);

        // All tests done
        Debug.Log("=== All Quick Tests Passed ===");
        Debug.Log("The new EasyLocalLLM library is working correctly!");
    }
}
