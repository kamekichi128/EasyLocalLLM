# Ollama Tools 対応 - 設計ドキュメント

## 目的
Ollama の Function Calling（Tools） 機能に対応し、ユーザーが任意のコールバック関数をクライアントに登録して、LLM が tool_call を返した際に自動実行できるようにする。

---

## 設計概要

### 1. アーキテクチャ
```
┌─────────────────────────────┐
│     OllamaClient            │
│  (ToolManager組み込み)       │
├─────────────────────────────┤
│ - RegisterTool()            │
│ - UnregisterTool()          │
│ - RemoveAllTools()          │
│ - Tool自動実行ロジック      │
└─────────────────────────────┘
        ↓
┌─────────────────────────────┐
│  Tool定義 & Tool Call       │
├─────────────────────────────┤
│ - ToolDefinition            │
│ - ToolCall                  │
│ - ChatMessage拡張           │
└─────────────────────────────┘
        ↓
┌─────────────────────────────┐
│   Ollama API通信            │
├─────────────────────────────┤
│ - tools パラメータ送信      │
│ - tool_calls レスポンス処理 │
│ - tool_results 送信         │
└─────────────────────────────┘
```

### 2. ユーザー使用パターン（改善版：スキーマ自動生成）

#### **パターンA: シンプル（パラメータ1つ）**
```csharp
var client = new OllamaClient(config);

// Tool1 登録: 計算機能
// → inputSchema は自動生成（リフレクション使用）
// → 戻り値も自動的に文字列変換（ToString() 不要）
client.RegisterTool(
    name: "calculator",
    description: "Evaluate a math expression",
    callback: (string expression) =>
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
    }
);
```

#### **パターンB: 複数パラメータ＆デフォルト値**
```csharp
// Tool2 登録: Web検索
// → inputSchema は自動生成
// → maxResults は省略可（デフォルト値あり）
client.RegisterTool(
    name: "search_web",
    description: "Search the web",
    callback: (string query, int maxResults = 5) =>
    {
        // LLM側では maxResults は省略可と認識
        // string を返せば、そのまま使用される
        return $"Found {maxResults} results for '{query}'";
    }
);
```

#### **パターンC: パラメータ説明を付ける場合**
```csharp
// Tool3 登録: 天気取得
// → 各パラメータの説明を Attribute で指定
client.RegisterTool(
    name: "get_weather",
    description: "Get weather for a city",
    callback: (
        [ToolParameter("City name (e.g., Tokyo, New York)")] string city,
        [ToolParameter("Temperature unit: celsius or fahrenheit", "celsius")] string unit = "celsius"
    ) =>
    {
        return $"Weather in {city}: 20°{(unit == "celsius" ? "C" : "F")}";
    }
);
```

#### **パターンD: プリミティブ型の戻り値（自動変換）**
```csharp
// Tool4 登録: 数値計算
// → int を返しても、ライブラリが自動的に文字列化
client.RegisterTool(
    name: "add_numbers",
    description: "Add two numbers",
    callback: (int a, int b) => a + b  // ToString() 不要！
);

// Tool5 登録: 真偽値の戻り値
client.RegisterTool(
    name: "is_even",
    description: "Check if number is even",
    callback: (int number) => number % 2 == 0  // bool も自動変換
);

#### **パターンE: カスタムオブジェクトの戻り値（JSON変換）**
```csharp
// Tool6 登録: カスタムオブジェクトを返す
// → オブジェクトは自動的に JSON にシリアライズされる
client.RegisterTool(
    name: "get_user_info",
    description: "Get user information",
    callback: (string userId) => new
    {
        id = userId,
        name = "John Doe",
        age = 30,
        email = "john@example.com"
    }  // 自動的に JSON 文字列に変換される
);

// メッセージ送信 → LLMがtool_callを返す → 自動実行 → 結果を含めて再送
var response = await client.SendMessageTaskAsync(
    "What is 5 + 3?",
    options: new ChatRequestOptions()
);
```

---

## 新規ファイル

### 1. `Core/ToolParameterAttribute.cs` ★ 新規
パラメータの説明を指定するための Attribute。

```csharp
[AttributeUsage(AttributeTargets.Parameter)]
public class ToolParameterAttribute : Attribute
{
    public string Description { get; set; }
    
