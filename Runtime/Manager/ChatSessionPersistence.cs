using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// セッション履歴の永続化ユーティリティ
    /// </summary>
    internal static class ChatSessionPersistence
    {
        /// <summary>
        /// セッションをファイルに保存
        /// </summary>
        public static void SaveSession(string filePath, ChatSession session, string encryptionKey = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            try
            {
                // セッションをJSON化
                var sessionData = new
                {
                    session.SessionId,
                    session.SystemPrompt,
                    session.CreatedAt,
                    session.LastUpdatedAt,
                    History = session.History
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
        /// ファイルからセッションを復元
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

                // 暗号化されていれば復号化
                if (!string.IsNullOrEmpty(encryptionKey))
                {
                    json = ChatEncryption.Decrypt(json, encryptionKey);
                }

                var sessionData = JObject.Parse(json);

                var session = new ChatSession(
                    sessionData["SessionId"]?.ToString() ?? "",
                    sessionData["SystemPrompt"]?.ToString()
                );

                // 日時情報を復元
                if (DateTime.TryParse(sessionData["CreatedAt"]?.ToString(), out var createdAt))
                {
                    session.CreatedAt = createdAt;
                }

                if (DateTime.TryParse(sessionData["LastUpdatedAt"]?.ToString(), out var lastUpdatedAt))
                {
                    session.LastUpdatedAt = lastUpdatedAt;
                }

                // メッセージ履歴を復元
                var historyArray = sessionData["History"] as JArray;
                if (historyArray != null)
                {
                    foreach (var msgToken in historyArray)
                    {
                        var message = new ChatMessage
                        {
                            Role = msgToken["Role"]?.ToString() ?? "",
                            Content = msgToken["Content"]?.ToString() ?? ""
                        };
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
        /// すべてのセッションをファイルに保存
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
        /// ディレクトリからすべてのセッションを復元
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
                        historyManager.Clear(session.SessionId);
                        foreach (var message in session.History)
                        {
                            historyManager.AddMessage(session.SessionId, message, null);
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
        /// セッションIDをファイル名として安全な形式に変換
        /// </summary>
        private static string SanitizeFileName(string sessionId)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = System.Text.RegularExpressions.Regex.Replace(sessionId, "[" + System.Text.RegularExpressions.Regex.Escape(new string(invalidChars)) + "]", "_");
            return sanitized;
        }
    }
}
