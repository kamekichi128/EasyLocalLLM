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

基本的な初期化では、開発環境などで、すでに指定のポートでollamaサーバーが立ち上がっており、かつ、モデルもpullされた状態であるものとします。  
ollama未インストール環境に適用するため、ollama.exeを同梱するパターンは、[こちら](#4-ollama-サーバの自動管理)を参照して環境を構築してください。  
なお、**Windows以外には現時点では対応していません。**

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

**現在の実装の制限:**
- `StartCoroutine` + コールバック形式での処理が必須です
- async/await は未対応です（今後の拡張予定を参照）
- キャンセル以外の途中制御はできません
```

### 2. メッセージ送信（一度に完全回答を取得）

コールバックは**1回だけ**呼ばれます。
短い応答や完全な回答が必要な場合に使用してください。

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
        (response, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"Error: {error.Message}");
                return;
            }

            Debug.Log($"Assistant: {response.Content}");
        },
        options
    ));
}
```

### 3. ストリーミング送信（段階的に回答を受け取る）

コールバックは**複数回**呼ばれます。回答が到着するたびに部分応答が返され、最後に `IsFinal=true` で完了が通知されます。
長文生成時のUI更新やリアルタイム表示に向いています。

**処理フローの比較：**

```
非ストリーミング:
SendMessage() → [サーバ処理...] → コールバック(IsFinal=true) → 完了

ストリーミング:
SendStreamingMessage() → [サーバ処理開始...] 
  → コールバック(IsFinal=false, 部分1)
  → コールバック(IsFinal=false, 部分2)
  → ... 
  → コールバック(IsFinal=true, 最終部分) → 完了
```

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
        (response, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"Error: {error.Message}");
                return;
            }

            if (!response.IsFinal)
            {
                // 複数回呼ばれる：部分応答
                Debug.Log($"Receiving: {response.Content}");
            }
            else
            {
                // 最後に1回だけ：完全な応答
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
        ExecutablePath = "/path/to/ollama.exe",
        AutoStartServer = true
    };

    // サーバマネージャーを初期化（自動起動）
    OllamaServerManager.Initialize(config);

    // クライアントを作成
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

#### Windows ローカルインストール手順（自動管理を有効にする場合）

以下は、Streaming Assets を使った運用例です。Unity プロジェクトのビルド前に準備します。

**ステップ 1: Ollama バイナリのダウンロード**

