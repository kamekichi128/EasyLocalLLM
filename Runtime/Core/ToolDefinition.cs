using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Class representing tool definition
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// Tool name (unique)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Tool description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Parameter definition in JSON Schema format
        /// </summary>
        public JObject InputSchema { get; set; }

        /// <summary>
        /// User-defined callback function
        /// </summary>
        public Delegate Callback { get; set; }

        /// <summary>
        /// Parameter type information (for type conversion)
        /// </summary>
        public List<ParameterInfo> ParameterInfos { get; set; }

        /// <summary>
        /// Return type information (for string conversion)
        /// </summary>
        public Type ReturnType { get; set; }

        /// <summary>
        /// Convert to Ollama API format
        /// </summary>
        public JObject ToOllamaFormat()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = Name,
                    ["description"] = Description,
                    ["parameters"] = InputSchema
                }
            };
        }

        public override string ToString()
        {
            return $"Tool: {Name} - {Description}";
        }
    }
}
