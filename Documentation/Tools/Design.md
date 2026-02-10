# Tools（Function Calling）機能 - 概要ガイド

## ツール（Function Calling）とは

**ツール**（Tools / Function Calling）は、LLM がゲーム内の機能やデータに直接アクセスできるようにする仕組みです。LLM が「プレイヤーの所持金を確認したい」「アイテムを購入したい」などのアクションが必要だと判断すると、あなたが登録した C# 関数を自動的に呼び出します。

### 具体例

プレイヤーが NPC に「体力ポーションを3個買いたい」と話しかけた場合：

1. LLM が「購入機能が必要」と判断
2. 登録済みの `BuyItem` 関数を自動呼び出し（`BuyItem("health_potion", 3)`）
3. 関数が実行結果を返す（例：「購入成功。所持金: 450G」）
4. LLM がその結果を自然な会話文に変換（「体力ポーションを3個購入しました。残金は450Gです」）

これにより、NPC との会話だけでゲーム内の実際の処理を実行できます。

---

## 基本的な使い方

### 1. ツールを登録する

`RegisterTool()` で C# 関数を LLM に使えるように登録します：

```csharp
// 足し算ツールの登録
client.RegisterTool(
    name: "add_numbers",
    description: "Add two numbers together",
    callback: (Func<int, int, int>)((int a, int b) => a + b)
);
```

**重要なポイント：**
- `name`: ツール名（LLM が呼び出す際に使用）
- `description`: ツールの説明（LLM がいつ使うべきか判断する材料）
- `callback`: 実際に実行される関数（`Func<...>` 型でキャスト必須）

### 2. LLM にメッセージを送る

通常通りメッセージを送信するだけ。LLM が必要と判断すれば自動的にツールを呼び出します：

```csharp
StartCoroutine(client.SendMessageAsync(
    "What is 125 + 378?",
    (response, error) =>
    {
        Debug.Log(response.Content);
        // 出力例：「125 + 378 = 503 です」
    }
));
```

---

## アーキテクチャ概要
---

## アーキテクチャ概要

LLM とゲーム機能がどのように連携するかの全体像：

```
プレイヤー → LLM → 「ツールが必要」と判断 → 登録済み関数を自動実行 → 結果を取得 → LLM が最終回答
```

詳細フロー：

```
┌─────────────────────────────┐
│     OllamaClient            │
│  (ToolManager組み込み)       │
├─────────────────────────────┤
│ - RegisterTool()            │  ← ツール登録
│ - SendMessageAsync()        │  ← メッセージ送信
│ - ツール自動実行            │  ← LLM判断で自動実行
└─────────────────────────────┘
        ↓
┌─────────────────────────────┐
│   Ollama API                │
├─────────────────────────────┤
│ - ツール情報を含むリクエスト  │
│ - tool_callsレスポンス       │
│ - 実行結果を含む再リクエスト  │
└─────────────────────────────┘
```

---

## 実用的な使用パターン

---

## 実用的な使用パターン

### パターン1: シンプルな計算ツール（1パラメータ）

最もシンプルな例。パラメータが1つで、戻り値も自動変換されます。

```csharp
// 計算式を評価するツール
client.RegisterTool(
    name: "calculator",
    description: "Evaluate a math expression like '5+3' or '10*2'",
    callback: (Func<string, object>)((string expression) =>
    {
        try
        {
            // DataTableで計算（実装例）
            return new System.Data.DataTable().Compute(expression, null);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    })
);

// 使用例
// プレイヤー: "5 + 3はいくつ？"
// LLM: calculator("5+3") を呼び出し → 8 → 「5 + 3は8です」
```

**ポイント：**
- スキーマは自動生成される
- `object` 型の戻り値も自動的に文字列化される

### パターン2: 複数パラメータ＆デフォルト値

パラメータが複数あり、一部は省略可能な場合。
```csharp
var client = new OllamaClient(config);

// Tool1 登録: 計算機能
// → inputSchema は自動生成（リフレクション使用）
// → 戻り値も自動的に文字列変換（ToString() 不要）
client.RegisterTool(
    name: "calculator",
    description: "Evaluate a math expression",
    callback: (Func<string, object>)((string expression) =>
    {
        try
        {
            // 戻り値は object でも自動的に文字列化される
            return new System.Data.DataTable().Compute(expression, null);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    })
);
```

```csharp
// Web検索ツール（maxResultsは省略可）
client.RegisterTool(
    name: "search_web",
    description: "Search the web for information",
    callback: (Func<string, int, string>)((string query, int maxResults = 5) =>
    {
        // 実際の検索処理を実行
        var results = SearchEngine.Search(query, maxResults);
        return $"Found {results.Count} results for '{query}'";
    })
);

// 使用例
// プレイヤー: "Unityの最新情報を3件検索して"
// LLM: search_web("Unity", 3) を呼び出し
// プレイヤー: "Pythonについて調べて"
// LLM: search_web("Python") を呼び出し（maxResultsは5がデフォルト）
```

