# セッションシステムプロンプト設定コマンド

## 概要

EasyLocalLLM ライブラリに、セッションごとのローカルなシステムプロンプトを設定・管理するコマンドセットが追加されました。

これにより、グローバルなシステムプロンプトを使用しつつ、特定のセッションでは異なるプロンプトを使用できます。

---

## 追加されたメソッド

### 1. `SetSessionSystemPrompt(string sessionId, string systemPrompt)`

指定されたセッションのシステムプロンプトを設定します。

**パラメータ**:
- `sessionId`: セッションID
- `systemPrompt`: 設定するシステムプロンプト

**例**:
```csharp
var client = LLMClientFactory.CreateOllamaClient(config);

// プログラミング専門のプロンプトをセッション A に設定
client.SetSessionSystemPrompt("session-a", 
    "You are a programming expert. Help with code-related questions.");

// 翻訳専門のプロンプトをセッション B に設定
client.SetSessionSystemPrompt("session-b", 
    "You are a translation expert. Translate accurately between languages.");
```

---

### 2. `GetSessionSystemPrompt(string sessionId)`

指定されたセッションのシステムプロンプトを取得します。

**戻り値**: システムプロンプト、セッションが存在しない場合は `null`

**例**:
```csharp
string prompt = client.GetSessionSystemPrompt("session-a");
Debug.Log($"Session A プロンプト: {prompt}");
```

---

### 3. `ResetSessionSystemPrompt(string sessionId)`

指定されたセッションのシステムプロンプトをリセットします。
リセット後、グローバルシステムプロンプトが使用されます。

**例**:
```csharp
// セッション A のプロンプトをグローバル設定に戻す
client.ResetSessionSystemPrompt("session-a");
```

---

### 4. `SetSystemPromptForMultipleSessions(IEnumerable<string> sessionIds, string systemPrompt)`

複数のセッションに対して同じシステムプロンプトを一括設定します。

**パラメータ**:
- `sessionIds`: セッションID のリスト
- `systemPrompt`: 設定するシステムプロンプト

**例**:
```csharp
// 複数のセッションに同じプロンプトを設定
var sessionIds = new[] { "session-1", "session-2", "session-3" };
client.SetSystemPromptForMultipleSessions(sessionIds, 
    "You are a helpful customer service assistant.");
```

---

### 5. `ResetAllSessionSystemPrompts()`

すべてのセッションのシステムプロンプトをリセットします。

**例**:
```csharp
// すべてのセッションをグローバル設定に戻す
client.ResetAllSessionSystemPrompts();
```

---

### 6. `ClearSessionWithPrompt(string sessionId)`

指定されたセッションの履歴とシステムプロンプットの両方をリセットします。

**例**:
```csharp
// セッション A を完全にリセット
client.ClearSessionWithPrompt("session-a");
```

---

## 使用パターン

### パターン1: 異なるロール用のセッション分離

```csharp
// 翻訳タスク用セッション
client.SetSessionSystemPrompt("translator", 
    "You are a professional translator. Translate accurately and naturally.");

// プログラミングヘルプ用セッション
client.SetSessionSystemPrompt("programmer", 
    "You are a senior software engineer. Provide code examples and explanations.");

// レッスン用セッション
client.SetSessionSystemPrompt("teacher", 
    "You are an educational tutor. Explain concepts clearly with examples.");

// 各セッションで独立した会話を実施
StartCoroutine(client.SendMessageAsync(
    "Translate 'Hello' to Japanese",
    (response, error) => Debug.Log(response.Content),
    new ChatRequestOptions { ChatId = "translator" }
));
```

### パターン2: 言語別セッション管理

```csharp
// 日本語セッション
client.SetSessionSystemPrompt("ja", 
    "You are a helpful assistant. Always respond in Japanese.");

// 英語セッション
client.SetSessionSystemPrompt("en", 
    "You are a helpful assistant. Always respond in English.");

// フランス語セッション
client.SetSessionSystemPrompt("fr", 
    "You are a helpful assistant. Always respond in French.");
```

