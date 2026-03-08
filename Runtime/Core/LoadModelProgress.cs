namespace EasyLocalLLM.LLM.Core
{
    /// <summary>
    /// Load model progress information.
    /// </summary>
    public class LoadModelProgress
    {
        /// <summary>
        /// Progress value (0.0 to 1.0).
        /// </summary>
        public double Progress { get; private set; }

        /// <summary>
        /// True if loading is completed (even if failed).
        /// </summary>
        public bool IsCompleted { get; private set; }

        /// <summary>
        /// True if loading succeeded.
        /// </summary>
        public bool IsSuccessed { get; private set; }

        /// <summary>
        /// Message about loading status.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public LoadModelProgress(double progress, bool isCompleted, bool isSuccessed, string message)
        {
            Progress = progress;
            IsCompleted = isCompleted;
            IsSuccessed = isSuccessed;
            Message = message;
        }
    }
}
