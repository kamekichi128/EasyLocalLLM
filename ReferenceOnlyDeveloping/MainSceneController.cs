using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class MainSceneController : MonoBehaviour
{
    private class ChatMessage
    {
        public string role;
        public string content;
    };

    private const int MAX_RETRY = 3;

    private const float RETRY_DELAY = 1.0f;

    private bool isRunning = false;

    private IEnumerator SendMessageToChatbotAtOnce(string message, Action<string, bool> callback, float temperature = 0.0f, int seed = -1)
    {
        if (isRunning) {
            callback("", false);
            yield break;
        }
        
        isRunning = true;

        UnityEngine.Debug.Log("SendMessageToChatbotCalled : ");

        if (seed < 0) {
            seed = ChatBotRunner.SEED;
        }

        ChatMessage msg = new ChatMessage()
        {
            role = "user",
            content = message
        };
        ChatMessage systemMsg;
        if (inEnglish)
        {
            systemMsg =
                new()
                {
                    role = "system",
                    content = "You are a capable AI. Carefully read the given text. Also, please pay the utmost attention to consistency between your statements."
                };
        }
        else
        {
            systemMsg= 
                new()
                {
                    role = "system",
                    content = "あなたは有能なAIです。与えられた文章を注意深く読み解いてください。また、自身の発言間での整合性に最大限の注意を払ってください。"
                };
        }

        var requestContent = new
        {
            model = model,
            messages = new []{ systemMsg, msg },
            stream = false,
            options = new
            {
                seed = seed,
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

                UnityEngine.Debug.Log("callback will call");

                callback(chatMessage["content"].ToString(), true);

                isRunning = false;

                yield break;
            }
            else
            {
                // 通常発生しないが、500、503などサーバーエラーの可能性があるためリトライ
                UnityEngine.Debug.LogError($"Error: {request.error}");
                if (attempt >= MAX_RETRY)
                {
                    UnityEngine.Debug.LogError("Max retry attempts reached. Aborting.");

                    isRunning = false;

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