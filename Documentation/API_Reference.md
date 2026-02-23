#+#+#+#+## EasyLocalLLM Runtime Library

EasyLocalLLM is a Unity library for communicating with a local LLM via Ollama.

## Table of Contents

- [1. Main Features](#1-main-features)
- [2. Quick Start](#2-quick-start)
- [3. Limitations](#3-limitations)
- [4. Usage](#4-usage)
  - [4.1 Basic Initialization](#41-basic-initialization)
  - [4.2 Load Model](#42-load-model)
  - [4.3 Send Message (Single Complete Response)](#43-send-message-single-complete-response)
  - [4.4 Streaming Message (Receive Partial Responses)](#44-streaming-message-receive-partial-responses)
  - [4.5 Generation options](#45-generation-options)
  - [4.6 Ollama Server Auto-Management](#46-ollama-server-auto-management)
  - [4.7 Session Management](#47-session-management)
  - [4.8 System Prompts](#48-system-prompts)
  - [4.9 Priority Scheduling](#49-priority-scheduling)
  - [4.10 Cancellation](#410-cancellation)
  - [4.11 Retry and Error Handling](#411-retry-and-error-handling)
  - [4.12 Message Persistence](#412-message-persistence)
  - [4.13 Tools (Function Calling)](#413-tools-function-calling)
  - [4.14 JSON Response Format](#414-json-response-format)
- [5. Practical Examples](#5-practical-examples)
- [6. Class Structure](#6-class-structure)
- [7. Configuration Options](#7-configuration-options)
- [8. Default Settings](#8-default-settings)
- [9. Error Types and Handling](#9-error-types-and-handling)
- [10. Planned Extensions](#10-planned-extensions)

## 1. Main Features

- ✅ **Externalized configuration**: Flexible settings via `OllamaConfig`
- ✅ **Automatic retries**: Exponential backoff on request failures
- ✅ **Streaming support**: Receive responses incrementally
- ✅ **Session management**: Per-session chat history
- ✅ **Session-specific system prompts**: Different prompts per session
- ✅ **Error handling**: Detailed error information
- ✅ **Server lifecycle management**: Auto start/stop

## 2. Quick Start

Validate the setup with minimal code.

**Prerequisites**: Ollama server is running at `localhost:11434`, and the `mistral` model is installed.

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class QuickStart : MonoBehaviour
{
    void Start()
    {
        var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
        
        StartCoroutine(client.SendMessageAsync(
            "Hello",
            response => Debug.Log($"Response: {response.Content}"),
            error => Debug.LogError($"Error: {error.Message}"),
        ));
    }
}
```

**For detailed setup and usage, see [4.1 Basic Initialization](#41-basic-initialization).**

**If Ollama is not set up yet, see [4.6 Ollama Server Auto-Management](#46-ollama-server-auto-management).**

## 3. Limitations

### Important Constraints

- **Unity-only**: Depends on UnityWebRequest, so it does not work outside Unity.
- **Windows-only**: Currently supported on Windows only.
- **Main-thread dependency**: Task APIs are implemented by bridging coroutines.

### Usage Patterns

```csharp
// ✅ Task API (await/async)
async Task SendMessageAsync()
{
    var result = await client.SendMessageTaskAsync("Hello");
    Debug.Log(result.Content);
}

// ✅ Coroutine API
void SendMessage()
{
    StartCoroutine(client.SendMessageAsync(
        "Hello",
        response =>
        {
            // Handle result in callback
        },
        error =>
        {
            // Handle error in callback
        }
    ));
}
```

**Note**: The Task API bridges coroutines and does not work outside Unity.

## 4. Usage

### 4.1 Basic Initialization

Quick Start uses the default settings; this section explains full configuration.

**Prerequisites**: Ollama server is running at `localhost:11434`, and the model is installed.

**If Ollama is not set up yet, see [4.6 Ollama Server Auto-Management](#46-ollama-server-auto-management).**

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class ChatManager : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        // Create configuration
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            DebugMode = true
        };

        // Initialize client
        _client = LLMClientFactory.CreateOllamaClient(config);
    }
}
```

### 4.2 Load Model

Optionally pre-load a model with progress tracking. While models are automatically loaded on first message, `LoadModelRunnable()` is useful for:

- **Loading at app startup** to ensure the model is ready
- **Showing progress** to users during model loading
- **Handling errors** explicitly during initialization

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class ModelPreloader : MonoBehaviour
{
    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            DebugMode = true
        };

        var client = LLMClientFactory.CreateOllamaClient(config);
        StartCoroutine(PreloadModel(client, config.DefaultModelName));
    }

    IEnumerator PreloadModel(OllamaClient client, string modelName)
    {
        Debug.Log($"[Loading] Model: {modelName}");

        yield return client.LoadModelRunnable(
            modelName,
            180.0f, // timeout seconds for wamup
            progress =>
            {
                if (progress.IsCompleted)
                {
                    if (progress.IsSuccessed)
                    {
                        Debug.Log("✓ Model loaded successfully");
                    }
                    else
                    {
                        Debug.LogError("✗ Model loading failed");
                    }
                    return;
                }

                // Show loading progress
                float percentage = progress.Progress * 100f;
                Debug.Log($"Loading: {percentage:0.00}% | {progress.Message}");
            },
            true  // pull If Model is not available
        );
    }
}
```

**Parameters:**
- **modelName** (string): Model to load (e.g., "mistral", "llama2")
- **timeoutSecondsForWarmup** (float): HTTP timeout for warmup chat
- **progressCallback**: Called with `LoadProgress` containing:
  - `IsCompleted` (bool): Whether loading finished
  - `IsSuccessed` (bool): Whether loading was successful
  - `Progress` (float): Progress 0.0 to 1.0
  - `Message` (string): Status message
- **pullIfModelNotAvailable** (bool): If `true`, try to pull model from ollama cloud, if the model not available

**Returns:** Coroutine to yield on.

**Notes:**
- This is optional; models are automatically loaded when first needed
- Useful for UX to show loading screens or progress bars
- Pre-loading prevents delays when the first message is sent

### 4.3 Send Message (Single Complete Response)

The callback is called **once**.
Use this for short responses or when you need the full answer at once.

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

void SendMessage()
{
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        Temperature = 0.7f,
        Seed = 42
    };

    StartCoroutine(_client.SendMessageAsync(
        "Hello",
        response => Debug.Log($"Assistant: {response.Content}"),
        error => Debug.LogError($"Error: {error.Message}"),
        options
    ));
}
```

**Task API (await/async)**

```csharp
using EasyLocalLLM.LLM;
using System.Threading.Tasks;
using UnityEngine;

async Task SendMessageAsync()
{
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        Temperature = 0.7f,
        Seed = 42
    };

    var response = await _client.SendMessageTaskAsync("Hello", options);
    Debug.Log($"Assistant: {response.Content}");
}
```

### 4.4 Streaming Message (Receive Partial Responses)

The callback is called **multiple times**. Each time a partial response arrives, you get an update; the final callback has `IsFinal=true`.
This is suitable for long outputs and real-time UI updates.

**Flow comparison:**

```
Non-streaming:
SendMessage() -> [server processing...] -> callback(IsFinal=true) -> complete

Streaming:
SendStreamingMessage() -> [server starts processing...]
  -> callback(IsFinal=false, chunk 1)
  -> callback(IsFinal=false, chunk 2)
  -> ...
  -> callback(IsFinal=true, final chunk) -> complete
```

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

void SendStreamingMessage()
{
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        Temperature = 0.7f
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "Write a poem",
        response =>
        {
            if (!response.IsFinal)
            {
                // Called multiple times: partial response
                Debug.Log($"Receiving: {response.Content}");
            }
            else
            {
                // Called once at the end: complete response
                Debug.Log($"Complete: {response.Content}");
            }
        },
        error => Debug.LogError($"Error: {error.Message}"),
        options
    ));
}
```

**Task API (receive progress)**

```csharp
using EasyLocalLLM.LLM;
using System;
using System.Threading.Tasks;
using UnityEngine;

async Task SendStreamingMessageAsync()
{
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        Temperature = 0.7f
    };

    var progress = new Progress<ChatResponse>(response =>
    {
        if (!response.IsFinal)
        {
            Debug.Log($"Receiving: {response.Content}");
        }
    });

    var final = await _client.SendMessageStreamingTaskAsync(
        "Write a poem",
        progress,
        options
    );

    Debug.Log($"Complete: {final.Content}");
}
```

#### 4.5 Generation options

Use these parameters when you need to control response style, randomness, or output length.
If a parameter is not set, Ollama/model defaults are used.

| Parameter | What it controls | Typical starting values |
|-----------|------------------|-------------------------|
| `Temperature` | Creativity/randomness. Lower = more deterministic, higher = more diverse. | `0.2` to `0.8` |
| `Seed` | Reproducibility for similar outputs. | Any fixed integer (e.g., `42`) |
| `TopK` | Limits token candidates to top-K likely tokens. | `20` to `80` |
| `TopP` | Nucleus sampling probability mass. | `0.8` to `0.95` |
| `MinP` | Filters very low-probability tokens. | `0.01` to `0.1` |
| `Stop` | Stops generation when any stop sequence appears. | e.g., `"\nUser:"`, `"</END>"` |
| `NumCtx` | Context window size (how much history can be considered). | `2048`, `4096`, `8192` |
| `NumPredict` | Maximum number of generated tokens. | `128` to `1024` |

**Practical presets**

- **Deterministic Q&A**: `Temperature=0.2`, `Seed=42`, `TopP=0.9`, `NumPredict=256`
- **Creative writing**: `Temperature=0.8`, `TopP=0.95`, `TopK=60`, `NumPredict=512`
- **Strict short output**: `Temperature=0.3`, `Stop=["\nUser:"]`, `NumPredict=128`

```csharp
using System.Collections.Generic;

var options = new ChatRequestOptions
{
    SessionId = "chat-session-1",
    Temperature = 0.7f,
    Seed = 42,
    TopK = 40,
    TopP = 0.9f,
    MinP = 0.05f,
    Stop = new List<string> { "\nUser:", "<|eot_id|>" },
    NumCtx = 4096,
    NumPredict = 512
};
```

### 4.6 Ollama Server Auto-Management

#### Choose a Setup Method

When embedding Ollama in your application, there are two ways to obtain models.
Pick the approach that fits your environment.

**Method A: `ollama pull` (recommended, easiest)**
- ✅ Easy setup
- ✅ Automatically optimized
- ✅ Easy model updates
- ❌ Requires network download (first time can take a while)

**Method B: Place GGUF files directly (custom/special use)**
- ✅ Fully customizable models
- ✅ Use custom models not supported by Ollama
- ❌ More complex setup
- ❌ Requires manual GGUF downloads

**Recommended usage:**

| Scenario | Recommended | Reason |
|--------|--------|------|
| Typical use | Method A | Simple and sufficient in most cases |
| Custom models not supported by Ollama | Method B | Use your own GGUF file |
| Fine-grained model customization | Method B | Tune parameters via Modelfile |

**Method A is recommended for most cases.** Use Method B only when a specific GGUF or Modelfile customization is required.

#### Setup Steps

**Step 0: Common preparation**

1. Download the Windows standalone binary from the [Ollama website](https://ollama.ai)
   - [GitHub releases](https://github.com/ollama/ollama/releases)
   - Typically named like `ollama-windows-amd64.zip`
   - For AMD Radeon GPU: also download `ollama-windows-amd64-rocm.zip`

2. Create the following directory structure in your Unity project:

```
Assets/StreamingAssets/Ollama/
├── ollama.exe                    # Ollama binary
├── lib /                         # Libraries used by Ollama
└── models/                       # Model directory (empty initially)
```

**Method A: `ollama pull` (recommended)**

This is the easiest and recommended setup.

1. Open PowerShell and run:

```powershell
$env:OLLAMA_MODELS="<ProjectPath>\Assets\StreamingAssets\Ollama\models"
mkdir $env:OLLAMA_MODELS
cd "<ProjectPath>\Assets\StreamingAssets\Ollama"
.\ollama.exe serve
```

2. In a separate PowerShell window, run:

```powershell
# Example models: mistral, llama2, neural-chat, dolphin-mixtral, etc.
cd "<ProjectPath>\Assets\StreamingAssets\Ollama"
.\ollama.exe pull mistral
```

3. Wait until the download completes (minutes to hours depending on model size)

4. Close both windows when finished

5. `StreamingAssets/Ollama/models/` will now contain `blobs/` and `manifests/`

**Model selection guide:**

```
Small (lightweight, fast)
├── qwen3-4b-instruct-2507 (4B) Recommended. Good for Multiluingal, Used in sample（kamekichi128/qwen3-4b-instruct-2507）
├── mistral (7B)                Recommended balance
├── neural-chat (7B)            Optimized for chat
└── phi (2.7B)                  Lightest option

Medium (standard)
├── llama2 (13B)          Higher accuracy
└── dolphin-mixtral (8x7B) High performance (high memory)

Large (high accuracy, high memory)
└── llama2 (70B)          Research use; 24GB+ GPU memory recommended
```

**Method B: Place GGUF files directly (custom setup)**

Use this if you want custom models or models not supported by Ollama.

**Step 1: Download a GGUF file**

Download a GGUF model from these sources:

- [Hugging Face](https://huggingface.co/models?search=gguf) - Largest collection
- [GGUF Zoo](https://ggml.ai) - Curated and optimized models

Example: download a mistral model
1. Open [mistral on Hugging Face](https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.1-GGUF)
2. Download `mistral-7b-instruct-v0.1.Q4_K_M.gguf` (recommended balance)
   - `Q4_K_M` = 4-bit quantization, best size-quality balance
   - `Q5_K_M` = 5-bit quantization, higher quality, larger size
   - `Q2_K` = 2-bit quantization, smaller but lower accuracy

3. Place it here:
```
Assets/StreamingAssets/Ollama/models/mistral/
└── mistral-7b-instruct-v0.1.Q4_K_M.gguf
```

**Step 2: Create a Modelfile**

Create a `Modelfile` in the same directory as the GGUF file.

**Basic template:**
```
FROM ./your-model-name.Q4_K_M.gguf

PARAMETER temperature 0.7
PARAMETER top_k 40
PARAMETER top_p 0.9
```

**Modelfile parameter reference:**

| Parameter | Default | Description | Recommended |
|----------|----------|------|--------|
| `temperature` | 0.8 | Response diversity (low = deterministic, high = diverse) | 0.7 to 1.0 |
| `top_k` | 40 | Choose from top-k candidates | 30 to 50 |
| `top_p` | 0.9 | Choose from cumulative probability p | 0.85 to 0.95 |
| `repeat_penalty` | 1.0 | Repetition penalty (1.0 = none, 1.1 = stronger) | 1.0 to 1.2 |

**Step 3: Register the model with Ollama**

Run the following in PowerShell:

```powershell
$env:OLLAMA_MODELS="<ProjectPath>\Assets\StreamingAssets\Ollama\models"
cd "<ProjectPath>\Assets\StreamingAssets\Ollama\models\mistral"
..\..\ollama.exe create mistral -f ./Modelfile
```

**Step 4: Verify registration**

```powershell
# List registered models
..\..\ollama.exe list
```

Example output:
```
NAME                                    ID              SIZE
mistral:latest                          a1b2c3d4...     3.5GB
```

**Step 5: Configure in Unity**

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
    ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
    DefaultModelName = "mistral",  // Registered model name
    AutoStartServer = true,
    DebugMode = true
};

OllamaServerManager.Initialize(config);
var client = LLMClientFactory.CreateOllamaClient(config);
```

#### Troubleshooting

**Q: Model download is slow**
- A: Check your internet connection. Large models can be several GB to tens of GB.

**Q: "ollama.exe serve" fails**
- A: Ensure port 11434 is not already in use. `netstat -an | findstr :11434`

**Q: "not found" error when creating Modelfile**
- A: Ensure the GGUF file path is relative and starts with `./`.

**Q: Out-of-memory error**
- A: Use a smaller quantized model (e.g., Q2_K -> Q4_K_M) or set `MaxConcurrentSessions` to 1.

**Q: Want more model customization examples**
- A: Official guide: [Ollama Modelfile](https://github.com/ollama/ollama/blob/main/docs/modelfile.md)

#### Recommended setup (game development)

```csharp
public class OllamaSetupManager : MonoBehaviour
{
    void Start()
    {
        // Choose settings based on environment
        OllamaConfig config;

#if UNITY_EDITOR
        // Development: enable debug mode
        config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            AutoStartServer = true,
            DebugMode = true,
            MaxRetries = 5,
            EnableHealthCheck = true
        };
#else
        // Build: minimize logs
        config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            AutoStartServer = true,
            DebugMode = false,
            MaxRetries = 3,
            HttpTimeoutSeconds = 90.0f,
            EnableHealthCheck = true
        };
#endif

        OllamaServerManager.Initialize(config);
        var client = LLMClientFactory.CreateOllamaClient(config);
    }
}
```

### 4.7 Session Management

#### Session concept

Sessions identified by `SessionId` have the following behavior:

- **Auto-create**: Created on first `SendMessageAsync()` / `SendMessageStreamingAsync()`.
- **Auto-accumulate history**: Messages and responses for the same `SessionId` are stored.
- **Persistence**: Kept in memory until `ClearMessages()` is called.
- **Isolation**: Different `SessionId` values maintain separate histories.

#### Basic session management

```csharp
void ManageSessions()
{
    // Manage multiple conversations by session ID
    var session1Options = new ChatRequestOptions { SessionId = "session-1" };
    var session2Options = new ChatRequestOptions { SessionId = "session-2" };

    // Conversation in session 1
    StartCoroutine(_client.SendMessageAsync(
        "Message for session 1",
        OnResponse,
        session1Options
    ));

    // Conversation in session 2 (fully independent from session 1)
    StartCoroutine(_client.SendMessageAsync(
        "Message for session 2",
        OnResponse,
        session2Options
    ));

    // Next message in session 1 (automatically references previous history)
    StartCoroutine(_client.SendMessageAsync(
        "Second message in session 1",
        OnResponse,
        session1Options  // Same session ID
    ));
}
```

#### Reset history

**Clear a specific session:**

```csharp
// Clear session 1 history
// The next message with the same session ID will create a new session
_client.ClearMessages("session-1");
```

**Clear all history:**

```csharp
// Clear all sessions
// Useful for memory cleanup or app shutdown
_client.ClearAllMessages();
```

#### Access session information

You can access session state and history details:

```csharp
void InspectSessions()
{
    // Work with session 1
    string sessionId = "session-1";
    
    // Check if session exists
    if (_client.HasSession(sessionId))
    {
        Debug.Log("Session exists");
        
        // Get message count
        int messageCount = _client.GetSessionMessageCount(sessionId);
        Debug.Log($"Message count: {messageCount}");
        
        // Retrieve session info (history, created/updated timestamps, etc.)
        var session = _client.GetSession(sessionId);
        Debug.Log($"Created at: {session.CreatedAt}");
        Debug.Log($"Last updated at: {session.LastUpdatedAt}");
        Debug.Log($"Messages: {string.Join("\n", session.History)}");
    }
    
    // Get all session IDs
    var allSessions = _client.GetAllSessionIds();
    foreach (var id in allSessions)
    {
        Debug.Log($"Session ID: {id}");
    }
}
```

#### Practical example: multiple sessions

```csharp
public class MultiSessionChat : MonoBehaviour
{
    private OllamaClient _client;
    
    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral"
        };
        _client = LLMClientFactory.CreateOllamaClient(config);
    }

    // Conversation session for User A
    void ChatWithUserA()
    {
        var userASession = new ChatRequestOptions { SessionId = "user-a-session" };
        StartCoroutine(_client.SendMessageAsync(
            "Message from User A",
            OnResponse,
            userASession
        ));
    }

    // Conversation session for User B
    void ChatWithUserB()
    {
        var userBSession = new ChatRequestOptions { SessionId = "user-b-session" };
        StartCoroutine(_client.SendMessageAsync(
            "Message from User B",
            OnResponse,
            userBSession
        ));
    }

    // Topic-based sessions (same user, different topics)
    void ChatAboutTopic(string topic)
    {
        var topicSession = new ChatRequestOptions { SessionId = $"topic-{topic}" };
        StartCoroutine(_client.SendMessageAsync(
            $"Tell me about {topic}",
            OnResponse,
            topicSession
        ));
    }

    void OnResponse(ChatResponse response, ChatError error)
    {
        if (error != null)
        {
            Debug.LogError($"Error: {error.Message}");
            return;
        }
        
        Debug.Log($"Response: {response.Content}");
    }

    // On app quit
    void OnApplicationQuit()
    {
        _client.ClearAllMessages();  // Clean up memory
    }
}
```

#### Session management notes

- **If `SessionId` is null**: A one-time session is created with an auto-generated Guid.
- **MaxHistory**: Default is 50 messages; oldest messages are removed when exceeded.
- **Isolation**: A system prompt in one session does not affect another session.
- **Memory**: Keeping many sessions increases memory usage; clear unused sessions with `ClearMessages()`.
- **Session info**: `GetSession()` returns a `ChatSession` with `CreatedAt`, `LastUpdatedAt`, and `History`.

### 4.9 System Prompts

#### What is a system prompt?

**System prompts** tell the LLM what role or personality to adopt. Unlike user messages, they influence the entire conversation.

**Core roles:**

- **Role definition**: "Act as a doctor" or "Act as customer support".
- **Response style**: "Be concise" or "Explain in detail".
- **Scope restriction**: "Only answer about this topic".
- **Language/format**: "Respond in Japanese" or "Return JSON".

**Example:**

For a doctor role:

```
"You are a professional medical advisor. Provide accurate medical information but always 
remind the user to consult a real doctor for serious conditions."
```

If the user says "I have a headache," the LLM responds as a doctor.
If the user asks "Teach me programming," the LLM keeps the doctor persona but answers appropriately.

#### Global system prompt (all sessions)

Set a shared prompt for all sessions via `OllamaConfig`:

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    Model = "mistral",
    GlobalSystemPrompt = "You are a helpful assistant. Always be polite, accurate."
};

var client = LLMClientFactory.CreateOllamaClient(config);
```

The global prompt is used only when no session-specific or request-level prompt is set.

**Best practices for global prompts:**

- **Language**: Keep the response language consistent.
- **Ethics**: Set the baseline tone and honesty.
- **Style**: Define default verbosity (detailed vs concise).

#### Session-specific system prompts

Session prompts let you manage multiple roles within a single application (doctor, engineer, support, etc.).

**Prompt priority (highest first):**

1. **Request-level prompt**: `ChatRequestOptions.SystemPrompt` (single request only)
2. **Session-level prompt**: set via `SetSessionSystemPrompt()` (applies to the session)
3. **Global prompt**: `OllamaConfig.GlobalSystemPrompt` (all sessions)

**Example: priority demonstration**

```csharp
// Global setting (shared by all sessions)
var config = new OllamaConfig
{
    GlobalSystemPrompt = "You are a general assistant."
};

// Set session-specific prompt
_client.SetSessionSystemPrompt(
    "session-1",
    "You are a technical expert. Explain complex topics with technical depth."
);

// Case 1: request-level prompt (highest priority)
var options1 = new ChatRequestOptions
{
    SessionId = "session-1",
    SystemPrompt = "You are a beginner-friendly tutor. Explain simply."  // Highest priority
};
StartCoroutine(_client.SendMessageAsync("What is programming?", OnResponse, options1));
// Result: responds as a beginner-friendly tutor

// Case 2: session-level prompt only
StartCoroutine(_client.SendMessageAsync("What is the difference between C# and Java?", OnResponse,
    new ChatRequestOptions { SessionId = "session-1" }
));
// Result: responds as a technical expert

// Case 3: different session (no session-specific prompt)
StartCoroutine(_client.SendMessageAsync("Hello", OnResponse,
    new ChatRequestOptions { SessionId = "session-2" }
));
// Result: responds as the global prompt "general assistant"
```

#### Set session-specific prompts

**Single session:**

```csharp
// Set a custom system prompt for "doctor-session"
_client.SetSessionSystemPrompt(
    "doctor-session",
    "You are a professional medical advisor. Provide accurate medical information. " +
    "Always remind the user to consult a real doctor for serious conditions."
);

// Send a message within this session (doctor role)
StartCoroutine(_client.SendMessageAsync(
    "I have a headache. What could be the cause?",
    OnResponse,
    new ChatRequestOptions { SessionId = "doctor-session" }
));
```

**Multiple sessions at once:**

```csharp
// Set the same prompt across multiple sessions (efficient role init)
var sessionIds = new List<string> { "support-1", "support-2", "support-3" };
_client.SetSystemPromptForMultipleSessions(
    sessionIds,
    "You are a helpful customer support specialist. Respond concisely and professionally."
);
```

#### Retrieve and manage session prompts

**Retrieve:**

```csharp
// Get the system prompt for "doctor-session"
string prompt = _client.GetSessionSystemPrompt("doctor-session");
if (prompt != null)
{
    Debug.Log($"Current session prompt: {prompt}");
}
else
{
    Debug.Log("No session-specific prompt set. Using global prompt.");
}
```

**Reset:**

```csharp
// Remove the session-specific prompt (revert to global prompt)
_client.ResetSessionSystemPrompt("doctor-session");
// This session now uses the global prompt
```

**Reset all session prompts:**

```csharp
// Remove all session-specific prompts
_client.ResetAllSessionSystemPrompts();
// Only the global prompt remains
```

#### Practical example: managing multiple roles

```csharp
void InitializeMultipleRoles()
{
    // Doctor role
    _client.SetSessionSystemPrompt(
        "doctor-session",
        "You are a professional medical advisor. Provide evidence-based medical information. " +
        "Always remind users to consult a licensed physician for serious health concerns."
    );

    // Software engineer role
    _client.SetSessionSystemPrompt(
        "engineer-session",
        "You are an expert software engineer with 10 years of experience. " +
        "Help users with coding problems, design patterns, best practices, and architecture decisions. " +
        "Prefer modern C# patterns and explain trade-offs."
    );

    // Translator role
    _client.SetSessionSystemPrompt(
        "translator-session",
        "You are a professional English-Japanese translator with expertise in technical translation. " +
        "Translate accurately while preserving meaning, nuance, and context. " +
        "Maintain consistency in technical terminology."
    );

    // Customer support role
    _client.SetSessionSystemPrompt(
        "support-session",
        "You are a friendly and professional customer support specialist. " +
        "Help customers with common questions and issues. Be empathetic and solution-focused. " +
        "Respond with a warm tone."
    );
}

// Chat with each role
void ChatWithMultipleRoles()
{
    // Ask a doctor about medical concerns
    StartCoroutine(_client.SendMessageAsync(
        "My blood pressure is high. What should I do?",
        OnResponse,
        new ChatRequestOptions { SessionId = "doctor-session" }
    ));

    // Ask an engineer about code
    StartCoroutine(_client.SendMessageAsync(
        "What is the best way to implement a singleton in C#?",
        OnResponse,
        new ChatRequestOptions { SessionId = "engineer-session" }
    ));

    // Ask a translator to translate
    StartCoroutine(_client.SendMessageAsync(
        "Translate: 'The quick brown fox jumps over the lazy dog.'",
        OnResponse,
        new ChatRequestOptions { SessionId = "translator-session" }
    ));

    // Ask customer support
    StartCoroutine(_client.SendMessageAsync(
        "My order hasn't arrived. What should I do?",
        OnResponse,
        new ChatRequestOptions { SessionId = "support-session" }
    ));
}

// Cleanup
void CleanupRoles()
{
    // Clear history for each role session
    _client.ClearMessages("doctor-session");
    _client.ClearMessages("engineer-session");
    _client.ClearMessages("translator-session");
    _client.ClearMessages("support-session");
    
    // Or reset only the prompts (keep history)
    _client.ResetAllSessionSystemPrompts();
}
```

#### When to use session-specific prompts

| Scenario | Example | Benefit |
|------|-------|------|
| Multi-character conversations | Multiple NPCs in a game | Preserve unique personalities |
| Roleplay | Different personas | Better conversation quality |
| Domain experts | Doctor, lawyer, engineer | More reliable responses by role |
| Language handling | Japanese, English, Chinese sessions | Language-optimized behavior |
| Tone control | Polite/casual/formal | Context-appropriate responses |
| A/B testing | Different prompt versions | Measure and improve quality |

#### Design best practices

**1. Prompt templates**

```csharp
// Define prompt templates
public static class SystemPromptTemplates
{
    public const string Doctor =
        "You are a professional medical advisor. " +
        "Always recommend consulting a real doctor for serious conditions.";
    
    public const string Engineer =
        "You are an expert software engineer. Provide technical depth.";
    
    public const string CustomerSupport =
        "You are a helpful customer support agent. Be empathetic and professional.";
}

// Use
_client.SetSessionSystemPrompt("doctor-1", SystemPromptTemplates.Doctor);
_client.SetSessionSystemPrompt("engineer-1", SystemPromptTemplates.Engineer);
```

**2. Session configuration class**

```csharp
public class SessionConfig
{
    public string SessionId { get; set; }
    public string SystemPrompt { get; set; }
    public string RoleName { get; set; }
}

void SetupSession(SessionConfig config)
{
    _client.SetSessionSystemPrompt(config.SessionId, config.SystemPrompt);
    Debug.Log($"Initialized session: {config.RoleName}");
}
```

**3. Dynamic prompt customization**

```csharp
// Customize prompts based on user settings
string CreateCustomPrompt(string baseProfession, string tonePreference, string language)
{
    return $"You are a {baseProfession}. Your tone should be {tonePreference}. " +
           $"Always respond in {language}.";
}

_client.SetSessionSystemPrompt(
    "custom-session",
    CreateCustomPrompt("technical writer", "casual and friendly", "English")
);
```

#### Notes and limitations

- **Priority awareness**: Request-level prompts take highest priority. The global prompt is used only when no session prompt is set.
- **Prompt overrides**: Use `ChatRequestOptions.SystemPrompt` to override within a single request.
- **Memory**: Clearing sessions with `ClearMessages()` also removes session prompts.
- **Prompt length**: Very long prompts reduce available context; keep them concise.
- **Bulk init**: `SetSystemPromptForMultipleSessions()` is efficient for large batches (1000+ sessions).

See [SessionSystemPrompt.md](SessionSystemPrompt.md) for the detailed API reference.

### 4.8 Priority Scheduling



#### Why priority scheduling

In resource-constrained environments (GPU capacity, etc.), multiple requests can arrive at the same time.
This library assumes scenarios like:

- **High-priority messages**: Critical inference for system operation or game progression (e.g., key NPC responses).
- **Low-priority messages**: Flavor text where failure does not impact gameplay (e.g., small talk NPCs).

By prioritizing important messages and queueing low-priority ones, you can keep the system stable and responsive.

#### Default behavior

**Defaults:**
- `MaxConcurrentSessions = 1`: Only one session runs at a time.
- Multiple requests are queued in priority order.
- When resources free up, the next highest priority request runs.

**Flow:**

```
Multiple requests arrive
  ↓
Queued by priority
  ↓
Check resources (MaxConcurrentSessions)
  ↓
Execute by priority
  ├─ High priority -> runs immediately
  ├─ Medium priority -> waits for previous request
  └─ Low priority -> waits further back in queue

Each request completes
  ↓
Next priority request runs
```

#### WaitIfBusy behavior

**`WaitIfBusy = true` (recommended):**

If the client is busy, the request is queued and waits.

```csharp
void SendWithPriority()
{
    var systemMessage = new ChatRequestOptions
    {
        SessionId = "system-npc",
        Priority = 10,           // Higher number = higher priority
        WaitIfBusy = true        // Queue when busy
    };

    var flavorMessage = new ChatRequestOptions
    {
        SessionId = "flavor-npc",
        Priority = 0,            // Low priority (default)
        WaitIfBusy = true        // Queue when busy
    };

    // High-priority system message
    StartCoroutine(_client.SendMessageAsync(
        "Important information for the player",
        OnResponse,
        systemMessage
    ));

    // Low-priority flavor message
    StartCoroutine(_client.SendMessageAsync(
        "Casual small talk",
        OnResponse,
        flavorMessage
    ));

    // systemMessage runs immediately (higher priority)
    // flavorMessage waits until systemMessage finishes
}
```

**`WaitIfBusy = false` (immediate error):**

If the client is busy, the request is rejected with an error.

```csharp
void SendWithoutWaiting()
{
    var options = new ChatRequestOptions
    {
        SessionId = "session-1",
        Priority = 0,
        WaitIfBusy = false       // Error if busy
    };

    StartCoroutine(_client.SendMessageAsync(
        "Message",
        response => Debug.Log($"Receiving: {response.Content}"),
        error => {
            if (error.ErrorType == LLMErrorType.Unknown &&
                error.Message.Contains("busy"))
            {
                Debug.Log("Client is busy, request was rejected");
            }
        }
        options
    ));
}
```

#### Priority design example

```csharp
public class PrioritizedChatManager : MonoBehaviour
{
    private OllamaClient _client;

    // Priority constants
    private const int PRIORITY_CRITICAL = 100;      // Required for game progression
    private const int PRIORITY_HIGH = 50;           // Important NPC conversations
    private const int PRIORITY_NORMAL = 0;          // Normal NPC conversations
    private const int PRIORITY_LOW = -50;           // Flavor conversations

    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral"
        };
        _client = LLMClientFactory.CreateOllamaClient(config);
    }

    // Critical message
    void SendCriticalMessage(string message)
    {
        var options = new ChatRequestOptions
        {
            SessionId = "critical-system",
            Priority = PRIORITY_CRITICAL,
            WaitIfBusy = true
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    // Important NPC conversation
    void SendImportantNPCMessage(string npcId, string message)
    {
        var options = new ChatRequestOptions
        {
            SessionId = $"npc-{npcId}-important",
            Priority = PRIORITY_HIGH,
            WaitIfBusy = true
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    // Normal NPC conversation
    void SendNormalNPCMessage(string npcId, string message)
    {
        var options = new ChatRequestOptions
        {
            SessionId = $"npc-{npcId}-normal",
            Priority = PRIORITY_NORMAL,
            WaitIfBusy = true
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    // Flavor conversation (okay to drop)
    void SendFlavorMessage(string npcId, string message)
    {
        var options = new ChatRequestOptions
        {
            SessionId = $"npc-{npcId}-flavor",
            Priority = PRIORITY_LOW,
            WaitIfBusy = false  // Drop if busy
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    void OnResponse(ChatResponse response, ChatError error)
    {
        Debug.Log($"Response: {response.Content}");
    }

    void OnError(ChatError error)
    {
        Debug.LogError($"Error: {error.Message}");
    }
}
```

#### Concurrent sessions

Increase `MaxConcurrentSessions` to run sessions in parallel:

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    MaxConcurrentSessions = 2  // Run up to 2 sessions concurrently
};

OllamaServerManager.Initialize(config);
var client = LLMClientFactory.CreateOllamaClient(config);

// Requests from different sessions will run in parallel
// Within the same session, requests still respect priority
```

#### Scheduling behavior summary

| Setting | `WaitIfBusy=true` | `WaitIfBusy=false` |
|-----|------------------|-------------------|
| Client is idle | Run immediately | Run immediately |
| Client is busy | Queue by priority | Return error |
| Use cases | System-critical / important NPCs | Flavor / acceptable failure |

#### Performance tips

- **Keep priorities coarse**: Too many levels increase complexity; 3-5 levels is usually enough.
- **Separate sessions**: Put different importance levels in different sessions.
- **MaxConcurrentSessions**: Tune to GPU capacity (1-4 typical). Too high can cause OOM or overload.
- **Monitor resources**: In production, watch GPU and system memory.

### 4.10 Cancellation

Supports the standard Unity `CancellationToken` pattern. Use `CancellationTokenSource` when cancellation is needed.

```csharp
private CancellationTokenSource _cancellationTokenSource;

void SendWithCancel()
{
    // Create cancellation token source
    _cancellationTokenSource = new CancellationTokenSource();
    
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        CancellationToken = _cancellationTokenSource.Token
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "Generate a long response",
        response => Debug.Log($"Receiving: {response.Content}"),
        error =>
        {
            if (error.ErrorType == LLMErrorType.Cancelled)
            {
                Debug.Log("Request cancelled");
            }
            else
            {
                Debug.LogError($"Error: {error.Message}");
            }
        }
        options
    ));
}

// Call from a UI button, etc.
void CancelRequest()
{
    _cancellationTokenSource?.Cancel();
}

// Cleanup
void OnDestroy()
{
    _cancellationTokenSource?.Dispose();
}
```

**Cancellation with timeout:**

```csharp
void SendWithTimeout()
{
    // Auto-cancel after 10 seconds
    _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        CancellationToken = _cancellationTokenSource.Token
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "Generate a response",
        response => Debug.Log($"Receiving: {response.Content}"),
        error => {
            if (error.ErrorType == LLMErrorType.Cancelled)
            {
                Debug.Log("Request timeout");
            }
            else
            {
                Debug.LogError($"Error: {error.Message}");
            }
        }
        options
    ));
}
```

### 4.11 Retry and Error Handling

#### How automatic retries work

This library automatically retries transient failures such as network errors or temporary server issues.
The following errors are retried up to `MaxRetries`:

**Retryable errors:**
- `ConnectionFailed`: Server connection failure (timeouts, refused connections, etc.)
- `ServerError`: Server-side error (HTTP 500, 503, etc.)
- `Timeout`: Request timeout

Retries are handled internally, and **callers only receive the final result**.

#### Global retry settings

Configure retry behavior in `OllamaConfig`:

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    MaxRetries = 3,              // Retry up to 3 times
    RetryDelaySeconds = 1.0f,    // Initial delay 1s
    HttpTimeoutSeconds = 60.0f   // Request timeout 60s
};

var client = LLMClientFactory.CreateOllamaClient(config);

// You can also update retry settings later
config.MaxRetries = 5;
config.RetryDelaySeconds = 2.0f;
```

#### Retry strategy: exponential backoff

Automatic retry uses exponential backoff. Delay is calculated as:

```
Delay = RetryDelaySeconds × 2^(attempt-1)

Example when RetryDelaySeconds = 1.0:
  Attempt 1 fails -> wait 1s, retry
  Attempt 2 fails -> wait 2s, retry
  Attempt 3 fails -> wait 4s, retry
  MaxRetries reached -> return error
```

This reduces request bursts during high load.

#### Handling errors after retries

Errors returned to callers already exhausted `MaxRetries`.
This indicates a **non-transient problem**.

```csharp
void SendMessageAndHandleError()
{
    var options = new ChatRequestOptions
    {
        SessionId = "session-1"
    };

    StartCoroutine(_client.SendMessageAsync(
        "Message",
        response => Debug.Log($"Response: {response.Content}"),
        error =>
        {
            // Errors here have already retried up to MaxRetries
            Debug.LogError($"Error after retries: {error.Message}");
            
            HandleError(error);
        }
        options
    ));
}

void HandleError(ChatError error)
{
    switch (error.ErrorType)
    {
        case LLMErrorType.ConnectionFailed:
        case LLMErrorType.Timeout:
            // Network errors persisted after retries
            Debug.LogError(
                "Server is not responding. Please check:\n" +
                "1. Ollama server is running\n" +
                "2. Network connection is stable\n" +
                "3. Server URL is correct"
            );
            break;

        case LLMErrorType.ServerError:
            Debug.LogError(
                $"Server error (HTTP {error.HttpStatus}). " +
                "Please check server logs and health status."
            );
            break;

        case LLMErrorType.ModelNotFound:
            Debug.LogError(
                $"Model not found: {error.Message}. " +
                "Please install the model with: ollama pull <model-name>"
            );
            break;

        case LLMErrorType.InvalidResponse:
            Debug.LogError(
                $"Invalid response from server: {error.Message}. " +
                "The server response format is incorrect."
            );
            break;

        case LLMErrorType.Cancelled:
            Debug.Log("Request was cancelled by user");
            break;

        default:
            Debug.LogError($"Unrecoverable error: {error.Message}");
            break;
    }
}
```

#### Error handling guide

| Error type | Retried | Caller response |
|-----------|----------|------------|
| `ConnectionFailed` | ✅ | Check server and network |
| `ServerError` | ✅ | Check server logs, consider restart |
| `Timeout` | ✅ | Increase `HttpTimeoutSeconds`, check server performance |
| `ModelNotFound` | ❌ | Install model with `ollama pull` |
| `InvalidResponse` | ❌ | Check Ollama version |
| `Cancelled` | ❌ | Update UI (user intent) |
| `Unknown` | ❌ | Enable debug logs for details |

#### Recommended retry settings

| Scenario | MaxRetries | RetryDelaySeconds | HttpTimeoutSeconds |
|--------|-----------|-----------------|-----------------|
| Stable environment (LAN) | 2 | 0.5 | 30 |
| Typical local LLM | 3 | 1.0 | 60 |
| Unstable environment | 5 | 2.0 | 90 |
| Resource-constrained | 1 | 0.2 | 20 |

#### Best practices

- **Set during initialization**: Configure once in `OllamaConfig` at app start.
- **Use DebugMode**: `DebugMode = true` logs retry details.
- **Health checks**: `EnableHealthCheck = true` verifies server after startup.
- **Log errors**: In production, record errors after retries for analysis.

#### Verify retries with DebugMode

Enable debug logs to inspect retry behavior:

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    MaxRetries = 3,
    RetryDelaySeconds = 1.0f,
    DebugMode = true  // Logs retry details
};

var client = LLMClientFactory.CreateOllamaClient(config);

// Example logs:
// [Ollama] Sending request (attempt 1/3)
// [Ollama] Request failed (attempt 1/3): Connection reset (HTTP 0)
// [Ollama] Retrying in 1 seconds...
// [Ollama] Sending request (attempt 2/3)
// [Ollama] Request failed (attempt 2/3): Connection reset (HTTP 0)
// [Ollama] Retrying in 2 seconds...
// [Ollama] Sending request (attempt 3/3)
// [Ollama] Response received: {...}
```

### 4.12 Message Persistence

Session history can be saved to and restored from files. You can also encrypt saved files.

#### Save and restore a single session

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

void SaveAndLoadSession()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    // Chat in a session
    StartCoroutine(client.SendMessageAsync(
        "Hello",
        response => { },
        error => { },
        new ChatRequestOptions { SessionId = "my-session" }
    ));
    
    // Save the session later
    string savePath = Application.persistentDataPath + "/my_session.json";
    client.SaveSession(savePath, "my-session");
    Debug.Log($"Session saved to: {savePath}");
}

void RestoreSession()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string savePath = Application.persistentDataPath + "/my_session.json";
    client.LoadSession(savePath, "my-session");
    Debug.Log("Session restored from file");
    
    // Continue the restored session
    StartCoroutine(client.SendMessageAsync(
        "Do you remember the previous conversation?",
        response => Debug.Log($"Assistant: {response.Content}"),
        error => { }
        new ChatRequestOptions { SessionId = "my-session" }
    ));
}
```

#### Save with encryption

```csharp
void SaveWithEncryption()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    // Save a session with an encryption key
    string savePath = Application.persistentDataPath + "/my_session_encrypted.json";
    string encryptionKey = "my-secret-password-1234";
    
    client.SaveSession(savePath, "my-session", encryptionKey);
    Debug.Log($"Encrypted session saved to: {savePath}");
}

void RestoreEncryptedSession()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string savePath = Application.persistentDataPath + "/my_session_encrypted.json";
    string encryptionKey = "my-secret-password-1234";
    
    try
    {
        client.LoadSession(savePath, "my-session", encryptionKey);
        Debug.Log("Encrypted session restored");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Failed to restore session: {ex.Message}");
    }
}
```

#### Save and restore all sessions

```csharp
void SaveAllSessions()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    // Conversations across multiple sessions...
    
    // Save all sessions to a directory
    string saveDir = Application.persistentDataPath + "/chat_sessions";
    client.SaveAllSessions(saveDir);
    Debug.Log($"All sessions saved to: {saveDir}");
}

void RestoreAllSessions()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string saveDir = Application.persistentDataPath + "/chat_sessions";
    client.LoadAllSessions(saveDir);
    Debug.Log("All sessions restored");
}
```

#### Save multiple sessions with encryption

```csharp
void SaveAllSessionsWithEncryption()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string saveDir = Application.persistentDataPath + "/encrypted_sessions";
    string encryptionKey = "shared-encryption-key";
    
    // Save all sessions with encryption
    client.SaveAllSessions(saveDir, encryptionKey);
    Debug.Log("All sessions encrypted and saved");
}

void RestoreAllEncryptedSessions()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string saveDir = Application.persistentDataPath + "/encrypted_sessions";
    string encryptionKey = "shared-encryption-key";
    
    try
    {
        client.LoadAllSessions(saveDir, encryptionKey);
        Debug.Log("All encrypted sessions restored");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Failed to restore sessions: {ex.Message}");
    }
}
```

#### Persistence specs

- **File format**: JSON (plaintext) or JSON+encryption
- **Encryption algorithm**: AES-256-CBC (PBKDF2 key derivation)
- **Stored data**: Session ID, system prompt, message history, timestamps
- **File names**: Automatically derived from sanitized session IDs (`session-id.json`, etc.)
- **Error handling**: Invalid keys throw `InvalidOperationException`

#### Best practices

```csharp
public class ChatSessionManager : MonoBehaviour
{
    private OllamaClient _client;
    private string _sessionDir;
    private string _encryptionKey = "my-app-encryption-key";

    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral"
        };
        _client = LLMClientFactory.CreateOllamaClient(config);
        
        // Set session directory
        _sessionDir = System.IO.Path.Combine(Application.persistentDataPath, "chat_history");
    }

    // On app start: restore previous sessions
    void RestorePreviousSessions()
    {
        if (System.IO.Directory.Exists(_sessionDir))
        {
            try
            {
                _client.LoadAllSessions(_sessionDir, _encryptionKey);
                Debug.Log("Previous sessions restored");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to restore sessions: {ex.Message}");
                // Error handling: start fresh
            }
        }
    }

    // Send a new message
    void SendMessage(string sessionId, string message)
    {
        StartCoroutine(_client.SendMessageAsync(
            message,
            response =>
            {
                // Auto-save on success
                SaveSession(sessionId);
            },
            error => { },
            new ChatRequestOptions { SessionId = sessionId }
        ));
    }

    // Save a session (auto backup)
    void SaveSession(string sessionId)
    {
        try
        {
            string filePath = System.IO.Path.Combine(_sessionDir, $"{sessionId}.json");
            _client.SaveSession(filePath, sessionId, _encryptionKey);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save session: {ex.Message}");
        }
    }

    // On app quit: save all sessions
    void OnApplicationQuit()
    {
        try
        {
            _client.SaveAllSessions(_sessionDir, _encryptionKey);
            Debug.Log("All sessions saved on quit");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save sessions on quit: {ex.Message}");
        }
    }
}
```

### 4.14 Tools (Function Calling)

Supports Function Calling so the LLM can invoke external tools.
You only register callback functions and the LLM uses them automatically.

**Docs:**
- **[Tools/Design.md](Tools/Design.md)** - Architecture, flow, and implementation details
- **[Tools/InputSchema_Examples.md](Tools/InputSchema_Examples.md)** - inputSchema examples and best practices

**Key features:**
- ✅ **Auto schema generation**: Reflection builds JSON Schema from callback signatures
- ✅ **Type-safe**: JSON parameters are converted into C# types before invocation
- ✅ **Automatic return conversion**: Primitive, object, and array return values are stringified automatically
- ✅ **Infinite-loop guard**: Max iteration count prevents tool call loops
- ✅ **Streaming support**: Tools work in streaming mode too

#### Auto schema limitations and workarounds

Current auto schema inference supports **simple types only**. For Shop tools like in `SimpleChat.cs`,
signatures using `string` / `int` / `bool` / `float` / `double` / `List<T>` / arrays are fine.

For **complex inputs (object types, nested structures, custom classes)**, auto inference treats input as `string`.
In that case, the LLM is less likely to produce valid JSON, so **manual schema is recommended**.

**Supported input types (auto)**
- `string`
- `int`, `long`, `short`, `byte`
- `float`, `double`, `decimal`
- `bool`
- `T[]`, `List<T>`, `IList<T>`, `IEnumerable<T>` (T is one of the above)

**Use manual schema when**
- Inputs are nested objects
- You need custom classes/structs as inputs
- You want constraints like `enum` or `min/max`

**Example from SimpleChat.cs (auto schema)**
```csharp
client.RegisterTool("BuyItem", "Buy an item from your shop", (Func<string, string>)BuyItem);
client.RegisterTool("SellItem", "Sell an item to your shop", (Func<string, int, string>)SellItem);
```
When parameters are **primitive only**, auto schema generation works well.

**Manual schema for complex input (recommended)**
```csharp
client.RegisterTool(
    name: "create_order",
    description: "Create an order",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            customer = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    age = new { type = "integer" }
                },
                required = new[] { "name", "age" }
            },
            items = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string" },
                        quantity = new { type = "integer" }
                    },
                    required = new[] { "id", "quantity" }
                }
            }
        },
        required = new[] { "customer", "items" }
    },
    callback: (Func<Newtonsoft.Json.Linq.JObject, Newtonsoft.Json.Linq.JObject, string>)(Newtonsoft.Json.Linq.JObject customer, Newtonsoft.Json.Linq.JArray items) => "ok"
);
```

**Note**: Enable DebugMode to log auto-generated schemas at registration time.

#### Basic tool registration

The simplest example:

**Important**: This example uses only primitive types, so auto schema generation works as expected.
If you need complex input (objects, custom classes, nested structures), use the manual schema above.

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class ToolExample : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig
        {
            DefaultModelName = "llama3.2",  // Use a tools-capable model
            DebugMode = true
        });

        // Tool registration: addition (primitive types only)
        // Schema is auto-generated and return values are stringified
        _client.RegisterTool(
            name: "add_numbers",
            description: "Add two numbers together",
            callback: (Func<int, int, int>)((a, b) => a + b)
        );

        // Tool registration: current time
        _client.RegisterTool(
            name: "get_current_time",
            description: "Get the current time",
            callback: (Func<DateTime>)(() => System.DateTime.Now)
        );

        // Send a message to the LLM
        StartCoroutine(_client.SendMessageAsync(
            "What is 125 + 378? And what time is it now?",
            response =>
            {
                // LLM calls tools automatically
                Debug.Log($"Assistant: {response.Content}");
                // Example: "125 + 378 = 503. The current time is 2026-02-07 15:30:45."
            },
            error => Debug.LogError($"Error: {error.Message}")
        ));
    }
}
```

#### Default values and parameter descriptions

```csharp
using EasyLocalLLM.LLM.Core;

void RegisterAdvancedTools()
{
    // Optional parameters with default values
    _client.RegisterTool(
        name: "search_web",
        description: "Search the web for information",
        callback: (Func<string, int, string>)((string query, int maxResults = 5) =>
        {
            // maxResults is optional (LLM recognizes it as such)
            return $"Found {maxResults} results for '{query}'";
        })
    );

    // Use ToolParameter for detailed parameter descriptions
    _client.RegisterTool(
        name: "calculate_distance",
        description: "Calculate distance between two points",
        callback: (Func<double, double, double, double, double>)(
            ([ToolParameter("Latitude of starting point")] double lat1,
            [ToolParameter("Longitude of starting point")] double lon1,
            [ToolParameter("Latitude of destination")] double lat2,
            [ToolParameter("Longitude of destination")] double lon2
        ) =>
        {
            // Calculate distance via Haversine formula
            double distance = CalculateHaversine(lat1, lon1, lat2, lon2);
            return distance;  // double is also stringified
        })
    );
}
```

#### Custom object return values

```csharp
void RegisterDataTools()
{
    // Tool returning a custom object
    // -> Automatically serialized to JSON
    _client.RegisterTool(
        name: "get_user_info",
        description: "Get user information by ID",
        callback: (Func<string, object>)((userId) => new
        {
            id = userId,
            name = "John Doe",
            age = 30,
            email = "john@example.com",
            isActive = true
        })  // JSON: {"id":"123","name":"John Doe",...}
    );

    // Tool returning an array
    _client.RegisterTool(
        name: "list_recent_messages",
        description: "Get recent chat messages",
        callback: (Func<int, object>)((count) => new[]
        {
            new { sender = "Alice", message = "Hello!" },
            new { sender = "Bob", message = "Hi there!" }
        })  // Passed to LLM as a JSON array
    );
}
```

#### Error handling

Errors during tool execution are caught and returned to the LLM as error messages:

```csharp
void RegisterToolWithErrorHandling()
{
    _client.RegisterTool(
        name: "divide_numbers",
        description: "Divide two numbers",
        callback: (Func<double, double, object>)((double numerator, double denominator) =>
        {
            // Handle error cases
            if (denominator == 0)
            {
                return "Error: Division by zero is not allowed";
            }
            
            return numerator / denominator;
        })
    );

    // Or throw exceptions (caught and converted to error messages)
    _client.RegisterTool(
        name: "get_player_health",
        description: "Get player health points",
        callback: (Func<string, int>)((string playerId) =>
        {
            var player = FindPlayer(playerId);
            if (player == null)
            {
                throw new System.Exception($"Player '{playerId}' not found");
            }
            return player.Health;
        })
    );
}
```

#### Set max tool iterations

Prevent infinite loops by limiting tool call iterations:

```csharp
void SendWithToolOptions()
{
    var options = new ChatRequestOptions
    {
        SessionId = "my-session",
        MaxToolIterations = 10  // Default is 5
    };

    StartCoroutine(_client.SendMessageAsync(
        "Calculate 5 + 3, then multiply by 2, then subtract 4",
        response => {
            // LLM may call tools multiple times
            Debug.Log(response.Content);
        },
        error => { },
        options
    ));
}
```

#### Manage tools

```csharp
void ManageTools()
{
    // List registered tools
    var tools = _client.GetRegisteredTools();
    foreach (var tool in tools)
    {
        Debug.Log($"Tool: {tool.Name} - {tool.Description}");
    }

    // Check if a tool is registered
    bool hasAddTool = _client.HasTool("add_numbers");

    // Unregister a tool
    _client.UnregisterTool("add_numbers");

    // Remove all tools
    _client.RemoveAllTools();
}
```

#### Streaming + tools

```csharp
void StreamingWithTools()
{
    _client.RegisterTool("get_time", "Get current time", (Func<DateTime>)(() => System.DateTime.Now));

    StartCoroutine(_client.SendMessageStreamingAsync(
        "What time is it?",
        response =>
        {
            if (!response.IsFinal)
            {
                // Partial streaming response
                Debug.Log($"Partial: {response.Content}");
            }
            else
            {
                // Final response (after tool execution)
                Debug.Log($"Final: {response.Content}");
            }
        },
        error => { },
        new ChatRequestOptions { SessionId = "streaming-session" }
    ));
}
```

#### Manual schema (advanced)

You can specify JSON Schema manually instead of relying on reflection:

```csharp
void RegisterToolWithManualSchema()
{
    _client.RegisterTool(
        name: "custom_tool",
        description: "A tool with manual schema",
        inputSchema: new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "User name" },
                age = new { type = "integer", minimum = 0, maximum = 150 }
            },
            required = new[] { "name" }
        },
        callback: (string json) =>
        {
            // Parse JSON manually
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            string name = obj["name"].ToString();
            int age = obj["age"]?.Value<int>() ?? 0;
            
            return $"Hello {name}, age {age}";
        }
    );
}
```

#### Notes

- **Supported models**: Tools are supported only by some models (e.g., `llama3.2`, `mistral`).
- **Debug mode**: `DebugMode = true` logs tool execution.
- **Return types**: Primitive types, custom objects, and arrays are all auto-converted.
- **Performance**: Tool calls require additional LLM requests, so multiple round-trips occur.

### 4.13 JSON Response Format

You can request responses in JSON format. This is useful for structured data or schema-constrained output.

#### Basic JSON response

Set `Format` to `ChatRequestOptions.FormatConstants.Json` to request JSON output.

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using UnityEngine;

public class JsonFormatExample : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig
        {
            DefaultModelName = "llama3.2",
            DebugMode = true
        });

        // Request JSON response
        var options = new ChatRequestOptions
        {
            Format = ChatRequestOptions.FormatConstants.Json
        };

        StartCoroutine(_client.SendMessageAsync(
            "Generate a user profile with name, age, and email",
            response =>
            {
                // Response is JSON string
                Debug.Log($"JSON Response: {response.Content}");
                // Example: {"name": "John Doe", "age": 30, "email": "john@example.com"}

                // Parse and use
                var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                string name = json["name"]?.ToString();
                int age = json["age"]?.Value<int>() ?? 0;
                Debug.Log($"User: {name}, Age: {age}");
            },
            error => Debug.LogError($"Error: {error.Message}"),
            options
        ));
    }
}
```

#### JSON Schema for stricter structure

Use `FormatSchema` to specify JSON Schema and enforce output structure.

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using UnityEngine;

public class JsonSchemaExample : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());

        // Specify JSON Schema
        var options = new ChatRequestOptions
        {
            FormatSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    age = new { type = "number" },
                    email = new { type = "string" },
                    isActive = new { type = "boolean" }
                },
                required = new[] { "name", "age" }
            }
        };

        StartCoroutine(_client.SendMessageAsync(
            "Create a user profile for a 25-year-old software engineer named Alice",
            response =>
            {
                Debug.Log($"Structured JSON: {response.Content}");
                // Response matches the schema
            },
            error => { },
            options
        ));
    }
}
```

#### Schema with arrays

```csharp
void RequestArrayData()
{
    var options = new ChatRequestOptions
    {
        FormatSchema = new
        {
            type = "object",
            properties = new
            {
                teamName = new { type = "string" },
                members = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            role = new { type = "string" },
                            level = new { type = "number" }
                        },
                        required = new[] { "name", "role" }
                    }
                }
            },
            required = new[] { "teamName", "members" }
        }
    };

    StartCoroutine(_client.SendMessageAsync(
        "Generate a fantasy RPG party with 4 members",
        response =>
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
            string teamName = json["teamName"]?.ToString();
            var members = json["members"] as Newtonsoft.Json.Linq.JArray;
            
            Debug.Log($"Team: {teamName}");
            foreach (var member in members)
            {
                string name = member["name"]?.ToString();
                string role = member["role"]?.ToString();
                Debug.Log($"- {name} ({role})");
            }
        },
        error => { },
        options
    ));
}
```

#### Practical game data generation

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using System;
using UnityEngine;

[Serializable]
public class EnemyData
{
    public string name;
    public int health;
    public int attack;
    public int defense;
    public string[] weaknesses;
}

public class GameDataGenerator : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    }

    public void GenerateEnemy(string theme, Action<EnemyData> onComplete)
    {
        var options = new ChatRequestOptions
        {
            FormatSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    health = new { type = "number", minimum = 50, maximum = 500 },
                    attack = new { type = "number", minimum = 10, maximum = 100 },
                    defense = new { type = "number", minimum = 5, maximum = 50 },
                    weaknesses = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    }
                },
                required = new[] { "name", "health", "attack", "defense" }
            }
        };

        StartCoroutine(_client.SendMessageAsync(
            $"Generate a {theme} enemy with stats and weaknesses",
            response =>
            {
                try
                {
                    // Deserialize JSON
                    var enemyData = Newtonsoft.Json.JsonConvert.DeserializeObject<EnemyData>(response.Content);
                    Debug.Log($"Generated: {enemyData.name} (HP: {enemyData.health}, ATK: {enemyData.attack})");
                    onComplete?.Invoke(enemyData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse enemy data: {ex.Message}");
                }
            },
            error => Debug.LogError($"Failed to generate enemy: {error.Message}"),
            options
        ));
    }
}
```

#### Task-based usage

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using System.Threading.Tasks;
using UnityEngine;

public class AsyncJsonExample : MonoBehaviour
{
    private OllamaClient _client;

    async void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());

        var options = new ChatRequestOptions
        {
            Format = ChatRequestOptions.FormatConstants.Json
        };

        try
        {
            var response = await _client.SendMessageTaskAsync(
                "Generate random item data with name, price, and rarity",
                options
            );

            var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
            Debug.Log($"Item: {json["name"]}, Price: {json["price"]}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error: {ex.Message}");
        }
    }
}
```

