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

## Notes

Loading animations and the experimental Fabric loading-screen mod were removed from this build. Minecraft now starts directly from the launcher without extra animation layers or client-side loading-screen modifications.

---

## v21 Builds Manager Update

This version adds a full builds/profiles manager:

- separate profiles for Vanilla, Forge, Fabric, Quilt, PvP, Survival and Shaders;
- per-profile mods, resourcepacks, shaderpacks, config, RAM, Java mode and FPS preset;
- Fabric / Quilt loader installer integration;
- Forge official installer page shortcut;
- Modrinth one-click mod install with compatibility warnings and dependency download;
- world backups and restore;
- auto-backup before launching selected profile;
- FPS presets for low / medium / high PCs;
- crash diagnostics tab with copyable report.

## Real Modrinth Modpacks

The launcher can search and install real Modrinth modpacks.

Features:

- search `project_type: modpack` through the Modrinth API;
- open project pages through `modrinth.black`;
- download `.mrpack` files;
- read `modrinth.index.json`;
- download client files listed in the modpack manifest;
- extract `overrides` into the selected launcher profile;
- update the profile Minecraft version and loader from the modpack dependencies;
- keep each profile isolated with its own `mods`, `resourcepacks`, `shaderpacks`, and `config` folders.

The launcher does not bundle third-party modpacks. They are downloaded by the user from Modrinth when installing a pack.

## Built-in Modrinth Store

AstraCraft includes an in-launcher Modrinth store. Users can search for mods, shaders, resource packs and modpacks, choose a launcher profile, filter by Minecraft version/loader compatibility, and install content with one click without opening a browser.

The launcher downloads files directly from Modrinth API, installs required dependencies, imports `.mrpack` modpacks, and places files into the selected build profile folders.


## Public release tools

AstraCraft includes tools that make the launcher easier to publish publicly:

- first-run setup for GitHub repository and support links;
- built-in update check through GitHub Releases;
- release channel selector: stable, beta, nightly;
- support bundle export with launcher logs, system info, latest.log and crash reports;
- public release checklist inside the launcher;
- GitHub issue templates;
- GitHub Actions build workflow;
- SECURITY.md, PRIVACY.md and RELEASE_CHECKLIST.md;
- SHA256SUMS helper script in Tools/Write-ReleaseHashes.ps1.

AstraCraft Launcher is not an official Mojang, Microsoft or Modrinth product. Do not commit Minecraft game files, Java runtimes, `.minecraft`, `bin/`, `obj/` or installer outputs to source control. Attach built installers to GitHub Releases instead.


## Modrinth Store Browsing

The built-in Modrinth store supports browsing without a search query. Users can select project type, category, sorting mode, profile compatibility filter, and move through pages with Back/Next buttons. Mods, shaders, resource packs and modpacks can be installed directly from the launcher.


## Stable polished v45

This build focuses on a clean public launcher experience: stable UI, safe Java/RAM launch defaults, Smart Center actions, backups, diagnostics, language sync with Minecraft, and no X-Ray/hitbox automation.


---

## Cross-platform build

AstraCraft Launcher can be published for Windows, Linux and macOS.

### Windows x64

```bat
publish_windows_x64.cmd
```

Output:

```text
dist\win-x64\AstraCraftLauncher.exe
```

### Linux x64

```bash
chmod +x publish_linux_x64.sh
./publish_linux_x64.sh
```

Output:

```text
dist/linux-x64/AstraCraftLauncher
dist/AstraCraftLauncher-linux-x64.tar.gz
```

### macOS Intel x64

```bash
chmod +x publish_macos_x64.sh
./publish_macos_x64.sh
```

Output:

```text
dist/osx-x64/AstraCraftLauncher
dist/AstraCraftLauncher-macos-x64.tar.gz
```

### macOS Apple Silicon arm64

```bash
chmod +x publish_macos_arm64.sh
./publish_macos_arm64.sh
```

Output:

```text
dist/osx-arm64/AstraCraftLauncher
dist/AstraCraftLauncher-macos-arm64.tar.gz
```

### Notes

- Windows installer scripts require Windows and Inno Setup.
- Linux and macOS builds are portable self-contained builds.
- macOS public releases should be signed and notarized with Apple Developer ID.
- Java is still required for Minecraft itself; the launcher can use a configured Java path or Java from PATH/JAVA_HOME.

---

## AstraCraft Launcher v5.0 Huge Update

Version `5.0.0` is focused on stability, public release quality and cross-platform packaging.

### Added

- Readiness Report for checking Java, Minecraft folders, selected version, profiles and recent launcher logs.
- Release Bundle for bug reports with logs, crash report, profile summary and readiness report.
- Smart RAM improvements based on selected profile/loader.
- Cross-platform verification scripts:
  - `verify_project_windows.cmd`
  - `verify_project_linux_macos.sh`
- Build cleanup scripts:
  - `clean_build_outputs.cmd`
  - `clean_build_outputs.sh`
- Better public release notes and checklist files.

### Kept clean

The public build does not include X-Ray, hitboxes, noclip, packet cheats or fake feature buttons.

### Verify before release

Windows:

```bat
verify_project_windows.cmd
```

Linux/macOS:

```bash
./verify_project_linux_macos.sh
```
