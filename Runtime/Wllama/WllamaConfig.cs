namespace EasyLocalLLM.LLM.WebGL
{
    /// <summary>
    /// Configuration for WebGL llama.cpp WASM client.
    /// </summary>
    public class WllamaConfig
    {
        /// <summary>
        /// Model URL passed to JS/WASM layer.
        /// </summary>
        public string ModelUrl { get; set; } = "StreamingAssets/EasyLocalLLM/models/model.gguf";

        /// <summary>
        /// Default context window size.
        /// </summary>
        public int ContextSize { get; set; } = 2048;

        /// <summary>
        /// Default maximum generated tokens.
        /// </summary>
        public int DefaultMaxTokens { get; set; } = 256;

        /// <summary>
        /// Prefer WebGPU execution when available.
        /// </summary>
        public bool UseWebGpu { get; set; } = true;

        /// <summary>
        /// Base URL for llama.cpp WASM runtime assets.
        /// </summary>
        public string WasmBaseUrl { get; set; } = "StreamingAssets/llama-wasm";

        /// <summary>
        /// Disable IndexedDB cache in browser runtime.
        /// </summary>
        public bool DisableCache { get; set; } = false;

        /// <summary>
        /// Initialization timeout in seconds.
        /// </summary>
        public float InitTimeoutSeconds { get; set; } = 30.0f;

        /// <summary>
        /// Default session id when none is specified.
        /// </summary>
        public string DefaultSessionId { get; set; } = "default";

        /// <summary>
        /// Enable detailed logs.
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Maximum number of concurrent requests (currently 1 is recommended for WASM).
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 1;
    }
}
