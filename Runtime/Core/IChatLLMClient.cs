using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// LLM chat client interface
    /// </summary>
    public interface IChatLLMClient
    {
        /// <summary>
        /// Global system prompt
        /// </summary>
        string GlobalSystemPrompt { get; set; }

        /// <summary>
        /// Clear all message history
        /// </summary>
        void ClearAllMessages();

        /// <summary>
        /// Clear message history for the specified sessionId
        /// </summary>
        void ClearMessages(string sessionId);

        /// <summary>
        /// Send message asynchronously (IEnumerator based)
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="onResponse">Successed callback</param>
        /// <param name="onError">Error callback</param>
        /// <param name="options">Request options</param>
        IEnumerator SendMessageAsync(
            string message,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null);


        /// <summary>
        /// Send message with Task (get complete answer)
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="options">Request options</param>
        /// <param name="cancellationToken">External cancellation token</param>
        Task<ChatResponse> SendMessageTaskAsync(
            string message,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Send message with streaming (IEnumerator based)
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="onResponse">Response: (response) - completion determined by response.IsFinal</param>
        /// <param name="onError">Error callback</param>
        /// <param name="options">Request options</param>
        IEnumerator SendMessageStreamingAsync(
            string message,
            Action<ChatResponse> onResponse,
            Action<ChatError> onError = null,
            ChatRequestOptions options = null);

        /// <summary>
        /// Send message with streaming (Task version)
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="onProgress">Progress during streaming reception</param>
        /// <param name="options">Request options</param>
        /// <param name="cancellationToken">External cancellation token</param>
        Task<ChatResponse> SendMessageStreamingTaskAsync(
            string message,
            IProgress<ChatResponse> onProgress,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Save session history to file
        /// </summary>
        /// <param name="filePath">Save destination file path</param>
        /// <param name="sessionId">Session ID</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        void SaveSession(string filePath, string sessionId, string encryptionKey = null);

        /// <summary>
        /// Restore session history from file
        /// </summary>
        /// <param name="filePath">File path to load</param>
        /// <param name="sessionId">Session ID</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        void LoadSession(string filePath, string sessionId, string encryptionKey = null);

        /// <summary>
        /// Save all session history to directory
        /// </summary>
        /// <param name="dirPath">Save destination directory path</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        void SaveAllSessions(string dirPath, string encryptionKey = null);

        /// <summary>
        /// Load all session history from directory
        /// </summary>
        /// <param name="dirPath">Directory path to load from</param>
        /// <param name="encryptionKey">Encryption key (optional)</param>
        void LoadAllSessions(string dirPath, string encryptionKey = null);

        /// <summary>
        /// Set system prompt for session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="systemPrompt">System prompt to set</param>
        void SetSessionSystemPrompt(string sessionId, string systemPrompt);

        /// <summary>
        /// Get system prompt for session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <returns>System prompt, or null if session does not exist</returns>
        string GetSessionSystemPrompt(string sessionId);

        /// <summary>
        /// Reset session system prompt (use global prompt)
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        void ResetSessionSystemPrompt(string sessionId);

        /// <summary>
        /// Batch set system prompt for multiple sessions
        /// </summary>
        /// <param name="sessionIds">List of session IDs</param>
        /// <param name="systemPrompt">System prompt to set</param>
        void SetSystemPromptForMultipleSessions(System.Collections.Generic.IEnumerable<string> sessionIds, string systemPrompt);

        /// <summary>
        /// Reset system prompt for all sessions
        /// </summary>
        void ResetAllSessionSystemPrompts();

        /// <summary>
        /// Reset session system prompt and history
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        void ClearSessionWithPrompt(string sessionId);

        /// <summary>
        /// Register tool (auto schema generation)
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <param name="description">Tool description</param>
        /// <param name="callback">Callback function (any signature)</param>
        void RegisterTool(string name, string description, System.Delegate callback);

        /// <summary>
        /// Register tool (manual schema specification)
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <param name="description">Tool description</param>
        /// <param name="inputSchema">JSON Schema</param>
        /// <param name="callback">Callback function</param>
        void RegisterTool(string name, string description, object inputSchema, System.Delegate callback);

        /// <summary>
        /// Unregister tool
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>true if unregistration was successful</returns>
        bool UnregisterTool(string name);

        /// <summary>
        /// Remove all tools
        /// </summary>
        void RemoveAllTools();

        /// <summary>
        /// Get registered tools list
        /// </summary>
        /// <returns>List of tool definitions</returns>
        System.Collections.Generic.List<ToolDefinition> GetRegisteredTools();

        /// <summary>
        /// Check if tool is registered
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>true if registered</returns>
        bool HasTool(string name);
    }
}
