using System;
using System.Collections.Generic;
using System.Threading;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// チャットリクエストのオプション
    /// </summary>
    public class ChatRequestOptions
    {
        /// <summary>
        /// チャットセッションID（同じセッション内で履歴を共有）
        /// </summary>
        public string SessionId { get; set; }

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

        /// <summary>
        /// キャンセルトークン（CancellationTokenSource.Token を指定）
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        /// このリクエストで使用するツール一覧（null の場合は登録済みツール全てを使用）
        /// </summary>
        public List<ToolDefinition> Tools { get; set; }

        /// <summary>
        /// ツール呼び出しの最大反復回数（デフォルト: 5）
        /// 無限ループを防ぐために、ツールが繰り返し呼ばれる回数を制限
        /// </summary>
        public int MaxToolIterations { get; set; } = 5;
    }
}
