[CmdletBinding()]
param(
    [string]$BepInExSource,
    [string]$PluginDll,
    [string]$OutputDirectory,
    [string]$Version = '1.5.0'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ([string]::IsNullOrWhiteSpace($BepInExSource)) {
    $BepInExSource = Join-Path $workspaceRoot 'vendor\BepInEx'
}
if ([string]::IsNullOrWhiteSpace($PluginDll)) {
    $PluginDll = Join-Path $workspaceRoot 'artifacts\UnturnedSingleplayerCheatMenu.dll'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $workspaceRoot 'dist'
}

$bepInExSourceFull = [IO.Path]::GetFullPath($BepInExSource)
$pluginDllFull = [IO.Path]::GetFullPath($PluginDll)
$outputDirectoryFull = [IO.Path]::GetFullPath($OutputDirectory)

$requiredBepInExFiles = @(
    '.doorstop_version',
    'doorstop_config.ini',
    'winhttp.dll',
    'BepInEx\core\BepInEx.dll',
    'BepInEx\core\BepInEx.Preloader.dll',
    'BepInEx\core\0Harmony.dll'
)
foreach ($relativePath in $requiredBepInExFiles) {
    $candidate = Join-Path $bepInExSourceFull $relativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "BepInEx source is incomplete: $candidate"
    }
}
if (-not (Test-Path -LiteralPath $pluginDllFull -PathType Leaf)) {
    throw "Plugin DLL not found: $pluginDllFull"
}

$pluginVersion = (Get-Item -LiteralPath $pluginDllFull).VersionInfo.FileVersion
if ($pluginVersion -ne "$Version.0") {
    throw "Plugin file version is $pluginVersion, expected $Version.0."
}

$releaseId = Get-Date -Format 'yyyyMMdd-HHmmss'
$stagingRoot = Join-Path $workspaceRoot "artifacts\package-staging-$releaseId"
$fullStage = Join-Path $stagingRoot 'full'
$pluginOnlyStage = Join-Path $stagingRoot 'plugin-only'
$validationRoot = Join-Path $workspaceRoot "artifacts\package-validation-$releaseId"

New-Item -ItemType Directory -Path $fullStage, $pluginOnlyStage, $validationRoot, $outputDirectoryFull -Force | Out-Null

foreach ($sourceFile in Get-ChildItem -LiteralPath $bepInExSourceFull -File -Force) {
    Copy-Item -LiteralPath $sourceFile.FullName -Destination (Join-Path $fullStage $sourceFile.Name)
}
Copy-Item -LiteralPath (Join-Path $bepInExSourceFull 'BepInEx') -Destination $fullStage -Recurse

$pluginRelativePath = 'BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll'
foreach ($stage in @($fullStage, $pluginOnlyStage)) {
    $pluginTarget = Join-Path $stage $pluginRelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $pluginTarget) -Force | Out-Null
    Copy-Item -LiteralPath $pluginDllFull -Destination $pluginTarget
}

$launchCmd = @'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Launch-Unturned-Singleplayer.ps1"
if errorlevel 1 pause
'@
$verifyCmd = @'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Launch-Unturned-Singleplayer.ps1" -ValidateOnly
if errorlevel 1 pause
'@
$portableLauncher = @'
[CmdletBinding()]
param(
    [string]$GameRoot,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = Split-Path -Parent $PSScriptRoot
}

$gameRootFull = [IO.Path]::GetFullPath($GameRoot)
$executable = Join-Path $gameRootFull 'Unturned.exe'
$plugin = Join-Path $gameRootFull 'BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll'

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Unturned.exe not found. Extract the package into the Unturned game root: $gameRootFull"
}
if (-not (Test-Path -LiteralPath $plugin -PathType Leaf)) {
    throw "Plugin DLL not found: $plugin"
}

if ($ValidateOnly) {
    Write-Host "Installation layout is valid: $gameRootFull"
    Write-Host "Plugin: $plugin"
    exit 0
}

