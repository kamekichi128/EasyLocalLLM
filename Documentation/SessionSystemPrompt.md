# Session System Prompt Commands

## Overview

EasyLocalLLM now includes a command set for configuring and managing session-specific system prompts.

This lets you use a global system prompt while applying a different prompt for specific sessions.

---

## Added Methods

### 1. `SetSessionSystemPrompt(string sessionId, string systemPrompt)`

Sets the system prompt for a specific session.

**Parameters**:
- `sessionId`: Session ID
- `systemPrompt`: The system prompt to set

**Example**:
```csharp
var client = LLMClientFactory.CreateOllamaClient(config);

// Programming prompt for session A
client.SetSessionSystemPrompt("session-a", 
    "You are a programming expert. Help with code-related questions.");

// Translation prompt for session B
client.SetSessionSystemPrompt("session-b", 
    "You are a translation expert. Translate accurately between languages.");
```

---

### 2. `GetSessionSystemPrompt(string sessionId)`

Gets the system prompt for a specific session.

**Returns**: The system prompt, or `null` if the session does not exist.

**Example**:
```csharp
string prompt = client.GetSessionSystemPrompt("session-a");
Debug.Log($"Session A prompt: {prompt}");
```

---

### 3. `ResetSessionSystemPrompt(string sessionId)`

Resets the system prompt for a specific session.
After reset, the global system prompt is used.

**Example**:
```csharp
// Reset session A prompt to the global setting
client.ResetSessionSystemPrompt("session-a");
```

---

### 4. `SetSystemPromptForMultipleSessions(IEnumerable<string> sessionIds, string systemPrompt)`

Sets the same system prompt for multiple sessions at once.

**Parameters**:
- `sessionIds`: List of session IDs
- `systemPrompt`: The system prompt to set

**Example**:
```csharp
// Apply the same prompt to multiple sessions
var sessionIds = new[] { "session-1", "session-2", "session-3" };
client.SetSystemPromptForMultipleSessions(sessionIds, 
    "You are a helpful customer service assistant.");
```

---

### 5. `ResetAllSessionSystemPrompts()`

Resets the system prompts for all sessions.

**Example**:
```csharp
// Reset all sessions to the global prompt
client.ResetAllSessionSystemPrompts();
```

---

### 6. `ClearSessionWithPrompt(string sessionId)`

Clears both the session history and the session prompt.

**Example**:
```csharp
// Fully reset session A
client.ClearSessionWithPrompt("session-a");
```

---

## Usage Patterns

### Pattern 1: Separate sessions for different roles

```csharp
// Translation session
client.SetSessionSystemPrompt("translator", 
    "You are a professional translator. Translate accurately and naturally.");

// Programming help session
client.SetSessionSystemPrompt("programmer", 
    "You are a senior software engineer. Provide code examples and explanations.");

// Teaching session
client.SetSessionSystemPrompt("teacher", 
    "You are an educational tutor. Explain concepts clearly with examples.");

// Independent conversation per session
StartCoroutine(client.SendMessageAsync(
    "Translate 'Hello' to Japanese",
    response => Debug.Log(response.Content),
    error => { },
    new ChatRequestOptions { SessionId = "translator" }
));
```

### Pattern 2: Language-specific sessions

```csharp
// Japanese session
client.SetSessionSystemPrompt("ja", 
    "You are a helpful assistant. Always respond in Japanese.");

// English session
client.SetSessionSystemPrompt("en", 
    "You are a helpful assistant. Always respond in English.");

// French session
client.SetSessionSystemPrompt("fr", 
    "You are a helpful assistant. Always respond in French.");
```

### Pattern 3: Project-specific prompts

```csharp
// Project A session
client.SetSessionSystemPrompt("project-a", 
    "Context: You are helping with an educational game project. " +
    "Focus on interactive and engaging explanations.");

// Project B session
client.SetSessionSystemPrompt("project-b", 
    "Context: You are helping with a business analytics project. " +
    "Focus on data-driven insights and technical accuracy.");
```

---

## Implementation Details

### Hierarchical Prompt Management

1. **Global level**: `GlobalSystemPrompt` property
   - Default for all sessions
   
2. **Session level**: `SetSessionSystemPrompt()` method
   - Specific to a session
   - Overrides the global prompt

3. **Request level**: `ChatRequestOptions.SystemPrompt`
   - Applies to a single request
   - Overrides the session prompt

**Priority order**:
```
Request level > Session level > Global level
```

### DebugMode Support

If `OllamaConfig.DebugMode = true`, the following actions are logged:

```csharp
[Ollama] Session 'session-a' system prompt updated: You are a programming expert...
[Ollama] Session 'session-a' system prompt reset to global
[Ollama] System prompt set for 3 sessions
[Ollama] Session 'session-a' cleared with prompt reset
```

---

## Unit Tests

Five tests were added to `NonStreamingTests.cs`:

| Test | Description |
|--------|------|
| Test_SessionSystemPrompt_SetAndRetrieve | Set and retrieve prompt |
| Test_SessionSystemPrompt_Reset | Reset prompt |
| Test_SetSystemPromptForMultipleSessions | Bulk set prompts |
| Test_ResetAllSessionSystemPrompts | Reset all prompts |
| Test_ClearSessionWithPrompt | Clear history and prompt |

**How to run**:
```
Unity Test Runner -> PlayMode -> Run NonStreamingTests
```

---

## API Compatibility

All new methods are defined in `IChatLLMClient`:

- Implemented in `OllamaClient`
- Implemented in `MockChatLLMClient` (for testing)
- Supported by future client implementations

---

## Best Practices

### 1. Session ID naming

Use meaningful session IDs to improve readability:

```csharp
// Good
client.SetSessionSystemPrompt("translator-en-ja", "...");
client.SetSessionSystemPrompt("user-support-session-123", "...");

// Avoid
client.SetSessionSystemPrompt("session1", "...");
client.SetSessionSystemPrompt("temp", "...");
```

### 2. Set once per session

Set session prompts when the session is created:

```csharp
// Recommended
var options = new ChatRequestOptions 
{ 
    SessionId = "programmer",
    SystemPrompt = "You are a programming expert."
};
// Or
client.SetSessionSystemPrompt("programmer", "You are a programming expert.");

// Do not specify SystemPrompt on subsequent messages
StartCoroutine(client.SendMessageAsync("Write a function", callback, 
    new ChatRequestOptions { SessionId = "programmer" }));
```

### 3. Clear unused sessions

Clear sessions to reduce memory usage:

```csharp
// On session end
client.ClearSessionWithPrompt("session-id");

// Or individually
client.ClearMessages("session-id");
client.ResetSessionSystemPrompt("session-id");
```

---

## Summary

Session system prompt commands allow you to:

- Manage multiple roles within a single client
- Build language-specific sessions easily
- Apply project-specific context per session
- Maintain hierarchical prompt control
- Validate behavior with tests