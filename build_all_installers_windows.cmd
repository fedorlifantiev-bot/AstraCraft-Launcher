@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Launcher - build EXE + MSI + MSIX installers
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK was not found. Install .NET 8 SDK or newer.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "Build\Build-AllInstallers.ps1"
if errorlevel 1 goto fail
echo Done. Check the dist folder.
pause
exit /b 0
:fail
echo Build failed. Read the error above.
pause
exit /b 1
