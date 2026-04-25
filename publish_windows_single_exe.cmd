@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Avalonia - publish single self-contained exe
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet was not found. Install .NET 8 SDK or newer.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "Build\Publish-SelfContained.ps1"
if errorlevel 1 goto fail
echo Done: dist\AstraCraftLauncher\AstraCraftLauncher.exe
echo Installed folder target size: at least 270 MB.
pause
exit /b 0
:fail
echo Publish failed. This window stays open so you can read the error.
pause
exit /b 1
