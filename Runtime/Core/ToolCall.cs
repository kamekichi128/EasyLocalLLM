using Newtonsoft.Json.Linq;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Class representing tool call information when LLM requests tool execution
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// Tool name
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// Tool input parameters in JSON format
        /// </summary>
        public JToken Arguments { get; set; }

        public override string ToString()
        {
            return $"ToolCall: {ToolName}";
        }
    }
}