#### Streaming usage

```csharp
void StreamingJsonExample()
{
    var options = new ChatRequestOptions
    {
        Format = ChatRequestOptions.FormatConstants.Json
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "Generate a character profile",
        response =>
        {
            if (response.IsFinal)
            {
                // Complete JSON
                Debug.Log($"Complete JSON: {response.Content}");
                var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                // Process...
            }
            else
            {
                // Partial JSON chunks (for display)
                Debug.Log($"Partial: {response.Content}");
            }
        },
        error => { },
        options
    ));
}
```

#### Notes

- **FormatSchema vs Format**: If `FormatSchema` is set, `Format` is ignored.
- **Supported models**: JSON format works correctly only on compatible models (e.g., `llama3.2`, `mistral`).
- **Parsing**: JSON responses are strings; parse with `Newtonsoft.Json` or similar.
- **Error handling**: Complex schemas or unsupported models can cause errors.
- **Streaming**: Full JSON is guaranteed only when `IsFinal = true`.

## 5. Practical Examples

Here are practical examples that combine the features covered so far.

### In-game NPC conversation system

A common game development pattern including UI integration, cancellation, and error handling.

```csharp
using EasyLocalLLM.LLM;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class NPCChatSystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text npcDialogueText;
    [SerializeField] private InputField playerInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button cancelButton;
    
    private OllamaClient _client;
    private CancellationTokenSource _cancellationTokenSource;
    private string _currentNPCId = "friendly-shopkeeper";
    private string _sessionDirectory;

    void Start()
    {
        // Configuration
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            MaxConcurrentSessions = 1,
            DebugMode = Application.isEditor
        };
        
        _client = LLMClientFactory.CreateOllamaClient(config);
        
        // Configure session save directory
        _sessionDirectory = System.IO.Path.Combine(
            Application.persistentDataPath,
            "NPCChatSessions"
        );
        if (!System.IO.Directory.Exists(_sessionDirectory))
        {
            System.IO.Directory.CreateDirectory(_sessionDirectory);
        }
        
        // UI event setup
        sendButton.onClick.AddListener(OnSendClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        cancelButton.gameObject.SetActive(false);
        
        // Restore previous session
        LoadPreviousSession();
    }

    void OnSendClicked()
    {
        string userMessage = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;
        
        // Update UI
        playerInputField.text = "";
        playerInputField.interactable = false;
        sendButton.interactable = false;
        cancelButton.gameObject.SetActive(true);
        npcDialogueText.text = "(Thinking...)";
        
        // Create cancellation token
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Get NPC response (streaming)
        var options = new ChatRequestOptions
        {
            SessionId = _currentNPCId,
            Temperature = 0.8f,
            Priority = 50,  // Normal priority
            WaitIfBusy = true,
            CancellationToken = _cancellationTokenSource.Token,
            SystemPrompt = "You are a friendly general store owner. Speak warmly to adventurers."
        };
        
        StartCoroutine(_client.SendMessageStreamingAsync(
            userMessage,
            response => OnNPCResponse(response),
            error => OnError(error),
            options
        ));
    }
    
    void LoadPreviousSession()
    {
        try
        {
            string sessionPath = System.IO.Path.Combine(
                _sessionDirectory,
                _currentNPCId + ".json"
            );

            if (System.IO.File.Exists(sessionPath))
            {
                _client.LoadSession(sessionPath, _currentNPCId, encryptionKey: null);
                Debug.Log($"Restored previous session: {_currentNPCId}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Session restore error: {ex.Message}");
        }
    }

    void OnError(ChatError error)
    {
        HandleError(error);
        ResetUI();
    }

    void OnNPCResponse(ChatResponse response, ChatError error)
    {
        // Streamed incremental display
        npcDialogueText.text = response.Content;
        
        if (response.IsFinal)
        {
            // Save session on completion
            SaveCurrentSession();
            ResetUI();
        }
    }
    
    void SaveCurrentSession()
    {
        try
        {
            string sessionPath = System.IO.Path.Combine(
                _sessionDirectory,
                _currentNPCId + ".json"
            );

            _client.SaveSession(sessionPath, _currentNPCId, encryptionKey: null);
            Debug.Log($"Session saved: {sessionPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Session save error: {ex.Message}");
        }
    }

    void OnCancelClicked()
    {
        _cancellationTokenSource?.Cancel();
        npcDialogueText.text = "(Conversation canceled)";
    }

    void HandleError(ChatError error)
    {
        switch (error.ErrorType)
        {
            case LLMErrorType.ConnectionFailed:
                npcDialogueText.text = "(The shopkeeper seems to be away...)";
                Debug.LogWarning("NPC system error: connection failed");
                break;
                
            case LLMErrorType.Cancelled:
                npcDialogueText.text = "(Conversation interrupted)";
                break;
                
            default:
                npcDialogueText.text = "(The shopkeeper is at a loss for words...)";
                Debug.LogError($"NPC system error: {error.Message}");
                break;
        }
    }

    void ResetUI()
    {
        playerInputField.interactable = true;
        sendButton.interactable = true;
        cancelButton.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        _cancellationTokenSource?.Dispose();
        // Save session on exit
        SaveCurrentSession();
        _client?.ClearAllMessages();
    }
}
```

    ### Parallel conversation management for multiple NPCs

    Example for concurrent NPC conversations, including priority and session management.