if (Get-Process -Name 'Unturned_BE' -ErrorAction SilentlyContinue) {
    throw 'BattlEye Unturned is running. Exit it before using this singleplayer-only plugin.'
}
$battleyeService = Get-Service -Name 'BEService' -ErrorAction SilentlyContinue
if ($null -ne $battleyeService -and $battleyeService.Status -eq 'Running') {
    throw 'BattlEye service is still running. Stop it manually or restart Windows before launching this singleplayer-only plugin.'
}
if (Get-Process -Name 'Unturned' -ErrorAction SilentlyContinue) {
    Write-Host 'Unturned is already running.'
    exit 0
}

Start-Process 'steam://launch/304930/dialog'
Write-Host 'Steam launch options opened. Select the no-BattlEye option, then use singleplayer only.'
'@

$scriptsDirectory = Join-Path $fullStage 'scripts'
New-Item -ItemType Directory -Path $scriptsDirectory -Force | Out-Null
$launchCmd | Set-Content -LiteralPath (Join-Path $fullStage 'Launch-Unturned-Singleplayer.cmd') -Encoding ASCII
$verifyCmd | Set-Content -LiteralPath (Join-Path $fullStage 'Verify-Installation.cmd') -Encoding ASCII
$portableLauncher | Set-Content -LiteralPath (Join-Path $scriptsDirectory 'Launch-Unturned-Singleplayer.ps1') -Encoding UTF8

$fullReadme = @"
Unturned 单人作弊菜单 v$Version
================================

适用环境：
- Unturned 3.26.3.8
- Unity 2022.3.62f3
- Windows x64 / Unity Mono
- 内含 BepInEx 5.4.23.5 x64

安装：
1. 完全退出 Unturned。
2. 打开 Steam 库，右键 Unturned，选择“管理”→“浏览本地文件”。
3. 把本压缩包内的所有文件直接解压到出现 Unturned.exe 的游戏根目录。
4. 如果 Windows 询问是否合并 BepInEx 文件夹或覆盖同名 BepInEx 运行时文件，请确认。
5. 可双击 Verify-Installation.cmd 检查目录是否正确。
6. 双击 Launch-Unturned-Singleplayer.cmd。
7. Steam 弹出启动方式后，明确选择“不使用 BattlEye Anti-Cheat”。
8. 只进入单人世界，角色加载完成后按 End 打开菜单。

说明：
- 地图界面的“单人作弊指令”可以不勾选，本插件不依赖原生作弊指令开关。
- 收藏、传送点和配置会在首次运行后写入 BepInEx\config。
- 本包不包含任何人的收藏、传送点、日志、缓存或个人配置。
- 物品和车辆只会显示本次游戏已成功加载的原版、地图及 Workshop 资产。

安全边界：
- 仅用于无 BattlEye 的单人模式。
- 不要用本插件进入多人服务器。
- 启动脚本不会停止、修改或禁用 BattlEye；若检测到相关进程或服务正在运行，会拒绝启动。

卸载：
- 完全退出游戏。
- 删除 BepInEx\plugins\UnturnedSingleplayerCheatMenu 文件夹即可停用插件。
- 如果游戏此前没有安装 BepInEx，而你希望一并移除，请先验证 Steam 游戏文件，或按 BepInEx 官方卸载方式处理根目录文件。

插件 DLL SHA-256：
$((Get-FileHash -LiteralPath $pluginDllFull -Algorithm SHA256).Hash)
"@
$pluginOnlyReadme = @"
Unturned 单人作弊菜单 v$Version（仅插件）
========================================

此包不含 BepInEx。仅适用于已经正确安装 BepInEx 5.4.23.5 x64 的 Unturned。

把压缩包内容解压到出现 Unturned.exe 的游戏根目录，确保最终路径为：
BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll

只在无 BattlEye 的单人世界中使用。角色加载完成后按 End 打开菜单。
地图界面的“单人作弊指令”可以不勾选。

插件 DLL SHA-256：
$((Get-FileHash -LiteralPath $pluginDllFull -Algorithm SHA256).Hash)
"@
$fullReadme | Set-Content -LiteralPath (Join-Path $fullStage 'README-安装说明.txt') -Encoding UTF8
$pluginOnlyReadme | Set-Content -LiteralPath (Join-Path $pluginOnlyStage 'README-安装说明.txt') -Encoding UTF8

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

