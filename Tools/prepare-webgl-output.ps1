param(
    [Parameter(Mandatory = $false)]
    [string]$PackageVersion = "latest",

    [Parameter(Mandatory = $false)]
    [switch]$SkipWllamaInstall,

    [Parameter(Mandatory = $false)]
    [switch]$SkipValidation
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([string]$PathValue)

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    return (Join-Path -Path (Get-Location) -ChildPath $PathValue)
}

function Copy-DirectoryContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDir
    )

    if (-not (Test-Path -Path $SourceDir -PathType Container)) {
        throw "Source directory not found: $SourceDir"
    }

    if (Test-Path -Path $DestinationDir -PathType Container) {
        Remove-Item -Path $DestinationDir -Recurse -Force
    }

    New-Item -Path $DestinationDir -ItemType Directory -Force | Out-Null

    $items = Get-ChildItem -Path $SourceDir -Force
    foreach ($item in $items) {
        $targetPath = Join-Path -Path $DestinationDir -ChildPath $item.Name
        Copy-Item -Path $item.FullName -Destination $targetPath -Recurse -Force
    }
}

function Assert-LastExitCodeSuccess {
    param([string]$CommandName)

    if ($null -eq $LASTEXITCODE) {
        return
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$CommandName failed with exit code: $LASTEXITCODE"
    }
}

$templateSource = Resolve-FullPath "Assets/EasyLocalLLM/WebGLBuildSources/WebGLTemplates/EasyLocalLLM"
$templateTarget = Resolve-FullPath "Assets/WebGLTemplates/EasyLocalLLM"
$runtimeSource = Resolve-FullPath "Assets/EasyLocalLLM/WebGLBuildSources/StreamingAssets/EasyLocalLLM/WebGL/EasyLocalLLMLlamaBridgeRuntime.js"
$runtimeTargetDir = Resolve-FullPath "Assets/StreamingAssets/EasyLocalLLM/WebGL"
$runtimeTarget = Join-Path -Path $runtimeTargetDir -ChildPath "EasyLocalLLMLlamaBridgeRuntime.js"

Write-Host "Deploying WebGL template from EasyLocalLLM source..."
Copy-DirectoryContent -SourceDir $templateSource -DestinationDir $templateTarget

Write-Host "Deploying bridge runtime JS from EasyLocalLLM source..."
if (-not (Test-Path -Path $runtimeSource -PathType Leaf)) {
    throw "Runtime source JS not found: $runtimeSource"
}

if (-not (Test-Path -Path $runtimeTargetDir -PathType Container)) {
    New-Item -Path $runtimeTargetDir -ItemType Directory -Force | Out-Null
}

Copy-Item -Path $runtimeSource -Destination $runtimeTarget -Force

if (-not $SkipWllamaInstall) {
    $prepareWllamaScript = Resolve-FullPath "Assets/EasyLocalLLM/Tools/prepare-wllama-assets-isolated.ps1"
    if (-not (Test-Path -Path $prepareWllamaScript -PathType Leaf)) {
        throw "prepare-wllama-assets-isolated.ps1 not found: $prepareWllamaScript"
    }

    Write-Host "Preparing wllama assets into Assets/StreamingAssets/llama-wasm..."
    & $prepareWllamaScript -PackageVersion $PackageVersion
    Assert-LastExitCodeSuccess -CommandName "prepare-wllama-assets-isolated.ps1"
}

if (-not $SkipValidation) {
    $validateScript = Resolve-FullPath "Assets/EasyLocalLLM/Tools/validate-webgl-llama-setup.ps1"
    if (-not (Test-Path -Path $validateScript -PathType Leaf)) {
        throw "validate-webgl-llama-setup.ps1 not found: $validateScript"
    }

    Write-Host "Validating generated WebGL output..."
    & $validateScript
    Assert-LastExitCodeSuccess -CommandName "validate-webgl-llama-setup.ps1"
}

Write-Host "WebGL output preparation completed." -ForegroundColor Green
Write-Host "  Template output: $templateTarget"
Write-Host "  Runtime JS output: $runtimeTarget"
Write-Host "  WASM assets output: $(Resolve-FullPath 'Assets/StreamingAssets/llama-wasm')"
