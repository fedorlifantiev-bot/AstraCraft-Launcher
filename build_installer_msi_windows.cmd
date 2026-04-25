@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Launcher - MSI builder
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet SDK was not found. Install .NET 8 SDK or newer.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "Build\Build-Msi.ps1"
if errorlevel 1 goto fail
echo Done: dist\AstraCraftLauncherSetup-x64.msi
pause
exit /b 0
:fail
echo MSI build failed.
pause
exit /b 1
