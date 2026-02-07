namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// LLM からの応答
    /// </summary>
    public class ChatResponse
    {
        /// <summary>
        /// セッションのID
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 応答内容（ストリーミング時は部分応答、IsFinal=true で完全応答）
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// メッセージのロール（通常は "assistant"）
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 最終的な応答か（ストリーミング完了時に true）
        /// </summary>
        public bool IsFinal { get; set; }

        /// <summary>
        /// トークン使用数（あれば）
        /// </summary>
        public int? TokenCount { get; set; }

        /// <summary>
        /// 生のレスポンス JSON（デバッグ用）
        /// </summary>
        public object RawResponse { get; set; }

        public override string ToString()
        {
            return $"[{Role}] {SessionId} > {Content}" +
                   (IsFinal ? " (Final)" : "") +
                   (TokenCount.HasValue ? $" ({TokenCount} tokens)" : "");
        }
    }
}
