@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Avalonia - dev run
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
dotnet run --project "AstraCraft.Avalonia.csproj"
if errorlevel 1 goto fail
exit /b 0
:fail
echo Run failed. This window stays open so you can read the error.
pause
exit /b 1
