# Ollama セットアップガイド

Windows向けにOllamaのセットアップ方法をまとめます。

## セットアップ方法の選択

Ollama をアプリケーションに組み込む場合、モデルの取得方法で2つのパターンがあります。

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

| シナリオ | 推奨方法 | 理由 |
|--------|--------|------|
| 通常のケース | 方法 A | セットアップが簡単。ほとんどの場合これで十分 |
| Ollama未対応のカスタムモデルを使う | 方法 B | 独自のGGUFファイルを使用可能 |
| モデル動作を細かくカスタマイズしたい | 方法 B | Modelfile でパラメータを調整可能 |

## ステップ 0: 共通準備

1. [Ollama 公式ウェブサイト](https://ollama.ai) または [GitHub Releases](https://github.com/ollama/ollama/releases) から Windows バイナリを取得
2. Unity プロジェクト内に次の構成を作成

```text
Assets/StreamingAssets/EasyLocalLLM/Ollama/
├── ollama.exe
├── lib/
└── models/
```

## 方法 A: `ollama pull`（推奨）

1. PowerShell 1つ目（サーバ起動）

```powershell
$env:OLLAMA_MODELS="<プロジェクトパス>\Assets\StreamingAssets\EasyLocalLLM\Ollama\models"
mkdir $env:OLLAMA_MODELS
cd "<プロジェクトパス>\Assets\StreamingAssets\EasyLocalLLM\Ollama"
.\ollama.exe serve
```

2. PowerShell 2つ目（モデル取得）

```powershell
cd "<プロジェクトパス>\Assets\StreamingAssets\EasyLocalLLM\Ollama"
.\ollama.exe pull mistral
```

3. 完了後、`StreamingAssets/EasyLocalLLM/Ollama/models/` に `blobs/` と `manifests/` が生成されることを確認

## 方法 B: GGUF 直接配置

### 1) GGUF ファイルを配置

例:

```text
Assets/StreamingAssets/EasyLocalLLM/Ollama/models/mistral/
└── mistral-7b-instruct-v0.1.Q4_K_M.gguf
```

### 2) `Modelfile` を作成

```text
FROM ./your-model-name.Q4_K_M.gguf

PARAMETER temperature 0.7
PARAMETER top_k 40
PARAMETER top_p 0.9
```

### 3) モデル登録

```powershell
$env:OLLAMA_MODELS="<プロジェクトパス>\Assets\StreamingAssets\EasyLocalLLM\Ollama\models"
cd "<プロジェクトパス>\Assets\StreamingAssets\EasyLocalLLM\Ollama\models\mistral"
..\..\ollama.exe create mistral -f ./Modelfile
```

### 4) 登録確認

```powershell
..\..\ollama.exe list
```

## Unity 側設定例

```csharp
var config = new OllamaConfig
{
    ServerUrl = "http://localhost:11434",
    ExecutablePath = Application.streamingAssetsPath + "/EasyLocalLLM/Ollama/ollama.exe",
    ModelsDirectory = Application.streamingAssetsPath + "/EasyLocalLLM/Ollama/models",
    DefaultModelName = "mistral",
    AutoStartServer = true,
    DebugMode = true
};

OllamaServerManager.Initialize(config);
var client = LLMClientFactory.CreateOllamaClient(config);
```

## トラブルシューティング

- モデルダウンロードが遅い: 回線速度を確認（モデルは数GB以上）
- `ollama.exe serve` が失敗: `11434` ポートの競合確認（`netstat -an | findstr :11434`）
- `Modelfile` で `not found`: `FROM ./xxx.gguf` の相対パスを確認
- メモリ不足: 小さい量子化版へ変更、または `MaxConcurrentSessions = 1`

## 推奨セットアップ（ゲーム開発向け）

```csharp
public class OllamaSetupManager : MonoBehaviour
{
    void Start()
    {
        OllamaConfig config;

#if UNITY_EDITOR
        config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            ExecutablePath = Application.streamingAssetsPath + "/EasyLocalLLM/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/EasyLocalLLM/Ollama/models",
            AutoStartServer = true,
            DebugMode = true,
            MaxRetries = 5,
            EnableHealthCheck = true
        };
#else
        config = new OllamaConfig
        {
            ServerUrl = "http://localhost:11434",
            DefaultModelName = "mistral",
            ExecutablePath = Application.streamingAssetsPath + "/EasyLocalLLM/Ollama/ollama.exe",
            ModelsDirectory = Application.streamingAssetsPath + "/EasyLocalLLM/Ollama/models",
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