**ポイント：**
- デフォルト値のあるパラメータは、LLM側で「省略可能」と認識される
- スキーマ自動生成時に `required` から除外される

### パターン3: パラメータに詳細説明を付ける

`[ToolParameter]` 属性でパラメータの説明を追加すると、LLM の判断精度が向上します。

```csharp
using EasyLocalLLM.LLM.Core;

// 天気情報取得ツール
client.RegisterTool(
    name: "get_weather",
    description: "Get current weather information for a city",
    callback: (Func<string, string, string>)((
        [ToolParameter("City name (e.g., Tokyo, New York)")] string city,
        [ToolParameter("Temperature unit: celsius or fahrenheit")] string unit = "celsius"
    ) =>
    {
        // 実際の天気情報取得処理
        var weather = WeatherAPI.GetWeather(city, unit);
        return $"Weather in {city}: {weather.Temperature}°{(unit == "celsius" ? "C" : "F")}, {weather.Condition}";
    })
);

// 使用例
// プレイヤー: "東京の天気を華氏で教えて"
// LLM: get_weather("Tokyo", "fahrenheit") を呼び出し
```

**ポイント：**
- `[ToolParameter]` で各パラメータの説明を明確化
- LLM がより正確に判断できるようになる

### パターン4: プリミティブ型の戻り値（自動変換）

戻り値が `int`、`bool`、`double` などのプリミティブ型の場合も自動的に文字列化されます。

```csharp
// 足し算ツール（intを返す）
client.RegisterTool(
    name: "add_numbers",
    description: "Add two numbers together",
    callback: (Func<int, int, int>)((int a, int b) => a + b)
);

// 偶数判定ツール（boolを返す）
client.RegisterTool(
    name: "is_even",
    description: "Check if a number is even",
    callback: (Func<int, bool>)((int number) => number % 2 == 0)
);

// 割り算ツール（doubleを返す）
client.RegisterTool(
    name: "divide",
    description: "Divide two numbers",
    callback: (Func<double, double, double>)((double a, double b) => a / b)
);

// 使用例
// プレイヤー: "10 + 5はいくつ？"
// LLM: add_numbers(10, 5) → 15 → 「15です」
// プレイヤー: "8は偶数？"
// LLM: is_even(8) → true → 「はい、8は偶数です」

**ポイント：**
- `.ToString()` を書く必要なし
- ライブラリが自動的に文字列化

### パターン5: カスタムオブジェクトの戻り値（JSON変換）

オブジェクトや配列を返す場合、自動的に JSON 文字列にシリアライズされます。

```csharp
// ユーザー情報取得ツール（オブジェクトを返す）
client.RegisterTool(
    name: "get_player_info",
    description: "Get player information by player ID",
    callback: (Func<string, object>)((string playerId) =>
    {
        var player = PlayerManager.GetPlayer(playerId);
        return new
        {
            id = player.Id,
            name = player.Name,
            level = player.Level,
            gold = player.Gold,
            health = new { current = player.HP, max = player.MaxHP }
        };
    })
);

// インベントリ取得ツール（配列を返す）
client.RegisterTool(
    name: "get_inventory",
    description: "Get all items in player's inventory",
    callback: (Func<List<object>>)(() =>
    {
        return Inventory.GetAllItems().Select(item => new
        {
            id = item.Id,
            name = item.Name,
            quantity = item.Quantity
        }).ToList();
    })
);

// 使用例
// プレイヤー: "私のステータスを教えて"
// LLM: get_player_info("player1") を呼び出し
// → {"id":"player1","name":"勇者","level":15,...} がJSON文字列で返る
// → LLMが自然な文章に変換: "あなたはレベル15の勇者で、所持金は500Gです…"
```

**ポイント：**
- オブジェクトや配列も自動的に JSON 化される
- LLM が構造化データを理解して自然な文章に変換

### パターン6: ゲーム内の実用例

実際のゲームでの使用例：

```csharp
public class NPCShopkeeper : MonoBehaviour
{
    private OllamaClient client;
    
    void Start()
    {
        client = LLMClientFactory.CreateOllamaClient(config);
        
        // ショップのアイテム一覧を取得
        client.RegisterTool(
            name: "GetShopItems",
            description: "Get list of items available in the shop",
            callback: (Func<List<ShopItem>>)(() => ShopManager.GetAvailableItems())
        );
        
        // アイテムを購入
        client.RegisterTool(
            name: "BuyItem",
            description: "Buy an item from the shop",
            callback: (Func<string, int, string>)((string itemName, int quantity) =>
            {
                var result = ShopManager.BuyItem(itemName, quantity);
                if (result.Success)
                    return $"Purchased {quantity}x {itemName}. Remaining gold: {Player.Gold}";
                else
                    return $"Error: {result.ErrorMessage}";
            })
        );
    }
}

