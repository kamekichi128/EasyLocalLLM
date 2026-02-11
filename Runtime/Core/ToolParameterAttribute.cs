using System;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Attribute for adding description to tool parameters
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class ToolParameterAttribute : Attribute
    {
        /// <summary>
        /// Parameter description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Initialize ToolParameterAttribute
        /// </summary>
        /// <param name="description">Parameter description</param>
        public ToolParameterAttribute(string description)
        {
            Description = description;
        }
    }
}
