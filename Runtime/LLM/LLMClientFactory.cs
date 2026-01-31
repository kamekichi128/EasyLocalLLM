namespace EasyLocalLLM.LLM
{
    /// <summary>
    /// LLM クライアントを生成するファクトリクラス
    /// </summary>
    public static class LLMClientFactory
    {
        /// <summary>
        /// Ollama クライアントを生成
        /// </summary>
        /// <param name="config">Ollama の設定（null の場合はデフォルト設定を使用）</param>
        /// <returns>OllamaClient インスタンス</returns>
        public static OllamaClient CreateOllamaClient(OllamaConfig config = null)
        {
            return new OllamaClient(config);
        }

        // 将来の拡張のための他のクライアント
        // public static ILlamaCppClient CreateLlamaCppClient(LlamaCppConfig config) { ... }
        // public static IGptClient CreateGptClient(GptConfig config) { ... }
    }
}