```csharp
using EasyLocalLLM.LLM;
using System.Collections.Generic;
using UnityEngine;

public class MultiNPCManager : MonoBehaviour
{
    private OllamaClient _client;
    private Dictionary<string, NPCProfile> _npcProfiles;

    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            MaxConcurrentSessions = 2  // Up to 2 concurrent conversations
        };
        
        _client = LLMClientFactory.CreateOllamaClient(config);
        
        // Define NPC profiles
        _npcProfiles = new Dictionary<string, NPCProfile>
        {
            ["shopkeeper"] = new NPCProfile
            {
                SessionId = "npc-shopkeeper",
                Priority = 50,  // Normal priority
                SystemPrompt = "You are a friendly general store owner."
            },
            ["quest-giver"] = new NPCProfile
            {
                SessionId = "npc-quest-giver",
                Priority = 100,  // High priority (important for quest progress)
                SystemPrompt = "You are a wise sage who gives important quests."
            },
            ["random-villager"] = new NPCProfile
            {
                SessionId = "npc-villager",
                Priority = -50,  // Low priority (flavor)
                SystemPrompt = "You are a villager who makes small talk."
            }
        };
    }

    public void TalkToNPC(string npcId, string playerMessage, System.Action<string> onComplete)
    {
        if (!_npcProfiles.ContainsKey(npcId))
        {
            Debug.LogError($"Unknown NPC: {npcId}");
            return;
        }
        
        var profile = _npcProfiles[npcId];
        var options = new ChatRequestOptions
        {
            SessionId = profile.SessionId,
            Temperature = 0.8f,
            Priority = profile.Priority,
            WaitIfBusy = true,
            SystemPrompt = profile.SystemPrompt
        };
        
        StartCoroutine(_client.SendMessageAsync(
            playerMessage,
            response => onComplete?.Invoke(response.Content),
            error => 
            {
                Debug.LogError($"NPC {npcId} error: {error.Message}");
                onComplete?.Invoke("...");
            }
            options
        ));
    }

    public void ResetNPCMemory(string npcId)
    {
        if (_npcProfiles.ContainsKey(npcId))
        {
            _client.ClearMessages(_npcProfiles[npcId].SessionId);
        }
    }

    private class NPCProfile
    {
        public string SessionId;
        public int Priority;
        public string SystemPrompt;
    }
}
```

