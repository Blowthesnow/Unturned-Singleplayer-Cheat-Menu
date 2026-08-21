[CmdletBinding()]
param(
    [string]$PluginDll,
    [string]$OutputDirectory,
    [string]$Version = '1.7.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ([string]::IsNullOrWhiteSpace($PluginDll)) {
    $PluginDll = Join-Path $workspaceRoot 'artifacts\UnturnedSingleplayerCheatMenu.dll'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $workspaceRoot 'dist'
}

$pluginDllFull = [IO.Path]::GetFullPath($PluginDll)
$outputDirectoryFull = [IO.Path]::GetFullPath($OutputDirectory)

if (-not (Test-Path -LiteralPath $pluginDllFull -PathType Leaf)) {
    throw "Plugin DLL not found: $pluginDllFull"
}

$pluginVersion = (Get-Item -LiteralPath $pluginDllFull).VersionInfo.FileVersion
if ($pluginVersion -ne "$Version.0") {
    throw "Plugin file version is $pluginVersion, expected $Version.0."
}

$releaseId = Get-Date -Format 'yyyyMMdd-HHmmss'
$stagingRoot = Join-Path $workspaceRoot "artifacts\plugin-only-package-staging-$releaseId"
$pluginOnlyStage = Join-Path $stagingRoot 'plugin-only'
$validationRoot = Join-Path $workspaceRoot "artifacts\plugin-only-package-validation-$releaseId"

New-Item -ItemType Directory -Path $pluginOnlyStage, $validationRoot, $outputDirectoryFull -Force | Out-Null

$pluginRelativePath = 'BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll'
$pluginTarget = Join-Path $pluginOnlyStage $pluginRelativePath
New-Item -ItemType Directory -Path (Split-Path -Parent $pluginTarget) -Force | Out-Null
Copy-Item -LiteralPath $pluginDllFull -Destination $pluginTarget -Force

$pluginHash = (Get-FileHash -LiteralPath $pluginDllFull -Algorithm SHA256).Hash
$pluginOnlyReadme = @"
Unturned Singleplayer Cheat Menu v$Version - Plugin-Only

This archive intentionally does NOT contain BepInEx.
Install BepInEx 5.4.23.5 x64 separately, then extract this archive into the Unturned game root that contains Unturned.exe.

Final plugin path:
BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll

Keep exactly one active UnturnedSingleplayerCheatMenu.dll under BepInEx\plugins. Rename old backups to a non-.dll suffix.
Use only in singleplayer without BattlEye.

插件 DLL SHA-256:
$pluginHash
"@
$pluginOnlyReadme | Set-Content -LiteralPath (Join-Path $pluginOnlyStage 'README-Install.txt') -Encoding UTF8

function Write-ChecksumManifest {
    param([string]$Stage)

    $manifestPath = Join-Path $Stage 'SHA256SUMS.txt'
    $entries = foreach ($file in Get-ChildItem -LiteralPath $Stage -Recurse -File -Force | Sort-Object FullName) {
        if ($file.FullName -eq $manifestPath) {
            continue
        }
        $relative = $file.FullName.Substring($Stage.Length).TrimStart('\').Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        "$hash *$relative"
    }
    $entries | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}

Write-ChecksumManifest -Stage $pluginOnlyStage

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-AvailableArchivePath {
    param([string]$BasePath)

    if (-not (Test-Path -LiteralPath $BasePath)) {
        return $BasePath
    }
    $directory = Split-Path -Parent $BasePath
    $name = [IO.Path]::GetFileNameWithoutExtension($BasePath)
    $extension = [IO.Path]::GetExtension($BasePath)
    return Join-Path $directory "$name-$releaseId$extension"
}

$pluginOnlyArchive = Get-AvailableArchivePath (
    Join-Path $outputDirectoryFull "Unturned-Singleplayer-Cheat-Menu-v$Version-Plugin-Only.zip")

[IO.Compression.ZipFile]::CreateFromDirectory($pluginOnlyStage, $pluginOnlyArchive, [IO.Compression.CompressionLevel]::Optimal, $false)

$pluginValidation = Join-Path $validationRoot 'plugin-only'
[IO.Compression.ZipFile]::ExtractToDirectory($pluginOnlyArchive, $pluginValidation)

$requiredPackageFiles = @(
    $pluginRelativePath,
    'README-Install.txt',
    'SHA256SUMS.txt'
)
foreach ($relativePath in $requiredPackageFiles) {
    $candidate = Join-Path $pluginValidation $relativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Plugin-Only package validation failed. Missing: $relativePath"
    }
}

$forbiddenPaths = @(
    '.doorstop_version',
    'doorstop_config.ini',
    'winhttp.dll',
    'BepInEx\core',
    'BepInEx\config',
    'BepInEx\cache',
    'BepInEx\LogOutput.log',
    'BepInEx\config\UnturnedSingleplayerCheatMenu.favorites.json',
    'BepInEx\config\UnturnedSingleplayerCheatMenu.teleports.json'
)
foreach ($relativePath in $forbiddenPaths) {
    if (Test-Path -LiteralPath (Join-Path $pluginValidation $relativePath)) {
        throw "Plugin-Only package contains forbidden BepInEx/runtime/private data: $relativePath"
    }
}

$packagedPluginHash = (Get-FileHash -LiteralPath (Join-Path $pluginValidation $pluginRelativePath) -Algorithm SHA256).Hash
if ($pluginHash -ne $packagedPluginHash) {
    throw 'Packaged plugin DLL hash does not match the Release build.'
}

[PSCustomObject]@{
    PluginOnlyArchive = $pluginOnlyArchive
    PluginOnlyArchiveLength = (Get-Item -LiteralPath $pluginOnlyArchive).Length
    PluginOnlyArchiveSHA256 = (Get-FileHash -LiteralPath $pluginOnlyArchive -Algorithm SHA256).Hash
    PluginDLLSHA256 = $pluginHash
    PluginOnlyFileCount = (Get-ChildItem -LiteralPath $pluginValidation -Recurse -File -Force).Count
    ValidationDirectory = $validationRoot
}
