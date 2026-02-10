# EasyLocalLLM Runtime Library

Ollama を使用してローカル LLM と通信するための Unity ライブラリです。

## 目次

- [1. 主な機能](#1-主な機能)
- [2. クイックスタート](#2-クイックスタート)
- [3. 制限事項](#3-制限事項)
- [4. 使用方法](#4-使用方法)
  - [4.1 基本的な初期化](#41-基本的な初期化)
  - [4.2 メッセージ送信（一度に完全回答を取得）](#42-メッセージ送信一度に完全回答を取得)
  - [4.3 ストリーミング送信（段階的に回答を受け取る）](#43-ストリーミング送信段階的に回答を受け取る)
  - [4.4 Ollama サーバの自動管理](#44-ollama-サーバの自動管理)
  - [4.5 セッション管理](#45-セッション管理)
  - [4.6 システムプロンプト](#46-システムプロンプト)
  - [4.7 優先度スケジューリング](#47-優先度スケジューリング)
  - [4.8 キャンセル](#48-キャンセル)
  - [4.9 リトライとエラーハンドリング](#49-リトライとエラーハンドリング)
  - [4.10 メッセージ永続化](#410-メッセージ永続化)
  - [4.11 ツール（Function Calling）](#411-ツールfunction-calling)
  - [4.12 JSON形式のレスポンス指定](#412-json形式のレスポンス指定)
- [5. 実践例](#5-実践例)
- [6. クラス構成](#6-クラス構成)
- [7. 設定オプション](#7-設定オプション)
- [8. デフォルト設定について](#8-デフォルト設定について)
- [9. エラータイプと対処方法](#9-エラータイプと対処方法)
- [10. 今後の拡張予定](#10-今後の拡張予定)

## 1. 主な機能

- ✅ **設定管理の外部化**：`OllamaConfig` で柔軟に設定可能
- ✅ **自動リトライ機能**：リクエスト失敗時の指数バックオフ対応
- ✅ **ストリーミング対応**：段階的な回答受け取り
- ✅ **セッション管理**：チャットセッションごとの履歴管理
- ✅ **セッション固有のシステムプロンプト**：セッションごとに異なるプロンプトを設定可能
- ✅ **エラーハンドリング**：詳細なエラー情報提供
- ✅ **サーバライフサイクル管理**：自動起動・停止機能

## 2. クイックスタート

最小限のコードで動作確認できます。

**前提条件**: Ollama サーバが `localhost:11434` で起動済み、`mistral` モデルがインストール済み

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class QuickStart : MonoBehaviour
{
    void Start()
    {
        var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
        
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

**詳しい設定や使い方は、[4.1 基本的な初期化](#41-基本的な初期化)を参照してください。**

**Ollama のセットアップが未完了の場合は、[4.4 Ollama サーバの自動管理](#44-ollama-サーバの自動管理)を参照してください。**

## 3. 制限事項

### ⚠️ 重要な制約

- **Unity 専用**：UnityWebRequest に依存しているため、Unity 外では動作しません
- **Windows 専用**：現時点では Windows 以外に対応していません
- **メインスレッド依存**：Task 版 API も内部的にはコルーチンで動作します

### 処理パターン

```csharp
// ✅ Task 版 API（await/async）
async Task SendMessageAsync()
{
    var result = await client.SendMessageTaskAsync("Hello");
    Debug.Log(result.Content);
}

// ✅ コルーチン版 API
void SendMessage()
{
    StartCoroutine(client.SendMessageAsync(
        "Hello",
        (response, error) =>
        {
            // コールバックで結果を処理
        }
    ));
}
```

**補足**: Task 版 API はコルーチンをブリッジした実装です。Unity 外では動作しません。

## 4. 使用方法

### 4.1 基本的な初期化

クイックスタートではデフォルト設定を使用しましたが、ここでは詳細な設定方法を説明します。

**前提条件**: Ollama サーバが `localhost:11434` で起動済み、かつモデルがインストール済みであることを前提とします。

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

### 4.2 メッセージ送信（一度に完全回答を取得）

コールバックは**1回だけ**呼ばれます。
短い応答や完全な回答が必要な場合に使用してください。

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

void SendMessage()
{
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
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

**Task 版（await/async）**

```csharp
using EasyLocalLLM.LLM;
using System.Threading.Tasks;
using UnityEngine;

async Task SendMessageAsync()
{
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        Temperature = 0.7f,
        Seed = 42
    };

    var response = await _client.SendMessageTaskAsync("こんにちは", options);
    Debug.Log($"Assistant: {response.Content}");
}
```

### 4.3 ストリーミング送信（段階的に回答を受け取る）

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
        SessionId = "chat-session-1",
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

**Task 版（進捗を受け取る）**

```csharp
using EasyLocalLLM.LLM;
using System;
using System.Threading.Tasks;
using UnityEngine;

async Task SendStreamingMessageAsync()
{
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
        Temperature = 0.7f
    };

    var progress = new Progress<ChatResponse>(response =>
    {
        if (!response.IsFinal)
        {
            Debug.Log($"Receiving: {response.Content}");
        }
    });

    var final = await _client.SendMessageStreamingTaskAsync(
        "日本語で詩を書いてください",
        progress,
        options
    );

    Debug.Log($"Complete: {final.Content}");
}
```

### 4.4 Ollama サーバの自動管理

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

1. PowerShell を開き、以下を実行：

```powershell
$env:OLLAMA_MODELS="<プロジェクトパス>\Assets\StreamingAssets\Ollama\models"
mkdir $env:OLLAMA_MODELS
cd "<プロジェクトパス>\Assets\StreamingAssets\Ollama"
.\ollama.exe serve
```

2. 別の PowerShell ウィンドウで以下を実行：

```powershell
# 利用可能なモデル例：mistral, llama2, neural-chat, dolphin-mixtral など
cd "<プロジェクトパス>\Assets\StreamingAssets\Ollama"
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

GGUF ファイルと同じディレクトリに `Modelfile` を作成します。

**基本テンプレート：**
```
FROM ./your-model-name.Q4_K_M.gguf

PARAMETER temperature 0.7
PARAMETER top_k 40
PARAMETER top_p 0.9
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

### 4.5 セッション管理

#### セッションの概念

`SessionId` で指定されたセッションは、以下の特徴を持ちます：

- **自動作成**：初回の `SendMessageAsync()` / `SendMessageStreamingAsync()` で自動作成
- **履歴の自動蓄積**：同じ `SessionId` で送信したメッセージと応答は自動的に累積
- **永続性**：`ClearMessages()` するまでメモリに保持される
- **独立管理**：異なる `SessionId` はそれぞれ独立した履歴を持つ

#### 基本的なセッション管理

```csharp
void ManageSessions()
{
    // 異なるセッションIDで複数の会話を管理
    var session1Options = new ChatRequestOptions { SessionId = "session-1" };
    var session2Options = new ChatRequestOptions { SessionId = "session-2" };

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
        var userASession = new ChatRequestOptions { SessionId = "user-a-session" };
        StartCoroutine(_client.SendMessageAsync(
            "ユーザーAからのメッセージ",
            OnResponse,
            userASession
        ));
    }

    // ユーザーB との会話セッション
    void ChatWithUserB()
    {
        var userBSession = new ChatRequestOptions { SessionId = "user-b-session" };
        StartCoroutine(_client.SendMessageAsync(
            "ユーザーBからのメッセージ",
            OnResponse,
            userBSession
        ));
    }

    // テーマ別セッション（同じユーザーでも異なるテーマを管理）
    void ChatAboutTopic(string topic)
    {
        var topicSession = new ChatRequestOptions { SessionId = $"topic-{topic}" };
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
            Debug.LogError($"エラー: {error.Message}");
            return;
        }
        
        Debug.Log($"応答: {response.Content}");
    }

    // アプリ終了時
    void OnApplicationQuit()
    {
        _client.ClearAllMessages();  // メモリをクリーンアップ
    }
}
```

#### セッション管理の注意点

- **SessionId が null の場合**：Guid で自動生成される一度限りのセッション
- **MaxHistory 設定**：デフォルト50メッセージ。超過時は古いメッセージから削除
- **セッション間の独立性**：あるセッションのシステムプロンプトが他に影響することはない
- **メモリ管理**：多くのセッションを保持し続けるとメモリ消費が増加。不要なセッションは `ClearMessages()` で削除推奨
- **セッション情報の取得**：`GetSession()` で返される `ChatSession` オブジェクトから、作成日時（`CreatedAt`）、最終更新日時（`LastUpdatedAt`）、メッセージ履歴（`History`）にアクセス可能

### 4.6 システムプロンプト

#### システムプロンプトとは

**システムプロンプト**は、LLM に対して「どのような役割や性格で返答するか」を指示するための特別なプロンプトです。ユーザーメッセージとは異なり、会話全体を通じて LLM の動作を制御します。

**基本的な役割：**

- **キャラクター・ロール定義**：「医師として」「カスタマーサポートとして」などの役割を付与
- **回答スタイル指定**：「簡潔に答える」「詳しく説明する」などのトーンを制御
- **機能制限**：「このトピックについてのみ答える」など、回答範囲を限定
- **言語・フォーマット指定**：「日本語で答える」「JSON形式で返す」など

**具体的な例：**

医師ロールの場合、以下のようなシステムプロンプトを設定します：

```
"You are a professional medical advisor. Provide accurate medical information but always 
remind the user to consult a real doctor for serious conditions. Respond in Japanese."
```

ユーザーが「頭痛がします」と聞くと、LLM は医師の立場で回答します。
一方、「プログラミングを教えてください」と聞くと、医師の性格を保ちながら、その質問には適切に対応します。

#### グローバルシステムプロンプト（全セッション共通）

すべてのセッションに適用される共通プロンプトは `OllamaConfig` で設定します。

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    Model = "mistral",
    GlobalSystemPrompt = "You are a helpful assistant. Always be polite, accurate, and respond in Japanese."
};

var client = LLMClientFactory.CreateOllamaClient(config);
```

このグローバルプロンプトは、セッション固有またはリクエスト個別のプロンプトが設定されていない場合に使用されます。

**グローバルプロンプト設定のベストプラクティス：**

- **言語指定**：「Always respond in Japanese」など、使用言語を統一
- **基本的な倫理観**：「Be honest and helpful」などの基本姿勢
- **応答形式**：デフォルトの返答スタイル（詳細/簡潔など）

#### セッション固有のシステムプロンプト

セッション固有のシステムプロンプトを設定することで、同じアプリケーション内で異なるロールやキャラクターを管理できます。例えば、医師、エンジニア、顧客サポートなど、複数の専門家を同時に管理できます。

**プロンプトの優先度（高い順）：**

1. **リクエスト個別のシステムプロンプト**：`ChatRequestOptions.SystemPrompt`（1回のリクエストのみに適用）
2. **セッション固有のシステムプロンプト**：`SetSessionSystemPrompt()` で設定（そのセッション内のすべてのメッセージに適用）
3. **グローバルシステムプロンプト**：`OllamaConfig.GlobalSystemPrompt`（全セッション共通）

**例：優先度のデモンストレーション**

```csharp
// グローバル設定（すべてのセッション共通）
var config = new OllamaConfig
{
    GlobalSystemPrompt = "You are a general assistant."
};

// セッション固有プロンプトを設定
_client.SetSessionSystemPrompt(
    "session-1",
    "You are a technical expert. Explain complex topics with technical depth."
);

// ケース1：リクエスト個別プロンプト設定（最優先）
var options1 = new ChatRequestOptions
{
    SessionId = "session-1",
    SystemPrompt = "You are a beginner-friendly tutor. Explain simply."  // ← これが優先される
};
StartCoroutine(_client.SendMessageAsync("プログラミングとは何ですか？", OnResponse, options1));
// 結果："beginner-friendly tutor" として、簡単な説明で返答

// ケース2：セッション固有プロンプトのみ使用
StartCoroutine(_client.SendMessageAsync("C#とJavaの違いは？", OnResponse, 
    new ChatRequestOptions { SessionId = "session-1" }
));
// 結果："technical expert" として、技術的に詳しく返答

// ケース3：異なるセッション（セッション固有プロンプト未設定）
StartCoroutine(_client.SendMessageAsync("こんにちは", OnResponse,
    new ChatRequestOptions { SessionId = "session-2" }
));
// 結果：グローバルプロンプト "general assistant" として返答
```

#### セッション固有プロンプトの設定

**単一セッションへのプロンプト設定：**

```csharp
// セッション "doctor-session" にカスタムシステムプロンプトを設定
_client.SetSessionSystemPrompt(
    "doctor-session",
    "You are a professional medical advisor. Provide accurate medical information. " +
    "Always remind the user to consult a real doctor for serious conditions. Respond in Japanese."
);

// このセッションでメッセージを送信（医師として返答）
StartCoroutine(_client.SendMessageAsync(
    "頭痛がします。何が原因ですか？",
    OnResponse,
    new ChatRequestOptions { SessionId = "doctor-session" }
));
```

**複数セッションへの一括設定：**

```csharp
// 複数のセッションに同じプロンプトを設定（効率的なロール初期化）
var sessionIds = new List<string> { "support-1", "support-2", "support-3" };
_client.SetSystemPromptForMultipleSessions(
    sessionIds,
    "You are a helpful customer support specialist. Respond concisely and professionally in Japanese."
);
```

#### セッション固有プロンプトの取得と管理

**プロンプトの取得：**

```csharp
// セッション "doctor-session" のシステムプロンプトを取得
string prompt = _client.GetSessionSystemPrompt("doctor-session");
if (prompt != null)
{
    Debug.Log($"Current session prompt: {prompt}");
}
else
{
    Debug.Log("No session-specific prompt set. Using global prompt.");
}
```

**セッション固有プロンプトのリセット：**

```csharp
// セッション固有プロンプトを削除（グローバルプロンプトに戻す）
_client.ResetSessionSystemPrompt("doctor-session");
// 以降、このセッションではグローバルプロンプトが使用される
```

**すべてのセッション固有プロンプトをリセット：**

```csharp
// すべてのセッション固有プロンプトを削除
_client.ResetAllSessionSystemPrompts();
// アプリケーション全体でグローバルプロンプトのみが使用される
```

#### 実践例：複数ロールの管理

異なるロール（キャラクター）を複数管理する具体的な例です：

```csharp
void InitializeMultipleRoles()
{
    // 医師ロール
    _client.SetSessionSystemPrompt(
        "doctor-session",
        "You are a professional medical advisor. Provide evidence-based medical information. " +
        "Always remind users to consult a licensed physician for serious health concerns. Respond in Japanese."
    );

    // ソフトウェアエンジニアロール
    _client.SetSessionSystemPrompt(
        "engineer-session",
        "You are an expert software engineer with 10 years of experience. " +
        "Help users with coding problems, design patterns, best practices, and architecture decisions. " +
        "Prefer modern C# patterns and explain trade-offs. Respond in Japanese."
    );

    // 日本語翻訳者ロール
    _client.SetSessionSystemPrompt(
        "translator-session",
        "You are a professional Japanese translator with expertise in technical translation. " +
        "Translate accurately while preserving meaning, nuance, and context. " +
        "Maintain consistency in technical terminology."
    );

    // 顧客サポートロール
    _client.SetSessionSystemPrompt(
        "support-session",
        "You are a friendly and professional customer support specialist. " +
        "Help customers with common questions and issues. Be empathetic and solution-focused. " +
        "Respond in Japanese with a warm tone."
    );
}

// 各ロールとの会話
void ChatWithMultipleRoles()
{
    // 医師に医学について相談
    StartCoroutine(_client.SendMessageAsync(
        "血圧が高いです。対策は？",
        OnResponse,
        new ChatRequestOptions { SessionId = "doctor-session" }
    ));

    // エンジニアにコード相談
    StartCoroutine(_client.SendMessageAsync(
        "C#でシングルトンパターンを実装する最善の方法は？",
        OnResponse,
        new ChatRequestOptions { SessionId = "engineer-session" }
    ));

    // 翻訳者に翻訳を依頼
    StartCoroutine(_client.SendMessageAsync(
        "Translate: 'The quick brown fox jumps over the lazy dog.'",
        OnResponse,
        new ChatRequestOptions { SessionId = "translator-session" }
    ));

    // カスタマーサポートに問い合わせ
    StartCoroutine(_client.SendMessageAsync(
        "商品が届きません。どうしたらいいですか？",
        OnResponse,
        new ChatRequestOptions { SessionId = "support-session" }
    ));
}

// クリーンアップ
void CleanupRoles()
{
    // 各ロールセッションの履歴をクリア
    _client.ClearMessages("doctor-session");
    _client.ClearMessages("engineer-session");
    _client.ClearMessages("translator-session");
    _client.ClearMessages("support-session");
    
    // または、セッション固有プロンプトのみリセット（履歴は残す）
    _client.ResetAllSessionSystemPrompts();
}
```

#### セッション固有プロンプトの活用シーン

| シーン | 例 | 利点 |
|------|-------|------|
| マルチキャラクター会話 | ゲーム内の複数のNPC | 各キャラクターの個性を表現 |
| ロールプレイング | 異なるペルソナ | 会話品質の向上 |
| ドメイン別の専門家 | 医師、弁護士、エンジニア | 各分野での信頼性 |
| 言語別対応 | 日本語、英語、中国語セッション | 言語ごとの最適化 |
| トーン制御 | 丁寧/カジュアル/フォーマル | 状況に応じた適切な対応 |
| A/Bテスト | 異なるプロンプト版 | 品質測定と改善 |

#### 設計のベストプラクティス

**1. プロンプトテンプレート化**

```csharp
// プロンプトテンプレートを定義
public static class SystemPromptTemplates
{
    public const string Doctor = 
        "You are a professional medical advisor. Respond in Japanese. " +
        "Always recommend consulting a real doctor for serious conditions.";
    
    public const string Engineer = 
        "You are an expert software engineer. Provide technical depth. Respond in Japanese.";
    
    public const string CustomerSupport = 
        "You are a helpful customer support agent. Be empathetic and professional.";
}

// 利用
_client.SetSessionSystemPrompt("doctor-1", SystemPromptTemplates.Doctor);
_client.SetSessionSystemPrompt("engineer-1", SystemPromptTemplates.Engineer);
```

**2. セッション設定クラス**

```csharp
public class SessionConfig
{
    public string SessionId { get; set; }
    public string SystemPrompt { get; set; }
    public string RoleName { get; set; }
}

void SetupSession(SessionConfig config)
{
    _client.SetSessionSystemPrompt(config.SessionId, config.SystemPrompt);
    Debug.Log($"Initialized session: {config.RoleName}");
}
```

**3. プロンプトの動的カスタマイズ**

```csharp
// ユーザー設定に応じてプロンプトをカスタマイズ
string CreateCustomPrompt(string baseProfession, string tonePreference, string language)
{
    return $"You are a {baseProfession}. Your tone should be {tonePreference}. " +
           $"Always respond in {language}.";
}

_client.SetSessionSystemPrompt(
    "custom-session",
    CreateCustomPrompt("technical writer", "casual and friendly", "Japanese")
);
```

#### 注意点と制限事項

- **優先度の理解**：リクエスト個別プロンプトが最優先。セッション固有プロンプトが未設定の場合のみグローバルプロンプトが使用される
- **プロンプト衝突**：同じセッションで複数プロンプトを使い分けたい場合、`ChatRequestOptions.SystemPrompt` で上書き可能
- **メモリ管理**：セッションを削除（`ClearMessages()`）するとプロンプトも自動削除
- **プロンプト長制限**：非常に長いプロンプトは LLM コンテキストを圧迫するため、適度な長さに保つ
- **一括設定の効率性**：`SetSystemPromptForMultipleSessions()` で大量セッション初期化可能。1000以上のセッション初期化時に有効

詳細なAPIリファレンスは [SessionSystemPrompt.md](SessionSystemPrompt.md) を参照してください。

### 4.8 優先度スケジューリング

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
        SessionId = "system-npc",
        Priority = 10,           // 高優先度（値が大きいほど優先される）
        WaitIfBusy = true        // ビジー中ならキューで待機
    };

    var flavorMessage = new ChatRequestOptions
    {
        SessionId = "flavor-npc",
        Priority = 0,            // 低優先度（デフォルト値）
        WaitIfBusy = true        // ビジー中ならキューで待機
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
        SessionId = "session-1",
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
            SessionId = "critical-system",
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
            SessionId = $"npc-{npcId}-important",
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
            SessionId = $"npc-{npcId}-normal",
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
            SessionId = $"npc-{npcId}-flavor",
            Priority = PRIORITY_LOW,
            WaitIfBusy = false  // ビジー中なら棄却OK
        };

        StartCoroutine(_client.SendMessageAsync(message, OnResponse, options));
    }

    void OnResponse(ChatResponse response, ChatError error)
    {
        if (error != null)
        {
            Debug.LogError($"エラー: {error.Message}");
            return;
        }

        Debug.Log($"応答: {response.Content}");
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

### 4.9 キャンセル

Unity の標準 `CancellationToken` パターンに対応しています。キャンセルが必要な場合は `CancellationTokenSource` を使用してください。

```csharp
private CancellationTokenSource _cancellationTokenSource;

void SendWithCancel()
{
    // キャンセルトークンソースを作成
    _cancellationTokenSource = new CancellationTokenSource();
    
    var options = new ChatRequestOptions
    {
        SessionId = "chat-session-1",
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
        SessionId = "chat-session-1",
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

### 4.10 リトライとエラーハンドリング

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
        SessionId = "session-1"
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

            Debug.Log($"応答: {response.Content}");
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

### 4.10 メッセージ永続化

セッション履歴をファイルに保存・復元できます。暗号化オプションで保存ファイルを暗号化することも可能です。

#### 単一セッションの保存と復元

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

void SaveAndLoadSession()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    // セッションで会話
    StartCoroutine(client.SendMessageAsync(
        "こんにちは",
        (response, error) => { },
        new ChatRequestOptions { SessionId = "my-session" }
    ));
    
    // 後で会話を保存
    string savePath = Application.persistentDataPath + "/my_session.json";
    client.SaveSession(savePath, "my-session");
    Debug.Log($"Session saved to: {savePath}");
}

void RestoreSession()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string savePath = Application.persistentDataPath + "/my_session.json";
    client.LoadSession(savePath, "my-session");
    Debug.Log("Session restored from file");
    
    // 復元されたセッションで新しい会話を続行
    StartCoroutine(client.SendMessageAsync(
        "前の会話を覚えていますか？",
        (response, error) => {
            Debug.Log($"Assistant: {response.Content}");
        },
        new ChatRequestOptions { SessionId = "my-session" }
    ));
}
```

#### 暗号化オプション付き保存

```csharp
void SaveWithEncryption()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    // 暗号化キーを指定してセッションを保存
    string savePath = Application.persistentDataPath + "/my_session_encrypted.json";
    string encryptionKey = "my-secret-password-1234";
    
    client.SaveSession(savePath, "my-session", encryptionKey);
    Debug.Log($"Encrypted session saved to: {savePath}");
}

void RestoreEncryptedSession()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string savePath = Application.persistentDataPath + "/my_session_encrypted.json";
    string encryptionKey = "my-secret-password-1234";
    
    try
    {
        client.LoadSession(savePath, "my-session", encryptionKey);
        Debug.Log("Encrypted session restored");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Failed to restore session: {ex.Message}");
    }
}
```

#### すべてのセッションをまとめて保存・復元

```csharp
void SaveAllSessions()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    // 複数セッションで会話...
    
    // すべてのセッションをディレクトリに保存
    string saveDir = Application.persistentDataPath + "/chat_sessions";
    client.SaveAllSessions(saveDir);
    Debug.Log($"All sessions saved to: {saveDir}");
}

void RestoreAllSessions()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string saveDir = Application.persistentDataPath + "/chat_sessions";
    client.LoadAllSessions(saveDir);
    Debug.Log("All sessions restored");
}
```

#### 暗号化付きで複数セッションを保存

```csharp
void SaveAllSessionsWithEncryption()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string saveDir = Application.persistentDataPath + "/encrypted_sessions";
    string encryptionKey = "shared-encryption-key";
    
    // すべてのセッションを暗号化して保存
    client.SaveAllSessions(saveDir, encryptionKey);
    Debug.Log("All sessions encrypted and saved");
}

void RestoreAllEncryptedSessions()
{
    var client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    
    string saveDir = Application.persistentDataPath + "/encrypted_sessions";
    string encryptionKey = "shared-encryption-key";
    
    try
    {
        client.LoadAllSessions(saveDir, encryptionKey);
        Debug.Log("All encrypted sessions restored");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Failed to restore sessions: {ex.Message}");
    }
}
```

#### 永続化の仕様

- **ファイル形式**：JSON（平文）または JSON+暗号化
- **暗号化アルゴリズム**：AES-256-CBC（PBKDF2でキー導出）
- **保存内容**：セッションID、システムプロンプト、メッセージ履歴、日時情報
- **ファイル名**：セッションID をサニタイズして自動生成（`session-id.json` など）
- **エラーハンドリング**：暗号化キーが不正な場合は `InvalidOperationException` をスロー

#### ベストプラクティス

```csharp
public class ChatSessionManager : MonoBehaviour
{
    private OllamaClient _client;
    private string _sessionDir;
    private string _encryptionKey = "my-app-encryption-key";

    void Start()
    {
        var config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral"
        };
        _client = LLMClientFactory.CreateOllamaClient(config);
        
        // セッションディレクトリを設定
        _sessionDir = System.IO.Path.Combine(Application.persistentDataPath, "chat_history");
    }

    // アプリ起動時：前回のセッションを復元
    void RestorePreviousSessions()
    {
        if (System.IO.Directory.Exists(_sessionDir))
        {
            try
            {
                _client.LoadAllSessions(_sessionDir, _encryptionKey);
                Debug.Log("Previous sessions restored");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to restore sessions: {ex.Message}");
                // エラーハンドリング：新規開始
            }
        }
    }

    // 新しいメッセージを送信
    void SendMessage(string sessionId, string message)
    {
        StartCoroutine(_client.SendMessageAsync(
            message,
            (response, error) =>
            {
                if (error == null)
                {
                    // メッセージ送信成功時にセッションを自動保存
                    SaveSession(sessionId);
                }
            },
            new ChatRequestOptions { SessionId = sessionId }
        ));
    }

    // セッションを保存（自動バックアップ）
    void SaveSession(string sessionId)
    {
        try
        {
            string filePath = System.IO.Path.Combine(_sessionDir, $"{sessionId}.json");
            _client.SaveSession(filePath, sessionId, _encryptionKey);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save session: {ex.Message}");
        }
    }

    // アプリ終了時：すべてのセッションを保存
    void OnApplicationQuit()
    {
        try
        {
            _client.SaveAllSessions(_sessionDir, _encryptionKey);
            Debug.Log("All sessions saved on quit");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save sessions on quit: {ex.Message}");
        }
    }
}
```

### 4.11 ツール（Function Calling）

LLM が外部ツールを呼び出すことができる Function Calling 機能に対応しています。
ユーザーはコールバック関数を登録するだけで、LLM が自動的にツールを使用して回答を生成します。

**📚 詳細ドキュメント：**
- **[Tools/Design.md](Tools/Design.md)** - 設計ドキュメント（アーキテクチャ、処理フロー、実装詳細）
- **[Tools/InputSchema_Examples.md](Tools/InputSchema_Examples.md)** - inputSchema の具体例とベストプラクティス

**主な特徴：**
- ✅ **スキーマ自動生成**：リフレクションでコールバックのシグネチャから JSON Schema を自動生成
- ✅ **型安全**：JSON パラメータを C# の型に自動変換してコールバックを呼び出し
- ✅ **戻り値の自動変換**：任意の戻り値型を自動的に文字列化（プリミティブ型、カスタムオブジェクト、配列対応）
- ✅ **無限ループ防止**：最大反復回数の設定で無限ツール呼び出しを防止
- ✅ **ストリーミング対応**：ストリーミングモードでもツールが使用可能

#### スキーマ自動生成の制限と回避策

現状の自動スキーマ推定は **シンプルな型のみ** に対応します。`SimpleChat.cs` の Shop ツールのように、
`string` / `int` / `bool` / `float` / `double` / `List<T>` / 配列 などの **プリミティブ中心のシグネチャ** なら問題ありません。

一方、**複雑な入力（オブジェクト型、ネスト構造、独自クラス）** は自動推定で `string` 扱いになります。
その場合、LLM は期待する JSON 構造を正しく生成しにくくなるため、**手動スキーマ指定を推奨**します。

**自動生成で対応できる型（入力）**
- `string`
- `int`, `long`, `short`, `byte`
- `float`, `double`, `decimal`
- `bool`
- `T[]`, `List<T>`, `IList<T>`, `IEnumerable<T>`（T が上記の型）

**手動スキーマ推奨のケース**
- 複数階層のオブジェクト入力
- 独自クラスや構造体を入力で受けたい
- `enum` や `min/max` などの制約を細かく指定したい

**SimpleChat.cs での実例（自動スキーマ）**
```csharp
client.RegisterTool("BuyItem", "Buy an item from your shop", (Func<string, string>)BuyItem);
client.RegisterTool("SellItem", "Sell an item to your shop", (Func<string, int, string>)SellItem);
```
このように **パラメータがプリミティブ型のみ** の場合、スキーマ自動生成で問題ありません。

**複雑入力は手動スキーマ指定（推奨）**
```csharp
client.RegisterTool(
    name: "create_order",
    description: "Create an order",
    inputSchema: new
    {
        type = "object",
        properties = new
        {
            customer = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    age = new { type = "integer" }
                },
                required = new[] { "name", "age" }
            },
            items = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string" },
                        quantity = new { type = "integer" }
                    },
                    required = new[] { "id", "quantity" }
                }
            }
        },
        required = new[] { "customer", "items" }
    },
    callback: (Func<Newtonsoft.Json.Linq.JObject, Newtonsoft.Json.Linq.JObject, string>)(Newtonsoft.Json.Linq.JObject customer, Newtonsoft.Json.Linq.JArray items) => "ok"
);
```

**補足**: DebugMode を有効にすると、登録時に自動生成されたスキーマがログ出力されます。

#### 基本的なツール登録

最もシンプルな例：

**重要**: この例は `int` などのプリミティブ型のみを使うため、スキーマ自動生成が期待通りに動作します。
複雑な入力（オブジェクト、独自クラス、ネスト構造）が必要な場合は、上の「手動スキーマ指定」を参照してください。

```csharp
using EasyLocalLLM.LLM;
using UnityEngine;

public class ToolExample : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig
        {
            DefaultModelName = "llama3.2",  // Tools対応モデルを使用
            DebugMode = true
        });

        // ツール登録：足し算（プリミティブ型のみ）
        // スキーマは自動生成され、戻り値も自動的に文字列化
        _client.RegisterTool(
            name: "add_numbers",
            description: "Add two numbers together",
            callback: (Func<int, int, int>)((a, b) => a + b)
        );

        // ツール登録：現在時刻取得
        _client.RegisterTool(
            name: "get_current_time",
            description: "Get the current time",
            callback: (Func<DateTime>)(() => System.DateTime.Now)
        );

        // LLM にメッセージ送信
        StartCoroutine(_client.SendMessageAsync(
            "What is 125 + 378? And what time is it now?",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"Error: {error.Message}");
                    return;
                }

                // LLM がツールを自動的に呼び出して回答
                Debug.Log($"Assistant: {response.Content}");
                // 例: "125 + 378 = 503. The current time is 2026-02-07 15:30:45."
            }
        ));
    }
}
```

#### デフォルト値とパラメータ説明

```csharp
using EasyLocalLLM.LLM.Core;

void RegisterAdvancedTools()
{
    // デフォルト値付きパラメータ（省略可能）
    _client.RegisterTool(
        name: "search_web",
        description: "Search the web for information",
        callback: (string query, int maxResults = 5) =>
        {
            // maxResults は省略可能（LLM 側もそのように認識）
            return $"Found {maxResults} results for '{query}'";
        }
    );

    // ToolParameter Attribute でパラメータに説明を追加
    _client.RegisterTool(
        name: "calculate_distance",
        description: "Calculate distance between two points",
        callback: (Func<double, double, double, double, double>)((
            [ToolParameter("Latitude of starting point")] double lat1,
            [ToolParameter("Longitude of starting point")] double lon1,
            [ToolParameter("Latitude of destination")] double lat2,
            [ToolParameter("Longitude of destination")] double lon2
        ) =>
        {
            // Haversine 公式で距離計算
            double distance = CalculateHaversine(lat1, lon1, lat2, lon2);
            return distance;  // double も自動的に文字列化
        })
    );
}
```

#### カスタムオブジェクトの戻り値

```csharp
void RegisterDataTools()
{
    // カスタムオブジェクトを返すツール
    // → 自動的に JSON にシリアライズされる
    _client.RegisterTool(
        name: "get_user_info",
        description: "Get user information by ID",
        callback: (Func<string, object>)((userId) => new
        {
            id = userId,
            name = "John Doe",
            age = 30,
            email = "john@example.com",
            isActive = true
        })  // JSON: {"id":"123","name":"John Doe",...} として LLM に渡される
    );

    // 配列を返すツール
    _client.RegisterTool(
        name: "list_recent_messages",
        description: "Get recent chat messages",
        callback: (Func<int, object>)((count) => new[]
        {
            new { sender = "Alice", message = "Hello!" },
            new { sender = "Bob", message = "Hi there!" }
        })  // JSON 配列として LLM に渡される
    );
}
```

#### エラーハンドリング

ツール実行中のエラーは自動的にキャッチされ、LLM にエラーメッセージとして返されます：

```csharp
void RegisterToolWithErrorHandling()
{
    _client.RegisterTool(
        name: "divide_numbers",
        description: "Divide two numbers",
        callback: (double numerator, double denominator) =>
        {
            // エラーケースをハンドリング
            if (denominator == 0)
            {
                return "Error: Division by zero is not allowed";
            }
            
            return numerator / denominator;
        }
    );

    // または、例外をスロー（自動的にキャッチされてエラーメッセージに変換）
    _client.RegisterTool(
        name: "get_player_health",
        description: "Get player health points",
        callback: (string playerId) =>
        {
            var player = FindPlayer(playerId);
            if (player == null)
            {
                throw new System.Exception($"Player '{playerId}' not found");
            }
            return player.Health;
        }
    );
}
```

#### 最大反復回数の設定

ツールが繰り返し呼ばれる無限ループを防ぐため、最大反復回数を設定できます：

```csharp
void SendWithToolOptions()
{
    var options = new ChatRequestOptions
    {
        SessionId = "my-session",
        MaxToolIterations = 10  // デフォルトは 5
    };

    StartCoroutine(_client.SendMessageAsync(
        "Calculate 5 + 3, then multiply by 2, then subtract 4",
        (response, error) =>
        {
            // LLM が複数回ツールを呼び出して計算
            Debug.Log(response.Content);
        },
        options
    ));
}
```

#### ツールの管理

```csharp
void ManageTools()
{
    // 登録済みツール一覧を取得
    var tools = _client.GetRegisteredTools();
    foreach (var tool in tools)
    {
        Debug.Log($"Tool: {tool.Name} - {tool.Description}");
    }

    // ツールが登録されているか確認
    bool hasAddTool = _client.HasTool("add_numbers");

    // ツールを削除
    _client.UnregisterTool("add_numbers");

    // すべてのツールを削除
    _client.RemoveAllTools();
}
```

#### ストリーミングとツールの組み合わせ

```csharp
void StreamingWithTools()
{
    _client.RegisterTool("get_time", "Get current time", () => System.DateTime.Now);

    StartCoroutine(_client.SendMessageStreamingAsync(
        "What time is it?",
        (response, error) =>
        {
            if (error != null) return;

            if (!response.IsFinal)
            {
                // ストリーミング中の部分応答
                Debug.Log($"Partial: {response.Content}");
            }
            else
            {
                // 最終応答（ツール実行後）
                Debug.Log($"Final: {response.Content}");
            }
        },
        new ChatRequestOptions { SessionId = "streaming-session" }
    ));
}
```

#### 手動スキーマ指定（高度な使い方）

リフレクションによる自動生成ではなく、手動で JSON Schema を指定することもできます：

```csharp
void RegisterToolWithManualSchema()
{
    _client.RegisterTool(
        name: "custom_tool",
        description: "A tool with manual schema",
        inputSchema: new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "User name" },
                age = new { type = "integer", minimum = 0, maximum = 150 }
            },
            required = new[] { "name" }
        },
        callback: (string json) =>
        {
            // JSON 文字列を手動でパース
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            string name = obj["name"].ToString();
            int age = obj["age"]?.Value<int>() ?? 0;
            
            return $"Hello {name}, age {age}";
        }
    );
}
```

#### 注意事項

- **対応モデル**：Tools 機能は一部のモデルのみ対応（例: `llama3.2`, `mistral` など）
- **デバッグモード**：`DebugMode = true` でツール実行のログを確認可能
- **戻り値の型**：プリミティブ型、カスタムオブジェクト、配列はすべて自動変換されます
- **パフォーマンス**：ツール呼び出しは追加の LLM リクエストを伴うため、複数回の往復が発生します

### 4.12 JSON形式のレスポンス指定

LLM からのレスポンスを JSON 形式で取得できます。構造化されたデータが必要な場合や、特定のスキーマに従ったレスポンスを得たい場合に便利です。

#### 基本的な JSON 形式の指定

`Format` プロパティに `ChatRequestOptions.FormatConstants.Json` を指定することで、レスポンスを JSON 形式で受け取れます。

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using UnityEngine;

public class JsonFormatExample : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig
        {
            DefaultModelName = "llama3.2",
            DebugMode = true
        });

        // JSON 形式でレスポンスを取得
        var options = new ChatRequestOptions
        {
            Format = ChatRequestOptions.FormatConstants.Json
        };

        StartCoroutine(_client.SendMessageAsync(
            "Generate a user profile with name, age, and email",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"Error: {error.Message}");
                    return;
                }

                // レスポンスは JSON 文字列
                Debug.Log($"JSON Response: {response.Content}");
                // 例: {"name": "John Doe", "age": 30, "email": "john@example.com"}

                // パースして使用
                var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                string name = json["name"]?.ToString();
                int age = json["age"]?.Value<int>() ?? 0;
                Debug.Log($"User: {name}, Age: {age}");
            },
            options
        ));
    }
}
```

#### JSON スキーマを使った詳細な指定

`FormatSchema` プロパティで JSON Schema を指定することで、より厳密な構造を持つレスポンスを得られます。

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using UnityEngine;

public class JsonSchemaExample : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());

        // JSON Schema を指定
        var options = new ChatRequestOptions
        {
            FormatSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    age = new { type = "number" },
                    email = new { type = "string" },
                    isActive = new { type = "boolean" }
                },
                required = new[] { "name", "age" }
            }
        };

        StartCoroutine(_client.SendMessageAsync(
            "Create a user profile for a 25-year-old software engineer named Alice",
            (response, error) =>
            {
                if (error == null)
                {
                    Debug.Log($"Structured JSON: {response.Content}");
                    // レスポンスは指定したスキーマに従った JSON
                }
            },
            options
        ));
    }
}
```

#### 配列を含むスキーマ

```csharp
void RequestArrayData()
{
    var options = new ChatRequestOptions
    {
        FormatSchema = new
        {
            type = "object",
            properties = new
            {
                teamName = new { type = "string" },
                members = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            role = new { type = "string" },
                            level = new { type = "number" }
                        },
                        required = new[] { "name", "role" }
                    }
                }
            },
            required = new[] { "teamName", "members" }
        }
    };

    StartCoroutine(_client.SendMessageAsync(
        "Generate a fantasy RPG party with 4 members",
        (response, error) =>
        {
            if (error == null)
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                string teamName = json["teamName"]?.ToString();
                var members = json["members"] as Newtonsoft.Json.Linq.JArray;
                
                Debug.Log($"Team: {teamName}");
                foreach (var member in members)
                {
                    string name = member["name"]?.ToString();
                    string role = member["role"]?.ToString();
                    Debug.Log($"- {name} ({role})");
                }
            }
        },
        options
    ));
}
```

#### ゲームデータ生成の実用例

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using System;
using UnityEngine;

[Serializable]
public class EnemyData
{
    public string name;
    public int health;
    public int attack;
    public int defense;
    public string[] weaknesses;
}

public class GameDataGenerator : MonoBehaviour
{
    private OllamaClient _client;

    void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());
    }

    public void GenerateEnemy(string theme, Action<EnemyData> onComplete)
    {
        var options = new ChatRequestOptions
        {
            FormatSchema = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    health = new { type = "number", minimum = 50, maximum = 500 },
                    attack = new { type = "number", minimum = 10, maximum = 100 },
                    defense = new { type = "number", minimum = 5, maximum = 50 },
                    weaknesses = new
                    {
                        type = "array",
                        items = new { type = "string" }
                    }
                },
                required = new[] { "name", "health", "attack", "defense" }
            }
        };

        StartCoroutine(_client.SendMessageAsync(
            $"Generate a {theme} enemy with stats and weaknesses",
            (response, error) =>
            {
                if (error != null)
                {
                    Debug.LogError($"Failed to generate enemy: {error.Message}");
                    return;
                }

                try
                {
                    // JSON をデシリアライズ
                    var enemyData = Newtonsoft.Json.JsonConvert.DeserializeObject<EnemyData>(response.Content);
                    Debug.Log($"Generated: {enemyData.name} (HP: {enemyData.health}, ATK: {enemyData.attack})");
                    onComplete?.Invoke(enemyData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse enemy data: {ex.Message}");
                }
            },
            options
        ));
    }
}
```

