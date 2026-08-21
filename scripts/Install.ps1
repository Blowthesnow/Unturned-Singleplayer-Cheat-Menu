[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameRoot,
    [Parameter(Mandatory = $true)]
    [string]$BepInExArchive,
    [string]$PluginDll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$gameRootFull = [IO.Path]::GetFullPath($GameRoot)
$archiveFull = [IO.Path]::GetFullPath($BepInExArchive)
if ([string]::IsNullOrWhiteSpace($PluginDll)) {
    $PluginDll = Join-Path $workspaceRoot 'artifacts\UnturnedSingleplayerCheatMenu.dll'
}
$pluginDllFull = [IO.Path]::GetFullPath($PluginDll)

if (-not (Test-Path -LiteralPath (Join-Path $gameRootFull 'Unturned.exe') -PathType Leaf)) {
    throw "Invalid Unturned game directory: $gameRootFull"
}
if (-not (Test-Path -LiteralPath $archiveFull -PathType Leaf)) {
    throw "BepInEx archive not found: $archiveFull"
}
if (-not (Test-Path -LiteralPath $pluginDllFull -PathType Leaf)) {
    throw "Plugin DLL not found. Build Release first: $pluginDllFull"
}
if (Get-Process -Name 'Unturned', 'Unturned_BE' -ErrorAction SilentlyContinue) {
    throw 'Unturned is running. Exit the game before installation.'
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = Join-Path $workspaceRoot "backups\deploy-$timestamp"
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempParent ("unturned-cheat-menu-" + [Guid]::NewGuid().ToString('N'))
$manifest = [Collections.Generic.List[string]]::new()

function Backup-TargetFile {
    param([string]$TargetPath, [string]$RelativePath)
    if (Test-Path -LiteralPath $TargetPath -PathType Leaf) {
        $backupPath = Join-Path $backupRoot $RelativePath
        $backupDirectory = Split-Path -Parent $backupPath
        New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
        Copy-Item -LiteralPath $TargetPath -Destination $backupPath -Force
        $manifest.Add("BACKUP`t$RelativePath")
    }
}

try {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    Expand-Archive -LiteralPath $archiveFull -DestinationPath $tempRoot

    $expectedCore = Join-Path $tempRoot 'BepInEx\core\BepInEx.dll'
    if (-not (Test-Path -LiteralPath $expectedCore -PathType Leaf)) {
        throw 'Archive is not the expected BepInEx 5 x64 package.'
    }

    foreach ($source in Get-ChildItem -LiteralPath $tempRoot -Recurse -File) {
        $relative = $source.FullName.Substring($tempRoot.Length).TrimStart('\')
        $target = Join-Path $gameRootFull $relative
        Backup-TargetFile -TargetPath $target -RelativePath $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $source.FullName -Destination $target -Force
        $manifest.Add("INSTALL`t$relative")
    }

    $pluginRelative = 'BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll'
    $pluginTarget = Join-Path $gameRootFull $pluginRelative
    Backup-TargetFile -TargetPath $pluginTarget -RelativePath $pluginRelative
    New-Item -ItemType Directory -Path (Split-Path -Parent $pluginTarget) -Force | Out-Null
    Copy-Item -LiteralPath $pluginDllFull -Destination $pluginTarget -Force
    $manifest.Add("INSTALL`t$pluginRelative")

    # Keep exactly one active copy of this plugin under BepInEx\plugins.
    # Historical root-level or duplicate DLLs can otherwise be loaded beside
    # the canonical single-DLL subdirectory installation.
    $pluginsRoot = Join-Path $gameRootFull 'BepInEx\plugins'
    $activePluginDlls = @(
        Get-ChildItem -LiteralPath $pluginsRoot -Recurse -File -Filter 'UnturnedSingleplayerCheatMenu.dll' |
            Where-Object { [IO.Path]::GetFullPath($_.FullName) -ne [IO.Path]::GetFullPath($pluginTarget) }
    )
    foreach ($duplicate in $activePluginDlls) {
        $duplicateRelative = $duplicate.FullName.Substring($gameRootFull.Length).TrimStart('\')
        Backup-TargetFile -TargetPath $duplicate.FullName -RelativePath $duplicateRelative
        $disabledPath = $duplicate.FullName + '.disabled'
        if (Test-Path -LiteralPath $disabledPath) {
            $disabledPath += '-' + $timestamp
        }
        Move-Item -LiteralPath $duplicate.FullName -Destination $disabledPath
        $manifest.Add("DISABLE`t$duplicateRelative`t$([IO.Path]::GetFileName($disabledPath))")
    }

    $remainingPluginDlls = @(
        Get-ChildItem -LiteralPath $pluginsRoot -Recurse -File -Filter 'UnturnedSingleplayerCheatMenu.dll'
    )
    if ($remainingPluginDlls.Count -ne 1 -or
        [IO.Path]::GetFullPath($remainingPluginDlls[0].FullName) -ne [IO.Path]::GetFullPath($pluginTarget)) {
        throw 'Deployment invariant failed: exactly one active UnturnedSingleplayerCheatMenu.dll must remain at the canonical subdirectory path.'
    }

    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $manifestPath = Join-Path $backupRoot 'install-manifest.txt'
    $manifest | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    Write-Host "Installation complete: $pluginTarget"
    Write-Host "Backup and install manifest: $manifestPath"
}
finally {
    $resolvedTemp = [IO.Path]::GetFullPath($tempRoot)
    if ($resolvedTemp.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTemp)) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
