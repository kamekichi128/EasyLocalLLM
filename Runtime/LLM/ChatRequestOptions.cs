namespace EasyLocalLLM.LLM
{
    /// <summary>
    /// チャットリクエストのオプション
    /// </summary>
    public class ChatRequestOptions
    {
        /// <summary>
        /// チャットセッションID（同じセッション内で履歴を共有）
        /// </summary>
        public string ChatId { get; set; }

        /// <summary>
        /// 使用するモデル名（null の場合はデフォルト）
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// 回答の多様性（0.0 = 確定的、1.0+ = より多様）
        /// </summary>
        public float Temperature { get; set; } = 0.7f;

        /// <summary>
        /// ランダムシード（-1 = ランダム）
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// リクエストの優先度（高い値ほど優先）
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// クライアントがビジー中の場合、終了するまで待機するか
        /// </summary>
        public bool WaitIfBusy { get; set; } = false;

        /// <summary>
        /// このリクエスト用のシステムプロンプト（null の場合はグローバル設定）
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// メッセージ履歴の最大保持数（0 = 無制限）
        /// </summary>
        public int? MaxHistory { get; set; }
    }
}