// 使用例
// プレイヤー: "何が売ってる？"
// → LLM が GetShopItems() を呼び出し、一覧を取得して自然な文章で説明
//
// プレイヤー: "体力ポーションを3個ください"
// → LLM が BuyItem("health_potion", 3) を呼び出し、購入処理を実行
```

---

## スキーマ自動生成の仕組み

### 自動生成とは

ツールを登録する際、`inputSchema`（パラメータの型情報）を手動で書く必要はありません。ライブラリがリフレクションを使って自動的に生成します。

### サポートされる型

| C# 型 | JSON Schema 型 | 備考 |
|-------|---------------|------|
| `string` | `string` | 文字列 |
| `int`, `long` | `integer` | 整数 |
| `double`, `float` | `number` | 浮動小数点数 |
| `bool` | `boolean` | 真偽値 |
| `List<T>`, `T[]` | `array` | 配列 |
| `DateTime` | `string` | ISO 8601 形式 |
| `Guid` | `string` | GUID 文字列 |

### 自動生成の例

**コード：**
```csharp
client.RegisterTool(
    name: "search",
    description: "Search for items",
    callback: (Func<string, int, string>)((string query, int maxResults = 5) => "...")
);
```

**自動生成されるスキーマ：**
```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string",
      "description": "query"
    },
    "maxResults": {
      "type": "integer",
      "description": "maxResults"
    }
  },
  "required": ["query"]
}
```

`maxResults` はデフォルト値があるため `required` から除外されます。

### 手動スキーマ指定

複雑な入力（ネストしたオブジェクトなど）が必要な場合は、手動でスキーマを指定できます：

```csharp
client.RegisterTool(
    name: "create_character",
    description: "Create a new game character",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            name = new { type = "string", description = "Character name" },
            stats = new
            {
                type = "object",
                properties = new
                {
                    strength = new { type = "integer", minimum = 1, maximum = 10 },
                    agility = new { type = "integer", minimum = 1, maximum = 10 }
                },
                required = new[] { "strength", "agility" }
            }
        },
        required = new[] { "name", "stats" }
    },
    callback: (Func<string, string>)((string json) =>
    {
        // JSON を手動パース
        var data = JObject.Parse(json);
        // 処理...
        return "Character created";
    })
);
```

詳細な手動スキーマの書き方は [InputSchema_Examples.md](InputSchema_Examples.md) を参照してください。

---

## 処理フローの理解

### 基本的な流れ

```
1. ツール登録
   RegisterTool() で関数を登録
   ↓
2. メッセージ送信
   SendMessageAsync("何か質問") を呼び出し
   ↓
3. LLM が判断
   「この質問にはツールが必要」と判断
   ↓
4. ツール自動実行
   登録済み関数を自動呼び出し
   ↓
5. 結果を LLM に返す
   実行結果を含めて再度リクエスト
   ↓
6. 最終回答
   LLM が結果を自然な文章に変換して返答
```

### 詳細フロー（内部動作）

```
[ユーザー] SendMessageAsync("125 + 378は？")
    ↓
[OllamaClient] リクエスト生成
    - メッセージ: "125 + 378は？"
    - 利用可能ツール: [add_numbers]
    ↓
[Ollama API] tool_call を返す
    - tool_name: "add_numbers"
    - arguments: {"a": 125, "b": 378}
    ↓
[ToolManager] ツール実行
    - JSON Arguments → C# 型に変換
    - callback(125, 378) を呼び出し
    - 戻り値: 503 (int)
    - int → "503" (string) に変換
    ↓
[OllamaClient] 結果を含めて再リクエスト
    - メッセージ履歴
      - user: "125 + 378は？"
      - tool_result: "503"
    ↓
[Ollama API] 最終回答を生成
    - "125 + 378 = 503 です"
    ↓