    public ToolParameterAttribute(string description)
    {
        Description = description;
    }
}
```

### 2. `Core/ToolDefinition.cs`
ツールの定義を表すクラス。Ollama API の tools パラメータに変換される。

**内容:**
- `Name` (string): ツール名（一意）
- `Description` (string): ツール説明
- `InputSchema` (object/JObject): JSON Schema形式（自動生成または手動指定）
- `Callback` (Delegate): ユーザー定義のコールバック関数（任意のシグネチャ対応、任意の戻り値型対応）
- `ParameterInfos` (List<ParameterInfo>): パラメータの型情報（型変換用）
- `ReturnType` (Type): 戻り値の型情報（文字列変換用）

### 3. `Core/ToolCall.cs`
LLM がツール呼び出しを要求した際の情報を表すクラス。

**内容:**
- `ToolName` (string): ツール名
- `ToolCallId` (string): Tool call の一意ID（Ollama レスポンス内に含まれる）
- `Arguments` (string): JSON形式のツール入力パラメータ

### 4. `Core/ToolSchemaGenerator.cs` ★ 新規
Delegate のシグネチャからリフレクションで JSON Schema を自動生成するユーティリティ。

**主要メソッド:**
```csharp
public static class ToolSchemaGenerator
{
    /// <summary>
    /// Delegate のシグネチャから JSON Schema を自動生成
    /// </summary>
    public static JObject GenerateSchema(Delegate callback);
    
    /// <summary>
    /// パラメータ情報を取得
    /// </summary>
    public static List<ParameterInfo> GetParameterInfos(Delegate callback);
}
```

**生成ロジック:**
- `string` → `{ type: "string" }`
- `int` / `long` → `{ type: "integer" }`
- `double` / `float` → `{ type: "number" }`
- `bool` → `{ type: "boolean" }`
- `List<T>` / `T[]` → `{ type: "array", items: {...} }`
- デフォルト値あり → `required` から除外
- `[ToolParameter]` Attribute → description に使用

**注意: 現状の自動推定はシンプルな型に限定されます**
- 複雑なオブジェクト型・ネスト構造・独自クラスは `string` 扱いになる
- 入力でオブジェクトを渡したい場合は **手動スキーマ指定を推奨**
- `SimpleChat.cs` の Shop ツールのように、`string` / `int` などのプリミティブ型中心のシグネチャは自動生成で安定

### 5. `Manager/ToolManager.cs`
ツールの登録・管理・実行を行うマネージャー。

**主要メソッド:**
```csharp
public class ToolManager
{
    // ★ オーバーロード版1: Delegate + スキーマ自動生成
    public void RegisterTool<TDelegate>(
        string name, 
        string description, 
        TDelegate callback)
        where TDelegate : Delegate;
    
    // オーバーロード版2: 手動スキーマ指定（互換性維持）
    public void RegisterTool(
        string name, 
        string description, 
        object inputSchema, 
        Delegate callback);
    
    // Tool削除
    public bool UnregisterTool(string name);
    
    // 全Tool削除
    public void RemoveAllTools();
    
    // Tool一覧取得（Ollama API 形式）
    public IEnumerable<ToolDefinition> GetAllTools();
    
    // ★ Tool実行（入力の型変換 + 戻り値の文字列変換も自動対応）
    public string ExecuteTool(string toolName, string argumentsJson);
    
