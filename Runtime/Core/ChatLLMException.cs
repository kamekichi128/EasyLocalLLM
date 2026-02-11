using System;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Exception that holds LLM error information
    /// </summary>
    public class ChatLLMException : Exception
    {
        public ChatError Error { get; }

        public ChatLLMException(ChatError error)
            : base(error?.Message)
        {
            Error = error;
        }
    }
}