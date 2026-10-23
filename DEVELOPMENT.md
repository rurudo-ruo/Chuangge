# Development

## Project

窗格 is a 64-bit Windows Forms application targeting **.NET Framework 4.8**. The solution is `窗格.sln`, and the project is in `窗格\窗格.csproj`.

## Prerequisites

- Windows
- Visual Studio 2022 Build Tools (or Visual Studio) with the **MSBuild** component
- The **.NET Framework 4.8 targeting pack**. In Visual Studio Installer, select it under **Individual components**. Alternatively, install the [.NET Framework 4.8 Developer Pack](https://aka.ms/msbuild/developerpacks).

## Build

From the repository folder, run in PowerShell:

```powershell
.\build.ps1
```

The script checks for MSBuild and the .NET Framework 4.8 targeting pack before building. If PowerShell blocks running the script, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The Release executable is created at `窗格\bin\Release\窗格.exe`. The target computer needs the **.NET Framework 4.8 runtime**, which is separate from the targeting pack used to build the application.

## GitHub releases

Pushing a tag matching `v*` starts `.github/workflows/release.yml`. It uses `actions/checkout@v5` (Node.js 24) and discovers MSBuild through Visual Studio's `vswhere.exe`. The workflow constructs the Unicode app name from code points in PowerShell so Windows PowerShell does not corrupt the solution or asset paths. It builds the Release configuration and attaches two assets: an EXE-only download and a ZIP containing only the app executable and Microsoft's .NET Framework 4.8 web installer. The workflow checks the downloaded installer's Authenticode signature and requires a valid Microsoft Corporation signer. The web installer downloads runtime components from Microsoft and requires internet access; it is not an offline runtime bundle.

## Display scaling

The app opts into Per-Monitor V2 DPI awareness. Fence contents, resize hit areas, and image thumbnails scale with monitor DPI; custom-rendered text uses grid-fitted anti-aliasing. Saved window positions and sizes retain the existing format.

## UI appearance

The interface uses Fluent Design as a visual baseline without adding a UI-framework dependency. `窗格\FluentTheme.cs` centralizes the settings and dialog palette; buttons use stable colors without hover/pressed fills. Fence windows remain custom-drawn and translucent, with a soft cursor-following glow and persistent selection/drag feedback scaled from logical units for per-monitor DPI.

## Fence data and compatibility

The application continues to use `%LOCALAPPDATA%\NoFences` so existing layouts and contents remain available. It reads the original XML metadata format, JSON records in `Fences\<id>\fence.json`, and XML metadata in `Backup_XML_*` directories. Alternate-format sources are read without being changed. Removing a fence deletes its saved metadata in the XML and JSON locations and matching recovery-backup records.

## License and attribution

This repository is a personal fork of [Twometer's NoFences](https://github.com/Twometer/NoFences). The project is distributed under the MIT License. The original copyright and license notice are retained; changes in this fork are maintained by **rurudo**. See [LICENSE](LICENSE).
