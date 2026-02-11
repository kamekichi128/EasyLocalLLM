using EasyLocalLLM.LLM.Core;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using UnityEngine;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// Chat session persistence utility
    /// </summary>
    internal static class ChatSessionPersistence
    {
        /// <summary>
        /// Save session to file
        /// <param name="filePath">File path to save the session</param>
        /// <param name="session">Chat session to save</param>
        /// <param name="encryptionKey">Optional encryption key. If provided, the session will be encrypted before saving.</param>
        /// </summary>
        public static void SaveSession(string filePath, ChatSession session, string encryptionKey = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            try
            {
                // Convert session to JSON
                var sessionData = new
                {
                    session.Id,
                    session.SystemPrompt,
                    session.CreatedAt,
                    session.LastUpdatedAt,
                    session.History
                };

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(
                    sessionData,
                    Newtonsoft.Json.Formatting.Indented
                );

                // 暗号化が指定されていればする
                if (!string.IsNullOrEmpty(encryptionKey))
                {
                    json = ChatEncryption.Encrypt(json, encryptionKey);
                }

                // ディレクトリが存在しなければ作成
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, json);

                if (Application.isEditor)
                {
                    Debug.Log($"[ChatSessionPersistence] Session saved to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatSessionPersistence] Failed to save session: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load session from file
        /// <param name="filePath">File path to load the session from</param>
        /// <param name="encryptionKey">Optional encryption key. If the session is encrypted, this key will be used to decrypt it.</param>
        /// </summary>
        public static ChatSession LoadSession(string filePath, string encryptionKey = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Session file not found: {filePath}");

            try
            {
                string json = File.ReadAllText(filePath);

                // Decrypt if encrypted
                if (!string.IsNullOrEmpty(encryptionKey))
                {
                    json = ChatEncryption.Decrypt(json, encryptionKey);
                }

                var sessionData = JObject.Parse(json);

                var session = new ChatSession();
                session.Id = sessionData["Id"]?.ToString() ?? "";
                session.SystemPrompt = sessionData["SystemPrompt"]?.ToString();

                // Restore date information
                if (DateTime.TryParse(sessionData["CreatedAt"]?.ToString(), out var createdAt))
                {
                    session.CreatedAt = createdAt;
                }

                if (DateTime.TryParse(sessionData["LastUpdatedAt"]?.ToString(), out var lastUpdatedAt))
                {
                    session.LastUpdatedAt = lastUpdatedAt;
                }

                // Restore message history
                var historyArray = sessionData["History"] as JArray;
                if (historyArray != null)
                {
                    foreach (var msgToken in historyArray)
                    {
                        var message = new ChatMessage();
                        message.Role = msgToken["Role"]?.ToString() ?? "";
                        message.Content = msgToken["Content"]?.ToString() ?? "";
                        message.ToolCalls = msgToken["ToolCalls"]?.ToObject<System.Collections.Generic.List<ToolCall>>() ?? null;
                        session.History.Add(message);
                    }
                }

                if (Application.isEditor)
                {
                    Debug.Log($"[ChatSessionPersistence] Session loaded from: {filePath}");
                }

                return session;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatSessionPersistence] Failed to load session: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Save all sessions to directory
        /// <param name="dirPath">Directory path to save sessions</param>
        /// <param name="historyManager">ChatHistoryManager instance containing sessions to save</param
        /// <param name="encryptionKey">Optional encryption key. If provided, sessions will be encrypted before saving.</param>
        /// </summary>
        public static void SaveAllSessions(string dirPath, ChatHistoryManager historyManager, string encryptionKey = null)
        {
            if (string.IsNullOrEmpty(dirPath))
                throw new ArgumentException("Directory path cannot be empty", nameof(dirPath));

            if (historyManager == null)
                throw new ArgumentNullException(nameof(historyManager));

            try
            {
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                var sessionIds = historyManager.GetAllSessionIds();
                foreach (var sessionId in sessionIds)
                {
                    var session = historyManager.GetOrCreateSession(sessionId);
                    string filePath = Path.Combine(dirPath, $"{SanitizeFileName(sessionId)}.json");
                    SaveSession(filePath, session, encryptionKey);
                }

                if (Application.isEditor)
                {
                    Debug.Log($"[ChatSessionPersistence] All sessions saved to: {dirPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatSessionPersistence] Failed to save all sessions: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load all sessions from Directory
        /// <param name="dirPath">Directory path to load sessions from</param>
        /// <param name="historyManager">ChatHistoryManager instance to load sessions into</param>
        /// <param name="encryptionKey">Optional encryption key. If the sessions are encrypted, this key will be used to decrypt them.</param>
        /// </summary>
        public static void LoadAllSessions(string dirPath, ChatHistoryManager historyManager, string encryptionKey = null)
        {
            if (string.IsNullOrEmpty(dirPath))
                throw new ArgumentException("Directory path cannot be empty", nameof(dirPath));

            if (historyManager == null)
                throw new ArgumentNullException(nameof(historyManager));

            if (!Directory.Exists(dirPath))
                throw new DirectoryNotFoundException($"Directory not found: {dirPath}");

            try
            {
                var jsonFiles = Directory.GetFiles(dirPath, "*.json");
                foreach (var filePath in jsonFiles)
                {
                    try
                    {
                        var session = LoadSession(filePath, encryptionKey);
                        historyManager.Clear(session.Id);
                        foreach (var message in session.History)
                        {
                            historyManager.AddMessage(session.Id, message, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ChatSessionPersistence] Failed to load session from {filePath}: {ex.Message}");
                    }
                }

                if (Application.isEditor)
                {
                    Debug.Log($"[ChatSessionPersistence] All sessions loaded from: {dirPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatSessionPersistence] Failed to load all sessions: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sanitize file name by removing invalid characters
        /// </summary>
        private static string SanitizeFileName(string sessionId)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = System.Text.RegularExpressions.Regex.Replace(sessionId, "[" + System.Text.RegularExpressions.Regex.Escape(new string(invalidChars)) + "]", "_");
            return sanitized;
        }
    }
}