## 6. Class Structure

```
Runtime/LLM/
├── Core Data Models
│   ├── ChatMessage.cs           # Chat message
│   ├── ChatResponse.cs          # LLM response
│   ├── ChatError.cs             # Error info
│   ├── ChatLLMException.cs      # Task exception wrapper
│   └── ChatRequestOptions.cs    # Request options
│
├── Manager & Client
│   ├── ChatHistoryManager.cs    # Message history (session-aware)
│   ├── ChatSessionPersistence.cs# Session persistence (Save/Load)
│   ├── ChatEncryption.cs        # Encryption/decryption (AES-256)
│   ├── CoroutineRunner.cs       # Task bridge runner
│   ├── OllamaConfig.cs          # Configuration
│   ├── OllamaServerManager.cs   # Server lifecycle management
│   ├── OllamaClient.cs          # Ollama client implementation
│   ├── HttpRequestHelper.cs     # HTTP retry logic (internal)
│
└── Factory & Interface
    ├── IChatLLMClient.cs        # Client interface
    └── LLMClientFactory.cs      # Client factory
```

## 7. Configuration Options

Key properties in `OllamaConfig`:

| Property | Type | Default | Description |
|-----------|-----|----------|------|
| ServerUrl | string | http://localhost:11434 | Ollama server URL (can be remote) |
| DefaultModelName | string | mistral | Default model name; see `ollama list` |
| MaxRetries | int | 3 | Max retry attempts for network failures |
| RetryDelaySeconds | float | 1.0f | Initial retry delay (exponential backoff) |
| DefaultSeed | int | -1 | Default seed; -1 = random, fixed for reproducibility |
| HttpTimeoutSeconds | float | 60.0f | HTTP request timeout (seconds) |
| DebugMode | bool | false | Debug logs; true for detailed logs |
| AutoStartServer | bool | true | Auto-start Ollama server; false for manual control |
| EnableHealthCheck | bool | true | Run health check after server start |
| ExecutablePath | string | - | Ollama executable path (required if AutoStartServer=true) |
| ModelsDirectory | string | ./Models | Ollama models directory; maps to OLLAMA_MODELS |
| MaxConcurrentSessions | int | 1 | Maximum concurrent sessions; tune to GPU memory |

