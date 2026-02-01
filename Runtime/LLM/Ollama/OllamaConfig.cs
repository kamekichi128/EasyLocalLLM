using UnityEngine;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// Ollama サーバの設定を管理するクラス
    /// </summary>
    public class OllamaConfig
    {
        /// <summary>
        /// Ollama サーバの URL（デフォルト: http://localhost:11434）
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:11434";

        /// <summary>
        /// デフォルトで使用するモデル名（例: "mistral"、"neural-chat"）
        /// </summary>
        public string DefaultModelName { get; set; } = "mistral";

        /// <summary>
        /// HTTP リクエストの最大リトライ回数
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// リトライ時の初期遅延秒数（指数バックオフで増加）
        /// </summary>
        public float RetryDelaySeconds { get; set; } = 1.0f;

        /// <summary>
        /// デフォルトシード値（-1 で毎回ランダム）
        /// </summary>
        public int DefaultSeed { get; set; } = -1;

        /// <summary>
        /// Ollama 実行ファイルのパス（サーバ自動起動時に使用）
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Ollama モデルディレクトリ（環境変数 OLLAMA_MODELS）
        /// </summary>
        public string ModelsDirectory { get; set; } = "./Models";

        /// <summary>
        /// HTTP タイムアウト秒数
        /// </summary>
        public float HttpTimeoutSeconds { get; set; } = 60.0f;

        /// <summary>
        /// デバッグモード（詳細なログ出力）
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Ollama サーバを自動起動するか
        /// </summary>
        public bool AutoStartServer { get; set; } = true;

        /// <summary>
        /// 同時実行可能なセッション数（1 以上）
        /// </summary>
        public int MaxConcurrentSessions { get; set; } = 1;
    }
}
