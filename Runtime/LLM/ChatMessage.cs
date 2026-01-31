namespace EasyLocalLLM.LLM
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
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// メッセージの内容
        /// </summary>
        public string Content { get; set; }

        public override string ToString()
        {
            return $"[{Role}] {Content}";
        }
    }
}
