using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Ollama;
using EasyLocalLLM.LLM.Factory;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

/// <summary>
/// シンプルなチャット画面のサンプル
/// ロードしたLLMモデルに対して、プロンプトを送り応答を受け取ります
/// 何種類かのAIタイプを用意し、選択したAIタイプに応じてシステムプロンプトやツールを切り替えます
/// </summary>
public class SimpleChat : MonoBehaviour
{
    public UIDocument UIDocument;

    private OllamaClient client;

    private class AIType {
        public string Name { get; private set; }
        public string SystemPrompt { get; private set; } 
        public string Description { get; private set; }

        public object FormatSchema { get; private set; }

        public Action OnChangeCallback { get; private set; }

        public AIType(string name, string systemPrompt, string description, object formatSchema, Action onChangeCallback)
        {
            Name = name;
            SystemPrompt = systemPrompt;
            Description = description;
            FormatSchema = formatSchema;
            OnChangeCallback = onChangeCallback;
        }
    }

    private readonly Dictionary<string, AIType> aiTypes = new();

    private AIType currentAIType = null;

    void Start()
    {
        Debug.Log("=== EasyLocalLLM Simple Chat Sample ===");

        InitializeAITypes();
        InitializeEasyLocalLLMClient();
    }

    private void InitializeAITypes()
    {
        aiTypes.Add(
             "AI Assistant", new AIType(
                "AI Assistant",
                "You are a friendly and helpful assistant.",
                "General AI Assitant.",
                null,
                RemoveAllTools));
        aiTypes.Add(
             "Shopper", new AIType(
                "Shopper",
                "You are shopper AI in game. You recieve request from customer. You sell your items from your stock. And you also can buy items from your customer.",
                "Shopper AI in game. Controll money and store.",
                null,
                OnShopperSelected));
        aiTypes.Add(
            "Character Generator", new AIType(
                "Character Generator",
                "Generate character sheet from input lines. Return only JSON.",
                "Generate character sheet from your character story telling.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        STR = new { type = "integer", minimum = 1, maximum = 10, description = "Strength parameter" },
                        AGL = new { type = "integer", minimum = 1, maximum = 10, description = "Agility parameter" },
                        MGK = new { type = "integer", minimum = 1, maximum = 10, description = "Magic parameter" },
                    },
                    required = new[] { "STR", "AGL", "MGK" }
                },
                RemoveAllTools)
        );
        aiTypes.Add(
             "AIアシスタント", new AIType(
                "AIアシスタント",
                "あなたは有能でフレンドリーなAIアシスタントです。応答は必ず日本語でしてください。",
                "一般的なAIアシスタント。",
                null,
                RemoveAllTools));
        aiTypes.Add(
             "店員", new AIType(
                "店員",
                "あなたはRPGの気さくなおじさん店員です。応答は必ず日本語で、おじさんらしく返答してください。あなたは自分の店の商品を売ることができます。また、プレイヤーから商品を買い取ることもできます。",
                "RPGの気さくなおじさん店員。店の在庫を管理し、販売、購入することができる。",
                null,
                OnShopperSelectedJapanese));
        aiTypes.Add(
            "キャラクターシート生成器", new AIType(
                "キャラクターシート生成器",
                "入力されたキャラクターの個性に合わせて、キャラクターシートを生成してください。\n"
                + "力の強さ（STR）、俊敏さ（AGL）、魔法力（MGK）を1～10で設定してください。\n"
                + "なお、STR、AGLは5が一般的な成人男性で、オリンピック選手が8、子供が2です。魔法力は普通は1で、平均的な魔法使いだと5、伝説の魔法使いが9です。\n"
                + "JSONだけを返却してください。",
                "あなたの作りたいキャラクターを紹介してください。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        STR = new { type = "integer", minimum = 1, maximum = 10, description = "力の強さのパラメータ。1～10で、5が成人男性の平均。" },
                        AGL = new { type = "integer", minimum = 1, maximum = 10, description = "俊敏さのパラメータ。1～10で、5が成人男性の平均。" },
                        MGK = new { type = "integer", minimum = 1, maximum = 10, description = "魔力のパラメータ。1～10で、5が魔法使いの平均。一般的には1。" },
                    },
                    required = new[] { "STR", "AGL", "MGK" }
                },
                RemoveAllTools)
        );
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
            LoadHistory();
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
        var sendAsync = root.Q<Button>("SendAsync");
        var sendStreaming = root.Q<Button>("SendStreaming");
        var promptInput = root.Q<TextField>("PromptInput");
        var clearHistory = root.Q<Button>("ClearHistory");
        var aiTypeDropdown = root.Q<DropdownField>("AITypeDropdown");
        sendAsync.clicked += OnSendAsyncClicked;
        sendStreaming.clicked += OnSendStreamingClicked;
        clearHistory.clicked += OnClearHistoryClicked;
        aiTypeDropdown.RegisterValueChangedCallback(OnAITypeDropdownValueChanged);
        sendAsync.SetEnabled(true);
        sendStreaming.SetEnabled(true);
        promptInput.SetEnabled(true);
        clearHistory.SetEnabled(true);
        aiTypeDropdown.SetEnabled(true);
        aiTypeDropdown.index = 0;
    }

    private void RemoveUIEvent()
    {
        var root = UIDocument.rootVisualElement;
        var sendAsync = root.Q<Button>("SendAsync");
        var sendStreaming = root.Q<Button>("SendStreaming");
        var clearHistory = root.Q<Button>("ClearHistory");
        var aiTypeDropdown = root.Q<DropdownField>("AITypeDropdown");
        sendAsync.clicked -= OnSendAsyncClicked;
        sendStreaming.clicked -= OnSendStreamingClicked;
        clearHistory.clicked -= OnClearHistoryClicked;
        aiTypeDropdown.UnregisterValueChangedCallback(OnAITypeDropdownValueChanged);
    }

    private void OnSendAsyncClicked()
    {
        var root = UIDocument.rootVisualElement;
        var promptInput = root.Q<TextField>("PromptInput");
        var result = root.Q<Label>("Result");
        string prompt = promptInput.value;

        StartCoroutine(client.SendMessageAsync(
            prompt,
            (chatResponse, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.ErrorType} - {error.Message}");
                    Debug.LogError($"  HttpStatus: {error.HttpStatus}");
                    result.text = "error occured...";
                    return;
                }
                Debug.Log($"✓ Response received: {chatResponse.Content}");
                result.text = chatResponse.Content;
            },
            new ChatRequestOptions {
                SessionId = currentAIType.Name, 
                SystemPrompt = currentAIType.SystemPrompt, 
                FormatSchema = currentAIType.FormatSchema
            }
        ));
    }

    private void OnSendStreamingClicked()
    {
        var root = UIDocument.rootVisualElement;
        var promptInput = root.Q<TextField>("PromptInput");
        var result = root.Q<Label>("Result");
        string prompt = promptInput.value;

        StartCoroutine(client.SendMessageStreamingAsync(
            prompt,
            (chatResponse, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"✗ Error: {error.ErrorType} - {error.Message}");
                    Debug.LogError($"  HttpStatus: {error.HttpStatus}");
                    result.text = "error occured...";
                    return;
                }
                if (!chatResponse.IsFinal)
                {
                    result.text = chatResponse.Content;
                    Debug.Log($"...streaming chunk: {chatResponse.Content}");
                    return;
                }
                Debug.Log($"✓ Response received: {chatResponse.Content}");
                result.text = chatResponse.Content;
            },
            new ChatRequestOptions
            {
                SessionId = currentAIType.Name,
                SystemPrompt = currentAIType.SystemPrompt,
                FormatSchema = currentAIType.FormatSchema
            }
        ));
    }

    private void OnClearHistoryClicked()
    {
        client.ClearAllMessages();
    }

    private void OnAITypeDropdownValueChanged(ChangeEvent<string> changeEvent)
    {
        var aiTypeDescription = UIDocument.rootVisualElement.Q<Label>("AITypeDescription");
        if (aiTypes.TryGetValue(changeEvent.newValue, out var aiType))
        {
            currentAIType = aiType;
            aiTypeDescription.text = aiType.Description;
            aiType.OnChangeCallback.Invoke();
        }
    }

    // Custom callbacks for Shopper AI
    int money = 1000;

    private class ShopItem { 
        public string Name { get; private set; }
        public int Price { get; private set; }
        public ShopItem(string name, int price)
        {
            Name = name;
            Price = price;
        }
    }

    private readonly List<ShopItem> shopItems = new ()
    {
        new ShopItem("Health Potion", 50),
        new ShopItem("Health Potion", 50),
        new ShopItem("Mana Potion", 30),
        new ShopItem("Mana Potion", 30),
        new ShopItem("Sword", 200),
        new ShopItem("Shield", 150),
    };

    private readonly List<ShopItem> enableToSellItems = new()
    {
        new ShopItem("Health Potion", 25),
        new ShopItem("Mana Potion", 15),
        new ShopItem("Sword", 100),
        new ShopItem("Shield", 75),
        new ShopItem("Bow", 90),
        new ShopItem("Arrow Bundle", 10),
        new ShopItem("Helmet", 60),
        new ShopItem("Armor", 150),
    };


    private readonly List<ShopItem> shopItemsJapanese = new()
    {
        new ShopItem("回復ポーション", 50),
        new ShopItem("回復ポーション", 50),
        new ShopItem("魔法ポーション", 30),
        new ShopItem("魔法ポーション", 30),
        new ShopItem("はがねの剣", 200),
        new ShopItem("はがねの盾", 150),
    };

    private readonly List<ShopItem> enableToSellItemsJapanese = new()
    {
        new ShopItem("回復ポーション", 25),
        new ShopItem("魔法ポーション", 15),
        new ShopItem("はがねの剣", 100),
        new ShopItem("はがねの盾", 75),
        new ShopItem("木の弓", 90),
        new ShopItem("木の矢束", 10),
        new ShopItem("はがねの兜", 60),
        new ShopItem("はがねの鎧", 150),
    };

    private List<ShopItem> GetEnableToSellItems()
    {
        return enableToSellItems;
    }

    private List<ShopItem> GetShopItems()
    {
        return shopItems;
    }

    private string BuyItem(string itemName)
    {
        var item = shopItems.Find(i => i.Name == itemName);
        if (item != null)
        {
            if (money >= item.Price)
            {
                money -= item.Price;
                shopItems.Remove(item);
                return "Sold " + itemName + " for " + item.Price;
            }
            else
            {
                return "Not enough money to buy " + itemName;
            }            
        }
        return "Item " + itemName + " not found";
    }

    private string SellItem(string itemName, int price)
    {
        money += price;
        shopItems.Add(new ShopItem(itemName, price * 2));
        return "Bought " + itemName + " for " + price;
    }


    private List<ShopItem> GetEnableToSellItemsJapanese()
    {
        return enableToSellItemsJapanese;
    }

    private List<ShopItem> GetShopItemsJapanese()
    {
        return shopItemsJapanese;
    }

    private string BuyItemJapanese(string itemName)
    {
        var item = shopItemsJapanese.Find(i => i.Name == itemName);
        if (item != null)
        {
            if (money >= item.Price)
            {
                money -= item.Price;
                shopItems.Remove(item);
                return itemName + "を" + item.Price + "で売った";
            }
            else
            {
                return itemName + "を買う十分なお金がない";
            }
        }
        return itemName + "は見つからない";
    }

    private string SellItemJapanese(string itemName, int price)
    {
        money += price;
        shopItemsJapanese.Add(new ShopItem(itemName, price * 2));
        return itemName + "を" + price + "で買い取った";
    }

    private void RemoveAllTools() {
        client.RemoveAllTools();
    }

    private void OnShopperSelected()
    {
        client.RemoveAllTools();
        client.RegisterTool("GetShopItems", "Get list of items in your shop", (Func<List<ShopItem>>)GetShopItems);
        client.RegisterTool("GetEnableToSellItems", "Get list of items that can be sold to your shop", (Func<List<ShopItem>>)GetEnableToSellItems);
        client.RegisterTool("BuyItem", "Buy an item from your shop", (Func<string, string>)BuyItem);
        client.RegisterTool("SellItem", "Sell an item to your shop", (Func<string, int, string>)SellItem);
    }

    private void OnShopperSelectedJapanese()
    {
        client.RemoveAllTools();
        client.RegisterTool("GetShopItems", "店の在庫のリストを取得する", (Func<List<ShopItem>>)GetShopItemsJapanese);
        client.RegisterTool("GetEnableToSellItems", "店で買い取り可能な商品のリストを取得する", (Func<List<ShopItem>>)GetEnableToSellItemsJapanese);
        client.RegisterTool("BuyItem", "商品を購入する（店が販売する）", (Func<string, string>)BuyItemJapanese);
        client.RegisterTool("SellItem", "商品を売る（店が買い取る）", (Func<string, int, string>)SellItemJapanese);
    }

    private void LoadHistory()
    {
        try
        {
            client.LoadAllSessions("history.json");
            Debug.Log("✓ History loaded");
        } catch (Exception e)
        {
            Debug.LogWarning("✗ Failed to load history: " + e.Message);
        }
    }

    private void SaveHistory() {
        client.SaveAllSessions("history.json");
        Debug.Log("✓ History saved");
    }

    public void OnDisable()
    {
        RemoveUIEvent();
        SaveHistory();
    }
}
