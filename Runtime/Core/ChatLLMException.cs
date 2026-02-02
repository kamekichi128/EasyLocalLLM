using System;

namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// LLM エラー情報を保持する例外
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