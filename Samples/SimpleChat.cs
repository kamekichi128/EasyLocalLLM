using UnityEngine;
using EasyLocalLLM.LLM.Core;
using EasyLocalLLM.LLM.Ollama;
using EasyLocalLLM.LLM.Factory;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

/// <summary>
/// Simple chat screen sample
/// Sends prompts to the loaded LLM model and receives responses
/// Provides multiple AI types that can be switched to change system prompts and tools
/// </summary>
public class SimpleChat : MonoBehaviour
{
    public UIDocument UIDocument;

    private OllamaClient client;

    private class AIType
    {
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
                "Generate character sheet from input lines.\n"
                + "Strength (STR), Agility (AGL), and Magic (MGK) should be set from 1 to 10.\n"
                + "STR and AGL: 5 is average adult male, 8 is Olympic athlete, 2 is child. Magic is usually 1, 5 is average mage, 9 is legendary mage.\n"
                + "Return only JSON.",
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
    }

    private void InitializeEasyLocalLLMClient()
    {
        // Initialize client
        // If you have ollama.exe running to automatically start the server, please stop it or specify a port that is not in use.
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            DefaultModelName = "kamekichi128/qwen3-4b-instruct-2507",
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
            StartCoroutine(client.LoadModelRunnable(client.GetConfig().DefaultModelName, true, OnModelRunnable));
        }
        else
        {
            Debug.LogError("✗ Failed to initialize Ollama server.");
        }
    }

    private void OnModelRunnable(LoadModelProgress progress)
    {
        if (progress.IsCompleted)
        {
            if (progress.IsSuccessed)
            {
                Debug.Log("✓ Model is runnable.");
                LoadHistory();
                EnableUI();
            }
            else
            {
                Debug.LogError($"✗ Model failed to load: {progress.Message}");
            }
        }
        Debug.Log($"Model loading progress: {progress.Progress * 100}% | {progress.Message}");
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
            new ChatRequestOptions
            {
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

    private class ShopItem
    {
        public string Name { get; private set; }
        public int Price { get; private set; }
        public ShopItem(string name, int price)
        {
            Name = name;
            Price = price;
        }
    }

    private readonly List<ShopItem> shopItems = new()
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

    private List<ShopItem> GetEnableToSellItems()
    {
        return enableToSellItems;
    }

    private List<ShopItem> GetShopItems()
    {
        return shopItems;
    }

    private string SellItem(string itemName)
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

    private string BuyItem(string itemName, int price)
    {
        money += price;
        shopItems.Add(new ShopItem(itemName, price * 2));
        return "Bought " + itemName + " for " + price;
    }

    private void RemoveAllTools()
    {
        client.RemoveAllTools();
    }

    private void OnShopperSelected()
    {
        client.RemoveAllTools();
        client.RegisterTool("GetShopItems", "Get list of items in your shop", (Func<List<ShopItem>>)GetShopItems);
        client.RegisterTool("GetEnableToSellItems", "Get list of items that can be sold to your shop", (Func<List<ShopItem>>)GetEnableToSellItems);
        client.RegisterTool("SellItem", "Sell an item from your shop", (Func<string, string>)SellItem);
        client.RegisterTool("BuyItem", "Buy an item to your shop", (Func<string, int, string>)BuyItem);
    }

    private void LoadHistory()
    {
        try
        {
            client.LoadAllSessions("history.json");
            Debug.Log("✓ History loaded");
        }
        catch (Exception e)
        {
            Debug.LogWarning("✗ Failed to load history: " + e.Message);
        }
    }

    private void SaveHistory()
    {
        client.SaveAllSessions("history.json");
        Debug.Log("✓ History saved");
    }

    public void OnDisable()
    {
        RemoveUIEvent();
        SaveHistory();
    }
}
