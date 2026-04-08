param(
    [string]$OutputZip = "Wyrdrasil-ProjectSnapshot.zip"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($root)) {
    throw "Impossible de déterminer le dossier racine du script."
}

$outputPath = if ([System.IO.Path]::IsPathRooted($OutputZip)) {
    $OutputZip
} else {
    Join-Path $root $OutputZip
}

$excludedDirectoryNames = @(
    '.git',
    '.vs',
    '.idea',
    'bin',
    'obj',
    'TestResults',
    'packages',
    'node_modules'
)

$excludedExtensions = @(
    '.zip',
    '.7z',
    '.rar',
    '.dll',
    '.exe',
    '.pdb',
    '.cache',
    '.tmp',
    '.user',
    '.suo'
)

$excludedFileNames = @(
    '.DS_Store'
)

function Test-IsExcludedPath {
    param([string]$FullPath)

    $current = [System.IO.DirectoryInfo](Split-Path -Path $FullPath -Parent)
    while ($null -ne $current) {
        if ($excludedDirectoryNames -contains $current.Name) {
            return $true
        }

        if ($current.FullName -eq $root) {
            break
        }

        $current = $current.Parent
    }

    return $false
}

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("WyrdrasilSnapshot_" + [System.Guid]::NewGuid().ToString('N'))
$stagingRoot = Join-Path $tempRoot 'snapshot'
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

try {
    $files = Get-ChildItem -Path $root -Recurse -File | Where-Object {
        $fullName = $_.FullName

        if ($fullName -eq $outputPath) {
            return $false
        }

        if (Test-IsExcludedPath -FullPath $fullName) {
            return $false
        }

        if ($excludedFileNames -contains $_.Name) {
            return $false
        }

        if ($excludedExtensions -contains $_.Extension.ToLowerInvariant()) {
            return $false
        }

        return $true
    }

    foreach ($file in $files) {
        $relativePath = [System.IO.Path]::GetRelativePath($root, $file.FullName)
        $destinationPath = Join-Path $stagingRoot $relativePath
        $destinationDirectory = Split-Path -Path $destinationPath -Parent

        if (-not (Test-Path $destinationDirectory)) {
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        }

        Copy-Item -Path $file.FullName -Destination $destinationPath -Force
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory($stagingRoot, $outputPath)

    Write-Host "Archive créée : $outputPath"
    Write-Host "Fichiers inclus : $($files.Count)"
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item $tempRoot -Recurse -Force
    }
}
