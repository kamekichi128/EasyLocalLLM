# wllama セットアップ

この手順は `WebGLLlamaCppClient` と `EasyLocalLLMLlamaBridge.jslib` を使い、WebGL ビルドで wllama を実行する最小構成です。

## 1. ワンコマンド準備（推奨）

次を実行すると、テンプレート・ランタイムJS・wllama アセットをまとめて配置します。

```powershell
powershell -ExecutionPolicy Bypass -File Assets/EasyLocalLLM/Tools/prepare-webgl-output.ps1
```

主なオプション:

```powershell
# npm install をスキップ
powershell -ExecutionPolicy Bypass -File Assets/EasyLocalLLM/Tools/prepare-webgl-output.ps1 -SkipWllamaInstall

# 事前検証をスキップ
powershell -ExecutionPolicy Bypass -File Assets/EasyLocalLLM/Tools/prepare-webgl-output.ps1 -SkipValidation
```

## 2. Unity 設定

1. `Project Settings > Player > Resolution and Presentation > WebGL Template` で `EasyLocalLLM` を選択
2. モデル（`.gguf`）を `Assets/StreamingAssets/models/` 配下に配置
3. `WebGLLlamaCppConfig.ModelUrl` を `Application.streamingAssetsPath + "/models/<your-model>.gguf"` に設定

## 3. 利用例

```csharp
var client = new EasyLocalLLM.LLM.WebGL.WebGLLlamaCppClient(
    new EasyLocalLLM.LLM.WebGL.WebGLLlamaCppConfig
    {
        ModelUrl = Application.streamingAssetsPath + "/models/qwen2.5-1.5b-instruct-q4_k_m.gguf",
        ContextSize = 2048,
        UseWebGpu = true,
        DebugMode = true
    });
```

## 4. 注意点

- `WebGLLlamaCppClient` は `UNITY_WEBGL && !UNITY_EDITOR` を前提にしています。
- 初期版は `tool calling` を無効化しています。
