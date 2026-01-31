using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text;
using UnityEngine.Networking;

public class TitleSceneController : MonoBehaviour
{
    public void Start()
    {
        StartCoroutine(AwakeChatbot());
    }


    //
    // チャットボットのインストール系
    //

    private class ChatMessage
    {
        public string role;
        public string content;
    };

    private IEnumerator AwakeChatbot()
    {
        UnityEngine.Debug.Log("AwakeChatbot");

        var requestContent = new
        {
            model = "MyModel",
            messages = new[] {
                new ChatMessage() {
                    role = "system",
                    content = "you are a chatbot"
                },
                new ChatMessage() { 
                    role = "user",
                    content = "hello"
                }
            },
            stream = false,
            options = new
            {
                seed = ChatBotRunner.SEED,
                temperature = 0
            }
        };

        var json = JsonConvert.SerializeObject(requestContent);

        JsonConvert.SerializeObject(requestContent);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityEngine.Debug.Log("HTTP Response will Call : " + json);

        int tryAttempt = 5;
        bool ok = false;
        string error = "";

        while (tryAttempt > 0)
        {
            using UnityWebRequest request = new(ChatBotRunner.API_SERVER_URL + "/api/chat", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                yield return null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                error = request.error;
            }
            else {
                ok = true;
                break;
            }
            tryAttempt--;
            yield return new WaitForSeconds(1.0f);
        }
        if (!ok)
        {
            UnityEngine.Debug.LogError($"Error: {error}");
            throw new Exception(error);
        }

    }

    bool installingAIOnOption = false;

    private IEnumerator AwakeChatbotAfterChangeLanguage()
    {
        if (!installingAIOnOption) {
            installingAIOnOption = true;

            UnityEngine.Debug.Log("AwakeChatbotOnOption");

            LocalizedString lsModel = new LocalizedString("UILocalizationTable", "Model Name");
            lsModel.RefreshString();
            var requestContent = new
            {
                model = lsModel.GetLocalizedString(),
                messages = new[] {
                    new ChatMessage() {
                        role = "system",
                        content = "you are a chatbot"
                    },
                    new ChatMessage() {
                        role = "user",
                        content = "hello"
                    }
                },
                stream = false,
                options = new
                {
                    seed = ChatBotRunner.SEED,
                    temperature = 0
                }
            };

            var json = JsonConvert.SerializeObject(requestContent);

            JsonConvert.SerializeObject(requestContent);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            UnityEngine.Debug.Log("HTTP Response will Call : " + json);

            using UnityWebRequest request = new(ChatBotRunner.API_SERVER_URL + "/api/chat", "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
                yield return null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"Error: {request.error}");
                throw new Exception(request.error);
            }
            installingAIOnOption = false;
        }
    }
    
    private IEnumerator InstallAI()
    {
        string model = "MyModel";
        // プロセス情報の設定
        ProcessStartInfo startInfo = new()
        {
            FileName = ChatBotRunner.EXECUTABLE_PATH,
            Arguments = "create " + model + " -f " + Application.streamingAssetsPath + "/LLM/model/mymodel",
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
            WorkingDirectory = System.Environment.CurrentDirectory
        };
        startInfo.EnvironmentVariables["OLLAMA_HOST"] = ChatBotRunner.API_SERVER_URL;
        startInfo.EnvironmentVariables["OLLAMA_FLASH_ATTENTION"] = "1";
        startInfo.EnvironmentVariables["OLLAMA_MODELS"] = ChatBotRunner.OLLAMA_MODELS;

        // プロセスの起動
        Process installProcess = new()
        {
            StartInfo = startInfo,
        };

        installProcess.Start();
        UnityEngine.Debug.Log("Install Chatbot Start");

        // メッセージを表示する
        SEAudioSource.PlayOneShot(SelectAC);
        VisualElement root = TitleSceneUIDocument.rootVisualElement;
        VisualElement infoWindow = root.Q<VisualElement>("InfoWindow");
        VisualElement infoWindowInner = root.Q<VisualElement>("InfoWindowInner");
        Label infoWindowLabel = root.Q<Label>("InfoWindowMessage");
        infoWindowLabel.text = new LocalizedString("UILocalizationTable", "AI Install").GetLocalizedString();
        infoWindow.style.display = DisplayStyle.Flex;
        infoWindowInner.style.display = DisplayStyle.Flex;
        infoWindow.SetEnabled(true);
        infoWindowInner.SetEnabled(true);

        yield return new WaitUntil(() =>
        {
            return installProcess.HasExited;
        });

        UnityEngine.Debug.Log("Install Chatbot End");

        if (installProcess.ExitCode != 0)
        {
            UnityEngine.Debug.LogError("Install Chatbot Failed");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;//ゲームプレイ終了
#else
    Application.Quit();//ゲームプレイ終了
#endif
        }
        else
        {
            UnityEngine.Debug.Log("Install Chatbot Successed");

            // メッセージを非表示にする
            infoWindow.SetEnabled(false);

            yield return new WaitForSeconds(0.5f);

            infoWindow.style.display = DisplayStyle.None;
            UnityEngine.Debug.Log("Window closed");
        }
    }
}