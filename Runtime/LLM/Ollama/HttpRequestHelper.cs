using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using EasyLocalLLM.LLM.Core;

namespace EasyLocalLLM.LLM.Ollama
{
    /// <summary>
    /// HTTP リクエストのリトライロジックを一元管理するヘルパークラス
    /// </summary>
    internal class HttpRequestHelper
    {
        private readonly OllamaConfig _config;

        public HttpRequestHelper(OllamaConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// リトライ機能付きで HTTP リクエストを実行
        /// </summary>
        public IEnumerator ExecuteWithRetry(
            string url,
            string jsonBody,
            Action<string> onSuccess,
            Action<ChatError> onError)
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
                            var chatError = new ChatError
                            {
                                ErrorType = DetermineErrorType(httpStatus, errorMsg),
                                Message = errorMsg,
                                HttpStatus = httpStatus,
                                IsRetryable = false
                            };
                            onError?.Invoke(chatError);
                            yield break;
                        }

                        // 指数バックオフでリトライ
                        float waitTime = _config.RetryDelaySeconds * Mathf.Pow(2, attempt - 1);
                        UnityEngine.Debug.LogWarning($"[Ollama] Retrying in {waitTime} seconds...");
                        yield return new WaitForSeconds(waitTime);
                    }
                }
            }
        }

        /// <summary>
        /// ストリーミング対応の HTTP リクエストを実行
        /// </summary>
        public IEnumerator ExecuteStreamingWithRetry(
            string url,
            string jsonBody,
            Action<string> onChunk,
            Action<bool> onComplete,
            Action<ChatError> onError)
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

                    int lastProcessedIndex = 0;

                    while (!operation.isDone)
                    {
                        if (request.downloadHandler.data != null && request.downloadHandler.data.Length > 0)
                        {
                            string responseChunk = Encoding.UTF8.GetString(request.downloadHandler.data);
                            string[] chunks = responseChunk.Split('\n');

                            for (int i = lastProcessedIndex; i < chunks.Length; i++)
                            {
                                string chunk = chunks[i].Trim();
                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    onChunk?.Invoke(chunk);
                                    lastProcessedIndex = i + 1;
                                }
                            }
                        }
                        yield return null;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // 最後のチャンクも処理
                        string lastChunk = Encoding.UTF8.GetString(request.downloadHandler.data);
                        string[] lastChunks = lastChunk.Split('\n');

                        for (int i = lastProcessedIndex; i < lastChunks.Length; i++)
                        {
                            string chunk = lastChunks[i].Trim();
                            if (!string.IsNullOrEmpty(chunk))
                            {
                                onChunk?.Invoke(chunk);
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
                            var chatError = new ChatError
                            {
                                ErrorType = DetermineErrorType(httpStatus, errorMsg),
                                Message = errorMsg,
                                HttpStatus = httpStatus,
                                IsRetryable = false
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
        /// エラーメッセージから エラータイプを判定
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
    }
}
