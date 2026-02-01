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
        (response, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"Error: {error.Message}");
                return;
            }

            if (response.IsFinal)
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
        (response, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"Error: {error.Message}");
                return;
            }

            if (!response.IsFinal)
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

`ChatRequestOptions.CancelRequested` にコールバックを指定すると、
途中でリクエストを中断できます。

```csharp
private bool _cancel;

void SendWithCancel()
{
    _cancel = false;
    var options = new ChatRequestOptions
    {
        ChatId = "chat-session-1",
        CancelRequested = () => _cancel
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
        },
        options
    ));
}

void Cancel()
{
    _cancel = true;
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