## 8. Default Settings

### Design intent for defaults

Defaults are tuned for **fast iteration in development environments**.

| Setting | Default | Rationale | Production recommendation |
|--------|---------|--------|-----------------|
| `AutoStartServer` | `true` | Skip manual startup in development | `false` |
| `MaxRetries` | `3` | Covers most transient network issues | `2` to `5` (environment-dependent) |
| `RetryDelaySeconds` | `1.0f` | Exponential backoff with short delays for dev | `1.0f` to `2.0f` |
| `HttpTimeoutSeconds` | `60.0f` | Local LLMs may need longer inference | `30.0f` to `90.0f` |
| `DebugMode` | `false` | Reduce log volume | `true` (during troubleshooting) |
| `EnableHealthCheck` | `true` | Favor startup reliability | `true` |
| `MaxConcurrentSessions` | `1` | Conservative for memory/GPU constraints | `1` to `4` (by GPU capacity) |

#### About AutoStartServer

**Recommended when `AutoStartServer = true`:**
- ✅ Development (Unity editor tests)
- ✅ Standalone builds where users do not have Ollama installed
- ✅ Fully self-contained applications

**Set `AutoStartServer = false` when:**
- ❌ Ollama is already running as a system service
- ❌ Multiple apps share a single Ollama instance
- ❌ You need explicit server lifecycle control
- ❌ Connecting to a remote server

