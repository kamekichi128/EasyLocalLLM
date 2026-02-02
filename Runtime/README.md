# EasyLocalLLM Runtime Library

Ollama を使用してローカル LLM と通信するための Unity ライブラリです。

## 目次

- [主な機能](#主な機能)
- [クイックスタート](#クイックスタート)
- [制限事項](#制限事項)
- [使用方法](#使用方法)
  - [1. 基本的な初期化](#1-基本的な初期化)
  - [2. メッセージ送信（一度に完全回答を取得）](#2-メッセージ送信一度に完全回答を取得)
  - [3. ストリーミング送信（段階的に回答を受け取る）](#3-ストリーミング送信段階的に回答を受け取る)
  - [4. Ollama サーバの自動管理](#4-ollama-サーバの自動管理)
  - [5. セッション管理](#5-セッション管理)
  - [6. 優先度スケジューリング](#6-優先度スケジューリング)
  - [7. キャンセル](#7-キャンセル)
  - [8. リトライとエラーハンドリング](#8-リトライとエラーハンドリング)
- [実践例](#実践例)
- [クラス構成](#クラス構成)
- [設定オプション](#設定オプション)
- [デフォルト設定について](#デフォルト設定について)
- [エラーハンドリング](#エラーハンドリング)
- [今後の拡張予定](#今後の拡張予定)

## 主な機能

- ✅ **設定管理の外部化**：`OllamaConfig` で柔軟に設定可能
- ✅ **自動リトライ機能**：リクエスト失敗時の指数バックオフ対応
- ✅ **ストリーミング対応**：段階的な回答受け取り
- ✅ **セッション管理**：チャットセッションごとの履歴管理
- ✅ **エラーハンドリング**：詳細なエラー情報提供
- ✅ **サーバライフサイクル管理**：自動起動・停止機能

## クイックスタート

最小限のコードで動作確認できます。Ollama サーバが `localhost:11434` で起動済みで、`mistral` モデルがインストール済みであることを前提とします。

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class QuickStart : MonoBehaviour
{
    void Start()
    {
        var config = new OllamaConfig();
        var client = LLMClientFactory.CreateOllamaClient(config);
        
        StartCoroutine(client.SendMessageAsync(
            "こんにちは",
            (response, error) => {
                if (error != null) {
                    Debug.LogError($"エラー: {error.Message}");
                    return;
                }
                Debug.Log($"応答: {response.Content}");
            }
        ));
    }
}
```

**Ollama のセットアップが未完了の場合は、[4. Ollama サーバの自動管理](#4-ollama-サーバの自動管理)を参照してください。**

## 制限事項

### ⚠️ 重要な制約

- **async/await 未対応**：`StartCoroutine` + コールバック形式のみサポート
- **Windows 専用**：現時点では Windows 以外に対応していません
- **Unity 専用**：UnityWebRequest に依存しているため、Unity 外では動作しません

### 処理パターンの制約

```csharp
// ❌ これは動作しません（async/await 未対応）
async Task SendMessageAsync() {
    var result = await client.SendMessageAsync("Hello");
}

// ✅ この形式を使用してください
void SendMessage() {
    StartCoroutine(client.SendMessageAsync(
        "Hello",
        (response, error) => {
            // コールバックで結果を処理
        }
    ));
}
```

**今後の拡張予定**: async/await サポートは検討中です。詳細は[今後の拡張予定](#今後の拡張予定)を参照してください。

## 使用方法

### 1. 基本的な初期化

Ollama サーバが `localhost:11434` で起動済み、かつモデルがインストール済みであることを前提とします。

**Ollama のセットアップが未完了の場合は、[4. Ollama サーバの自動管理](#4-ollama-サーバの自動管理)を参照してください。**

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

コールバックは**1回だけ**呼ばれます。
短い応答や完全な回答が必要な場合に使用してください。

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

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
using EasyLocalLLM.LLM;
using UnityEngine;

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

#### セットアップ方法の選択

Ollama をアプリケーションに組み込む場合、モデルの取得方法で2つのパターンがあります。
自分の環境に合わせて選択してください。

**方法 A: `ollama pull` コマンド（推奨・簡単）**
- ✅ セットアップが簡単
- ✅ 自動的に最適化される
- ✅ モデルアップデートが容易
- ❌ ネットワークダウンロードが必要（初回は時間がかかる可能性）

**方法 B: GGUF ファイルを直接配置（カスタマイズ・特殊用途）**
- ✅ モデルを自由にカスタマイズできる
- ✅ Ollama未対応のカスタムモデルを使用可能
- ❌ セットアップが複雑
- ❌ 事前準備（GGUF ダウンロード）が必要

**推奨される使い分け：**

| シナリオ | 推奨方法 | 理由 |
|--------|--------|------|
| 通常のケース | 方法 A | セットアップが簡単。ほとんどの場合これで十分 |
| Ollama未対応のカスタムモデルを使う | 方法 B | 独自のGGUFファイルを使用可能 |
| モデル動作を細かくカスタマイズしたい | 方法 B | Modelfile でパラメータを調整可能 |

**ほとんどのケースで方法Aを推奨します。** 方法Bは、特定のGGUFファイルやModelfileのカスタマイズが必要な場合のみ使用してください。

#### セットアップ手順

**ステップ 0: 共通準備**

1. [Ollama 公式ウェブサイト](https://ollama.ai) から Windows スタンドアロンバイナリをダウンロード
   - [GitHub のリリースページ](https://github.com/ollama/ollama/releases)
   - 通常は `ollama-windows-amd64.zip` のような形式
   - AMD Radeon GPU の場合：`ollama-windows-amd64-rocm.zip` もダウンロード

2. Unity プロジェクト内に以下のディレクトリ構造を作成：

```
Assets/StreamingAssets/Ollama/
├── ollama.exe                    # Ollama バイナリ
├── lib /                         # Ollama が利用するライブラリ
└── models/                       # モデルディレクトリ（初期状態で空）
```

**方法 A: `ollama pull` コマンド（推奨）**

最も簡単で推奨されるセットアップ方法です。

1. PowerShell を開き、以下を実行（ビルド前）：

```powershell
$env:OLLAMA_MODELS="<プロジェクトパス>\Assets\StreamingAssets\Ollama\models"
mkdir $env:OLLAMA_MODELS
cd "<プロジェクトパス>\Assets\StreamingAssets\Ollama"
.\ollama.exe serve
```

2. 別の PowerShell ウィンドウで以下を実行：

```powershell
# 利用可能なモデル例：mistral, llama2, neural-chat, dolphin-mixtral など
.\ollama.exe pull mistral
```

3. ダウンロードが完了するまで待機（モデルサイズによって数分～数時間）

4. 完了後、両方のウィンドウを閉じる

5. `StreamingAssets/Ollama/models/` に `blobs/` と `manifests/` ディレクトリが自動生成される

**モデル選択ガイド：**

```
小型（軽量・高速）
├── mistral (7B)          推奨。バランスが良い
├── neural-chat (7B)      会話に最適化
└── phi (2.7B)            最も軽量

中型（標準）
├── llama2 (13B)          高精度が必要な場合
└── dolphin-mixtral (8x7B) 高性能が必要な場合（メモリ消費大）

大型（高精度・高メモリ消費）
└── llama2 (70B)          研究用途。GPUメモリ24GB以上推奨
```

**方法 B: GGUF ファイルを直接配置（カスタマイズ対応）**

モデルをカスタマイズしたい場合や、Ollama未対応のモデルを使用する場合に使用します。

**ステップ 1: GGUF ファイルのダウンロード**

GGUF 形式のモデルファイルを以下のサイトからダウンロードします：

- [Hugging Face](https://huggingface.co/models?search=gguf) - 最大リソース
- [GGUF Zoo](https://ggml.ai) - 最適化されたモデル集

例：mistral モデルをダウンロード
1. [Hugging Face 上の mistral](https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.1-GGUF) にアクセス
2. `mistral-7b-instruct-v0.1.Q4_K_M.gguf`（推奨：バランスが良い）をダウンロード
   - `Q4_K_M` = 4-bit 量子化。品質とサイズのバランスが最適
   - `Q5_K_M` = 5-bit 量子化。高品質だがサイズが大きい
   - `Q2_K` = 2-bit 量子化。軽量だが精度低下

3. ダウンロード後、以下に配置：
```
Assets/StreamingAssets/Ollama/models/mistral/
└── mistral-7b-instruct-v0.1.Q4_K_M.gguf
```

**ステップ 2: Modelfile の作成**

GGUF ファイルと同じディレクトリに `Modelfile` を作成します。以下は一般的なテンプレートです：

**Mistral の場合：**
```
FROM ./mistral-7b-instruct-v0.1.Q4_K_M.gguf

PARAMETER temperature 0.7
PARAMETER top_k 40
PARAMETER top_p 0.9
```

**Llama2 の場合：**
```
FROM ./llama-2-13b-chat.Q4_K_M.gguf

PARAMETER temperature 0.8
PARAMETER top_k 50
PARAMETER top_p 0.95
PARAMETER repeat_penalty 1.1
```

**Neural-Chat の場合：**
```
FROM ./neural-chat-7b-v3-2.Q4_K_M.gguf

PARAMETER temperature 0.7
PARAMETER top_k 30
PARAMETER top_p 0.85
```

**Modelfile パラメータの説明：**

| パラメータ | デフォルト | 説明 | 推奨値 |
|----------|----------|------|--------|
| `temperature` | 0.8 | 回答の多様性（低=確定的、高=多様） | 0.7～1.0 |
| `top_k` | 40 | 上位k個の選択肢から選ぶ | 30～50 |
| `top_p` | 0.9 | 累積確率p以上の選択肢から選ぶ | 0.85～0.95 |
| `repeat_penalty` | 1.0 | 繰り返しの抑制（1.0=なし、1.1=強め） | 1.0～1.2 |

**ステップ 3: Ollama にモデルを登録**

PowerShell で以下を実行：

```powershell
$env:OLLAMA_MODELS="<プロジェクトパス>\Assets\StreamingAssets\Ollama\models"
cd "<プロジェクトパス>\Assets\StreamingAssets\Ollama\models\mistral"
..\..\ollama.exe create mistral -f ./Modelfile
```

**ステップ 4: 登録確認**

```powershell
# 登録済みモデル一覧を確認
..\..\ollama.exe list
```

出力例：
```
NAME                                    ID              SIZE
mistral:latest                          a1b2c3d4...     3.5GB
```

**ステップ 5: Unity での設定**

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
    ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
    DefaultModelName = "mistral",  // 登録したモデル名
    AutoStartServer = true,
    DebugMode = true
};

OllamaServerManager.Initialize(config);
var client = LLMClientFactory.CreateOllamaClient(config);
```

#### トラブルシューティング

**Q: モデルダウンロードが遅い**
- A: インターネット接続速度を確認。大型モデルは数GB～数十GBあります。

**Q: "ollama.exe serve" がエラーになる**
- A: ポート 11434 が既に使用されていないか確認。`netstat -an | findstr :11434`

**Q: Modelfile 作成時に "not found" エラー**
- A: GGUF ファイルのパスが相対パスになっているか確認。`./` で始まる相対パスを使用。

**Q: メモリ不足エラー**
- A: より小さな量子化版を使用（Q2_K → Q4_K_M など）。または`MaxConcurrentSessions`を1に。

**Q: モデルのカスタマイズ例をもっと見たい**
- A: 公式ガイド：[Ollama Modelfile](https://github.com/ollama/ollama/blob/main/docs/modelfile.md)

#### 推奨セットアップ（ゲーム開発向け）

```csharp
public class OllamaSetupManager : MonoBehaviour
{
    void Start()
    {
        // 環境に応じた設定を選択
        OllamaConfig config;

#if UNITY_EDITOR
        // 開発環境：デバッグモード有効
        config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            AutoStartServer = true,
            DebugMode = true,
            MaxRetries = 5,
            EnableHealthCheck = true
        };
#else
        // ビルド版：ログ最小化
        config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
            AutoStartServer = true,
            DebugMode = false,
            MaxRetries = 3,
            HttpTimeoutSeconds = 90.0f,
            EnableHealthCheck = true
        };
#endif

        OllamaServerManager.Initialize(config);
        var client = LLMClientFactory.CreateOllamaClient(config);
    }
}
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
        
        Debug.Log($"Response: {response.Content}");
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

#### 優先度スケジューリングの背景

リソースが限られた環境（GPU キャパシティなど）では、複数のリクエストが同時に到着することがあります。
このライブラリは以下のようなシナリオを想定しています：

- **高優先度メッセージ**：システム運用やゲーム進行に必須の推論（例：NPCの重要な応答）
- **低優先度メッセージ**：フレーバー的な推論で、失敗してもゲーム稼働に影響なし（例：雑談NPC）

限られたリソースの中で、重要なメッセージを優先的に処理し、低優先度メッセージはキューで待たせることで、
システムの安定性と応答性を両立させることができます。

#### デフォルト設定と動作

**デフォルト：**
- `MaxConcurrentSessions = 1`：同時実行セッションは1つまで
- 複数リクエストが到着すると、優先度順にキューに格納
- リソースが解放されたら、次の優先度の高いリクエストを処理

**処理フロー：**

```
複数リクエスト到着
  ↓
優先度順にキューに格納
  ↓
リソース確認（MaxConcurrentSessions確認）
  ↓
優先度の高い順に実行
  ├─ 高優先度リクエスト → 即座に実行
  ├─ 中優先度リクエスト → 前のリクエスト終了まで待機
  └─ 低優先度リクエスト → さらに奥のキュー

各リクエストの処理完了
  ↓
次の優先度リクエストを実行
```

#### WaitIfBusy の動作

**`WaitIfBusy = true`（推奨）：**

クライアントがビジー中の場合、リクエストをキューに登録して待機します。

```csharp
void SendWithPriority()
{
    var systemMessage = new ChatRequestOptions
    {
        ChatId = "system-npc",
        Priority = 10,           // 高優先度
        WaitIfBusy = true        // ビジー中なら待機
    };

    var flavorMessage = new ChatRequestOptions
    {
        ChatId = "flavor-npc",
        Priority = 0,            // 低優先度
        WaitIfBusy = true        // ビジー中なら待機
    };

    // システムメッセージ（高優先度）を送信
    StartCoroutine(_client.SendMessageAsync(
        "プレイヤーに与えるべき重要な情報",
        OnResponse,
        systemMessage
    ));

    // フレーバーメッセージ（低優先度）を送信
    StartCoroutine(_client.SendMessageAsync(
        "雑談的な応答",
        OnResponse,
        flavorMessage
    ));

    // systemMessage は即座に実行（優先度が高い）
    // flavorMessage は systemMessage が終了するまでキューで待機
}
```

**`WaitIfBusy = false`（即座エラー）：**

クライアントがビジー中の場合、エラーを返してリクエストを棄却します。

```csharp
void SendWithoutWaiting()
{
    var options = new ChatRequestOptions
    {
        ChatId = "session-1",
        Priority = 0,
        WaitIfBusy = false       // ビジー中ならエラー
    };

    StartCoroutine(_client.SendMessageAsync(
        "メッセージ",
        (response, error) =>
        {
            if (error != null)
            {
                if (error.ErrorType == LLMErrorType.Unknown && 
                    error.Message.Contains("busy"))
                {
                    Debug.Log("Client is busy, request was rejected");
                }
                return;
            }
        },
        options
    ));
}
```

#### 優先度の設計例

```csharp
public class PrioritizedChatManager : MonoBehaviour
{
    private OllamaClient _client;

    // 優先度定数の定義
    private const int PRIORITY_CRITICAL = 100;      // ゲーム進行に必須
    private const int PRIORITY_HIGH = 50;           // 重要なNPC会話
    private const int PRIORITY_NORMAL = 0;          // 通常のNPC会話
    private const int PRIORITY_LOW = -50;           // フレーバー会話

    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral"
        };
        _client = LLMClientFactory.CreateOllamaClient(config);
    }

    // ゲーム進行に必須のメッセージ
    void SendCriticalMessage(string message)
    {
        var options = new ChatRequestOptions
        {
            ChatId = "critical-system",
            Priority = PRIORITY_CRITICAL,
            WaitIfBusy = true
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    // 重要なNPC会話
    void SendImportantNPCMessage(string npcId, string message)
    {
        var options = new ChatRequestOptions
        {
            ChatId = $"npc-{npcId}-important",
            Priority = PRIORITY_HIGH,
            WaitIfBusy = true
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    // 通常のNPC会話
    void SendNormalNPCMessage(string npcId, string message)
    {
        var options = new ChatRequestOptions
        {
            ChatId = $"npc-{npcId}-normal",
            Priority = PRIORITY_NORMAL,
            WaitIfBusy = true
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    // フレーバー会話（失敗してもOK）
    void SendFlavorMessage(string npcId, string message)
    {
        var options = new ChatRequestOptions
        {
            ChatId = $"npc-{npcId}-flavor",
            Priority = PRIORITY_LOW,
            WaitIfBusy = false  // ビジー中なら棄却OK
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    void OnResponse(ChatResponse response, ChatError error)
    {
        if (error != null)
        {
            Debug.LogError($"Error: {error.Message}");
            return;
        }

        Debug.Log($"Response: {response.Content}");
    }
}
```

#### 複数セッションの同時実行

`MaxConcurrentSessions` を増やすことで、複数セッションを並列実行できます：

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    MaxConcurrentSessions = 2  // 同時に2セッションまで実行
};

OllamaServerManager.Initialize(config);
var client = LLMClientFactory.CreateOllamaClient(config);

// この場合、異なるセッションのリクエストは並列実行される
// 同じセッション内でも、優先度順に処理される
```

#### スケジューリングの仕様

| 設定 | `WaitIfBusy=true` | `WaitIfBusy=false` |
|-----|------------------|-------------------|
| クライアントが空いている | 即座に実行 | 即座に実行 |
| クライアントがビジー | キューで待機（優先度順） | エラー返却 |
| 利用シーン | システム必須 / 重要なNPC | フレーバー / 失敗許容 |

#### パフォーマンス最適化のヒント

- **優先度の粗さ**：細かく設定しすぎると管理が複雑。通常は3～5段階で充分
- **セッション分離**：重要度の異なるメッセージは異なるセッションにする
- **MaxConcurrentSessions**：GPU容量に応じて調整（1～4が目安）。大きすぎるとメモリ不足やGPU過負荷の原因に
- **リソース監視**：本番環境ではGPUメモリとシステムメモリの監視を推奨

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

### 8. リトライとエラーハンドリング

#### 自動リトライのしくみ

このライブラリは、ネットワークエラーやサーバの一時的な不具合に対して自動的にリトライを行います。
以下のエラーが発生した場合、`MaxRetries` の回数まで自動的に再試行されます：

**自動リトライの対象エラー：**
- `ConnectionFailed`：サーバ接続失敗（タイムアウト、接続拒否など）
- `ServerError`：サーバ側エラー（HTTP 500, 503など）
- `Timeout`：リクエストタイムアウト

リトライは内部で自動的に処理され、**呼び出し元にはリトライ結果のみが返されます**。

#### グローバルリトライ設定

`OllamaConfig` で全リクエストのリトライ動作を設定できます：

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    MaxRetries = 3,              // 最大3回までリトライ
    RetryDelaySeconds = 1.0f,    // 初期遅延1秒
    HttpTimeoutSeconds = 60.0f   // リクエストタイムアウト60秒
};

var client = LLMClientFactory.CreateOllamaClient(config);

// リトライ設定を後から更新することも可能
config.MaxRetries = 5;
config.RetryDelaySeconds = 2.0f;
```

#### リトライ戦略：指数バックオフ

自動リトライは指数バックオフを使用します。遅延時間は以下のように計算されます：

```
待機時間 = RetryDelaySeconds × 2^(試行回数-1)

例）RetryDelaySeconds=1.0 の場合：
  1回目失敗 → 1秒待機してリトライ
  2回目失敗 → 2秒待機してリトライ
  3回目失敗 → 4秒待機してリトライ
  MaxRetries に達したため、エラーを呼び出し元に返却
```

この仕組みにより、サーバ負荷が高い時期に多くのリクエストが集中するのを緩和できます。

#### リトライ終了後のエラー処理

呼び出し元に返却されるエラーは、すべて `MaxRetries` 回のリトライを経たものです。
つまり、ネットワークの一時的な問題ではなく、**より根本的な問題**が発生していることを意味します。

```csharp
void SendMessageAndHandleError()
{
    var options = new ChatRequestOptions
    {
        ChatId = "session-1"
    };

    StartCoroutine(_client.SendMessageAsync(
        "メッセージ",
        (response, error) =>
        {
            if (error != null)
            {
                // ここに到達するエラーは、すべて MaxRetries 回のリトライ後
                Debug.LogError($"Error after retries: {error.Message}");
                
                HandleError(error);
                return;
            }

            Debug.Log($"Response: {response.Content}");
        },
        options
    ));
}

void HandleError(ChatError error)
{
    switch (error.ErrorType)
    {
        case LLMErrorType.ConnectionFailed:
        case LLMErrorType.Timeout:
            // 複数回のリトライ後もネットワークエラーが継続
            Debug.LogError(
                "Server is not responding. Please check:\n" +
                "1. Ollama server is running\n" +
                "2. Network connection is stable\n" +
                "3. Server URL is correct"
            );
            break;

        case LLMErrorType.ServerError:
            // サーバエラーが継続している
            Debug.LogError(
                $"Server error (HTTP {error.HttpStatus}). " +
                "Please check server logs and health status."
            );
            break;

        case LLMErrorType.ModelNotFound:
            Debug.LogError(
                $"Model not found: {error.Message}. " +
                "Please install the model with: ollama pull <model-name>"
            );
            break;

        case LLMErrorType.InvalidResponse:
            Debug.LogError(
                $"Invalid response from server: {error.Message}. " +
                "The server response format is incorrect."
            );
            break;

        case LLMErrorType.Cancelled:
            Debug.Log("Request was cancelled by user");
            break;

        default:
            Debug.LogError($"Unrecoverable error: {error.Message}");
            break;
    }
}
```

#### エラータイプ別の対応ガイド

| エラータイプ | リトライ対象 | 呼び出し元での対応 |
|-----------|----------|------------|
| `ConnectionFailed` | ✅ | サーバ起動確認、ネットワーク確認 |
| `ServerError` | ✅ | サーバログ確認、サーバ再起動検討 |
| `Timeout` | ✅ | `HttpTimeoutSeconds` を延長、サーバ性能確認 |
| `ModelNotFound` | ❌ | `ollama pull` でモデル導入 |
| `InvalidResponse` | ❌ | Ollama バージョン確認 |
| `Cancelled` | ❌ | UI適切に更新（ユーザー意図） |
| `Unknown` | ❌ | デバッグモードで詳細ログ確認 |

#### リトライ設定の推奨値

| シナリオ | MaxRetries | RetryDelaySeconds | HttpTimeoutSeconds |
|--------|-----------|-----------------|-----------------|
| 安定環境（LAN） | 2 | 0.5 | 30 |
| 通常環境（ローカルLLM） | 3 | 1.0 | 60 |
| 不安定環境 | 5 | 2.0 | 90 |
| リソース制約環境 | 1 | 0.2 | 20 |

#### ベストプラクティス

- **設定は初期化時に**：アプリ起動時に `OllamaConfig` で一度設定すれば、あとは自動処理
- **デバッグモードの活用**：`DebugMode = true` にするとリトライの詳細がログに出力される
- **ヘルスチェック機能**：`EnableHealthCheck = true` でサーバ起動後に自動チェック
- **エラーログ記録**：本番環境では、リトライ終了後のエラーを詳細に記録して分析

#### デバッグモードでのリトライ確認

リトライロジックの動作を確認する場合は、デバッグモードを有効にしてください：

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    MaxRetries = 3,
    RetryDelaySeconds = 1.0f,
    DebugMode = true  // リトライの詳細がログに出力される
};

var client = LLMClientFactory.CreateOllamaClient(config);

// ログ出力例：
// [Ollama] Sending request (attempt 1/3)
// [Ollama] Request failed (attempt 1/3): Connection reset (HTTP 0)
// [Ollama] Retrying in 1 seconds...
// [Ollama] Sending request (attempt 2/3)
// [Ollama] Request failed (attempt 2/3): Connection reset (HTTP 0)
// [Ollama] Retrying in 2 seconds...
// [Ollama] Sending request (attempt 3/3)
// [Ollama] Response received: {...}
```

## 実践例

これまでに学んだ機能を組み合わせた、実際のゲーム開発での応用例を紹介します。

### ゲーム内 NPC 会話システム

実際のゲーム開発でよくあるパターンの実装例です。UI統合、キャンセル処理、エラーハンドリングを含みます。

```csharp
using EasyLocalLLM.LLM;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class NPCChatSystem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text npcDialogueText;
    [SerializeField] private InputField playerInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button cancelButton;
    
    private OllamaClient _client;
    private CancellationTokenSource _cancellationTokenSource;
    private string _currentNPCId = "friendly-shopkeeper";

    void Start()
    {
        // 設定
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            MaxConcurrentSessions = 1,
            DebugMode = Application.isEditor
        };
        
        _client = LLMClientFactory.CreateOllamaClient(config);
        
        // UI イベント設定
        sendButton.onClick.AddListener(OnSendClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        cancelButton.gameObject.SetActive(false);
    }

    void OnSendClicked()
    {
        string userMessage = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;
        
        // UI を更新
        playerInputField.text = "";
        playerInputField.interactable = false;
        sendButton.interactable = false;
        cancelButton.gameObject.SetActive(true);
        npcDialogueText.text = "(考え中...)";
        
        // キャンセルトークンを作成
        _cancellationTokenSource = new CancellationTokenSource();
        
        // NPC の応答を取得（ストリーミング）
        var options = new ChatRequestOptions
        {
            ChatId = _currentNPCId,
            Temperature = 0.8f,
            Priority = 50,  // 通常優先度
            WaitIfBusy = true,
            CancellationToken = _cancellationTokenSource.Token,
            SystemPrompt = "あなたは親切な雑貨屋の店主です。冒険者に親しみやすく話しかけてください。"
        };
        
        StartCoroutine(_client.SendMessageStreamingAsync(
            userMessage,
            OnNPCResponse,
            options
        ));
    }

    void OnNPCResponse(ChatResponse response, ChatError error)
    {
        if (error != null)
        {
            HandleError(error);
            ResetUI();
            return;
        }
        
        // ストリーミングで段階的に表示
        npcDialogueText.text = response.Content;
        
        if (response.IsFinal)
        {
            ResetUI();
        }
    }

    void OnCancelClicked()
    {
        _cancellationTokenSource?.Cancel();
        npcDialogueText.text = "(会話をキャンセルしました)";
    }

    void HandleError(ChatError error)
    {
        switch (error.ErrorType)
        {
            case LLMErrorType.ConnectionFailed:
                npcDialogueText.text = "(店主は今席を外しているようだ...)";
                Debug.LogWarning("NPC システムエラー: サーバ接続失敗");
                break;
                
            case LLMErrorType.Cancelled:
                npcDialogueText.text = "(会話を中断した)";
                break;
                
            default:
                npcDialogueText.text = "(店主は言葉に詰まっている...)";
                Debug.LogError($"NPC システムエラー: {error.Message}");
                break;
        }
    }

    void ResetUI()
    {
        playerInputField.interactable = true;
        sendButton.interactable = true;
        cancelButton.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        _cancellationTokenSource?.Dispose();
        _client?.ClearAllMessages();
    }
}
```

### 複数 NPC の並列会話管理

複数の NPC と同時に会話する場合の実装例です。優先度設定とセッション管理の実用例を示しています。

```csharp
using EasyLocalLLM.LLM;
using System.Collections.Generic;
using UnityEngine;

public class MultiNPCManager : MonoBehaviour
{
    private OllamaClient _client;
    private Dictionary<string, NPCProfile> _npcProfiles;

    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            MaxConcurrentSessions = 2  // 2人まで同時会話可能
        };
        
        _client = LLMClientFactory.CreateOllamaClient(config);
        
        // NPC プロファイルを定義
        _npcProfiles = new Dictionary<string, NPCProfile>
        {
            ["shopkeeper"] = new NPCProfile
            {
                SessionId = "npc-shopkeeper",
                Priority = 0,  // 通常優先度
                SystemPrompt = "あなたは親切な雑貨屋の店主です。"
            },
            ["quest-giver"] = new NPCProfile
            {
                SessionId = "npc-quest-giver",
                Priority = 100,  // 高優先度（クエスト進行に重要）
                SystemPrompt = "あなたは重要なクエストを与える賢者です。"
            },
            ["random-villager"] = new NPCProfile
            {
                SessionId = "npc-villager",
                Priority = -50,  // 低優先度（フレーバー）
                SystemPrompt = "あなたは村の住人です。世間話をします。"
            }
        };
    }

    public void TalkToNPC(string npcId, string playerMessage, System.Action<string> onComplete)
    {
        if (!_npcProfiles.ContainsKey(npcId))
        {
            Debug.LogError($"Unknown NPC: {npcId}");
            return;
        }
        
        var profile = _npcProfiles[npcId];
        var options = new ChatRequestOptions
        {
            ChatId = profile.SessionId,
            Temperature = 0.8f,
            Priority = profile.Priority,
            WaitIfBusy = true,
            SystemPrompt = profile.SystemPrompt
        };
        
        StartCoroutine(_client.SendMessageAsync(
            playerMessage,
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"NPC {npcId} error: {error.Message}");
                    onComplete?.Invoke("...");
                    return;
                }
                
                onComplete?.Invoke(response.Content);
            },
            options
        ));
    }

    public void ResetNPCMemory(string npcId)
    {
        if (_npcProfiles.ContainsKey(npcId))
        {
            _client.ClearMessages(_npcProfiles[npcId].SessionId);
        }
    }

    private class NPCProfile
    {
        public string SessionId;
        public int Priority;
        public string SystemPrompt;
    }
}
```

### デバッグ用コンソール

開発中のテスト用コンソールの実装例です。コマンド処理、セッション管理、キャンセル処理を含みます。

```csharp
using EasyLocalLLM.LLM;
using System.Threading;
using UnityEngine;

public class DebugLLMConsole : MonoBehaviour
{
    private OllamaClient _client;
    private CancellationTokenSource _cts;
    private string _sessionId = "debug-console";

    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            DebugMode = true,
            MaxRetries = 1  // デバッグ用なのでリトライは少なめ
        };
        
        _client = LLMClientFactory.CreateOllamaClient(config);
        
        Debug.Log("=== LLM Debug Console ===");
        Debug.Log("Commands: /help, /clear, /sessions, /cancel");
    }

    void Update()
    {
        // コンソール入力のシミュレーション（実際は UI から入力）
        if (Input.GetKeyDown(KeyCode.Return))
        {
            // 例: ProcessCommand("/help");
        }
    }

    public void ProcessCommand(string input)
    {
        if (input.StartsWith("/"))
        {
            HandleCommand(input);
        }
        else
        {
            SendMessage(input);
        }
    }

    void HandleCommand(string command)
    {
        switch (command.ToLower())
        {
            case "/help":
                Debug.Log("Commands:\n" +
                         "/clear - Clear chat history\n" +
                         "/sessions - List all sessions\n" +
                         "/cancel - Cancel current request");
                break;
                
            case "/clear":
                _client.ClearMessages(_sessionId);
                Debug.Log("Chat history cleared.");
                break;
                
            case "/sessions":
                var sessions = _client.GetAllSessionIds();
                Debug.Log($"Active sessions ({sessions.Count}):\n" +
                         string.Join("\n", sessions));
                break;
                
            case "/cancel":
                _cts?.Cancel();
                Debug.Log("Request cancelled.");
                break;
                
            default:
                Debug.LogWarning($"Unknown command: {command}");
                break;
        }
    }

    void SendMessage(string message)
    {
        _cts = new CancellationTokenSource();
        
        var options = new ChatRequestOptions
        {
            ChatId = _sessionId,
            Temperature = 0.7f,
            CancellationToken = _cts.Token
        };
        
        Debug.Log($"User: {message}");
        
        StartCoroutine(_client.SendMessageStreamingAsync(
            message,
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

    void OnDestroy()
    {
        _cts?.Dispose();
    }
}
```

**これらの実践例は、`Samples/` フォルダにも実際のシーンと共に含まれています。**

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
| ServerUrl | string | http://localhost:11434 | Ollama サーバの URL。リモートサーバも指定可能 |
| DefaultModelName | string | mistral | デフォルトのモデル名。`ollama list` で確認可能 |
| MaxRetries | int | 3 | リトライの最大回数。ネットワークエラー時の自動再試行回数 |
| RetryDelaySeconds | float | 1.0f | リトライの初期遅延時間。指数バックオフで増加 |
| DefaultSeed | int | -1 | デフォルトシード値。-1 でランダム、固定値で再現性確保 |
| HttpTimeoutSeconds | float | 60.0f | HTTP リクエストのタイムアウト時間（秒） |
| DebugMode | bool | false | デバッグログの出力。true で詳細なログを表示 |
| AutoStartServer | bool | true | Ollama サーバを自動起動。false で手動管理 |
| EnableHealthCheck | bool | true | サーバ起動後にヘルスチェックを実行。接続確認用 |
| ExecutablePath | string | - | Ollama 実行ファイルのパス。AutoStartServer=true の場合に必要 |
| ModelsDirectory | string | ./Models | Ollama モデルディレクトリ。環境変数 OLLAMA_MODELS に相当 |
| MaxConcurrentSessions | int | 1 | 同時実行可能なセッション数。GPU メモリ容量に応じて調整 |

## デフォルト設定について

### デフォルト値の設計方針

このライブラリのデフォルト値は、**開発環境での快速な反復開発**を優先して設計されています。

| 設定項目 | デフォルト値 | 設計理由 | 本番環境での推奨値 |
|--------|---------|--------|-----------------|
| `AutoStartServer` | `true` | 開発時、サーバの手動起動をスキップできる | `false` |
| `MaxRetries` | `3` | ネットワーク不安定時に3回のリトライで大半の問題をカバー | `2`～`5`（環境に応じて） |
| `RetryDelaySeconds` | `1.0f` | 指数バックオフで最大4秒の待機に。開発効率優先 | `1.0f`～`2.0f` |
| `HttpTimeoutSeconds` | `60.0f` | ローカル環境での大型モデル推論を想定 | `30.0f`～`90.0f` |
| `DebugMode` | `false` | ログ量を抑制 | `true`（トラブルシューティング時） |
| `EnableHealthCheck` | `true` | サーバ起動の確実性を重視 | `true` |
| `MaxConcurrentSessions` | `1` | メモリ制約やGPUリソース制限を想定 | `1`～`4`（GPU容量に応じて） |

#### AutoStartServer について

**`AutoStartServer = true` が推奨される場合：**
- ✅ 開発環境（Unity エディタでのテスト）
- ✅ スタンドアロンビルド（ユーザーがOllamaをインストールしていない場合）
- ✅ 完全に独立したアプリケーション

**`AutoStartServer = false` に変更すべき場合：**
- ❌ Ollama がシステムサービスとして別途起動している
- ❌ 複数のアプリケーションが同一の Ollama インスタンスを共有
- ❌ サーバのライフサイクルを明示的に制御したい
- ❌ リモートサーバに接続する場合

**推奨設定例：**

```csharp
// 開発環境
var devConfig = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    AutoStartServer = true,      // サーバを自動起動
    DebugMode = true,            // 詳細ログ出力
    MaxRetries = 3,              // 開発中はリトライ多めに
    EnableHealthCheck = true
};

// 本番環境（Windowsスタンドアロン）
var prodConfig = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    AutoStartServer = true,      // ユーザー環境で自動起動
    ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
    ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
    DebugMode = false,           // ログ出力は最小限に
    MaxRetries = 2,              // 安定環境なので少なめ
    HttpTimeoutSeconds = 90.0f,  // 大型モデル対応
    EnableHealthCheck = true
};

// 本番環境（システムサービス）
var serviceConfig = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    DefaultModelName = "mistral",
    AutoStartServer = false,     // 別途起動されているため不要
    DebugMode = false,
    MaxRetries = 2,
    HttpTimeoutSeconds = 60.0f,
    EnableHealthCheck = false    // 既に起動済みのため不要
};

// 本番環境（リモートサーバ）
var remoteConfig = new OllamaConfig
{
    ServerUrl = "http://llm-server.example.com:11434",
    DefaultModelName = "mistral",
    AutoStartServer = false,     // リモートのため起動できない
    DebugMode = false,
    MaxRetries = 5,              // ネットワーク不安定を想定
    RetryDelaySeconds = 2.0f,    // より長い待機
    HttpTimeoutSeconds = 120.0f, // ネットワーク遅延を考慮
    EnableHealthCheck = false    // リモートなため任意
};
```

#### MaxRetries について

**なぜデフォルトが 3 なのか：**

```
試行回数ごとの成功率（ネットワークエラーの場合）

         成功率  累積成功率
1回目   ~70%   70%
2回目   ~80%   94%        
3回目   ~85%   99%        ← ほとんどの一時的エラーをカバー
4回目   ~90%   99.9%
5回目   ~92%   99.99%
```

3回のリトライでネットワークの一時的な問題の **99%** をカバーできることから、デフォルトとして設定されています。

**ユースケース別推奨値：**

```csharp
// 開発環境：試行回数を多めに
if (isDevelopment)
{
    config.MaxRetries = 5;  // デバッグ時の失敗をなるべく避ける
}

// 本番環境・LAN：少なめで十分
if (isProduction && isLocalNetwork)
{
    config.MaxRetries = 2;  // ネットワークは安定している
}

// 本番環境・不安定なネットワーク：多めに
if (isProduction && isUnstableNetwork)
{
    config.MaxRetries = 5;  // より強力な再試行
    config.RetryDelaySeconds = 2.0f;  // 待機時間も長めに
}

// モバイルネットワーク環境
if (isMobileNetwork)
{
    config.MaxRetries = 4;
    config.RetryDelaySeconds = 1.5f;
    config.HttpTimeoutSeconds = 90.0f;  // より長いタイムアウト
}
```

#### その他のデフォルト値について

**`HttpTimeoutSeconds = 60.0f`**
- ローカル LLM の場合、通常は 1～30 秒で応答
- 大型モデル（llama2-70b など）の推論時間を考慮して 60 秒

**`RetryDelaySeconds = 1.0f`**
- 指数バックオフ：1秒 → 2秒 → 4秒 → 8秒…
- 最大 MaxRetries=3 で約 7 秒の待機
- サーバ一時的な問題からの復帰を想定

**`MaxConcurrentSessions = 1`**
- GPU メモリが限定的な環境を想定
- 複数セッションの並列実行には大きなリソースが必要
- 必要に応じて増やす（デフォルトは保守的に設定）

### 推奨される環境別設定パターン

#### Pattern 1: Unity エディタでの開発

```csharp
void SetupDevelopmentEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://localhost:11434",
        DefaultModelName = "mistral",
        AutoStartServer = true,
        DebugMode = true,
        MaxRetries = 5,
        HttpTimeoutSeconds = 120.0f,  // 長めに設定
        EnableHealthCheck = true,
        MaxConcurrentSessions = 1
    };
    
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

#### Pattern 2: Windows スタンドアロンビルド

```csharp
void SetupWindowsStandaloneEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://localhost:11434",
        DefaultModelName = "mistral",
        AutoStartServer = true,
        ExecutablePath = Application.streamingAssetsPath + "/Ollama/ollama.exe",
        ModelsDirectory = Application.streamingAssetsPath + "/Ollama/models",
        DebugMode = false,
        MaxRetries = 3,
        HttpTimeoutSeconds = 90.0f,
        EnableHealthCheck = true,
        MaxConcurrentSessions = 2  // スペックに応じて調整
    };
    
    OllamaServerManager.Initialize(config);
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

#### Pattern 3: システムサービス環境

```csharp
void SetupSystemServiceEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://localhost:11434",
        DefaultModelName = "mistral",
        AutoStartServer = false,  // サービスが別途起動
        DebugMode = false,
        MaxRetries = 2,  // 安定環境
        HttpTimeoutSeconds = 60.0f,
        EnableHealthCheck = false,
        MaxConcurrentSessions = 4  // リソースに余裕があれば
    };
    
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

#### Pattern 4: ネットワーク越しのリモート接続

```csharp
void SetupRemoteServerEnvironment()
{
    var config = new OllamaConfig
    {
        ServerUrl = "http://llm-server.company.com:11434",
        DefaultModelName = "mistral",
        AutoStartServer = false,  // リモートのため起動不可
        DebugMode = false,
        MaxRetries = 5,  // ネットワーク不安定を想定
        RetryDelaySeconds = 2.0f,  // 長めの待機
        HttpTimeoutSeconds = 120.0f,  // ネットワーク遅延対応
        EnableHealthCheck = false,
        MaxConcurrentSessions = 2
    };
    
    var client = LLMClientFactory.CreateOllamaClient(config);
}
```

### トラブルシューティングのためのチェックリスト

| 問題 | 原因 | 推奨される設定変更 |
|-----|-----|------------------|
| 頻繁に失敗する | ネットワーク不安定 | `MaxRetries` を増やす、`RetryDelaySeconds` を増やす |
| タイムアウトが頻発 | サーバ処理が遅い | `HttpTimeoutSeconds` を増やす |
| メモリ不足 | 複数セッション実行 | `MaxConcurrentSessions` を減らす |
| サーバが起動しない | 自動起動失敗 | `AutoStartServer=false` にして手動起動 |
| デバッグが困難 | ログが不足 | `DebugMode=true` に変更 |

## エラーハンドリング

### エラータイプと実際のメッセージ例

`ChatError` には以下のプロパティが含まれます：

```csharp
public class ChatError
{
    public LLMErrorType ErrorType { get; set; }  // エラーの種類
    public string Message { get; set; }           // エラーメッセージ
    public Exception Exception { get; set; }      // 例外情報（あれば）
    public int? HttpStatus { get; set; }          // HTTP ステータスコード（あれば）
}
```

### エラータイプ別の詳細

#### 1. ConnectionFailed（サーバ接続失敗）

**典型的なメッセージ例：**
```
Cannot connect to Ollama server at 'http://localhost:11434'. 
Please check: (1) Server is running, (2) URL is correct, (3) Firewall settings. 
Original error: Connection refused
```

**原因：**
- Ollama サーバが起動していない
- ポート番号が間違っている
- ファイアウォールがブロックしている

**対応方法：**
```csharp
if (error.ErrorType == LLMErrorType.ConnectionFailed)
{
    Debug.LogError($"接続エラー: {error.Message}");
    
    // ユーザーガイダンス
    ShowUserMessage(
        "サーバに接続できません",
        "Ollama サーバが起動していることを確認してください。\n" +
        $"接続先: {config.ServerUrl}"
    );
}
```

#### 2. ServerError（サーバ側エラー）

**典型的なメッセージ例：**
```
Ollama server error (HTTP 503). 
Please check: (1) Server logs, (2) Model is loaded correctly, (3) Server resources (memory/GPU). 
Original error: Service temporarily unavailable
```

**原因：**
- サーバ内部でクラッシュした
- サーバが過負荷状態
- モデルロード失敗
- GPU/CPUメモリ不足

**対応方法：**
```csharp
if (error.ErrorType == LLMErrorType.ServerError)
{
    Debug.LogError($"サーバエラー: {error.Message}");
    
    if (error.HttpStatus == 503)
    {
        ShowUserMessage(
            "サーバが過負荷です",
            "サーバが一時的に利用できません。しばらく待ってから再試行してください。"
        );
    }
    else
    {
        ShowUserMessage(
            "サーバエラー",
            $"サーバでエラーが発生しました (HTTP {error.HttpStatus})。\n" +
            "サーバを再起動してみてください。"
        );
    }
}
```

#### 3. ModelNotFound（モデルが見つからない）

**典型的なメッセージ例：**
```
Model 'mistral' not found (HTTP 404). 
Please run: 'ollama pull mistral' or check the model name is correct. 
Use 'ollama list' to see installed models.
```

**原因：**
- 指定したモデル名が間違っている
- モデルがインストールされていない
- モデル名のタイポ

**対応方法：**
```csharp
if (error.ErrorType == LLMErrorType.ModelNotFound)
{
    Debug.LogError($"モデルエラー: {error.Message}");
    
    string modelName = config.DefaultModelName;
    ShowUserMessage(
        "モデルが見つかりません",
        $"モデル '{modelName}' がインストールされていません。\n\n" +
        $"PowerShell で以下を実行してください：\n" +
        $"ollama pull {modelName}\n\n" +
        "または設定から別のモデルを選択してください。"
    );
    
    // モデル選択UIを表示
    ShowModelSelectionUI();
}
```

#### 4. Timeout（タイムアウト）

**典型的なメッセージ例：**
```
Request timed out after 60 seconds. 
Please consider: (1) Increase HttpTimeoutSeconds, (2) Use a smaller model, (3) Check server performance. 
Original error: The operation has timed out
```

**原因：**
- モデルの推論時間が長すぎる
- サーバ性能不足
- ネットワーク遅延
- 大型モデル使用時

**対応方法：**
```csharp
if (error.ErrorType == LLMErrorType.Timeout)
{
    Debug.LogError($"タイムアウトエラー: {error.Message}");
    
    ShowUserMessage(
        "処理がタイムアウトしました",
        $"処理に {config.HttpTimeoutSeconds}秒 以上かかりました。\n\n" +
        "対策：\n" +
        "1. より小さなモデルを使用する\n" +
        "2. タイムアウト時間を延長する\n" +
        "3. サーバのGPU/CPU性能を確認する"
    );
    
    // タイムアウト時間を自動延長（オプション）
    if (config.HttpTimeoutSeconds < 180.0f)
    {
        config.HttpTimeoutSeconds *= 1.5f;
        Debug.Log($"タイムアウトを {config.HttpTimeoutSeconds}秒 に延長しました");
    }
}
```

#### 5. InvalidResponse（レスポンスパース失敗）

**典型的なメッセージ例：**
```
Failed to parse response from model 'mistral': Unexpected character. 
Check Ollama version compatibility.
```

**原因：**
- サーバのバージョンが古い
- API 仕様の変更
- レスポンスの破損
- ライブラリとサーバの互換性問題

**対応方法：**
```csharp
if (error.ErrorType == LLMErrorType.InvalidResponse)
{
    Debug.LogError($"レスポンスパースエラー: {error.Message}");
    
    ShowUserMessage(
        "サーバレスポンスの解析に失敗しました",
        "Ollama サーバのバージョンを確認してください。\n" +
        "推奨バージョン: v0.1.0 以降\n\n" +
        "サーバを再起動しても解決しない場合は、\n" +
        "デバッグモードを有効にして詳細を確認してください。"
    );
    
    // デバッグモードを有効化
    if (!config.DebugMode)
    {
        config.DebugMode = true;
        Debug.Log("デバッグモードを有効にしました");
    }
}
```

#### 6. Cancelled（ユーザーによるキャンセル）

**典型的なメッセージ例：**
```
Request cancelled for session 'chat-session-1' by user
```

**原因：**
- ユーザーが `CancellationToken.Cancel()` を呼び出した
- タイムアウト付きキャンセルトークンで時間切れ

**対応方法：**
```csharp
if (error.ErrorType == LLMErrorType.Cancelled)
{
    Debug.Log($"キャンセル: {error.Message}");
    
    // UI を元の状態に戻す
    HideProgressBar();
    EnableInputField();
}
```

#### 7. Unknown（その他のエラー）

**典型的なメッセージ例：**
```
Client is busy. Running sessions: 1/1, Pending requests: 2. 
Set WaitIfBusy=true to queue the request.
```

**原因：**
- リクエストが多すぎる（`WaitIfBusy=false` の場合）
- 予期しない例外

**対応方法：**
```csharp
if (error.ErrorType == LLMErrorType.Unknown)
{
    Debug.LogError($"エラー: {error.Message}");
    
    if (error.Message.Contains("busy"))
    {
        ShowUserMessage(
            "クライアントが使用中です",
            "前のリクエストが完了するまで待機してください。\n" +
            "または WaitIfBusy=true に設定して自動的にキューに入れることができます。"
        );
    }
    else
    {
        ShowUserMessage(
            "予期しないエラー",
            $"エラーが発生しました: {error.Message}\n\n" +
            "デバッグモードを有効にして詳細を確認してください。"
        );
        
        if (error.Exception != null)
        {
            Debug.LogException(error.Exception);
        }
    }
}
```

### 包括的なエラーハンドリング例

```csharp
public class RobustChatManager : MonoBehaviour
{
    private OllamaClient _client;
    private OllamaConfig _config;

    void Start()
    {
        _config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            DebugMode = true
        };
        
        _client = LLMClientFactory.CreateOllamaClient(_config);
    }

    void SendMessageWithErrorHandling(string message)
    {
        var options = new ChatRequestOptions
        {
            ChatId = "session-1"
        };

        StartCoroutine(_client.SendMessageAsync(
            message,
            (response, error) =>
            {
                if (error != null)
                {
                    HandleError(error);
                    return;
                }

                Debug.Log($"Assistant: {response.Content}");
            },
            options
        ));
    }

    void HandleError(ChatError error)
    {
        // エラーログを記録（本番環境用）
        LogErrorToFile(error);

        switch (error.ErrorType)
        {
            case LLMErrorType.ConnectionFailed:
                ShowUserMessage(
                    "サーバに接続できません",
                    "Ollama サーバが起動していることを確認してください。"
                );
                break;

            case LLMErrorType.ServerError:
                ShowUserMessage(
                    "サーバエラーが発生しました",
                    $"サーバを再起動してください。(エラーコード: {error.HttpStatus})"
                );
                break;

            case LLMErrorType.ModelNotFound:
                string modelName = _config.DefaultModelName;
                ShowUserMessage(
                    "モデルが見つかりません",
                    $"モデル '{modelName}' がインストールされていません。\n" +
                    "設定からモデルをインストールしてください。"
                );
                ShowModelSelectionUI();
                break;

            case LLMErrorType.Timeout:
                ShowUserMessage(
                    "処理がタイムアウトしました",
                    "サーバの処理に時間がかかっています。もう一度試すか、より小さなモデルを使用してください。"
                );
                break;

            case LLMErrorType.InvalidResponse:
                ShowUserMessage(
                    "予期しないエラー",
                    "サーバとの通信に問題が発生しました。アプリを再起動してください。"
                );
                break;

            case LLMErrorType.Cancelled:
                Debug.Log("リクエストがキャンセルされました");
                break;

            default:
                ShowUserMessage(
                    "エラーが発生しました",
                    $"詳細: {error.Message}"
                );
                break;
        }
    }

    void LogErrorToFile(ChatError error)
    {
        string logMessage = $"[{DateTime.Now}] {error.ErrorType}: {error.Message}";
        if (error.HttpStatus.HasValue)
        {
            logMessage += $" (HTTP {error.HttpStatus})";
        }
        
        Debug.Log(logMessage);
        // 本番環境ではファイルに記録
        // File.AppendAllText("error_log.txt", logMessage + "\n");
    }

    void ShowUserMessage(string title, string message)
    {
        Debug.LogWarning($"{title}: {message}");
        // UI でダイアログ表示など
    }

    void ShowModelSelectionUI()
    {
        Debug.Log("モデル選択画面を表示");
    }
}
```

### エラー発生時のチェックリスト

| エラータイプ | 確認項目 | 解決方法 |
|-----------|---------|---------|
| `ConnectionFailed` | サーバ起動、URL、ポート | `ollama serve` でサーバ起動、URLを確認 |
| `ServerError` | サーバログ、モデル状態、リソース | サーバ再起動、`ollama list` で確認 |
| `ModelNotFound` | モデル名、インストール状況 | `ollama pull <model-name>` 実行 |
| `Timeout` | タイムアウト設定、モデルサイズ | `HttpTimeoutSeconds` を増やす、小型モデル使用 |
| `InvalidResponse` | Ollama バージョン | 最新版にアップデート、互換性確認 |
| `Cancelled` | キャンセル処理 | UI を適切に更新 |
| `Unknown` | DebugMode、ログ | `DebugMode=true` で詳細確認 |

### デバッグのヒント

**詳細ログを有効化：**
```csharp
config.DebugMode = true;  // 詳細なログが出力される
```

**エラー内容の確認：**
```csharp
if (error != null)
{
    Debug.Log($"エラータイプ: {error.ErrorType}");
    Debug.Log($"メッセージ: {error.Message}");
    Debug.Log($"HTTPステータス: {error.HttpStatus}");
    
    if (error.Exception != null)
    {
        Debug.LogException(error.Exception);
    }
}
```

**サーバ状態の確認コマンド：**
```powershell
# サーバが起動しているか確認
netstat -an | findstr :11434

# インストール済みモデルを確認
ollama list

# サーバログを確認（サーバ起動時のウィンドウ）
```

## 今後の拡張予定

- [ ] async/await サポート
- [ ] llama.cpp クライアント
- [ ] OpenAI API クライアント
- [ ] 複数モデルの並列処理
- [ ] メッセージ永続化
