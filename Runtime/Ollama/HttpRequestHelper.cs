using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using EasyLocalLLM.LLM.Core;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// Helper class to centrally manage HTTP request retry logic
    /// </summary>
    internal class HttpRequestHelper
    {
        public HttpRequestHelper(OllamaConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Execute HTTP request with retry functionality
        /// <param name="url">Request URL</param>
        /// <param name="jsonBody">JSON body</param>
        /// <param name="onSuccess">Success callback</param>
        /// <param name="onError">Error callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// </summary>
        public IEnumerator ExecuteWithRetry(
            string url,
            string jsonBody,
            Action<string> onSuccess,
            Action<ChatError> onError,
            System.Threading.CancellationToken cancellationToken = default)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            int attempt = 0;

            while (attempt < _config.MaxRetries)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = (int)_config.HttpTimeoutSeconds;

                    if (_config.DebugMode)
                    {
                        UnityEngine.Debug.Log($"[Ollama] Sending request (attempt {attempt + 1}/{_config.MaxRetries})");
                        UnityEngine.Debug.Log($"[Ollama] URL: {url}");
                        UnityEngine.Debug.Log($"[Ollama] Body: {jsonBody}");
                    }

                    var operation = request.SendWebRequest();

                    while (!operation.isDone)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            request.Abort();
                            onError?.Invoke(new ChatError
                            {
                                ErrorType = LLMErrorType.Cancelled,
                                Message = $"Request to '{url}' was cancelled by user"
                            });
                            yield break;
                        }
                        yield return null;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseBody = request.downloadHandler.text;

                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[Ollama] Response received: {responseBody}");
                        }

                        onSuccess?.Invoke(responseBody);
                        yield break;
                    }
                    else
                    {
                        string errorMsg = request.error;
                        int httpStatus = (int)request.responseCode;

                        UnityEngine.Debug.LogWarning($"[Ollama] Request failed (attempt {attempt + 1}/{_config.MaxRetries}): {errorMsg} (HTTP {httpStatus})");

                        attempt++;

                        if (attempt >= _config.MaxRetries)
                        {
                            var errorType = DetermineErrorType(httpStatus, errorMsg);
                            var detailedMessage = BuildDetailedErrorMessage(errorType, httpStatus, errorMsg, url, _config);
                            var chatError = new ChatError
                            {
                                ErrorType = errorType,
                                Message = detailedMessage,
                                HttpStatus = httpStatus
                            };
                            onError?.Invoke(chatError);
                            yield break;
                        }

                        // Exponential backoff before retrying
                        float waitTime = _config.RetryDelaySeconds * Mathf.Pow(2, attempt - 1);
                        UnityEngine.Debug.LogWarning($"[Ollama] Retrying in {waitTime} seconds...");
                        yield return new WaitForSeconds(waitTime);
                    }
                }
            }
        }

        /// <summary>
        /// Execute streaming HTTP request with retry functionality
        /// <param name="url">Request URL</param>
        /// <param name="jsonBody">JSON body</param>
        /// <param name="onChunk">Chunk received callback</param>
        /// <param name="onComplete">Completion callback (success flag)</param>
        /// <param name="onError">Error callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// </summary>
        public IEnumerator ExecuteStreamingWithRetry(
            string url,
            string jsonBody,
            Action<string> onChunk,
            Action<bool> onComplete,
            Action<ChatError> onError,
            System.Threading.CancellationToken cancellationToken = default)
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            int attempt = 0;

            while (attempt < _config.MaxRetries)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = (int)_config.HttpTimeoutSeconds;

                    if (_config.DebugMode)
                    {
                        UnityEngine.Debug.Log($"[Ollama] Streaming request started (attempt {attempt + 1}/{_config.MaxRetries})");
                    }

                    var operation = request.SendWebRequest();

                    int lastProcessedByteCount = 0;
                    var decoder = Encoding.UTF8.GetDecoder();
                    var buffer = new StringBuilder();

                    while (!operation.isDone)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            request.Abort();
                            onError?.Invoke(new ChatError
                            {
                                ErrorType = LLMErrorType.Cancelled,
                                Message = $"Streaming request to '{url}' was cancelled by user"
                            });
                            onComplete?.Invoke(false);
                            yield break;
                        }
                        var data = request.downloadHandler.data;
                        if (data != null && data.Length > lastProcessedByteCount)
                        {
                            int byteCount = data.Length - lastProcessedByteCount;
                            char[] charBuffer = new char[Encoding.UTF8.GetMaxCharCount(byteCount)];
                            int charsDecoded = decoder.GetChars(data, lastProcessedByteCount, byteCount, charBuffer, 0);
                            buffer.Append(charBuffer, 0, charsDecoded);
                            lastProcessedByteCount = data.Length;

                            string buffered = buffer.ToString();
                            int lastNewline = buffered.LastIndexOf('\n');
                            if (lastNewline >= 0)
                            {
                                string complete = buffered.Substring(0, lastNewline + 1);
                                string remainder = buffered.Substring(lastNewline + 1);
                                buffer.Clear();
                                buffer.Append(remainder);

                                string[] chunks = complete.Split('\n');
                                for (int i = 0; i < chunks.Length; i++)
                                {
                                    string chunk = chunks[i].Trim();
                                    if (!string.IsNullOrEmpty(chunk))
                                    {
                                        onChunk?.Invoke(chunk);
                                    }
                                }
                            }
                        }
                        yield return null;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // Process any remaining data after streaming completes
                        var finalData = request.downloadHandler.data;
                        if (finalData != null && finalData.Length > lastProcessedByteCount)
                        {
                            int byteCount = finalData.Length - lastProcessedByteCount;
                            char[] charBuffer = new char[Encoding.UTF8.GetMaxCharCount(byteCount)];
                            int charsDecoded = decoder.GetChars(finalData, lastProcessedByteCount, byteCount, charBuffer, 0);
                            buffer.Append(charBuffer, 0, charsDecoded);
                            lastProcessedByteCount = finalData.Length;
                        }

                        string remaining = buffer.ToString().Trim();
                        if (!string.IsNullOrEmpty(remaining))
                        {
                            string[] lastChunks = remaining.Split('\n');
                            for (int i = 0; i < lastChunks.Length; i++)
                            {
                                string chunk = lastChunks[i].Trim();
                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    onChunk?.Invoke(chunk);
                                }
                            }
                        }

                        if (_config.DebugMode)
                        {
                            UnityEngine.Debug.Log("[Ollama] Streaming completed successfully");
                        }

                        onComplete?.Invoke(true);
                        yield break;
                    }
                    else
                    {
                        string errorMsg = request.error;
                        int httpStatus = (int)request.responseCode;

                        UnityEngine.Debug.LogWarning($"[Ollama] Streaming failed (attempt {attempt + 1}/{_config.MaxRetries}): {errorMsg}");

                        attempt++;

                        if (attempt >= _config.MaxRetries)
                        {
                            var errorType = DetermineErrorType(httpStatus, errorMsg);
                            var detailedMessage = BuildDetailedErrorMessage(errorType, httpStatus, errorMsg, url, _config);
                            var chatError = new ChatError
                            {
                                ErrorType = errorType,
                                Message = detailedMessage,
                                HttpStatus = httpStatus
                            };
                            onError?.Invoke(chatError);
                            onComplete?.Invoke(false);
                            yield break;
                        }

                        float waitTime = _config.RetryDelaySeconds * Mathf.Pow(2, attempt - 1);
                        UnityEngine.Debug.LogWarning($"[Ollama] Retrying in {waitTime} seconds...");
                        yield return new WaitForSeconds(waitTime);
                    }
                }
            }
        }

        /// <summary>
        /// Determine error type from error message
        /// <param name="httpStatus">HTTP status code</param>
        /// <param name="errorMessage">Error message</param>
        /// </summary>
        private LLMErrorType DetermineErrorType(int httpStatus, string errorMessage)
        {
            if (httpStatus == 0 || errorMessage.Contains("Connection"))
                return LLMErrorType.ConnectionFailed;

            if (httpStatus >= 500)
                return LLMErrorType.ServerError;

            if (httpStatus == 404)
                return LLMErrorType.ModelNotFound;

            if (errorMessage.Contains("timeout", System.StringComparison.OrdinalIgnoreCase))
                return LLMErrorType.Timeout;

            return LLMErrorType.Unknown;
        }

        /// <summary>
        /// Build detailed error message based on error type
        /// <param name="errorType">Error type</param>
        /// <param name="httpStatus">HTTP status code</param>
        /// <param name="originalError">Original error message</param>
        /// <param name="url">Request URL</param>
        /// <param name="config">Ollama configuration</param>
        /// </summary>
        private string BuildDetailedErrorMessage(LLMErrorType errorType, int httpStatus, string originalError, string url, OllamaConfig config)
        {
            switch (errorType)
            {
                case LLMErrorType.ConnectionFailed:
                    return $"Cannot connect to Ollama server at '{url}'. " +
                           $"Please check: (1) Server is running, (2) URL is correct, (3) Firewall settings. " +
                           $"Original error: {originalError}";

                case LLMErrorType.ServerError:
                    return $"Ollama server error (HTTP {httpStatus}). " +
                           $"Please check: (1) Server logs, (2) Model is loaded correctly, (3) Server resources (memory/GPU). " +
                           $"Original error: {originalError}";

                case LLMErrorType.ModelNotFound:
                    return $"Model '{config.DefaultModelName}' not found (HTTP {httpStatus}). " +
                           $"Please run: 'ollama pull {config.DefaultModelName}' or check the model name is correct. " +
                           $"Use 'ollama list' to see installed models.";

                case LLMErrorType.Timeout:
                    return $"Request timed out after {config.HttpTimeoutSeconds} seconds. " +
                           $"Please consider: (1) Increase HttpTimeoutSeconds, (2) Use a smaller model, (3) Check server performance. " +
                           $"Original error: {originalError}";

                default:
                    return originalError;
            }
        }
    }
}
