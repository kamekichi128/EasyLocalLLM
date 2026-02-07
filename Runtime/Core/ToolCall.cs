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
        /// Tool call の一意ID（Ollama レスポンス内に含まれる）
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// JSON 形式のツール入力パラメータ
        /// </summary>
        public string Arguments { get; set; }

        public override string ToString()
        {
            return $"ToolCall: {ToolName} (ID: {ToolCallId})";
        }
    }
}
