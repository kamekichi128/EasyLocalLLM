
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System;
using UnityEngine.Networking;

public class ADVSceneController : MonoBehaviour
{
    /// <summary>
    /// 内部で保持するチャット履歴.
    /// </summary>
    private class ChatMessage
    {
        public string role;
        public string content;
    };

    /// <summary>
    /// メッセージのリスト.
    /// </summary>
    private List<ChatMessage> messageHistory = new()
    {
    };

    private void ClearMessageHistory()
    {
        ChatMessage systemMessage = messageHistory[0];
        messageHistory.Clear();
        messageHistory.Add(systemMessage);
    }

    private const int MAX_RETRY = 3;

    private const float RETRY_DELAY = 1.0f;

    private IEnumerator SendMessageToChatbotAtOnce(string message, Func<string, IEnumerator> callback, float temperature = 0, int seed = -1)
    {
        UnityEngine.Debug.Log("SendMessageToChatbotCalled");

        if (seed < 0) {
            seed = ChatBotRunner.SEED;
        }

        // メッセージを登録する
        messageHistory.Add(new ChatMessage()
        {
            role = "user",
            content = message
        });

        var requestContent = new
        {
            model = model,
            messages = messageHistory.ToArray(),
            stream = false,
            options = new
            {
                seed,
                temperature
            }
        };

        var json = JsonConvert.SerializeObject(requestContent);

        JsonConvert.SerializeObject(requestContent);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityEngine.Debug.Log("HTTP Response will Call : " + json);

        int attempt = 0;

        while (true)
        {
            using UnityWebRequest request = new(ChatBotRunner.API_SERVER_URL + "/api/chat", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                yield return null;


            if (request.result == UnityWebRequest.Result.Success)
            {
                var responseBody = request.downloadHandler.text;
                var chatResponse = JObject.Parse(responseBody);

                UnityEngine.Debug.Log("HTTP Response Finished : " + responseBody);

                var chatMessage = chatResponse["message"];
                messageHistory.Add(new()
                {
                    role = chatMessage["role"].ToString(),
                    content = chatMessage["content"].ToString()
                });

                UnityEngine.Debug.Log("callback will call");

                yield return callback(chatMessage["content"].ToString());
                yield break;
            }
            else
            {
                // 通常発生しないが、500、503などサーバーエラーの可能性があるためリトライ
                UnityEngine.Debug.LogError($"Error: {request.error}");
                if (attempt >= MAX_RETRY)
                {
                    UnityEngine.Debug.LogError("Max retry attempts reached. Aborting.");
                    throw new Exception(request.error);
                }

                // 指数関数的バックオフでリトライ
                attempt++;
                float wait = RETRY_DELAY * Mathf.Pow(2, attempt - 1);
                UnityEngine.Debug.LogWarning($"Retrying in {wait} seconds...");
                yield return new WaitForSeconds(wait);
            }
        }
    }

    private IEnumerator SendMessageToChatbotStreaming(string message, Func<string, bool, IEnumerator> callback)
    {
        UnityEngine.Debug.Log("SendMessageToChatbotCalled");

        // メッセージを登録する
        messageHistory.Add(new ChatMessage()
        {
            role = "user",
            content = message
        });

        var requestContent = new
        {
            model = model,
            messages = messageHistory.ToArray(),
            stream = true,
            options = new
            {
                seed = ChatBotRunner.SEED,
                temperature = 0.7f
            }
        };

        var json = JsonConvert.SerializeObject(requestContent);

        JsonConvert.SerializeObject(requestContent);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityEngine.Debug.Log("HTTP Response will Call : " + json);

        int attempt = 0;
        while (true)
        {
            using UnityWebRequest request = new(ChatBotRunner.API_SERVER_URL + "/api/chat", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            string recievedMessage = "";
            int recievedIndex = 0;
            while (!operation.isDone)
            {
                if (request.downloadHandler.data != null && request.downloadHandler.data.Length > 0)
                {
                    string responseChunk = Encoding.UTF8.GetString(request.downloadHandler.data);
                    string[] responseChunks = responseChunk.Split("\n");

                    for (int i = recievedIndex; i < responseChunks.Length; i++)
                    {
                        string chunk = responseChunks[i].Trim();
                        try
                        {
                            var responseChunkJson = JObject.Parse(chunk);
                            var responseChunkChatMessage = responseChunkJson["message"];
                            recievedMessage += responseChunkChatMessage["content"].ToString();
                        }
                        catch (Exception e)
                        {
                            break;
                        }
                        recievedIndex = i + 1;
                        yield return callback(recievedMessage, false);
                    }
                }
                yield return null; // コルーチンの次のフレームまで待つ
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 全部読みこんだので改めてパース
                string lastResponseChunk = Encoding.UTF8.GetString(request.downloadHandler.data);
                string[] lastResponseChunks = lastResponseChunk.Split("\n");
                recievedMessage = "";
                for (int i = 0; i < lastResponseChunks.Length; i++)
                {
                    string chunk = lastResponseChunks[i].Trim();
                    try
                    {
                        var responseChunkJson = JObject.Parse(chunk);
                        var responseChunkChatMessage = responseChunkJson["message"];
                        recievedMessage += responseChunkChatMessage["content"].ToString();
                    }
                    catch (Exception e)
                    {
                        continue;
                    }
                }

                messageHistory.Add(new()
                {
                    role = "assistant",
                    content = recievedMessage
                });

                yield return callback(recievedMessage, true);
                yield break;
            } else
            {
                // 通常発生しないが、500、503などサーバーエラーの可能性があるためリトライ
                UnityEngine.Debug.LogError($"Error: {request.error}");
                if (attempt >= MAX_RETRY)
                {
                    UnityEngine.Debug.LogError("Max retry attempts reached. Aborting.");
                    throw new Exception(request.error);
                }

                // 指数関数的バックオフでリトライ
                attempt++;
                float wait = RETRY_DELAY * Mathf.Pow(2, attempt - 1);
                UnityEngine.Debug.LogWarning($"Retrying in {wait} seconds...");
                yield return new WaitForSeconds(wait);
            }
        }
    }

    string lastTranlsatedText = "";
    private IEnumerator Translate(string message, string toLanguage)
    {
        UnityEngine.Debug.Log("SendMessageToChatbotCalled");

        // メッセージを登録する
        string systemMessage = "";
        if (toLanguage == "ja")
        {
            systemMessage = "You are a competent English to Japanese translator. Please respond in Japanese with the corresponding Japanese text that I will give you. However, please refrain from providing any additional responses beyond the translated Japanese.";
        }
        else {
            systemMessage = "あなたは有能な日本語から英語への翻訳者です。これから与える文言に対応した英文を応答してください。ただし、翻訳した英文以外の余分な応答はしないでください。";
        }
        List<ChatMessage> chatMessages = new()
        {
            new() {
                role = "system",
                content = systemMessage
            },
            new() {
                role = "user",
                content = message
            }
        };

        var requestContent = new
        {
            model,
            messages = chatMessages.ToArray(),
            stream = false,
            options = new
            {
                seed = ChatBotRunner.SEED,
                temperature = 0.2
            }
        };

        var json = JsonConvert.SerializeObject(requestContent);

        JsonConvert.SerializeObject(requestContent);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityEngine.Debug.Log("HTTP Response will Call : " + json);

        int attempt = 0;

        while (true)
        {
            using UnityWebRequest request = new(ChatBotRunner.API_SERVER_URL + "/api/chat", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                yield return null;

            if (request.result == UnityWebRequest.Result.Success)
            {
                var responseBody = request.downloadHandler.text;
                var chatResponse = JObject.Parse(responseBody);

                UnityEngine.Debug.Log("HTTP Response Finished : " + responseBody);

                var chatMessage = chatResponse["message"];

                UnityEngine.Debug.Log("callback will call");

                lastTranlsatedText = chatMessage["content"].ToString();

                yield break;
            } else
            {
                // 通常発生しないが、500、503などサーバーエラーの可能性があるためリトライ
                UnityEngine.Debug.LogError($"Error: {request.error}");
                if (attempt >= MAX_RETRY)
                {
                    UnityEngine.Debug.LogError("Max retry attempts reached. Aborting.");
                    throw new Exception(request.error);
                }

                // 指数関数的バックオフでリトライ
                attempt++;
                float wait = RETRY_DELAY * Mathf.Pow(2, attempt - 1);
                UnityEngine.Debug.LogWarning($"Retrying in {wait} seconds...");
                yield return new WaitForSeconds(wait);
            }
        }
    }
}