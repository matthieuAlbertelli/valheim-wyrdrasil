param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"

function Get-CsFiles {
    param([string]$BasePath)

    Get-ChildItem -Path $BasePath -Recurse -File -Filter *.cs |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
        }
}

$files = Get-CsFiles -BasePath $Root
$violations = New-Object System.Collections.Generic.List[object]

$serviceNamespaceRules = @(
    @{
        Name = 'Registry should prefer module APIs over Settlements.Services';
        Pattern = '^using\s+Wyrdrasil\.Settlements\.Services\s*;';
        Scope = 'Wyrdrasil.Registry';
        Allow = @(
            'Wyrdrasil.Registry/Bootstrap/RegistrySettlementsBootstrap.cs',
            'Wyrdrasil.Registry/Bootstrap/RegistrySettlementsCompositionServices.cs',
            'Wyrdrasil.Registry/Bootstrap/RegistryActionRegistryFactory.cs',
            'Wyrdrasil.Registry/Bootstrap/RegistryRuntimeBootstrap.cs',
            'Wyrdrasil.Registry/Services/RegistryDeletionService.cs',
            'Wyrdrasil.Registry/Services/RegistryFlushService.cs',
            'Wyrdrasil.Registry/Services/CraftStationAnchorEditorService.cs',
            'Wyrdrasil.Registry/Services/Interactions/RegistrySelectionFeedbackService.cs'
        )
    },
    @{
        Name = 'Registry should prefer module APIs over Souls.Services';
        Pattern = '^using\s+Wyrdrasil\.Souls\.Services\s*;';
        Scope = 'Wyrdrasil.Registry';
        Allow = @(
            'Wyrdrasil.Registry/Services/RegistryResidentService.cs'
        )
    },
    @{
        Name = 'Registry should prefer module APIs over Routines.Services';
        Pattern = '^using\s+Wyrdrasil\.Routines\.Services\s*;';
        Scope = 'Wyrdrasil.Registry';
        Allow = @(
            'Wyrdrasil.Registry/Occupations/ConstructionWorkOccupationSustainStrategy.cs'
        )
    }
)

$coreForbiddenPattern = '^using\s+Wyrdrasil\.(Registry|Settlements|Souls|Routines|Construction)\.'

foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath((Resolve-Path $Root), $file.FullName).Replace('\\', '/')
    $lines = Get-Content -Path $file.FullName

    if ($relative -like 'Wyrdrasil.Core/*') {
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match $coreForbiddenPattern) {
                $violations.Add([pscustomobject]@{
                    File = $relative
                    Line = $i + 1
                    Rule = 'Core must not depend on feature modules'
                    Text = $lines[$i].Trim()
                })
            }
        }
    }

    foreach ($rule in $serviceNamespaceRules) {
        if ($relative -notlike "$($rule.Scope)/*") {
            continue
        }

        $isAllowed = $rule.Allow -contains $relative
        if ($isAllowed) {
            continue
        }

        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match $rule.Pattern) {
                $violations.Add([pscustomobject]@{
                    File = $relative
                    Line = $i + 1
                    Rule = $rule.Name
                    Text = $lines[$i].Trim()
                })
            }
        }
    }
}

if ($violations.Count -eq 0) {
    Write-Host 'No module-boundary violations found.' -ForegroundColor Green
    exit 0
}

Write-Host ''
Write-Host 'Module-boundary violations found:' -ForegroundColor Yellow
Write-Host ''

foreach ($violation in $violations) {
    Write-Host ("{0}:{1} [{2}] {3}" -f $violation.File, $violation.Line, $violation.Rule, $violation.Text)
}

Write-Host ''
Write-Host ("Total violations: {0}" -f $violations.Count) -ForegroundColor Red
exit 1
