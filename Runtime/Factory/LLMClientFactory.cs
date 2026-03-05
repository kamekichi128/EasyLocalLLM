using EasyLocalLLM.LLM.Ollama;
using EasyLocalLLM.LLM.WebGL;

namespace EasyLocalLLM.LLM.Factory
{
    /// <summary>
    /// Factory class for creating LLM clients
    /// </summary>
    public static class LLMClientFactory
    {
        /// <summary>
        /// Create Ollama client
        /// </summary>
        /// <param name="config">Ollama configuration (null for default settings)</param>
        /// <returns>OllamaClient instance</returns>
        public static OllamaClient CreateOllamaClient(OllamaConfig config = null)
        {
            return new OllamaClient(config);
        }

        /// <summary>
        /// Create WebGL llama.cpp client.
        /// </summary>
        /// <param name="config">WebGL llama.cpp configuration (null for default settings)</param>
        /// <returns>WebGLLlamaCppClient instance</returns>
        public static WebGLLlamaCppClient CreateWebGLLlamaCppClient(WebGLLlamaCppConfig config = null)
        {
            return new WebGLLlamaCppClient(config);
        }

        // Future extension for other clients
        // public static ILlamaCppClient CreateLlamaCppClient(LlamaCppConfig config) { ... }
        // public static IGptClient CreateGptClient(GptConfig config) { ... }
    }
}