#### Task 版での使用

```csharp
using EasyLocalLLM.LLM;
using EasyLocalLLM.LLM.Core;
using System.Threading.Tasks;
using UnityEngine;

public class AsyncJsonExample : MonoBehaviour
{
    private OllamaClient _client;

    async void Start()
    {
        _client = LLMClientFactory.CreateOllamaClient(new OllamaConfig());

        var options = new ChatRequestOptions
        {
            Format = ChatRequestOptions.FormatConstants.Json
        };

        try
        {
            var response = await _client.SendMessageTaskAsync(
                "Generate random item data with name, price, and rarity",
                options
            );

            var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
            Debug.Log($"Item: {json["name"]}, Price: {json["price"]}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error: {ex.Message}");
        }
    }
}
```

#### ストリーミングでの使用

```csharp
void StreamingJsonExample()
{
    var options = new ChatRequestOptions
    {
        Format = ChatRequestOptions.FormatConstants.Json
    };

    StartCoroutine(_client.SendMessageStreamingAsync(
        "Generate a character profile",
        (response, error) =>
        {
            if (error != null) return;

            if (response.IsFinal)
            {
                // 最終的な完全な JSON
                Debug.Log($"Complete JSON: {response.Content}");
                var json = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                // 処理...
            }
            else
            {
                // ストリーミング中の部分的な JSON（表示用）
                Debug.Log($"Partial: {response.Content}");
            }
        },
        options
    ));
}
```

