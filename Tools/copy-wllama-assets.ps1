param(
    [Parameter(Mandatory = $false)]
    [string]$SourceDistPath = "node_modules/@wllama/wllama/dist",

    [Parameter(Mandatory = $false)]
    [string]$DestinationPath = "Assets/StreamingAssets/llama-wasm"
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([string]$PathValue)

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue
    }

    return (Join-Path -Path (Get-Location) -ChildPath $PathValue)
}

function Resolve-WllamaAssetRoot {
    param([string]$PathValue)

    if (-not (Test-Path -Path $PathValue -PathType Container)) {
        return $null
    }

    $name = Split-Path -Path $PathValue -Leaf
    if ($name -eq "dist" -or $name -eq "esm") {
        return $PathValue
    }

    $distPath = Join-Path -Path $PathValue -ChildPath "dist"
    if (Test-Path -Path $distPath -PathType Container) {
        return $distPath
    }

    $esmPath = Join-Path -Path $PathValue -ChildPath "esm"
    if (Test-Path -Path $esmPath -PathType Container) {
        return $esmPath
    }

    return $PathValue
}

$source = Resolve-FullPath -PathValue $SourceDistPath
$destination = Resolve-FullPath -PathValue $DestinationPath

if (-not (Test-Path -Path $source -PathType Container)) {
    throw "Source dist folder was not found: $source"
}

$sourceRoot = Resolve-WllamaAssetRoot -PathValue $source
if (-not $sourceRoot) {
    throw "wllama asset root could not be resolved from source path: $source"
}

if (-not (Test-Path -Path $destination -PathType Container)) {
    New-Item -Path $destination -ItemType Directory -Force | Out-Null
}

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
    Write-Warning "No files copied from '$sourceRoot'. Check @wllama/wllama package contents."
}

Write-Host "Copied $copiedCount file(s) from $sourceRoot to $destination"
