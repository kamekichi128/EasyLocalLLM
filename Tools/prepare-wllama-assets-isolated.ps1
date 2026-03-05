param(
    [Parameter(Mandatory = $false)]
    [string]$PackageVersion = "latest",

    [Parameter(Mandatory = $false)]
    [string]$DestinationPath = "Assets/StreamingAssets/llama-wasm",

    [Parameter(Mandatory = $false)]
    [switch]$KeepTemp
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([string]$PathValue)

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    return (Join-Path -Path (Get-Location) -ChildPath $PathValue)
}

function Restore-Env {
    param(
        [string]$OldCache,
        [string]$OldUpdateNotifier,
        [string]$OldFund,
        [string]$OldAudit
    )

    if ($null -eq $OldCache) { Remove-Item Env:npm_config_cache -ErrorAction SilentlyContinue } else { $env:npm_config_cache = $OldCache }
    if ($null -eq $OldUpdateNotifier) { Remove-Item Env:npm_config_update_notifier -ErrorAction SilentlyContinue } else { $env:npm_config_update_notifier = $OldUpdateNotifier }
    if ($null -eq $OldFund) { Remove-Item Env:npm_config_fund -ErrorAction SilentlyContinue } else { $env:npm_config_fund = $OldFund }
    if ($null -eq $OldAudit) { Remove-Item Env:npm_config_audit -ErrorAction SilentlyContinue } else { $env:npm_config_audit = $OldAudit }
}

function Invoke-Npm {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args
    )

    & npm @Args
    if ($LASTEXITCODE -ne 0) {
        throw "npm command failed (exit=$LASTEXITCODE): npm $($Args -join ' ')"
    }
}

function Resolve-WllamaAssetRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageRoot
    )

    $distPath = Join-Path -Path $PackageRoot -ChildPath "dist"
    if (Test-Path -Path $distPath -PathType Container) {
        return $distPath
    }

    $esmPath = Join-Path -Path $PackageRoot -ChildPath "esm"
    if (Test-Path -Path $esmPath -PathType Container) {
        return $esmPath
    }

    return $null
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm was not found in PATH. Install Node.js (includes npm) first."
}

$destination = Resolve-FullPath -PathValue $DestinationPath
if (-not (Test-Path -Path $destination -PathType Container)) {
    New-Item -Path $destination -ItemType Directory -Force | Out-Null
}

$tempRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("EasyLocalLLM-wllama-" + [Guid]::NewGuid().ToString("N"))
$tempProject = Join-Path -Path $tempRoot -ChildPath "project"
$cacheDir = Join-Path -Path $tempRoot -ChildPath ".npm-cache"

New-Item -Path $tempProject -ItemType Directory -Force | Out-Null
New-Item -Path $cacheDir -ItemType Directory -Force | Out-Null

$oldCache = $env:npm_config_cache
$oldUpdateNotifier = $env:npm_config_update_notifier
$oldFund = $env:npm_config_fund
$oldAudit = $env:npm_config_audit

try {
    $env:npm_config_cache = $cacheDir
    $env:npm_config_update_notifier = "false"
    $env:npm_config_fund = "false"
    $env:npm_config_audit = "false"

    Push-Location $tempProject

    Invoke-Npm -Args @("init", "-y") | Out-Null

    $targetPackage = "@wllama/wllama"
    if ($PackageVersion -and $PackageVersion -ne "latest") {
        $targetPackage = "$targetPackage@$PackageVersion"
    }

    Write-Host "Installing $targetPackage in isolated temp workspace..."
    Invoke-Npm -Args @("install", "--no-save", $targetPackage)

    $packageRoot = Join-Path -Path $tempProject -ChildPath "node_modules/@wllama/wllama"
    if (-not (Test-Path -Path $packageRoot -PathType Container)) {
        throw "wllama package folder not found after install: $packageRoot"
    }

    $sourceRoot = Resolve-WllamaAssetRoot -PackageRoot $packageRoot
    if (-not $sourceRoot) {
        throw "wllama asset root was not found. Expected either 'dist' or 'esm' under: $packageRoot"
    }

    Write-Host "Using wllama asset root: $sourceRoot"

    $files = Get-ChildItem -Path $sourceRoot -Recurse -File -ErrorAction Stop
    $copiedCount = 0

    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($sourceRoot.Length).TrimStart([char[]]@([char]92, [char]47))
        $targetPath = Join-Path -Path $destination -ChildPath $relativePath
        $targetDir = Split-Path -Path $targetPath -Parent
        if (-not (Test-Path -Path $targetDir -PathType Container)) {
            New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
        }

        Copy-Item -Path $file.FullName -Destination $targetPath -Force
        $copiedCount++
    }

    if ($copiedCount -eq 0) {
        throw "No files were copied from asset root: $sourceRoot"
    }

    Write-Host "Copied $copiedCount file(s) to $destination"
}
finally {
    Pop-Location -ErrorAction SilentlyContinue
    Restore-Env -OldCache $oldCache -OldUpdateNotifier $oldUpdateNotifier -OldFund $oldFund -OldAudit $oldAudit

    if (-not $KeepTemp -and (Test-Path -Path $tempRoot)) {
        Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($KeepTemp) {
        Write-Host "Temporary workspace kept at: $tempRoot"
    }
}
