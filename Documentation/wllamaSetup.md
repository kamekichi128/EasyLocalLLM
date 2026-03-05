# wllama Setup Guide

This guide covers the minimum setup for running WebGL inference with `WebGLLlamaCppClient`.

## 1. One-command Preparation (recommended)

Run the following command to copy/update template files, runtime JS, and wllama assets:

```powershell
powershell -ExecutionPolicy Bypass -File Assets/EasyLocalLLM/Tools/prepare-webgl-output.ps1
```

Common options:

```powershell
# Skip npm install for wllama assets
powershell -ExecutionPolicy Bypass -File Assets/EasyLocalLLM/Tools/prepare-webgl-output.ps1 -SkipWllamaInstall

# Skip pre-validation
powershell -ExecutionPolicy Bypass -File Assets/EasyLocalLLM/Tools/prepare-webgl-output.ps1 -SkipValidation
```

## 2. Unity Settings

1. Select `EasyLocalLLM` in `Project Settings > Player > Resolution and Presentation > WebGL Template`
2. Place `.gguf` model files under `Assets/StreamingAssets/models/`
3. Set `WebGLLlamaCppConfig.ModelUrl` to `Application.streamingAssetsPath + "/models/<your-model>.gguf"`

## 3. Usage Example

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

## 4. Notes and Limitations

- `WebGLLlamaCppClient` is intended for `UNITY_WEBGL && !UNITY_EDITOR`
- Tool calling and VLM are currently not supported in this WebGL path
- For `FormatSchema`, runtime-enforced grammar may be unavailable depending on wllama/runtime version, so validate final JSON in C# after generation
