$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$inputFile = Join-Path $projectRoot "demande-export.txt"
$outputFile = Join-Path $projectRoot "export.txt"

if (-not (Test-Path $inputFile)) {
    Write-Error "Fichier introuvable : $inputFile"
    exit 1
}

# On vide/recrée le fichier de sortie
Set-Content -Path $outputFile -Value "" -Encoding UTF8

Get-Content $inputFile | ForEach-Object {
    $relativePath = $_.Trim()

    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        return
    }

    $fullPath = Join-Path $projectRoot $relativePath

    Add-Content -Path $outputFile -Value "===== $relativePath =====" -Encoding UTF8

    if (Test-Path $fullPath) {
        Get-Content $fullPath | Add-Content -Path $outputFile -Encoding UTF8
    }
    else {
        Add-Content -Path $outputFile -Value "[FICHIER INTROUVABLE]" -Encoding UTF8
    }

    Add-Content -Path $outputFile -Value "" -Encoding UTF8
}

Write-Host "Export terminé dans $outputFile"