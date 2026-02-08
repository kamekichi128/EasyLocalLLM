using System.Collections.Generic;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// チャットメッセージ（ユーザー、アシスタント、システム）
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// メッセージのロール
        /// "user" = ユーザーメッセージ
        /// "assistant" = LLM の応答
        /// "system" = システムプロンプト
        /// "tool" = ツール実行結果
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// メッセージの内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// ツール呼び出し情報のリスト（assistant がツールを呼び出す場合）
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; }
    }
}
