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
        /// レスポンスフォーマットの定数値
        /// </summary>
        public static class FormatConstants
        {
            /// <summary>
            /// JSON形式でレスポンスを返す
            /// </summary>
            public const string Json = "json";
        }

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
        public List<string> Tools { get; set; }

        /// <summary>
        /// ツール呼び出しの最大反復回数（デフォルト: 5）
        /// 無限ループを防ぐために、ツールが繰り返し呼ばれる回数を制限
        /// </summary>
        public int MaxToolIterations { get; set; } = 5;

        /// <summary>
        /// レスポンスのフォーマット指定
        /// FormatSchemaが指定されている場合は無視される
        /// 使用例: Format = ChatRequestOptions.FormatConstants.Json
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// レスポンスのJSONスキーマ
        /// 指定された場合、LLMはこのスキーマに従ったJSONを生成する
        /// </summary>
        public object FormatSchema { get; set; }
    }
}
