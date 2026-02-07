using System;
using System.Collections.Generic;
using EasyLocalLLM.LLM.Core;

namespace EasyLocalLLM.LLM.Manager
{
    /// <summary>
    /// チャットセッション（会話履歴とメタデータ）
    /// </summary>
    public class ChatSession
    {
        /// <summary>セッションID</summary>
        public string Id { get; set; }

        /// <summary>このセッション専用のシステムプロンプト</summary>
        public string SystemPrompt { get; set; }

        /// <summary>メッセージ履歴</summary>
        public List<ChatMessage> History { get; set; } = new();

        /// <summary>セッション作成日時</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>最終更新日時</summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

        /// <summary>メッセージ履歴の最大保持数（0 = 無制限）</summary>
        public int MaxHistorySize { get; set; } = 50;
    }

    /// <summary>
    /// チャット履歴を管理するクラス
    /// セッションごとに履歴を保持し、リセットや削除が可能
    /// </summary>
    public class ChatHistoryManager
    {
        private readonly Dictionary<string, ChatSession> _sessions = new();

        /// <summary>
        /// セッションを取得または作成
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
        /// セッションのメッセージ履歴を取得
        /// </summary>
        public List<ChatMessage> GetHistory(string sessionId)
        {
            var session = GetOrCreateSession(sessionId);
            return session.History;
        }

        /// <summary>
        /// メッセージを履歴に追加
        /// </summary>
        public void AddMessage(string sessionId, ChatMessage message, int? maxHistory = null)
        {
            var session = GetOrCreateSession(sessionId);
            session.History.Add(message);
            session.LastUpdatedAt = DateTime.Now;

            // 最大履歴数を超えた場合、古いメッセージから削除
            int limit = maxHistory ?? session.MaxHistorySize;
            if (limit > 0 && session.History.Count > limit)
            {
                // 最初のメッセージがシステムプロンプト（Role = "system"）の場合は保護
                int startIndex = (session.History.Count > 0 && session.History[0].Role == "system") ? 1 : 0;
                session.History.RemoveRange(startIndex, session.History.Count - limit);
            }
        }

        /// <summary>
        /// 特定のセッションの履歴をクリア
        /// </summary>
        public void Clear(string sessionId)
        {
            _sessions.Remove(sessionId);
        }

        /// <summary>
        /// すべてのセッションをクリア
        /// </summary>
        public void ClearAll()
        {
            _sessions.Clear();
        }

        /// <summary>
        /// セッションを削除
        /// </summary>
        public bool RemoveSession(string sessionId)
        {
            return _sessions.Remove(sessionId);
        }

        /// <summary>
        /// セッションが存在するか確認
        /// </summary>
        public bool HasSession(string sessionId)
        {
            return _sessions.ContainsKey(sessionId);
        }

        /// <summary>
        /// 全セッションIDを取得
        /// </summary>
        public IEnumerable<string> GetAllSessionIds()
        {
            return _sessions.Keys;
        }

        /// <summary>
        /// セッション数を取得
        /// </summary>
        public int SessionCount => _sessions.Count;
    }
}
