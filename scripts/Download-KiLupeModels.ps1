param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$modelDirectory = Join-Path $projectRoot "artifacts\models\rtdetr_v2_r18vd-ONNX"
$correctionDirectory = Join-Path $projectRoot "artifacts\models\german-spelling-correction-onnx"
$fixtureDirectory = Join-Path $projectRoot "artifacts\fixtures"

New-Item -ItemType Directory -Force -Path (Join-Path $modelDirectory "onnx"), $correctionDirectory, $fixtureDirectory | Out-Null

function Save-RemoteFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if ((Test-Path $Destination) -and -not $Force) {
        Write-Host "Vorhanden, ueberspringe: $Destination"
        return
    }

    Write-Host "Lade herunter: $Uri"
    $temporaryDestination = "$Destination.download"
    try {
        Invoke-WebRequest `
            -Uri $Uri `
            -OutFile $temporaryDestination `
            -UseBasicParsing `
            -MaximumRedirection 10
        Move-Item -Force -Path $temporaryDestination -Destination $Destination
    }
    finally {
        if (Test-Path $temporaryDestination) {
            Remove-Item -Force $temporaryDestination
        }
    }
}

Save-RemoteFile `
    -Uri "https://huggingface.co/onnx-community/rtdetr_v2_r18vd-ONNX/resolve/main/onnx/model.onnx?download=true" `
    -Destination (Join-Path $modelDirectory "onnx\model.onnx")
Save-RemoteFile `
    -Uri "https://huggingface.co/onnx-community/rtdetr_v2_r18vd-ONNX/resolve/main/config.json?download=true" `
    -Destination (Join-Path $modelDirectory "config.json")
Save-RemoteFile `
    -Uri "https://huggingface.co/onnx-community/rtdetr_v2_r18vd-ONNX/resolve/main/preprocessor_config.json?download=true" `
    -Destination (Join-Path $modelDirectory "preprocessor_config.json")
Save-RemoteFile `
    -Uri "https://raw.githubusercontent.com/ultralytics/yolov5/master/data/images/bus.jpg" `
    -Destination (Join-Path $fixtureDirectory "bus.jpg")

$correctionModelUri = "https://huggingface.co/Vinctilus/onnx-oliverguhr-spelling-correction-german-base/resolve/main"
Save-RemoteFile `
    -Uri "$correctionModelUri/model.onnx?download=true" `
    -Destination (Join-Path $correctionDirectory "model.onnx")
Save-RemoteFile `
    -Uri "$correctionModelUri/spiece.model?download=true" `
    -Destination (Join-Path $correctionDirectory "spiece.model")
Save-RemoteFile `
    -Uri "$correctionModelUri/config.json?download=true" `
    -Destination (Join-Path $correctionDirectory "config.json")
Save-RemoteFile `
    -Uri "$correctionModelUri/generation_config.json?download=true" `
    -Destination (Join-Path $correctionDirectory "generation_config.json")
Save-RemoteFile `
    -Uri "$correctionModelUri/tokenizer_config.json?download=true" `
    -Destination (Join-Path $correctionDirectory "tokenizer_config.json")
Save-RemoteFile `
    -Uri "$correctionModelUri/special_tokens_map.json?download=true" `
    -Destination (Join-Path $correctionDirectory "special_tokens_map.json")
Save-RemoteFile `
    -Uri "$correctionModelUri/tokenizer.json?download=true" `
    -Destination (Join-Path $correctionDirectory "tokenizer.json")

Write-Host "RT-DETR-, Korrekturmodell und Fixture stehen unter $projectRoot."