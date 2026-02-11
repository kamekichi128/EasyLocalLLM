using System;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// LLM error types
    /// </summary>
    public enum LLMErrorType
    {
        /// <summary>Failed to connect to server</summary>
        ConnectionFailed,

        /// <summary>Server returned an error (500, 503, etc.)</summary>
        ServerError,

        /// <summary>Failed to parse response</summary>
        InvalidResponse,

        /// <summary>Request timed out</summary>
        Timeout,

        /// <summary>Specified model not found</summary>
        ModelNotFound,

        /// <summary>Cancelled by user</summary>
        Cancelled,

        /// <summary>Other error</summary>
        Unknown
    }

    /// <summary>
    /// Error information that occurred during communication with LLM
    /// </summary>
    public class ChatError
    {
        /// <summary>
        /// Error type
        /// </summary>
        public LLMErrorType ErrorType { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Exception information (if any)
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// HTTP status code (if any)
        /// </summary>
        public int? HttpStatus { get; set; }

        public override string ToString()
        {
            return $"[{ErrorType}] {Message}" +
                   (HttpStatus.HasValue ? $" (HTTP {HttpStatus})" : "");
        }
    }
}
