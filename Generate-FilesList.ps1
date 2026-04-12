$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputFile = Join-Path $projectRoot "files.txt"

Get-ChildItem -Path $projectRoot -Recurse -Filter *.cs -File |
        Where-Object { $_.FullName -notmatch '[\\/](obj)[\\/]' } |
        ForEach-Object {
            $_.FullName.Substring($projectRoot.Length + 1)
        } |
        Set-Content -Path $outputFile -Encoding UTF8

Write-Host "Liste générée dans $outputFile"