**Recommended configurations:**

```csharp
// Development
var devConfig = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    AutoStartServer = true,      // Auto-start server
    DebugMode = true,            // Detailed logs
    MaxRetries = 3,              // More retries in development
    EnableHealthCheck = true
};

// Production (Windows standalone)
var prodConfig = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    AutoStartServer = true,      // Auto-start for users
    ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
    ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
    DebugMode = false,           // Minimal logs
    MaxRetries = 2,              // Stable environment
    HttpTimeoutSeconds = 90.0f,  // Large models
    EnableHealthCheck = true
};

// Production (system service)
var serviceConfig = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    AutoStartServer = false,     // Already running
    DebugMode = false,
    MaxRetries = 2,
    HttpTimeoutSeconds = 60.0f,
    EnableHealthCheck = false    // Already running
};

// Production (remote server)
var remoteConfig = new OllamaConfig
{
    ServerUrl = "http://llm-server.example.com:11434",
    DefaultModelName = "mistral",
    AutoStartServer = false,     // Remote cannot be started locally
    DebugMode = false,
    MaxRetries = 5,              // Unstable network
    RetryDelaySeconds = 2.0f,    // Longer waits
    HttpTimeoutSeconds = 120.0f, // Network latency
    EnableHealthCheck = false    // Optional for remote
};
```

