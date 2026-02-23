using System.Collections.Generic;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Chat message (user, assistant, system)
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Message role
        /// "user" = User message
        /// "assistant" = LLM response
        /// "system" = System prompt
        /// "tool" = Tool execution result
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Message content
        /// </summary>
        public string Content { get; set; }

        public List<string> Images { get; set; }

        /// <summary>
        /// List of tool call information (when assistant calls tools)
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; }
    }
}
