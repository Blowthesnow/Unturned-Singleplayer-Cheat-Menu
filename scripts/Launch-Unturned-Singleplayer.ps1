[CmdletBinding()]
param([string]$GameRoot = $env:UNTURNED_GAME_DIR)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    throw 'Specify -GameRoot or set the UNTURNED_GAME_DIR environment variable.'
}

$gameRootFull = [IO.Path]::GetFullPath($GameRoot)
$executable = Join-Path $gameRootFull 'Unturned.exe'
$plugin = Join-Path $gameRootFull 'BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll'

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Unturned.exe not found: $executable"
}
if (-not (Test-Path -LiteralPath $plugin -PathType Leaf)) {
    throw "Plugin is not installed: $plugin"
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

# Directly starting Unturned.exe can be redirected to Unturned_BE.exe by Steam.
# Open Steam's launch-option dialog so the player can explicitly select the
# no-BattlEye entry instead of silently choosing a potentially unsafe route.
Start-Process 'steam://launch/304930/dialog'
Write-Host 'Steam launch options opened. Select the no-BattlEye option, then use singleplayer only.'
