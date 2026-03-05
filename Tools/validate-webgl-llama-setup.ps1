param(
    [Parameter(Mandatory = $false)]
    [string]$TemplatePath = "Assets/WebGLTemplates/EasyLocalLLM/index.html",

    [Parameter(Mandatory = $false)]
    [string]$WasmAssetsPath = "Assets/StreamingAssets/llama-wasm",

    [Parameter(Mandatory = $false)]
    [string]$RuntimeJsPath = "Assets/StreamingAssets/EasyLocalLLM/WebGL/EasyLocalLLMLlamaBridgeRuntime.js",

    [Parameter(Mandatory = $false)]
    [string]$ModelDirectory = "Assets/StreamingAssets/models",

    [Parameter(Mandatory = $false)]
    [switch]$RequireModel
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([string]$PathValue)

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    return (Join-Path -Path (Get-Location) -ChildPath $PathValue)
}

$issues = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

$templateFullPath = Resolve-FullPath -PathValue $TemplatePath
$wasmAssetsFullPath = Resolve-FullPath -PathValue $WasmAssetsPath
$runtimeJsFullPath = Resolve-FullPath -PathValue $RuntimeJsPath
$modelDirFullPath = Resolve-FullPath -PathValue $ModelDirectory

if (-not (Test-Path -Path $templateFullPath -PathType Leaf)) {
    $issues.Add("WebGL template not found: $templateFullPath")
}
else {
    $templateContent = Get-Content -Path $templateFullPath -Raw
    if ($templateContent -notmatch "wllama") {
        $issues.Add("Template does not include wllama script reference: $templateFullPath")
    }

    if ($templateContent -notmatch "EasyLocalLLMLlamaBridgeRuntime\.js") {
        $issues.Add("Template does not include EasyLocalLLMLlamaBridgeRuntime.js reference: $templateFullPath")
    }
}

if (-not (Test-Path -Path $runtimeJsFullPath -PathType Leaf)) {
    $issues.Add("Runtime JS not found: $runtimeJsFullPath")
}

if (-not (Test-Path -Path $wasmAssetsFullPath -PathType Container)) {
    $issues.Add("WASM assets directory not found: $wasmAssetsFullPath")
}
else {
    $wasmFiles = Get-ChildItem -Path $wasmAssetsFullPath -Recurse -Filter "*.wasm" -File -ErrorAction SilentlyContinue
    $jsLikeFiles = Get-ChildItem -Path $wasmAssetsFullPath -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $_.Extension -in @(".js", ".mjs", ".cjs")
    }

    if ($wasmFiles.Count -eq 0) {
        $issues.Add("No .wasm file found under: $wasmAssetsFullPath")
    }

    if ($jsLikeFiles.Count -eq 0) {
        $issues.Add("No JS runtime file (.js/.mjs/.cjs) found under: $wasmAssetsFullPath")
    }
}

if (Test-Path -Path $modelDirFullPath -PathType Container) {
    $ggufFiles = Get-ChildItem -Path $modelDirFullPath -Recurse -Filter "*.gguf" -File -ErrorAction SilentlyContinue
    if ($ggufFiles.Count -eq 0) {
        if ($RequireModel) {
            $issues.Add("No .gguf model file found under: $modelDirFullPath")
        }
        else {
            $warnings.Add("No .gguf model file found under: $modelDirFullPath")
        }
    }
}
else {
    if ($RequireModel) {
        $issues.Add("Model directory not found: $modelDirFullPath")
    }
    else {
        $warnings.Add("Model directory not found (optional): $modelDirFullPath")
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "Warnings:" -ForegroundColor Yellow
    foreach ($warning in $warnings) {
        Write-Host "  - $warning" -ForegroundColor Yellow
    }
}

if ($issues.Count -gt 0) {
    Write-Host "Validation failed:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "  - $issue" -ForegroundColor Red
    }
    exit 1
}

Write-Host "WebGL llama.cpp setup validation passed." -ForegroundColor Green
Write-Host "  Template: $templateFullPath"
Write-Host "  WASM assets: $wasmAssetsFullPath"
Write-Host "  Runtime JS: $runtimeJsFullPath"
