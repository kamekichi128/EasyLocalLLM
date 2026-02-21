using System;
using System.Collections.Generic;
using System.Threading;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Chat request options
    /// </summary>
    public class ChatRequestOptions
    {
        /// <summary>
        /// Constant values for response format
        /// </summary>
        public static class FormatConstants
        {
            /// <summary>
            /// Return response in JSON format
            /// </summary>
            public const string Json = "json";
        }

        /// <summary>
        /// Chat session ID (history is shared within the same session)
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Model name to use (null for default)
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Answer diversity (0.0 = deterministic, 1.0+ = more diverse)
        /// null = Ollama default
        /// </summary>
        public float? Temperature { get; set; }

        /// <summary>
        /// Random seed (-1 = random)
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Limits token selection to the top K likely candidates
        /// </summary>
        public int? TopK { get; set; }

        /// <summary>
        /// Nucleus sampling threshold (0.0 to 1.0)
        /// </summary>
        public float? TopP { get; set; }

        /// <summary>
        /// Minimum probability threshold for token filtering (0.0 to 1.0)
        /// </summary>
        public float? MinP { get; set; }

        /// <summary>
        /// Stop sequences; generation stops when any sequence is encountered
        /// </summary>
        public List<string> Stop { get; set; }

        /// <summary>
        /// Context window size (maximum tokens in context)
        /// </summary>
        public int? NumCtx { get; set; }

        /// <summary>
        /// Maximum number of tokens to predict
        /// </summary>
        public int? NumPredict { get; set; }

        /// <summary>
        /// Request priority (higher value = higher priority)
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// Wait for client to finish if busy, or return error
        /// </summary>
        public bool WaitIfBusy { get; set; } = false;

        /// <summary>
        /// System prompt for this request (null for global setting)
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Maximum number of messages to keep in history (0 = unlimited)
        /// </summary>
        public int? MaxHistory { get; set; }

        /// <summary>
        /// Cancellation token (specify CancellationTokenSource.Token)
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        /// Tools to use in this request (null for all registered tools)
        /// </summary>
        public List<string> Tools { get; set; }

        /// <summary>
        /// Maximum number of tool call iterations (default: 5)
        /// Limits the number of times tools can be called in sequence to prevent infinite loops
        /// </summary>
        public int MaxToolIterations { get; set; } = 5;

        /// <summary>
        /// Response format specification
        /// Ignored if FormatSchema is specified
        /// Usage example: Format = ChatRequestOptions.FormatConstants.Json
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Response JSON schema
        /// When specified, the LLM will generate JSON following this schema
        /// </summary>
        public object FormatSchema { get; set; }
    }
}
