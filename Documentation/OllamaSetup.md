# Ollama Setup Guide

This guide explains how to set up Ollama on Windows for Unity Editor / Standalone builds.

## Choose a Setup Method

There are two common ways to prepare models for Ollama.

**Method A: `ollama pull` (recommended)**
- ✅ Easiest setup
- ✅ Automatically optimized artifacts
- ✅ Easy model updates
- ❌ Requires initial network download

**Method B: Direct GGUF placement (advanced/custom)**
- ✅ Full control over model files
- ✅ Works with custom GGUF models
- ❌ More manual steps
- ❌ Requires downloading GGUF files in advance

| Scenario | Recommended | Reason |
| --- | --- | --- |
| Typical use | Method A | Simple and reliable |
| Custom unsupported model | Method B | Bring your own GGUF |
| Fine-tuned parameter control | Method B | Use Modelfile |

## Step 0: Common Preparation

1. Download Windows binaries from [Ollama](https://ollama.ai) or [GitHub Releases](https://github.com/ollama/ollama/releases)
2. Create this structure in your Unity project:

```text
Assets/StreamingAssets/EasyLocalLLM/Ollama/
├── ollama.exe
├── lib/
└── models/
```

## Method A: `ollama pull` (recommended)

1. PowerShell window #1 (start server):

```powershell
$env:OLLAMA_MODELS="<ProjectPath>\Assets\StreamingAssets\EasyLocalLLM\Ollama\models"
mkdir $env:OLLAMA_MODELS
cd "<ProjectPath>\Assets\StreamingAssets\EasyLocalLLM\Ollama"
.\ollama.exe serve
```

2. PowerShell window #2 (pull model):

```powershell
cd "<ProjectPath>\Assets\StreamingAssets\EasyLocalLLM\Ollama"
.\ollama.exe pull mistral
```

3. Confirm `blobs/` and `manifests/` are created under `StreamingAssets/EasyLocalLLM/Ollama/models/`.

## Method B: Direct GGUF Placement

### 1) Place a GGUF file

Example:

```text
Assets/StreamingAssets/EasyLocalLLM/Ollama/models/mistral/
└── mistral-7b-instruct-v0.1.Q4_K_M.gguf
```

### 2) Create `Modelfile`

```text
FROM ./your-model-name.Q4_K_M.gguf

PARAMETER temperature 0.7
PARAMETER top_k 40
PARAMETER top_p 0.9
```

### 3) Register model in Ollama

```powershell
$env:OLLAMA_MODELS="<ProjectPath>\Assets\StreamingAssets\EasyLocalLLM\Ollama\models"
cd "<ProjectPath>\Assets\StreamingAssets\EasyLocalLLM\Ollama\models\mistral"
..\..\ollama.exe create mistral -f ./Modelfile
```

### 4) Verify registration

```powershell
..\..\ollama.exe list
```

## Unity Configuration Example

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

## Troubleshooting

- Slow model downloads: verify internet throughput (models are often several GB)
- `ollama.exe serve` fails: check port conflict with `netstat -an | findstr :11434`
- `Modelfile` shows `not found`: ensure `FROM ./...gguf` uses relative path
- Out of memory: choose a smaller quantization or set `MaxConcurrentSessions = 1`
