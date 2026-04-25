@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Launcher - MSIX builder
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK was not found. Install .NET 8 SDK or newer.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "Build\Build-Msix.ps1"
if errorlevel 1 goto fail
echo Done: dist\AstraCraftLauncher-x64.msix
pause
exit /b 0
:fail
echo MSIX build failed. Make sure Windows 10/11 SDK is installed.
pause
exit /b 1
