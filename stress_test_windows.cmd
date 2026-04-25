@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Avalonia - stress test
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet was not found. Install .NET 8 SDK or newer.
  pause
  exit /b 1
)
dotnet restore "AstraCraft.Avalonia.csproj"
if errorlevel 1 goto fail
dotnet build "AstraCraft.Avalonia.csproj" -c Release
if errorlevel 1 goto fail
echo Static build test passed.
echo Start the app and test: install 1.21.1, launch, open Library, Bedrock, Settings.
pause
exit /b 0
:fail
echo Stress test failed.
pause
exit /b 1
