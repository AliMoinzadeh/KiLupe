param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$modelDirectory = Join-Path $projectRoot "artifacts\models\qwen2.5-3b-instruct"
$modelPath = Join-Path $modelDirectory "Qwen2.5-3B-Instruct-Q4_K_M.gguf"
$modelUri = "https://huggingface.co/bartowski/Qwen2.5-3B-Instruct-GGUF/resolve/main/Qwen2.5-3B-Instruct-Q4_K_M.gguf?download=true"

New-Item -ItemType Directory -Force -Path $modelDirectory | Out-Null

if ((Test-Path $modelPath) -and -not $Force) {
    Write-Host "Vorhanden, ueberspringe: $modelPath"
    exit 0
}

$temporaryPath = "$modelPath.download"
try {
    Write-Host "Lade grosses GGUF-Modell herunter: $modelUri"
    Invoke-WebRequest `
        -Uri $modelUri `
        -OutFile $temporaryPath `
        -UseBasicParsing `
        -MaximumRedirection 10
    Move-Item -Force -Path $temporaryPath -Destination $modelPath
}
finally {
    if (Test-Path $temporaryPath) {
        Remove-Item -Force $temporaryPath
    }
}

Write-Host "Lokales LLM steht unter $modelPath."