using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Factory;
using EasyLocalLLM.LLM.Ollama;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// グループチャット画面のサンプル
/// Alice、Bobがチャットしている中に、Charieとして参加し、会話に割り込むイメージです
/// </summary>
public class GroupChat : MonoBehaviour
{
    public UIDocument UIDocument;

    private IChatLLMClient client;

    private class ChatMember
    {
        public string Name;
        public string SystemPrompt;
        public ChatMember(string name, string systemPrompt)
        {
            Name = name;
            SystemPrompt = systemPrompt;
        }
    }

    private static readonly string SYSTEM_PROMPT_BASE =
        "あなたは{0}という、{1}です。あなたは友達の{2}とチャーリーとチャットをしています。"
       + "あなたは以下のフォーマットで友達からのチャットを受け取ります:\n"
       + "{2} : よう！最近どう？\n"
       + "\n"
       + "受け取ったメッセージや、これまでの流れを受けて、チャットのメッセージを30文字以内で生成してください。\n"
       + "もしも相手の返信をもっと聞きたいなど、特に返信する内容がない場合は「...」と返信してください。";

    private static readonly List<ChatMember> CHAT_MEMBERS = new()
    {
        new ("アリス", string.Format(SYSTEM_PROMPT_BASE, "アリス", "フレンドリーな16才の女の子", "ボブ")),
        new ("ボブ", string.Format(SYSTEM_PROMPT_BASE, "ボブ", "ちょっとやんちゃな18才の男の子", "アリス"))
    };

    private static readonly Dictionary<string, int> MESSAGE_PRIORITY = new()
    {
        { "アリス", 1 },
        { "ボブ", 1 },
        { "チャーリー", 0 },
    };

    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    void Start()
    {
        Debug.Log("=== EasyLocalLLM Group Chat Sample ===");

        InitializeEasyLocalLLMClient();
    }

    private void InitializeEasyLocalLLMClient()
    {
        // ステップ 1: クライアントの初期化
        // サーバーを自動起動するため、ollama.exeを立ち上げている場合は終了するか、
        // 立ち上げていないポートを指定してください。
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            DefaultModelName = "hoangquan456/qwen3-nothink:4b",
            AutoStartServer = true,
            DebugMode = true,
        };
        OllamaServerManager.Initialize(config, OnOllamaServerInitialized);
        client = LLMClientFactory.CreateOllamaClient(config);
        Debug.Log("✓ Client initialized");
    }

    private void OnOllamaServerInitialized(bool successed)
    {
        if (successed)
        {
            Debug.Log("✓ Ollama server initialized successfully.");
            EnableUI();
        }
        else
        {
            Debug.LogError("✗ Failed to initialize Ollama server.");
        }
    }

    private void EnableUI()
    {
        var root = UIDocument.rootVisualElement;
        var sendMessageButton = root.Q<Button>("SendMessageButton");
        var messageInput = root.Q<TextField>("MessageInput");
        sendMessageButton.clicked += OnSendMessageClicked;
        sendMessageButton.SetEnabled(true);
        messageInput.SetEnabled(true);
    }

    private void RemoveUIEvent()
    {
        var root = UIDocument.rootVisualElement;
        var sendMessageButton = root.Q<Button>("SendMessageButton");
        sendMessageButton.clicked -= OnSendMessageClicked;
    }

    private void OnSendMessageClicked()
    {
        // 生成中のメッセージをキャンセルする
        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new CancellationTokenSource();

        // 自分のメッセージを送る
        var root = UIDocument.rootVisualElement;
        var messageInput = root.Q<TextField>("MessageInput");
        string message = messageInput.value;
        AddMessage("チャーリー", message);
    }

    private void AddMessage(string sender, string message)
    {
        // 送られたメッセージを画面に追加する
        AddMessageOnUI(sender, message);

        // メッセージを他のメンバーに送信する
        StartCoroutine(SendMessageToMembersAfterSeconds(sender, message, 5.0f));
    }

    private void AddMessageOnUI(string sender, string message)
    {
        // メッセージボックスを追加する
        var root = UIDocument.rootVisualElement;
        var chatLog = root.Q<ScrollView>("ChatLog");
        var chatBoxResouce = Resources.Load<VisualTreeAsset>("ChatBox");
        VisualElement chatBox = CreateChatBox(sender, message);
        chatLog.contentContainer.Add(chatBox);

        // スクロールを一番下に移動する
        StartCoroutine(ScrollToEnd(chatLog));
    }

    private VisualElement CreateChatBox(string sender, string message)
    {
        var chatBoxResouce = Resources.Load<VisualTreeAsset>("ChatBox");
        VisualElement chatBox = chatBoxResouce.CloneTree();
        var senderLabel = chatBox.Q<Label>("SenderLabel");
        var messageLabel = chatBox.Q<Label>("MessageLabel");
        senderLabel.text = sender;
        messageLabel.text = message;
        return chatBox;
    }

    private IEnumerator ScrollToEnd(ScrollView scrollView)
    {
        // 次のフレームまで待つ
        yield return null;
        // スクロールを一番下に移動
        scrollView.scrollOffset = new Vector2(0, scrollView.contentContainer.layout.height);
    }

    private IEnumerator SendMessageToMembersAfterSeconds(string sender, string message, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SendMessageToMembers(sender, message);
    }

    private void SendMessageToMembers(string sender, string message)
    {
        Debug.Log($"New message > {sender} : {message}");
        foreach (var member in CHAT_MEMBERS)
        {
            if (member.Name == sender)
            {
                continue;
            }
            SendMessageToMember(member, sender, message);
        }
    }

    private void SendMessageToMember(ChatMember member, string sender, string message)
    {
        StartCoroutine(client.SendMessageAsync(
            sender + ":" + message,
            RecieveMessage,
            new ChatRequestOptions
            {
                SessionId = member.Name + "_chat",
                SystemPrompt = member.SystemPrompt,
                Priority = MESSAGE_PRIORITY[sender],
                CancellationToken = cancellationTokenSource.Token,
                WaitIfBusy = false
            }
        ));
        Debug.Log($"→ Message sent to {member.Name}> {sender} : {message}");
    }

    private void RecieveMessage(ChatResponse response, ChatError error)
    {
        if (error != null)
        {
            if (error.ErrorType == LLMErrorType.Cancelled)
            {
                Debug.Log("✗ Message generation canceled.");
            }
            else
            {
                Debug.Log($"✗ Error: {error.ErrorType} - {error.Message}");
                Debug.Log($"  HttpStatus: {error.HttpStatus}");
            }
            return;
        }

        Debug.Log($"✓ Response received: {response.Content}");


        var message = response.Content.Trim().Replace("\n", "").Replace("\\n", "");

        // "..."、または空文字の場合はメッセージを追加しない
        if (message == "..." || message.Length == 0)
        {
            return;
        }
        var sender = response.SessionId.Replace("_chat", "");
        AddMessage(sender, message);
    }

    public void OnDisable()
    {
        RemoveUIEvent();
        cancellationTokenSource.Dispose();
    }
}
