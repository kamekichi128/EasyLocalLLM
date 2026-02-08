using Newtonsoft.Json.Linq;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// LLM がツール呼び出しを要求した際の情報を表すクラス
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// ツール名
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// JSON 形式のツール入力パラメータ
        /// </summary>
        public JToken Arguments { get; set; }

        public override string ToString()
        {
            return $"ToolCall: {ToolName}";
        }
    }
}
