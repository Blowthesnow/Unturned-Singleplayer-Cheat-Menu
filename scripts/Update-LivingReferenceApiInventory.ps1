[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginProject = Join-Path $repoRoot 'src\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.csproj'
$auditProject = Join-Path $PSScriptRoot 'ApiAudit\ApiAudit.csproj'
$output = Join-Path $repoRoot 'docs\reference\current-plugin-external-api-inventory.tsv'

dotnet build $pluginProject -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE."
}

dotnet run --project $auditProject -c Release -- $repoRoot $pluginProject $output
if ($LASTEXITCODE -ne 0) {
    throw "API inventory generation failed with exit code $LASTEXITCODE."
}

$rows = @(Import-Csv -LiteralPath $output -Delimiter "`t")
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
Write-Output "External API rows: $($rows.Count)"
Write-Output "Inventory SHA-256: $hash"
Write-Output "Inventory: $output"
