@echo off
setlocal
cd /d "%~dp0"
echo [AstraCraft] Verifying project...
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: .NET SDK is not installed or not in PATH.
  echo Install .NET 8 SDK from https://dotnet.microsoft.com/download
  exit /b 1
)
dotnet --version
dotnet restore AstraCraft.Avalonia.csproj || exit /b 1
dotnet build AstraCraft.Avalonia.csproj -c Release --no-restore || exit /b 1
echo.
echo OK: Project builds successfully.