1. [Ollama 公式ウェブサイト](https://ollama.ai) から Windows スタンドアロンバイナリをダウンロード
   - [GitHub のリリースページ](https://github.com/ollama/ollama/releases)でダウンロードできる
   - 通常は `ollama-windows-amd64.zip` のような形式
   - AMD Radeon GPU を使う場合、対応するファイルを追加でインストールすること（通常は`ollama-windows-amd64-rocm.zip`の形で配布されている）

**ステップ 2: Streaming Assets への配置**

プロジェクト内に以下のディレクトリ構造を作成：

```
Assets/StreamingAssets/Ollama/
├── ollama.exe                    # Ollama バイナリ
├── lib /                         # Ollama が利用するライブラリ
└── models/                       # モデルディレクトリ（初期状態で空）
    └── Modelfile                 # （後述）
```

**ステップ 3: モデルの取得**

**方法A: `ollama pull` コマンド（推奨）**

簡単にモデルを利用する方法です。利用可能なモデルは[Ollama 公式ウェブサイト](https://ollama.ai)を参照してください。

1. Powershellで以下を実行（ビルド前）：

```bash
$env:OLLAMA_MODELS=<プロジェクト>\Assets\StreamingAssets\Ollama\models
md $OLLAMA_MODELS 
cd <プロジェクト>\Assets\StreamingAssets\Ollama
start .\ollama.exe serve
.\ollama.exe pull mistral
```

2. モデルは `StreamingAssets/Ollama/models/` 配下に`blobs/`ディレクトリと`manifests/`ディレクトリに分かれて格納される
3. 起動したコマンドプロンプトの新規ウィンドウを閉じる

**方法B: gguf ファイルの直接配置**

Ollamaが未対応名モデルや、Modelfileを独自に制御したい場合に参照してください。

1. GGUF 形式のモデルファイルをダウンロード（例: `mistral-7b-instruct.gguf`）
2. 任意の場所にに配置
3. 同階層に `Modelfile` を作成（例、モデルによって変更してください）：

```
FROM ./mistral-7b-instruct.gguf
PARAMETER temperature 0.7
```

4. Powershellで以下を実行（ビルド前）：

```bash
$env:OLLAMA_MODELS=<プロジェクト>\Assets\StreamingAssets\Ollama\models
md $OLLAMA_MODELS 
cd <プロジェクト>\Assets\StreamingAssets\Ollama
start .\ollama.exe serve
.\ollama.exe create <モデル名> -f <モデルファイルへのパス>
```

5. モデルは `StreamingAssets/Ollama/models/` 配下に`blobs/`ディレクトリと`manifests/`ディレクトリに分かれて格納される
6. 起動したコマンドプロンプトの新規ウィンドウを閉じる

**ステップ 4: Unity での設定**

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
    ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
    AutoStartServer = true,
    DebugMode = true
};

OllamaServerManager.Initialize(config);
var client = LLMClientFactory.CreateOllamaClient(config);
```

### 5. セッション管理

#### セッションの概念

`ChatId` で指定されたセッションは、以下の特徴を持ちます：

- **自動作成**：初回の `SendMessageAsync()` / `SendMessageStreamingAsync()` で自動作成
- **履歴の自動蓄積**：同じ `ChatId` で送信したメッセージと応答は自動的に累積
- **永続性**：`ClearMessages()` するまでメモリに保持される
- **独立管理**：異なる `ChatId` はそれぞれ独立した履歴を持つ

#### 基本的なセッション管理

```csharp
void ManageSessions()
{
    // 異なるセッションIDで複数の会話を管理
    var session1Options = new ChatRequestOptions { ChatId = "session-1" };
    var session2Options = new ChatRequestOptions { ChatId = "session-2" };

    // セッション 1 で会話
    StartCoroutine(_client.SendMessageAsync(
        "セッション1のメッセージ", 
        OnResponse, 
        session1Options
    ));

    // セッション 2 で会話（セッション1の履歴とは完全に独立）
    StartCoroutine(_client.SendMessageAsync(
        "セッション2のメッセージ", 
        OnResponse, 
        session2Options
    ));

    // セッション 1 の次のメッセージ（自動的に前のやり取りを参照）
    StartCoroutine(_client.SendMessageAsync(
        "セッション1の2番目のメッセージ",
        OnResponse,
        session1Options  // 同じセッションID
    ));
}
```

#### 履歴のリセット

**特定のセッションをクリア：**

```csharp
// セッション 1 の履歴をクリア
// 以降、同じセッションIDでメッセージを送信すると、
// 新しいセッションとして再作成される（前の履歴は失われる）
_client.ClearMessages("session-1");
```

**すべての履歴をクリア：**

```csharp
// すべてのセッションをクリア
// メモリ開放やアプリ終了時に有効
_client.ClearAllMessages();
```

#### セッション情報へのアクセス

セッションの状態や履歴情報にアクセスできます：

```csharp
void InspectSessions()
{
    // セッション 1 に関する操作
    string sessionId = "session-1";
    
    // セッションが存在するか確認
    if (_client.HasSession(sessionId))
    {
        Debug.Log("Session exists");
        
        // セッションのメッセージ数を確認
        int messageCount = _client.GetSessionMessageCount(sessionId);
        Debug.Log($"Message count: {messageCount}");
        
        // セッション情報を取得（履歴、作成日時、更新日時等）
        var session = _client.GetSession(sessionId);
        Debug.Log($"Created at: {session.CreatedAt}");
        Debug.Log($"Last updated at: {session.LastUpdatedAt}");
        Debug.Log($"Messages: {string.Join("\n", session.History)}");
    }
    
    // すべてのセッションIDを取得
    var allSessions = _client.GetAllSessionIds();
    foreach (var id in allSessions)
    {
        Debug.Log($"Session ID: {id}");
    }
}
```

#### 複数セッションの実践例

```csharp
public class MultiSessionChat : MonoBehaviour
{
    private OllamaClient _client;
    
    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral"
        };
        _client = LLMClientFactory.CreateOllamaClient(config);
    }

    // ユーザーA との会話セッション
    void ChatWithUserA()
    {
        var userASession = new ChatRequestOptions { ChatId = "user-a-session" };
        StartCoroutine(_client.SendMessageAsync(
            "ユーザーAからのメッセージ",
            OnResponse,
            userASession
        ));
    }

    // ユーザーB との会話セッション
    void ChatWithUserB()
    {
        var userBSession = new ChatRequestOptions { ChatId = "user-b-session" };
        StartCoroutine(_client.SendMessageAsync(
            "ユーザーBからのメッセージ",
            OnResponse,
            userBSession
        ));
    }

    // テーマ別セッション（同じユーザーでも異なるテーマを管理）
    void ChatAboutTopic(string topic)
    {
        var topicSession = new ChatRequestOptions { ChatId = $"topic-{topic}" };
        StartCoroutine(_client.SendMessageAsync(
            $"{topic}について教えて",
            OnResponse,
            topicSession
        ));
    }

    void OnResponse(ChatResponse response, ChatError error)
    {
        if (error != null)
        {
            Debug.LogError($"Error: {error.Message}");
            return;
        }
        
        if (response.IsFinal)
        {
            Debug.Log($"Response: {response.Content}");
        }
    }

    // アプリ終了時
    void OnApplicationQuit()
    {
        _client.ClearAllMessages();  // メモリをクリーンアップ
    }
}
```

#### セッション管理の注意点

- **ChatId が null の場合**：Guid で自動生成される一度限りのセッション
- **MaxHistory 設定**：デフォルト50メッセージ。超過時は古いメッセージから削除
- **セッション間の独立性**：あるセッションのシステムプロンプトが他に影響することはない
- **メモリ管理**：多くのセッションを保持し続けるとメモリ消費が増加。不要なセッションは `ClearMessages()` で削除推奨
- **セッション情報の取得**：`GetSession()` で返される `ChatSession` オブジェクトから、作成日時（`CreatedAt`）、最終更新日時（`LastUpdatedAt`）、メッセージ履歴（`History`）にアクセス可能

### 6. 優先度スケジューリング

`ChatRequestOptions.Priority` が高いほど先に実行されます。
同一優先度の場合は先着順で実行されます。

```csharp
void SendWithPriority()
{
    var high = new ChatRequestOptions
    {
        ChatId = "session-high",
        Priority = 10,
        WaitIfBusy = true
    };

    var low = new ChatRequestOptions
    {
        ChatId = "session-low",
        Priority = 0,
        WaitIfBusy = true
    };

    StartCoroutine(_client.SendMessageAsync(
        "高優先度のリクエスト",
        OnResponse,
        high
    ));

    StartCoroutine(_client.SendMessageAsync(
        "低優先度のリクエスト",
        OnResponse,
        low
    ));
}
```

### 7. キャンセル

Unity の標準 `CancellationToken` パターンに対応しています。キャンセルが必要な場合は `CancellationTokenSource` を使用してください。

```csharp
private CancellationTokenSource _cancellationTokenSource;