    // Tool存在確認
    public bool HasTool(string name);
}
```

---

## 既存ファイル修正

### 1. `Core/ChatResponse.cs`
Tool call 情報を追加。

**追加フィールド:**
- `ToolCalls` (List<ToolCall>): LLMがリクエストしたツール呼び出しのリスト

### 2. `Core/ChatMessage.cs`
Tool call 結果をメッセージに含められるように拡張。

**追加フィールド:**
- `ToolCallId` (string): 対応するツール呼び出しID
- `ToolResults` (string): ツール実行結果

### 3. `Core/ChatRequestOptions.cs`
ツール関連オプションを追加。

**追加フィールド:**
- `Tools` (List<ToolDefinition>): このリクエストで使用するツール一覧

### 4. `Ollama/OllamaClient.cs`
- `ToolManager` にメンバー変数として含める
- `SendMessageAsync()`, `SendMessageStreamingAsync()` で Tool対応処理
- APIリクエストに tools パラメータを含める
- Tool call のレスポンスを処理し、コールバックを実行
- Tool 実行結果を含むメッセージを履歴に追加

**追加メソッド:**
```csharp
public void RegisterTool(string name, string description, object inputSchema, Func<string, string> callback);
public void UnregisterTool(string name);
public void RemoveAllTools();
public List<ToolDefinition> GetRegisteredTools();
```

---

## 処理フロー

### パターンA: スキーマ自動生成フロー
1. ユーザーが `RegisterTool(name, description, callback)` を呼び出し
2. `ToolSchemaGenerator.GenerateSchema()` が callback の Delegate を分析
3. 各パラメータから JSON Schema を自動生成
   - パラメータ型 → JSON型へ変換
   - `[ToolParameter]` → description に使用
   - デフォルト値 → required から除外
4. `ToolManager` に登録

### パターンB: Tool Call 検出・実行フロー
1. ユーザーがメッセージ送信
2. OllamaClient が登録済みツール情報を含むリクエストを生成
3. Ollama API へリクエスト送信
4. Ollama が tool_calls を含むレスポンスを返す
5. OllamaClient が ToolCall を抽出
6. **ToolManager が自動実行**（重要）
   - JSON Arguments を取得
   - Tool のシグネチャから期待型を判定
   - JSON → C# オブジェクトへ自動変換
   - 型付きで callback を呼び出し
   - 戻り値 → string に変換
7. ツール実行結果を含むメッセージを history に追加
8. **再度 Ollama に同じ history を送信（tool_results を include）**
9. Ollama が最終回答を返す
10. ChatResponse に最終回答を設定して返却

### パターンC: 型変換の詳細（入力と出力）
```
JSON Arguments: {"a":5, "b":3}
        ↓
Callback シグネチャ分析: (int a, int b) => int
        ↓
入力の型変換:
  - 5 (JSON number) → int  ✓
  - 3 (JSON number) → int  ✓
        ↓
callback(5, 3) を実行
        ↓
戻り値: 8 (int)
        ↓
出力の文字列変換:
  - int → "8" (ToString())
        ↓
最終結果: "8" を Ollama に送信
```

### パターンD: 複雑な戻り値の変換
```
Callback: (string userId) => new { id = userId, name = "John" }
        ↓
戻り値: 匿名オブジェクト
        ↓
出力の文字列変換:
  - オブジェクト → JSON シリアライズ
  - 結果: "{\"id\":\"123\",\"name\":\"John\"}"
        ↓
