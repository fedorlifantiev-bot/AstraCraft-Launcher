@echo off
setlocal EnableExtensions
cd /d "%~dp0"
echo AstraCraft Avalonia - Inno EXE installer
call publish_windows_single_exe.cmd
if errorlevel 1 exit /b 1
where iscc >nul 2>nul
if errorlevel 1 (
  echo ERROR: Inno Setup Compiler is not in PATH.
  echo Install Inno Setup 6, or run: winget install --id JRSoftware.InnoSetup -e -s winget
  pause
  exit /b 1
)
iscc "Installer\AstraCraftLauncher.iss"
if errorlevel 1 goto fail
echo Installer ready in dist\AstraCraftLauncherSetup-x64.exe
pause
exit /b 0
:fail
echo Installer build failed.
pause
exit /b 1