void SendWithCancel()
{
    // キャンセルトークンソースを作成
    _cancellationTokenSource = new CancellationTokenSource();
    
    var options = new ChatRequestOptions
    {
        ChatId = "chat-session-1",
        CancellationToken = _cancellationTokenSource.Token
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "長い回答を生成して",
        (response, error) =>
        {
            if (error != null)
            {
                if (error.ErrorType == LLMErrorType.Cancelled)
                {
                    Debug.Log("Request cancelled");
                }
                else
                {
                    Debug.LogError($"Error: {error.Message}");
                }
                return;
            }
            
            Debug.Log($"Receiving: {response.Content}");
        },
        options
    ));
}

// UI ボタン等から呼び出し
void CancelRequest()
{
    _cancellationTokenSource?.Cancel();
}

// クリーンアップ
void OnDestroy()
{
    _cancellationTokenSource?.Dispose();
}
```

**タイムアウト付きキャンセルの例：**

```csharp
void SendWithTimeout()
{
    // 10秒後に自動キャンセル
    _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    
    var options = new ChatRequestOptions
    {
        ChatId = "chat-session-1",
        CancellationToken = _cancellationTokenSource.Token
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "回答を生成して",
        (response, error) =>
        {
            if (error != null)
            {
                if (error.ErrorType == LLMErrorType.Cancelled)
                {
                    Debug.Log("Request timeout");
                }
                else
                {
                    Debug.LogError($"Error: {error.Message}");
                }
                return;
            }
            
            Debug.Log($"Receiving: {response.Content}");
        },
        options
    ));
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
| EnableHealthCheck | bool | true | サーバ起動後にヘルスチェックを実行 |
| ExecutablePath | string | - | Ollama 実行ファイルのパス（自動管理時に指定） |
| ModelsDirectory | string | ./Models | Ollama モデルディレクトリ（OLLAMA_MODELS） |
| MaxConcurrentSessions | int | 1 | 同時実行可能なセッション数（GPU キャパシティ対応） |

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
    Cancelled,           // ユーザーによるキャンセル
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
