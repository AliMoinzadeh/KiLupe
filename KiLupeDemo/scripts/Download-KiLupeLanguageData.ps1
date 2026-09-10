param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$tessdataDirectory = Join-Path $projectRoot "tessdata"
$dictionaryDirectory = Join-Path $projectRoot "dictionaries"

New-Item -ItemType Directory -Force -Path $tessdataDirectory, $dictionaryDirectory | Out-Null

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
    Invoke-WebRequest -Uri $Uri -OutFile $Destination -UseBasicParsing
}

Save-RemoteFile `
    -Uri "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/deu.traineddata" `
    -Destination (Join-Path $tessdataDirectory "deu.traineddata")
Save-RemoteFile `
    -Uri "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/eng.traineddata" `
    -Destination (Join-Path $tessdataDirectory "eng.traineddata")

Save-RemoteFile `
    -Uri "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/de/de_DE_frami.aff" `
    -Destination (Join-Path $dictionaryDirectory "de_DE.aff")
Save-RemoteFile `
    -Uri "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/de/de_DE_frami.dic" `
    -Destination (Join-Path $dictionaryDirectory "de_DE.dic")
Save-RemoteFile `
    -Uri "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/en/en_US.aff" `
    -Destination (Join-Path $dictionaryDirectory "en_US.aff")
Save-RemoteFile `
    -Uri "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/en/en_US.dic" `
    -Destination (Join-Path $dictionaryDirectory "en_US.dic")

Write-Host "Sprachdaten stehen unter $projectRoot."