using UnityEngine;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// Class for managing Ollama server configuration
    /// </summary>
    public class OllamaConfig
    {
        /// <summary>
        /// Ollama server URL (default: http://localhost:11434)
        /// </summary>
        public string ServerUrl { get; set; } = "http://localhost:11434";

        /// <summary>
        /// Default model name to use (e.g. "mistral", "neural-chat")
        /// </summary>
        public string DefaultModelName { get; set; } = "mistral";

        /// <summary>
        /// Maximum number of HTTP request retries
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Initial delay in seconds for retries (increases with exponential backoff)
        /// </summary>
        public float RetryDelaySeconds { get; set; } = 1.0f;

        /// <summary>
        /// Default seed value (-1 for random each time)
        /// </summary>
        public int DefaultSeed { get; set; } = -1;

        /// <summary>
        /// Path to Ollama executable (used for auto-starting server)
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Ollama models directory (environment variable OLLAMA_MODELS)
        /// </summary>
        public string ModelsDirectory { get; set; } = "./Models";

        /// <summary>
        /// HTTP timeout in seconds
        /// </summary>
        public float HttpTimeoutSeconds { get; set; } = 60.0f;

        /// <summary>
        /// Debug mode (detailed logging)
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Auto-start Ollama server
        /// </summary>
        public bool AutoStartServer { get; set; } = true;

        /// <summary>
        /// Perform health check after server startup
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// Maximum number of concurrent sessions (1 or more)
        /// </summary>
        public int MaxConcurrentSessions { get; set; } = 1;
    }
}