#### 注意事項

- **FormatSchema と Format の優先順位**：`FormatSchema` が指定されている場合、`Format` は無視されます
- **対応モデル**：JSON 形式の指定は、対応するモデルでのみ正しく動作します（例: `llama3.2`, `mistral` など）
- **パース処理**：JSON レスポンスは文字列として返されるため、`Newtonsoft.Json` などでパースが必要です
- **エラーハンドリング**：スキーマが複雑すぎる場合や、モデルが対応していない場合、エラーが発生する可能性があります
- **ストリーミング時の注意**：ストリーミングモードでは、`IsFinal = true` の時のみ完全な JSON が保証されます

## 5. 実践例

これまでに学んだ機能を組み合わせた、実際のゲーム開発での応用例を紹介します。

### ゲーム内 NPC 会話システム

実際のゲーム開発でよくあるパターンの実装例です。UI統合、キャンセル処理、エラーハンドリングを含みます。

```csharp
using EasyLocalLLM.LLM;
using System;
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
    private string _sessionDirectory;

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
        
        // セッション保存ディレクトリを設定
        _sessionDirectory = System.IO.Path.Combine(
            Application.persistentDataPath,
            "NPCChatSessions"
        );
        if (!System.IO.Directory.Exists(_sessionDirectory))
        {
            System.IO.Directory.CreateDirectory(_sessionDirectory);
        }
        
        // UI イベント設定
        sendButton.onClick.AddListener(OnSendClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        cancelButton.gameObject.SetActive(false);
        
        // 前回のセッションを復元
        LoadPreviousSession();
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
            SessionId = _currentNPCId,
            Temperature = 0.8f,
            Priority = 50,  // 通常優先度
            WaitIfBusy = true,
            CancellationToken = _cancellationTokenSource.Token,
            SystemPrompt = "あなたは親切な雑貨屋の店主です。冒険者に親しみやすく話しかけてください。"
        };
        
        StartCoroutine(_client.SendMessageStreamingAsync(
            userMessage,
            (response, error) =>
            {
                OnNPCResponse(response, error);
            },
            options
        ));
    }
    
    void LoadPreviousSession()
    {
        try
        {
            string sessionPath = System.IO.Path.Combine(
                _sessionDirectory,
                _currentNPCId + ".json"
            );

            if (System.IO.File.Exists(sessionPath))
            {
                _client.LoadSession(sessionPath, _currentNPCId, encryptionKey: null);
                Debug.Log($"前回のセッションを復元しました: {_currentNPCId}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"セッション復元エラー: {ex.Message}");
        }
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
            // 応答完了時にセッションを保存
            SaveCurrentSession();
            ResetUI();
        }
    }
    
    void SaveCurrentSession()
    {
        try
        {
            string sessionPath = System.IO.Path.Combine(
                _sessionDirectory,
                _currentNPCId + ".json"
            );

            _client.SaveSession(sessionPath, _currentNPCId, encryptionKey: null);
            Debug.Log($"セッションを保存しました: {sessionPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"セッション保存エラー: {ex.Message}");
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
        // 終了時にセッションを保存
        SaveCurrentSession();
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
                Priority = 50,  // 通常優先度
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
            SessionId = profile.SessionId,
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

## 6. クラス構成

```
Runtime/LLM/
├── Core Data Models
│   ├── ChatMessage.cs           # チャットメッセージ
│   ├── ChatResponse.cs          # LLM からの応答
│   ├── ChatError.cs             # エラー情報
│   ├── ChatLLMException.cs      # Task 例外ラッパー
│   └── ChatRequestOptions.cs    # リクエストオプション
│
├── Manager & Client
│   ├── ChatHistoryManager.cs    # メッセージ履歴管理（セッション対応）
│   ├── ChatSessionPersistence.cs# セッション永続化（Save/Load）
│   ├── ChatEncryption.cs        # 暗号化・復号化（AES-256）
│   ├── CoroutineRunner.cs       # Task ブリッジ用ランナー
│   ├── OllamaConfig.cs          # 設定
│   ├── OllamaServerManager.cs   # サーバライフサイクル管理
│   ├── OllamaClient.cs          # Ollama クライアント実装
│   ├── HttpRequestHelper.cs     # HTTP リトライロジック（内部用）
│
└── Factory & Interface
    ├── IChatLLMClient.cs        # クライアントインターフェース
    └── LLMClientFactory.cs      # クライアント生成ファクトリ
```

## 7. 設定オプション

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

## 8. デフォルト設定について

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

## 9. エラータイプと対処方法

このセクションでは、各エラータイプの詳細と実際の対処方法を説明します。
自動リトライの仕組みについては、[4.8 リトライとエラーハンドリング](#48-リトライとエラーハンドリング)を参照してください。

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
            SessionId = "session-1"
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

