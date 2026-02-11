using System;
using System.Collections.Generic;
using EasyLocalLLM.LLM.Core;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// Chat session (conversation history and metadata)
    /// </summary>
    public class ChatSession
    {
        /// <summary>Session ID</summary>
        public string Id { get; set; }

        /// <summary>System prompt for this session</summary>
        public string SystemPrompt { get; set; }

        /// <summary>Message history</summary>
        public List<ChatMessage> History { get; set; } = new();

        /// <summary>Session creation date time</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Last update date time</summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

        /// <summary>Maximum number of messages to keep in history (0 = unlimited)</summary>
        public int MaxHistorySize { get; set; } = 50;
    }

    /// <summary>
    /// Class for managing chat history
    /// Maintains history per session, supports reset and deletion
    /// </summary>
    public class ChatHistoryManager
    {
        private readonly Dictionary<string, ChatSession> _sessions = new();

        /// <summary>
        /// Get or create session
        /// </summary>
        public ChatSession GetOrCreateSession(string sessionId, string systemPrompt = null)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                session = new ChatSession
                {
                    Id = sessionId,
                    SystemPrompt = systemPrompt
                };
                _sessions[sessionId] = session;
            }
            else if (!string.IsNullOrEmpty(systemPrompt))
            {
                session.SystemPrompt = systemPrompt;
            }
            return session;
        }

        /// <summary>
        /// Get message history for session
        /// </summary>
        public List<ChatMessage> GetHistory(string sessionId)
        {
            var session = GetOrCreateSession(sessionId);
            return session.History;
        }

        /// <summary>
        /// Add message to history
        /// </summary>
        public void AddMessage(string sessionId, ChatMessage message, int? maxHistory = null)
        {
            var session = GetOrCreateSession(sessionId);
            session.History.Add(message);
            session.LastUpdatedAt = DateTime.Now;

            // Remove old messages if max history exceeded
            int limit = maxHistory ?? session.MaxHistorySize;
            if (limit > 0 && session.History.Count > limit)
            {
                // If first message is system prompt (Role = "system"), protect it
                int startIndex = (session.History.Count > 0 && session.History[0].Role == "system") ? 1 : 0;
                session.History.RemoveRange(startIndex, session.History.Count - limit);
            }
        }

        /// <summary>
        /// Clear history for specific session
        /// </summary>
        public void Clear(string sessionId)
        {
            _sessions.Remove(sessionId);
        }

        /// <summary>
        /// Clear all sessions
        /// </summary>
        public void ClearAll()
        {
            _sessions.Clear();
        }

        /// <summary>
        /// Remove session
        /// </summary>
        public bool RemoveSession(string sessionId)
        {
            return _sessions.Remove(sessionId);
        }

        /// <summary>
        /// Check if session exists
        /// </summary>
        public bool HasSession(string sessionId)
        {
            return _sessions.ContainsKey(sessionId);
        }

        /// <summary>
        /// Get all session IDs
        /// </summary>
        public IEnumerable<string> GetAllSessionIds()
        {
            return _sessions.Keys;
        }

        /// <summary>
        /// Get session count
        /// </summary>
        public int SessionCount => _sessions.Count;
    }
}
