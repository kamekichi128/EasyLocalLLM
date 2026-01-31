using System;
using System.Collections;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// LLM チャットクライアントのインターフェース
    /// </summary>
    public interface IChatLLMClient
    {
        /// <summary>
        /// グローバルなシステムプロンプト
        /// </summary>
        string GlobalSystemPrompt { get; set; }

        /// <summary>
        /// すべてのメッセージ履歴をクリア
        /// </summary>
        void ClearAllMessages();

        /// <summary>
        /// 指定された chatId のメッセージ履歴をクリア
        /// </summary>
        void ClearMessages(string chatId);

        /// <summary>
        /// メッセージを非同期で送信（IEnumerator ベース）
        /// </summary>
        /// <param name="message">送信するメッセージ</param>
        /// <param name="callback">応答: (response, error) - response.IsFinal で完了判定</param>
        /// <param name="options">リクエストオプション</param>
        IEnumerator SendMessageAsync(
            string message,
            Action<ChatResponse, ChatError> callback,
            ChatRequestOptions options = null);

        /// <summary>
        /// メッセージをストリーミングで送信（IEnumerator ベース）
        /// </summary>
        /// <param name="message">送信するメッセージ</param>
        /// <param name="callback">応答: (response, error) - response.IsFinal で完了判定</param>
        /// <param name="options">リクエストオプション</param>
        IEnumerator SendMessageStreamingAsync(
            string message,
            Action<ChatResponse, ChatError> callback,
            ChatRequestOptions options = null);
    }
}
