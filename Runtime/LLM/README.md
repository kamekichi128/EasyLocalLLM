# EasyLocalLLM Runtime Library

Ollama を使用してローカル LLM と通信するための Unity ライブラリです。

## 主な機能

- ✅ **設定管理の外部化**：`OllamaConfig` で柔軟に設定可能
- ✅ **自動リトライ機能**：リクエスト失敗時の指数バックオフ対応
- ✅ **ストリーミング対応**：段階的な回答受け取り
- ✅ **セッション管理**：チャットセッションごとの履歴管理
- ✅ **エラーハンドリング**：詳細なエラー情報提供
- ✅ **サーバライフサイクル管理**：自動起動・停止機能

## 使用方法

### 1. 基本的な初期化

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class ChatManager : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        // 設定を作成
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            DebugMode = true
        };

        // クライアントを初期化
        _client = LLMClientFactory.CreateOllamaClient(config);
    }
}
```

### 2. メッセージ送信（一度に完全回答を取得）

```csharp
void SendMessage()
{
    var options = new ChatRequestOptions
    {
        ChatId = "chat-session-1",
        Temperature = 0.7f,
        Seed = 42
    };

    StartCoroutine(_client.SendMessageAsync(
        "こんにちは",
        (response, error, isFinal) =>
        {
            if (error != null)
            {
                Debug.LogError($"Error: {error.Message}");
                return;
            }

            if (isFinal)
            {
                Debug.Log($"Assistant: {response.Content}");
            }
        },
        options
    ));
}
```

### 3. ストリーミング送信（段階的に回答を受け取る）

```csharp
void SendStreamingMessage()
{
    var options = new ChatRequestOptions
    {
        ChatId = "chat-session-1",
        Temperature = 0.7f
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "日本語で詩を書いてください",
        (response, error, isFinal) =>
        {
            if (error != null)
            {
                Debug.LogError($"Error: {error.Message}");
                return;
            }

            if (!isFinal)
            {
                // ストリーミング中の部分応答
                Debug.Log($"Receiving: {response.Content}");
            }
            else
            {
                // 完了
                Debug.Log($"Complete: {response.Content}");
            }
        },
        options
    ));
}
```

### 4. Ollama サーバの自動管理

```csharp
void StartWithAutoServer()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://localhost:11434",
        ExecutablePath = @"C:\path\to\ollama.exe",
        AutoStartServer = true
    };

    // サーバマネージャーを初期化（自動起動）
    OllamaServerManager.Initialize(config);

    // クライアントを作成
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

### 5. セッション管理

```csharp
void ManageSessions()
{
    // 異なるセッションIDで複数の会話を管理
    var session1Options = new ChatRequestOptions { ChatId = "session-1" };
    var session2Options = new ChatRequestOptions { ChatId = "session-2" };

    // セッション 1 で会話
    StartCoroutine(_client.SendMessageAsync("セッション1のメッセージ", OnResponse, session1Options));

    // セッション 2 で会話
    StartCoroutine(_client.SendMessageAsync("セッション2のメッセージ", OnResponse, session2Options));

    // セッション 1 の履歴をクリア
    _client.ClearMessages("session-1");

    // すべての履歴をクリア
    _client.ClearAllMessages();
}
```

## クラス構成

```
Runtime/LLM/
├── Core Data Models
│   ├── ChatMessage.cs           # チャットメッセージ
│   ├── ChatResponse.cs          # LLM からの応答
│   ├── ChatError.cs             # エラー情報
│   └── ChatRequestOptions.cs    # リクエストオプション
│
├── Manager & Client
│   ├── ChatHistoryManager.cs    # メッセージ履歴管理（セッション対応）
│   ├── OllamaConfig.cs          # 設定
│   ├── OllamaServerManager.cs   # サーバライフサイクル管理
│   ├── OllamaClient.cs          # Ollama クライアント実装
│   ├── HttpRequestHelper.cs     # HTTP リトライロジック（内部用）
│
└── Factory & Interface
    ├── IChatLLMClient.cs        # クライアントインターフェース
    └── LLMClientFactory.cs      # クライアント生成ファクトリ
```

## 設定オプション

`OllamaConfig` の主要なプロパティ：

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|----------|------|
| ServerUrl | string | http://localhost:11434 | Ollama サーバの URL |
| DefaultModelName | string | mistral | デフォルトのモデル名 |
| MaxRetries | int | 3 | リトライの最大回数 |
| RetryDelaySeconds | float | 1.0f | リトライの初期遅延時間 |
| DefaultSeed | int | -1 | デフォルトシード（-1 でランダム） |
| HttpTimeoutSeconds | float | 60.0f | HTTP タイムアウト時間 |
| DebugMode | bool | false | デバッグログの出力 |
| AutoStartServer | bool | true | Ollama サーバを自動起動 |

## エラーハンドリング

`ChatError` のエラータイプ：

```csharp
public enum LLMErrorType
{
    ConnectionFailed,    // サーバ接続失敗
    ServerError,         // サーバ側エラー（500, 503等）
    InvalidResponse,     // レスポンスパース失敗
    Timeout,             // リクエストタイムアウト
    ModelNotFound,       // モデルが見つからない
    Unknown              // その他のエラー
}
```

エラーは `IsRetryable` プロパティでリトライ可能かを判定できます。

## 今後の拡張予定

- [ ] async/await サポート
- [ ] llama.cpp クライアント
- [ ] OpenAI API クライアント
- [ ] 複数モデルの並列処理
- [ ] メッセージ永続化
