[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,

    [switch]$RemoveRetiredReferences
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$gameRootFull = [IO.Path]::GetFullPath($GameRoot)
$managedRoot = Join-Path $gameRootFull 'Unturned_Data\Managed'
$bepInExCore = Join-Path $gameRootFull 'BepInEx\core'
$destination = Join-Path $workspaceRoot 'lib'

if (-not (Test-Path -LiteralPath (Join-Path $gameRootFull 'Unturned.exe') -PathType Leaf)) {
    throw "Invalid Unturned game directory: $gameRootFull"
}
if (-not (Test-Path -LiteralPath (Join-Path $bepInExCore 'BepInEx.dll') -PathType Leaf)) {
    throw 'BepInEx 5 is not installed in the selected Unturned directory.'
}

$sources = @{
    'BepInEx.dll' = Join-Path $bepInExCore 'BepInEx.dll'
    '0Harmony.dll' = Join-Path $bepInExCore '0Harmony.dll'
    'Assembly-CSharp.dll' = Join-Path $managedRoot 'Assembly-CSharp.dll'
    'Newtonsoft.Json.dll' = Join-Path $managedRoot 'Newtonsoft.Json.dll'
    'SDG.Glazier.Runtime.dll' = Join-Path $managedRoot 'SDG.Glazier.Runtime.dll'
    'UnityEngine.dll' = Join-Path $managedRoot 'UnityEngine.dll'
    'UnityEngine.CoreModule.dll' = Join-Path $managedRoot 'UnityEngine.CoreModule.dll'
    'UnityEngine.InputLegacyModule.dll' = Join-Path $managedRoot 'UnityEngine.InputLegacyModule.dll'
    'UnityEngine.PhysicsModule.dll' = Join-Path $managedRoot 'UnityEngine.PhysicsModule.dll'
    'UnityEngine.JSONSerializeModule.dll' = Join-Path $managedRoot 'UnityEngine.JSONSerializeModule.dll'
    'UnityEngine.TextRenderingModule.dll' = Join-Path $managedRoot 'UnityEngine.TextRenderingModule.dll'
    'UnityEngine.UI.dll' = Join-Path $managedRoot 'UnityEngine.UI.dll'
    'Unity.TextMeshPro.dll' = Join-Path $managedRoot 'Unity.TextMeshPro.dll'
    'UnityEngine.TextCoreFontEngineModule.dll' = Join-Path $managedRoot 'UnityEngine.TextCoreFontEngineModule.dll'
    'UnityEngine.TextCoreTextEngineModule.dll' = Join-Path $managedRoot 'UnityEngine.TextCoreTextEngineModule.dll'
    'UnityEngine.UIModule.dll' = Join-Path $managedRoot 'UnityEngine.UIModule.dll'
}

foreach ($entry in $sources.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
        throw "Required reference not found: $($entry.Value)"
    }
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($entry in $sources.GetEnumerator()) {
    Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $destination $entry.Key) -Force
}

$retiredReferences = @('UnityEngine.IMGUIModule.dll')
foreach ($retiredName in $retiredReferences) {
    $retiredPath = Join-Path $destination $retiredName
    if (-not (Test-Path -LiteralPath $retiredPath -PathType Leaf)) {
        continue
    }
    if ($RemoveRetiredReferences) {
        $resolvedDestination = [IO.Path]::GetFullPath($destination).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $resolvedRetired = [IO.Path]::GetFullPath($retiredPath)
        if (-not $resolvedRetired.StartsWith($resolvedDestination, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove a retired reference outside lib: $resolvedRetired"
        }
        Remove-Item -LiteralPath $resolvedRetired -Force
        Write-Host "Removed retired reference: $resolvedRetired"
    }
    else {
        Write-Warning "Retired reference remains in lib: $retiredPath. Re-run with -RemoveRetiredReferences after reviewing the target."
    }
}

Write-Host "Prepared $($sources.Count) local references in: $destination"
Write-Host 'The lib directory is git-ignored and must not be committed.'
