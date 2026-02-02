using System;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// LLM エラーの種類
    /// </summary>
    public enum LLMErrorType
    {
        /// <summary>サーバへの接続に失敗</summary>
        ConnectionFailed,

        /// <summary>サーバがエラーを返した（500, 503など）</summary>
        ServerError,

        /// <summary>レスポンスのパースに失敗</summary>
        InvalidResponse,

        /// <summary>リクエストがタイムアウト</summary>
        Timeout,

        /// <summary>指定されたモデルが見つからない</summary>
        ModelNotFound,

        /// <summary>ユーザーによるキャンセル</summary>
        Cancelled,

        /// <summary>その他のエラー</summary>
        Unknown
    }

    /// <summary>
    /// LLM との通信中に発生したエラー情報
    /// </summary>
    public class ChatError
    {
        /// <summary>
        /// エラーの種類
        /// </summary>
        public LLMErrorType ErrorType { get; set; }

        /// <summary>
        /// エラーメッセージ
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 例外情報（あれば）
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// HTTP ステータスコード（あれば）
        /// </summary>
        public int? HttpStatus { get; set; }

        public override string ToString()
        {
            return $"[{ErrorType}] {Message}" +
                   (HttpStatus.HasValue ? $" (HTTP {HttpStatus})" : "");
        }
    }
}
