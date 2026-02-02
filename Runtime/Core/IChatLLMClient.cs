using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

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
        /// メッセージを Task で送信（完全回答を取得）
        /// </summary>
        /// <param name="message">送信するメッセージ</param>
        /// <param name="options">リクエストオプション</param>
        /// <param name="cancellationToken">外部キャンセルトークン</param>
        Task<ChatResponse> SendMessageTaskAsync(
            string message,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default);

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

        /// <summary>
        /// メッセージをストリーミングで送信（Task 版）
        /// </summary>
        /// <param name="message">送信するメッセージ</param>
        /// <param name="onProgress">ストリーミング受信時の進捗</param>
        /// <param name="options">リクエストオプション</param>
        /// <param name="cancellationToken">外部キャンセルトークン</param>
        Task<ChatResponse> SendMessageStreamingTaskAsync(
            string message,
            IProgress<ChatResponse> onProgress,
            ChatRequestOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// セッション履歴をファイルに保存
        /// </summary>
        /// <param name="filePath">保存先ファイルパス</param>
        /// <param name="sessionId">セッションID</param>
        /// <param name="encryptionKey">暗号化キー（オプション）</param>
        void SaveSession(string filePath, string sessionId, string encryptionKey = null);

        /// <summary>
        /// ファイルからセッション履歴を復元
        /// </summary>
        /// <param name="filePath">読み込むファイルパス</param>
        /// <param name="sessionId">セッションID</param>
        /// <param name="encryptionKey">暗号化キー（オプション）</param>
        void LoadSession(string filePath, string sessionId, string encryptionKey = null);

        /// <summary>
        /// すべてのセッション履歴をディレクトリに保存
        /// </summary>
        /// <param name="dirPath">保存先ディレクトリパス</param>
        /// <param name="encryptionKey">暗号化キー（オプション）</param>
        void SaveAllSessions(string dirPath, string encryptionKey = null);

        /// <summary>
        /// ディレクトリからすべてのセッション履歴を復元
        /// </summary>
        /// <param name="dirPath">読み込むディレクトリパス</param>
        /// <param name="encryptionKey">暗号化キー（オプション）</param>
        void LoadAllSessions(string dirPath, string encryptionKey = null);
    }
}
