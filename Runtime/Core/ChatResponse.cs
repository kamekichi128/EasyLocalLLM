using System.Collections.Generic;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Response from LLM
    /// </summary>
    public class ChatResponse
    {
        /// <summary>
        /// Session ID
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Response content (partial response when streaming, complete response when IsFinal=true)
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Message role (usually "assistant")
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Whether this is the final response (true when streaming completes)
        /// </summary>
        public bool IsFinal { get; set; }

        /// <summary>
        /// Token count (if available)
        /// </summary>
        public int? TokenCount { get; set; }

        /// <summary>
        /// Raw response JSON (for debugging)
        /// </summary>
        public object RawResponse { get; set; }

        /// <summary>
        /// List of tool calls requested by LLM
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; }

        public override string ToString()
        {
            return $"[{Role}] {SessionId} > {Content}" +
                   (IsFinal ? " (Final)" : "") +
                   (TokenCount.HasValue ? $" ({TokenCount} tokens)" : "") +
                   (ToolCalls != null && ToolCalls.Count > 0 ? $" ({ToolCalls.Count} tool calls)" : "");
        }
    }
}
