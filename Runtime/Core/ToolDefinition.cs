using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// ツールの定義を表すクラス
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// ツール名（一意）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// ツール説明
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// JSON Schema 形式のパラメータ定義
        /// </summary>
        public JObject InputSchema { get; set; }

        /// <summary>
        /// ユーザー定義のコールバック関数
        /// </summary>
        public Delegate Callback { get; set; }

        /// <summary>
        /// パラメータの型情報（型変換用）
        /// </summary>
        public List<ParameterInfo> ParameterInfos { get; set; }

        /// <summary>
        /// 戻り値の型情報（文字列変換用）
        /// </summary>
        public Type ReturnType { get; set; }

        /// <summary>
        /// Ollama API 形式に変換
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