#### About MaxRetries

**Why the default is 3:**

```
Success rate by attempt (network errors)

         Success  Cumulative
Attempt 1 ~70%    70%
Attempt 2 ~80%    94%
Attempt 3 ~85%    99%        <- Covers most transient errors
Attempt 4 ~90%    99.9%
Attempt 5 ~92%    99.99%
```

Three retries cover about **99%** of transient network issues, so this is the default.

**Recommended values by use case:**

```csharp
// Development: more retries
if (isDevelopment)
{
    config.MaxRetries = 5;  // Reduce failures during debugging
}

// Production LAN: fewer retries
if (isProduction && isLocalNetwork)
{
    config.MaxRetries = 2;  // Stable network
}

// Production with unstable network: more retries
if (isProduction && isUnstableNetwork)
{
    config.MaxRetries = 5;  // Stronger retry
    config.RetryDelaySeconds = 2.0f;  // Longer wait
}

// Mobile networks
if (isMobileNetwork)
{
    config.MaxRetries = 4;
    config.RetryDelaySeconds = 1.5f;
    config.HttpTimeoutSeconds = 90.0f;  // Longer timeout
}
```

#### Other default values

**`HttpTimeoutSeconds = 60.0f`**
- Local LLMs typically respond within 1 to 30 seconds
- 60 seconds accounts for large model inference (e.g., llama2-70b)

**`RetryDelaySeconds = 1.0f`**
- Exponential backoff: 1s -> 2s -> 4s -> 8s...
- With MaxRetries=3, total wait is about 7s
- Assumes recovery from temporary server issues

**`MaxConcurrentSessions = 1`**
- Assumes limited GPU memory
- Parallel sessions require significant resources
- Increase only as needed (conservative default)

### Recommended environment-specific patterns

#### Pattern 1: Development in Unity Editor

```csharp
void SetupDevelopmentEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://localhost:11434",
        DefaultModelName = "mistral",
        AutoStartServer = true,
        DebugMode = true,
        MaxRetries = 5,
        HttpTimeoutSeconds = 120.0f,  // Longer timeout
        EnableHealthCheck = true,
        MaxConcurrentSessions = 1
    };
    
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

#### Pattern 2: Windows standalone build

```csharp
void SetupWindowsStandaloneEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://localhost:11434",
        DefaultModelName = "mistral",
        AutoStartServer = true,
        ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
        ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
        DebugMode = false,
        MaxRetries = 3,
        HttpTimeoutSeconds = 90.0f,
        EnableHealthCheck = true,
        MaxConcurrentSessions = 2  // Tune to hardware
    };
    
    OllamaServerManager.Initialize(config);
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

#### Pattern 3: System service environment

```csharp
void SetupSystemServiceEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://localhost:11434",
        DefaultModelName = "mistral",
        AutoStartServer = false,  // Service already running
        DebugMode = false,
        MaxRetries = 2,  // Stable environment
        HttpTimeoutSeconds = 60.0f,
        EnableHealthCheck = false,
        MaxConcurrentSessions = 4  // If resources allow
    };
    
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

#### Pattern 4: Remote connection over network

```csharp
void SetupRemoteServerEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://llm-server.company.com:11434",
        DefaultModelName = "mistral",
        AutoStartServer = false,  // Remote cannot be started locally
        DebugMode = false,
        MaxRetries = 5,  // Unstable network
        RetryDelaySeconds = 2.0f,  // Longer waits
        HttpTimeoutSeconds = 120.0f,  // Network latency
        EnableHealthCheck = false,
        MaxConcurrentSessions = 2
    };
    
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

