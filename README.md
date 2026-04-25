# AstraCraft Launcher Rebuild

Native Minecraft launcher made with **C# + Avalonia UI**. No Python, no Flutter, no WebView.

## Features

- Modern rebuilt UI from scratch.
- Official Java Edition version catalog from Mojang/Piston manifest.
- Installs Vanilla versions by downloading version JSON, client jar, libraries, assets and natives.
- Launches Minecraft with offline UUID and `javaw.exe` when available.
- Supports inherited loader profiles already installed in `.minecraft/versions`.
- Can run local Fabric/Forge/Quilt installer `.jar` files.
- Scans real `mods`, `shaderpacks`, `resourcepacks`, `saves` folders.
- Bedrock tab for Minecraft for Windows folder and `minecraft://` launch.
- Logs to `%LOCALAPPDATA%\AstraCraftLauncher\logs` and `.minecraft\logs\astracraft-launch.log`.

## Requirements

- Windows 10/11
- .NET 8 SDK or newer to build/run from source
- Java 17+ to run modern Minecraft

## Run

```bat
run_dev_windows.cmd
```

## Publish exe

```bat
publish_windows_single_exe.cmd
```

The result is:

```text
dist\AstraCraftLauncher\AstraCraftLauncher.exe
```

## UI/logic rework v3.1

- Added 30 language options through `Services/Localization.cs`.
- Added a language selector in Settings; it saves to `AppConfig.Language`.
- Reworked the home screen into real launcher functions: quick launch, installed versions, Minecraft folder and local library counts.
- Removed fake or misleading UI: Pro promo, fake news, featured modpacks, misleading Servers/Skins tabs and fake support/status links.
- Library deletion now moves items to `.astracraft-trash` instead of permanently deleting them.
- Loader installer .jar action is now in Advanced and marked as trusted-file only.
