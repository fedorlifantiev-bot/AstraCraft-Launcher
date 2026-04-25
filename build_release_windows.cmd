@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Avalonia - release build
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet was not found. Install .NET 8 SDK or newer.
  pause
  exit /b 1
)
if not exist "AstraCraft.Avalonia.csproj" (
  echo ERROR: Project file not found. Extract the ZIP before running this script.
  pause
  exit /b 1
)
dotnet restore "AstraCraft.Avalonia.csproj"
if errorlevel 1 goto fail
dotnet build "AstraCraft.Avalonia.csproj" -c Release
if errorlevel 1 goto fail
echo Build OK.
pause
exit /b 0
:fail
echo Build failed. This window stays open so you can read the error.
pause
exit /b 1
