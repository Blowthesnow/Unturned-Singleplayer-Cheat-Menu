@echo off
setlocal
set "GAME_ROOT=%~1"
if "%GAME_ROOT%"=="" set "GAME_ROOT=%UNTURNED_GAME_DIR%"
if "%GAME_ROOT%"=="" (
  echo Usage: %~nx0 "C:\path\to\Unturned"
  echo Or set the UNTURNED_GAME_DIR environment variable.
  exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Launch-Unturned-Singleplayer.ps1" -GameRoot "%GAME_ROOT%"
if errorlevel 1 pause
