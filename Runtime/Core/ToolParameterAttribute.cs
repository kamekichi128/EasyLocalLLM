using System;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// ツールのパラメータに説明を付与するための Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class ToolParameterAttribute : Attribute
    {
        /// <summary>
        /// パラメータの説明
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// ToolParameterAttribute を初期化
        /// </summary>
        /// <param name="description">パラメータの説明</param>
        public ToolParameterAttribute(string description)
        {
            Description = description;
        }
    }
}