### Troubleshooting checklist

| Issue | Cause | Suggested change |
|-----|-----|------------------|
| Frequent failures | Unstable network | Increase `MaxRetries` and `RetryDelaySeconds` |
| Frequent timeouts | Slow server processing | Increase `HttpTimeoutSeconds` |
| Out of memory | Too many concurrent sessions | Reduce `MaxConcurrentSessions` |
| Server won't start | Auto-start failed | Set `AutoStartServer=false` and start manually |
| Debugging is hard | Insufficient logs | Set `DebugMode=true` |

## 9. Error Types and Handling

This section describes each error type and how to handle it.
For retry behavior, see [4.9 Retry and Error Handling](#49-retry-and-error-handling).

### Error types and sample messages

`ChatError` includes the following properties:

```csharp
public class ChatError
{
    public LLMErrorType ErrorType { get; set; }  // Error type
    public string Message { get; set; }           // Error message
    public Exception Exception { get; set; }      // Exception details (if any)
    public int? HttpStatus { get; set; }          // HTTP status code (if any)
}
```

### Error types in detail

#### 1. ConnectionFailed (server connection failure)

**Typical message:**
```
Cannot connect to Ollama server at 'http://localhost:11434'. 
Please check: (1) Server is running, (2) URL is correct, (3) Firewall settings. 
Original error: Connection refused
```

**Causes:**
- Ollama server is not running
- Port is incorrect
- Firewall is blocking

**Handling:**
```csharp
if (error.ErrorType == LLMErrorType.ConnectionFailed)
{
    Debug.LogError($"Connection error: {error.Message}");
    
    // User guidance
    ShowUserMessage(
        "Unable to connect",
        "Please check that the Ollama server is running.\n" +
        $"Server URL: {config.ServerUrl}"
    );
}
```

#### 2. ServerError (server-side error)

**Typical message:**
```
Ollama server error (HTTP 503). 
Please check: (1) Server logs, (2) Model is loaded correctly, (3) Server resources (memory/GPU). 
Original error: Service temporarily unavailable
```

**Causes:**
- Server crash
- Server overload
- Model load failure
- GPU/CPU memory shortage

**Handling:**
```csharp
if (error.ErrorType == LLMErrorType.ServerError)
{
    Debug.LogError($"Server error: {error.Message}");
    
    if (error.HttpStatus == 503)
    {
        ShowUserMessage(
            "Server is overloaded",
            "The server is temporarily unavailable. Please wait and retry."
        );
    }
    else
    {
        ShowUserMessage(
            "Server error",
            $"Server error occurred (HTTP {error.HttpStatus}).\n" +
            "Try restarting the server."
        );
    }
}
```

#### 3. ModelNotFound (model not found)

**Typical message:**
```
Model 'mistral' not found (HTTP 404). 
Please run: 'ollama pull mistral' or check the model name is correct. 
Use 'ollama list' to see installed models.
```

**Causes:**
- Incorrect model name
- Model not installed
- Typo in model name

**Handling:**
```csharp
if (error.ErrorType == LLMErrorType.ModelNotFound)
{
    Debug.LogError($"Model error: {error.Message}");
    
    string modelName = config.DefaultModelName;
    ShowUserMessage(
        "Model not found",
        $"Model '{modelName}' is not installed.\n\n" +
        $"Run the following in PowerShell:\n" +
        $"ollama pull {modelName}\n\n" +
        "Or select a different model in settings."
    );
    
    // Show model selection UI
    ShowModelSelectionUI();
}
```

#### 4. Timeout

**Typical message:**
```
Request timed out after 60 seconds. 
Please consider: (1) Increase HttpTimeoutSeconds, (2) Use a smaller model, (3) Check server performance. 
Original error: The operation has timed out
```

**Causes:**
- Model inference is too slow
- Insufficient server performance
- Network latency
- Large model usage

**Handling:**
```csharp
if (error.ErrorType == LLMErrorType.Timeout)
{
    Debug.LogError($"Timeout error: {error.Message}");
    
    ShowUserMessage(
        "Request timed out",
        $"Processing exceeded {config.HttpTimeoutSeconds} seconds.\n\n" +
        "Suggestions:\n" +
        "1. Use a smaller model\n" +
        "2. Increase timeout\n" +
        "3. Check server GPU/CPU performance"
    );
    
    // Auto-extend timeout (optional)
    if (config.HttpTimeoutSeconds < 180.0f)
    {
        config.HttpTimeoutSeconds *= 1.5f;
        Debug.Log($"Extended timeout to {config.HttpTimeoutSeconds} seconds");
    }
}
```

#### 5. InvalidResponse (response parse failure)

**Typical message:**
```
Failed to parse response from model 'mistral': Unexpected character. 
Check Ollama version compatibility.
```

**Causes:**
- Server version is outdated
- API spec changes
- Corrupted response
- Library/server compatibility issue

**Handling:**
```csharp
if (error.ErrorType == LLMErrorType.InvalidResponse)
{
    Debug.LogError($"Response parse error: {error.Message}");
    
    ShowUserMessage(
        "Failed to parse server response",
        "Check the Ollama server version.\n" +
        "Recommended version: v0.1.0 or later\n\n" +
        "If restarting doesn't help,\n" +
        "enable debug mode to inspect details."
    );
    
    // Enable debug mode
    if (!config.DebugMode)
    {
        config.DebugMode = true;
        Debug.Log("Debug mode enabled");
    }
}
```

#### 6. Cancelled (user cancellation)

**Typical message:**
```
Request cancelled for session 'chat-session-1' by user
```

**Causes:**
- User called `CancellationToken.Cancel()`
- Timeout-based cancellation elapsed

**Handling:**
```csharp
if (error.ErrorType == LLMErrorType.Cancelled)
{
    Debug.Log($"Cancelled: {error.Message}");
    
    // Restore UI state
    HideProgressBar();
    EnableInputField();
}
```

#### 7. Unknown (other errors)

**Typical message:**
```
Client is busy. Running sessions: 1/1, Pending requests: 2. 
Set WaitIfBusy=true to queue the request.
```

**Causes:**
- Too many requests (when `WaitIfBusy=false`)
- Unexpected exception

**Handling:**
```csharp
if (error.ErrorType == LLMErrorType.Unknown)
{
    Debug.LogError($"Error: {error.Message}");
    
    if (error.Message.Contains("busy"))
    {
        ShowUserMessage(
            "Client is busy",
            "Please wait for the previous request to finish.\n" +
            "Or set WaitIfBusy=true to queue automatically."
        );
    }
    else
    {
        ShowUserMessage(
            "Unexpected error",
            $"An error occurred: {error.Message}\n\n" +
            "Enable debug mode to inspect details."
        );
        
        if (error.Exception != null)
        {
            Debug.LogException(error.Exception);
        }
    }
}
```

### Comprehensive error handling example

```csharp
public class RobustChatManager : MonoBehaviour
{
    private OllamaClient _client;
    private OllamaConfig _config;

    void Start()
    {
        _config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            DebugMode = true
        };
        
        _client = LLMClientFactory.CreateOllamaClient(_config);
    }

    void SendMessageWithErrorHandling(string message)
    {
        var options = new ChatRequestOptions
        {
            SessionId = "session-1"
        };

        StartCoroutine(_client.SendMessageAsync(
            message,
            response => Debug.Log($"Assistant: {response.Content}"),
            error => HandleError(error),
            options
        ));
    }

    void HandleError(ChatError error)
    {
        // Record error logs (production)
        LogErrorToFile(error);

        switch (error.ErrorType)
        {
            case LLMErrorType.ConnectionFailed:
                ShowUserMessage(
                    "Unable to connect",
                    "Please check that the Ollama server is running."
                );
                break;

            case LLMErrorType.ServerError:
                ShowUserMessage(
                    "Server error occurred",
                    $"Please restart the server. (Error code: {error.HttpStatus})"
                );
                break;

            case LLMErrorType.ModelNotFound:
                string modelName = _config.DefaultModelName;
                ShowUserMessage(
                    "Model not found",
                    $"Model '{modelName}' is not installed.\n" +
                    "Please install it from settings."
                );
                ShowModelSelectionUI();
                break;

            case LLMErrorType.Timeout:
                ShowUserMessage(
                    "Request timed out",
                    "The server is taking too long. Try again or use a smaller model."
                );
                break;

            case LLMErrorType.InvalidResponse:
                ShowUserMessage(
                    "Unexpected error",
                    "There was a problem communicating with the server. Restart the app."
                );
                break;

            case LLMErrorType.Cancelled:
                Debug.Log("Request was cancelled");
                break;

            default:
                ShowUserMessage(
                    "An error occurred",
                    $"Details: {error.Message}"
                );
                break;
        }
    }

    void LogErrorToFile(ChatError error)
    {
        string logMessage = $"[{DateTime.Now}] {error.ErrorType}: {error.Message}";
        if (error.HttpStatus.HasValue)
        {
            logMessage += $" (HTTP {error.HttpStatus})";
        }
        
        Debug.Log(logMessage);
        // Record to a file in production
        // File.AppendAllText("error_log.txt", logMessage + "\n");
    }

    void ShowUserMessage(string title, string message)
    {
        Debug.LogWarning($"{title}: {message}");
        // Show a dialog in UI, etc.
    }

    void ShowModelSelectionUI()
    {
        Debug.Log("Show model selection UI");
    }
}
```
### Error checklist

| Error type | Check | Resolution |
|-----------|---------|---------|
| `ConnectionFailed` | Server running, URL, port | Start with `ollama serve`, verify URL |
| `ServerError` | Server logs, model status, resources | Restart server, check `ollama list` |
| `ModelNotFound` | Model name, installed models | Run `ollama pull <model-name>` |
| `Timeout` | Timeout setting, model size | Increase `HttpTimeoutSeconds`, use smaller model |
| `InvalidResponse` | Ollama version | Update to latest, check compatibility |
| `Cancelled` | Cancellation handling | Update UI appropriately |
| `Unknown` | DebugMode, logs | Inspect details with `DebugMode=true` |

### Debugging tips

**Enable detailed logs:**
```csharp
config.DebugMode = true;  // Detailed logs
```

**Inspect error details:**
```csharp
if (error != null)
{
    Debug.Log($"Error type: {error.ErrorType}");
    Debug.Log($"Message: {error.Message}");
    Debug.Log($"HTTP status: {error.HttpStatus}");
    
    if (error.Exception != null)
    {
        Debug.LogException(error.Exception);
    }
}
```

**Server status commands:**
```powershell
# Check if the server is running
netstat -an | findstr :11434

# List installed models
ollama list

# View server logs (server window)
```

## 10. Planned Extensions

TBD.