最終結果: JSON 文字列を Ollama に送信
```

---

## 改修範囲 (概要)

| ファイル/フォルダ | 改修内容 | 種類 |
|---|---|---|
| `Core/ToolParameterAttribute.cs` | パラメータ説明用 Attribute | 新規 |
| `Core/ToolDefinition.cs` | ツール定義クラス | 新規 |
| `Core/ToolCall.cs` | ツール呼び出し情報クラス | 新規 |
| `Core/ToolSchemaGenerator.cs` | JSON Schema 自動生成（リフレクション） | 新規 |
| `Manager/ToolManager.cs` | ツール管理・実行・型変換 | 新規 |
| `Core/ChatResponse.cs` | ToolCalls フィールド追加 | 修正 |
| `Core/ChatMessage.cs` | ToolCallId, ToolResults フィールド追加 | 修正 |
| `Core/ChatRequestOptions.cs` | Tools フィールド追加 | 修正 |
| `Ollama/OllamaClient.cs` | Tool対応処理追加（大幅修正） | 修正 |

---

## 実装優先順位

1. **Phase 1** (基盤)
   - ToolParameterAttribute.cs 作成
   - ToolSchemaGenerator.cs 作成（リフレクション実装）
   - ToolDefinition.cs 作成
   - ToolCall.cs 作成

2. **Phase 2** (拡張)
   - ChatMessage 拡張
   - ChatResponse 拡張
   - ChatRequestOptions 拡張

3. **Phase 3** (管理)
   - ToolManager.cs 作成（型変換ロジック実装）
   - OllamaClient に ToolManager 統合

4. **Phase 4** (API対応)
   - OllamaClient の SendMessageAsync に Tool対応処理
   - Tool call 検出・実行・結果連携ロジック実装
   - 自動型変換ロジック実装

5. **Phase 5** (ストリーミング対応)
   - SendMessageStreamingAsync に Tool対応処理

6. **Phase 6** (テスト・ドキュメント)
   - サンプルコード作成
   - API ドキュメント更新

---

## その他考慮事項

### スキーマ自動生成の詳細
- **型対応**
  - `string` → `{ type: "string" }`
  - `int`, `long` → `{ type: "integer" }`
  - `double`, `float` → `{ type: "number" }`
  - `bool` → `{ type: "boolean" }`
  - `List<T>`, `T[]` → `{ type: "array", items: {...} }`
  - `Guid`, `DateTime` など → JSON での型に自動変換

- **デフォルト値の処理**
  - パラメータにデフォルト値がある → required から除外
  - LLM 側では「省略可」と認識

- **Nullable 型の処理**
  - `int?`, `string?` などに対応
  - JSON Schema では required から除外

### 型変換実装
**入力（JSON → C#）:**
- JSON プリミティブ型 → C# 型を自動変換
- 失敗時はエラーメッセージを tool_results に含める
- List<T> など複合型も対応

**出力（C# → string）:**
- `string` → そのまま
- プリミティブ型（int, bool, double など） → `.ToString()`
- `DateTime`, `Guid` など → `.ToString()` または適切なフォーマット
- カスタムオブジェクト/匿名型 → JSON シリアライズ（Newtonsoft.Json）
- `null` → `"null"` または空文字列
- 配列/List → JSON シリアライズ

### Tool call ループ防止
- 無限ループ検出機構（Tool call 回数の制限など）
- LLM が意図しない tool_call を繰り返す場合の処理

### エラーハンドリング
- Tool 実行失敗時は例外を catch してエラーメッセージを返す
- 型変換失敗時の処理
- Ollama API エラー時の処理

### デバッグ機能
- Tool 登録時のログ出力
- Tool 実行時のログ出力（入力・出力）
- スキーマ自動生成内容の確認機能

---

## 自動生成スキーマの実装例

### 例1: シンプルなパラメータ

**ユーザーコード:**
```csharp
client.RegisterTool(
    name: "add",
    description: "Add two numbers",
    callback: (int a, int b) => a + b  // ToString() 不要！
);
```

**自動生成スキーマ:**
```json
{
  "type": "object",
  "properties": {
    "a": { "type": "integer", "description": "a" },
    "b": { "type": "integer", "description": "b" }
  },
  "required": ["a", "b"]
}
```

### 例2: デフォルト値付き

**ユーザーコード:**
```csharp
client.RegisterTool(
    name: "search",
    description: "Search",
    callback: (
        string query,
        int maxResults = 5
    ) => {
        return $"Found {maxResults} results";
    }
);
```

**自動生成スキーマ:**
```json
{
  "type": "object",
  "properties": {
    "query": { "type": "string", "description": "query" },
    "maxResults": { "type": "integer", "description": "maxResults" }
  },
  "required": ["query"]
}
```

### 例3: Attribute で説明付き

**ユーザーコード:**
```csharp
client.RegisterTool(
    name: "divide",
    description: "Divide two numbers",
    callback: (
        [ToolParameter("Numerator")] double numerator,
        [ToolParameter("Denominator (must not be zero)")] double denominator
    ) => {
        if (denominator == 0)
            return "Error: Division by zero";  // string はそのまま
        return numerator / denominator;  // double も自動変換
    }
);
```

**自動生成スキーマ:**
```json
{
  "type": "object",
  "properties": {
    "numerator": { "type": "number", "description": "Numerator" },
    "denominator": { "type": "number", "description": "Denominator (must not be zero)" }
  },
  "required": ["numerator", "denominator"]
}
```