[ユーザー] ChatResponse を受け取る
```

### 複数回のツール呼び出し

LLM は必要に応じて複数回ツールを呼び出すことができます：

```
プレイヤー: "体力ポーションを3個買って、残金を教えて"
↓
LLM: BuyItem("health_potion", 3) を呼び出し
→ "購入成功。残金: 450G"
↓
LLM: 最終回答を生成
→ "体力ポーションを3個購入しました。残金は450Gです"
```

無限ループを防ぐため、最大反復回数（デフォルト: 5回）が設定されています。

---

## ベストプラクティス

### 1. ツール名と説明を明確に

LLM がいつツールを使うべきか判断するため、明確な名前と説明が重要です。

**❌ 悪い例：**
```csharp
client.RegisterTool(
    name: "func1",
    description: "Does something",
    callback: ...
);
```

**✅ 良い例：**
```csharp
client.RegisterTool(
    name: "get_player_health",
    description: "Get the current and maximum health points of a player by player ID",
    callback: ...
);
```

### 2. パラメータ説明を追加する

`[ToolParameter]` で説明を付けると LLM の判断精度が向上します。

```csharp
client.RegisterTool(
    name: "move_character",
    description: "Move character to a location",
    callback: (Func<string, float, float, string>)((
        [ToolParameter("Character ID or name")] string characterId,
        [ToolParameter("X coordinate")] float x,
        [ToolParameter("Y coordinate")] float y
    ) => { ... })
);
```

### 3. エラーハンドリングを忘れずに

ツール内でエラーが発生した場合、適切なエラーメッセージを返しましょう。

```csharp
client.RegisterTool(
    name: "buy_item",
    description: "Purchase an item",
    callback: (Func<string, int, string>)((string itemName, int quantity) =>
    {
        try
        {
            if (quantity <= 0)
                return "Error: Quantity must be positive";
            
            if (!Shop.HasItem(itemName))
                return $"Error: Item '{itemName}' not found in shop";
            
            if (Player.Gold < Shop.GetPrice(itemName) * quantity)
                return "Error: Not enough gold";
            
            Shop.BuyItem(itemName, quantity);
            return $"Success: Purchased {quantity}x {itemName}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    })
);
```

### 4. プリミティブ型を優先する

自動スキーマ生成は `string`、`int`、`bool` などのプリミティブ型で最も安定します。

**✅ 推奨：**
```csharp
callback: (Func<string, int, string>)((string itemName, int quantity) => ...)
```

**⚠️ 複雑な型は手動スキーマ推奨：**
```csharp
// カスタムクラスを入力にする場合は手動スキーマ指定
callback: (Func<string, string>)((string json) =>
{
    var data = JsonConvert.DeserializeObject<ComplexData>(json);
    ...
})
```

### 5. DebugMode でスキーマを確認

開発時は `DebugMode = true` にすると、自動生成されたスキーマがログ出力されます。

```csharp
var config = new OllamaConfig
{
    DebugMode = true  // スキーマ確認用
};
```

---

## トラブルシューティング

### Q1: ツールが呼ばれない

**原因：**
- ツールの `description` が不明瞭
- LLM がツールの必要性を理解できていない

**解決策：**
- より詳細な `description` を書く
- システムプロンプトでツールの使用を促す

```csharp
var options = new ChatRequestOptions
{
    SystemPrompt = "You are a helpful assistant. " +
                   "When the user asks about their inventory, use the get_inventory tool. " +
                   "When they want to buy items, use the buy_item tool."
};
```

### Q2: 型変換エラーが発生する

**原因：**
- LLM が想定外の型でパラメータを渡している

**解決策：**
- `DebugMode` でログを確認
- パラメータの型制約を `[ToolParameter]` で明示

```csharp
callback: (Func<string, int, string>)((
    [ToolParameter("Player ID (must be a string)")] string playerId,
    [ToolParameter("Amount (must be a positive integer)")] int amount
) => ...)
```

### Q3: 無限ループになる

**原因：**
- LLM が同じツールを繰り返し呼び出している

**解決策：**
- `MaxToolIterations` を調整
- ツールが明確な結果を返すようにする

```csharp
var options = new ChatRequestOptions
{
    MaxToolIterations = 3  // デフォルトは5
};
```

### Q4: 複雑なオブジェクトを入力にしたい

**原因：**
- 自動スキーマ生成はプリミティブ型に最適化されている

**解決策：**
- 手動でスキーマを指定し、JSON 文字列として受け取る

```csharp
client.RegisterTool(
    name: "complex_tool",
    description: "Tool with complex input",
    inputSchema: new { /* 手動スキーマ */ },
    callback: (Func<string, string>)((string json) =>
    {
        var data = JObject.Parse(json);
        // 処理...
    })
);
```

詳細は [InputSchema_Examples.md](InputSchema_Examples.md) を参照。

---

## まとめ

EasyLocalLLM のツール機能を使うと：

✅ LLM がゲーム内の機能を直接呼び出せる  
✅ スキーマは自動生成されるため手間いらず  
✅ 戻り値も自動変換されるため簡潔に書ける  
✅ 複雑なゲームロジックと自然な会話を組み合わせ可能  

**次のステップ:**
- [InputSchema_Examples.md](InputSchema_Examples.md) - 手動スキーマの詳細例
- [Samples/SimpleChat.cs](../../Samples/SimpleChat.cs) - 実装サンプル
- [API_Reference.md](../API_Reference.md) - 完全な API リファレンス