### パターン3: プロジェクトごとのプロンプト設定

```csharp
// プロジェクトAのセッション
client.SetSessionSystemPrompt("project-a", 
    "Context: You are helping with an educational game project. " +
    "Focus on interactive and engaging explanations.");

// プロジェクトBのセッション
client.SetSessionSystemPrompt("project-b", 
    "Context: You are helping with a business analytics project. " +
    "Focus on data-driven insights and technical accuracy.");
```

---

## 実装の特徴

### 階層的なプロンプト管理

1. **グローバルレベル**: `GlobalSystemPrompt` プロパティ
   - すべてのセッションのデフォルト
   
2. **セッションレベル**: `SetSessionSystemPrompt()` メソッド
   - 特定のセッション専用
   - グローバルプロンプトをオーバーライド

3. **リクエストレベル**: `ChatRequestOptions.SystemPrompt`
   - 単一リクエスト限定
   - セッションレベルをオーバーライド

**優先順位**:
```
リクエストレベル > セッションレベル > グローバルレベル
```

### DebugMode サポート

`OllamaConfig.DebugMode = true` の場合、以下の操作がログ出力されます：

```csharp
[Ollama] Session 'session-a' system prompt updated: You are a programming expert...
[Ollama] Session 'session-a' system prompt reset to global
[Ollama] System prompt set for 3 sessions
[Ollama] Session 'session-a' cleared with prompt reset
```

---

## 単体テスト

NonStreamingTests.cs に5つのテストが追加されました：

| テスト | 説明 |
|--------|------|
| Test_SessionSystemPrompt_SetAndRetrieve | プロンプトの設定と取得 |
| Test_SessionSystemPrompt_Reset | プロンプトのリセット |
| Test_SetSystemPromptForMultipleSessions | 複数セッションへの一括設定 |
| Test_ResetAllSessionSystemPrompts | すべてのセッションをリセット |
| Test_ClearSessionWithPrompt | 履歴とプロンプトをクリア |

**実行方法**:
```
Unity Test Runner → PlayMode → NonStreamingTests を実行
```

---

## API 互換性

新しいメソッドはすべて `IChatLLMClient` インターフェースに定義されており：

- ✅ `OllamaClient` で実装
- ✅ `MockChatLLMClient` で実装（テスト用）
- ✅ 将来のクライアント実装でも対応可能

---

## ベストプラクティス

### 1. セッションID の命名規則

意味のあるセッションID を使用してコードの可読性を高める：

```csharp
// 良い例
client.SetSessionSystemPrompt("translator-en-ja", "...");
client.SetSessionSystemPrompt("user-support-session-123", "...");

// 避ける
client.SetSessionSystemPrompt("session1", "...");
client.SetSessionSystemPrompt("temp", "...");
```

### 2. 設定は一度だけ

セッションプロンプトは初回セッション作成時に設定：

```csharp
// 推奨
var options = new ChatRequestOptions 
{ 
    ChatId = "programmer",
    SystemPrompt = "You are a programming expert."
};
// または
client.SetSessionSystemPrompt("programmer", "You are a programming expert.");

// その後のメッセージ送信では SystemPrompt を指定しない
StartCoroutine(client.SendMessageAsync("Write a function", callback, 
    new ChatRequestOptions { ChatId = "programmer" }));
```

### 3. 不要なセッションはクリア

メモリ使用量を抑えるため、不要なセッションは削除：

```csharp
// セッション終了時
client.ClearSessionWithPrompt("session-id");

// または個別に
client.ClearMessages("session-id");
client.ResetSessionSystemPrompt("session-id");
```

---

## まとめ

セッションシステムプロンプト設定コマンドにより：

✅ 複数のロールを1つのクライアントで管理  
✅ 言語別セッションを簡単に実装  
✅ プロジェクト固有のコンテキストをセッションに適用  
✅ グローバル設定との階層的な管理  
✅ テストで検証可能な実装  