Write-ChecksumManifest -Stage $fullStage
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

$fullArchive = Get-AvailableArchivePath (
    Join-Path $outputDirectoryFull "Unturned-Singleplayer-Cheat-Menu-v$Version-Full-BepInEx5-x64.zip")
$pluginOnlyArchive = Get-AvailableArchivePath (
    Join-Path $outputDirectoryFull "Unturned-Singleplayer-Cheat-Menu-v$Version-Plugin-Only.zip")

[IO.Compression.ZipFile]::CreateFromDirectory($fullStage, $fullArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
[IO.Compression.ZipFile]::CreateFromDirectory($pluginOnlyStage, $pluginOnlyArchive, [IO.Compression.CompressionLevel]::Optimal, $false)

$fullValidation = Join-Path $validationRoot 'full'
$pluginValidation = Join-Path $validationRoot 'plugin-only'
[IO.Compression.ZipFile]::ExtractToDirectory($fullArchive, $fullValidation)
[IO.Compression.ZipFile]::ExtractToDirectory($pluginOnlyArchive, $pluginValidation)

$requiredPackageFiles = @(
    '.doorstop_version',
    'doorstop_config.ini',
    'winhttp.dll',
    'BepInEx\core\BepInEx.dll',
    $pluginRelativePath,
    'Launch-Unturned-Singleplayer.cmd',
    'Verify-Installation.cmd',
    'scripts\Launch-Unturned-Singleplayer.ps1',
    'README-安装说明.txt',
    'SHA256SUMS.txt'
)
foreach ($relativePath in $requiredPackageFiles) {
    $candidate = Join-Path $fullValidation $relativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Full package validation failed. Missing: $relativePath"
    }
}

$forbiddenPaths = @(
    'BepInEx\config',
    'BepInEx\cache',
    'BepInEx\LogOutput.log',
    'BepInEx\config\UnturnedSingleplayerCheatMenu.favorites.json',
    'BepInEx\config\UnturnedSingleplayerCheatMenu.teleports.json'
)
foreach ($relativePath in $forbiddenPaths) {
    if (Test-Path -LiteralPath (Join-Path $fullValidation $relativePath)) {
        throw "Full package contains private/runtime data: $relativePath"
    }
}

$sourceHash = (Get-FileHash -LiteralPath $pluginDllFull -Algorithm SHA256).Hash
$fullPluginHash = (Get-FileHash -LiteralPath (Join-Path $fullValidation $pluginRelativePath) -Algorithm SHA256).Hash
$pluginOnlyHash = (Get-FileHash -LiteralPath (Join-Path $pluginValidation $pluginRelativePath) -Algorithm SHA256).Hash
if ($sourceHash -ne $fullPluginHash -or $sourceHash -ne $pluginOnlyHash) {
    throw 'Packaged plugin DLL hash does not match the Release build.'
}

New-Item -ItemType File -Path (Join-Path $fullValidation 'Unturned.exe') -Force | Out-Null
& (Join-Path $fullValidation 'scripts\Launch-Unturned-Singleplayer.ps1') -ValidateOnly
if ($LASTEXITCODE -ne 0) {
    throw "Portable launcher validation failed with exit code $LASTEXITCODE."
}

[PSCustomObject]@{
    FullArchive = $fullArchive
    FullArchiveLength = (Get-Item -LiteralPath $fullArchive).Length
    FullArchiveSHA256 = (Get-FileHash -LiteralPath $fullArchive -Algorithm SHA256).Hash
    PluginOnlyArchive = $pluginOnlyArchive
    PluginOnlyArchiveLength = (Get-Item -LiteralPath $pluginOnlyArchive).Length
    PluginOnlyArchiveSHA256 = (Get-FileHash -LiteralPath $pluginOnlyArchive -Algorithm SHA256).Hash
    PluginDLLSHA256 = $sourceHash
    FullPackageFileCount = (Get-ChildItem -LiteralPath $fullValidation -Recurse -File -Force).Count - 1
    ValidationDirectory = $validationRoot
}
