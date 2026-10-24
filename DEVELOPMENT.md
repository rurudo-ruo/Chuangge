# Development

## Project

窗格 is a 64-bit Windows Forms application targeting **.NET Framework 4.8**. The solution is `窗格.sln`, and the project is in `窗格\窗格.csproj`.

## Code structure

- `Model\FenceManager.cs` loads and saves fence data without creating UI. `FenceWindowCoordinator` is responsible for turning loaded or newly created fence records into windows.
- `FenceViewModel` owns fence-item and setting mutations, including rollback when a save fails. `FenceLayout` holds the shared item-positioning, drop-target, overflow, and fit-size calculations used by the window.
- `FenceWindow` handles drawing and interaction. Move and resize persistence is debounced while the window is active, with the latest geometry saved synchronously before closing.
- `AdvancedSettingsForm` composes settings sections from dedicated controls in `SettingsSectionControls.cs`; `SettingsSectionControl` provides their shared card presentation.
- Shell context-menu interop is grouped under `Win32`, including its supporting hook and exception types.

The project uses an explicit source-file list in `窗格\窗格.csproj`, so new `.cs` files must be added there. No automated test project is currently configured. For changes to persistence, layout, or settings, validate with the Release build and manually verify loading existing layouts, adding/removing/reordering items, moving items between fences, resizing/moving and closing fences, and applying settings.

## Prerequisites

- Windows
- Visual Studio 2022 Build Tools (or Visual Studio) with the **MSBuild** component
- The **.NET Framework 4.8 targeting pack**. In Visual Studio Installer, select it under **Individual components**. Alternatively, install the [.NET Framework 4.8 Developer Pack](https://aka.ms/msbuild/developerpacks).

## Build

From the repository folder, run in PowerShell:

```powershell
.\build.ps1
```

The script checks for MSBuild and the .NET Framework 4.8 targeting pack before building. If the matching Release app is running, it requests a normal window close and waits for the fences to save; it never force-terminates the app and cancels the build if the app cannot close safely. If PowerShell blocks running the script, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The Release executable is created at `窗格\bin\Release\窗格.exe`. The target computer needs the **.NET Framework 4.8 runtime**, which is separate from the targeting pack used to build the application.

## GitHub releases

Pushing a tag matching `v*` starts `.github/workflows/release.yml`. It uses `actions/checkout@v5` (Node.js 24) and discovers MSBuild through Visual Studio's `vswhere.exe`. The workflow constructs the Unicode app name from code points in PowerShell so Windows PowerShell does not corrupt the solution or asset paths. It builds the Release configuration and attaches two assets: an EXE-only download and a ZIP containing only the app executable and Microsoft's .NET Framework 4.8 web installer. The workflow checks the downloaded installer's Authenticode signature and requires a valid Microsoft Corporation signer. Release notes list first-parent commit subjects since the previous reachable tag, with English and Simplified Chinese text, links to each commit, and a full comparison. Maintain translations in `.github/release-notes-zh-CN.json`, keyed by the exact English commit subject; unmapped subjects are marked for translation and reported as workflow warnings. The web installer downloads runtime components from Microsoft and requires internet access; it is not an offline runtime bundle.

## Localization

The UI follows the current Windows UI language. Runtime text uses `UiText.resx` with a Simplified Chinese translation in `UiText.zh-CN.resx`; missing translations fall back to English. Designer-based dialogs and fence menus use their corresponding `.zh-CN.resx` files. Add new user-facing text to both the default and translated resources.

## Display scaling

The app opts into Per-Monitor V2 DPI awareness. Fence contents, resize hit areas, and image thumbnails scale with monitor DPI; custom-rendered text uses grid-fitted anti-aliasing. Advanced settings also provide a persisted 50–200% content scale, applied on top of monitor DPI to thumbnail icons, labels, and item spacing. Existing saved fence dimensions remain unchanged until the user invokes **Modify all**, which fits each fence to the scaled layout.

The optional sign-in launch uses a single shortcut in the current user's Windows Startup folder. Disabling it deletes that shortcut; the application does not create a Run registry entry, scheduled task, service, or separate startup preference.

## UI appearance

The interface uses Fluent Design as a visual baseline without adding a UI-framework dependency. `窗格\FluentTheme.cs` centralizes the settings and dialog palette; buttons use stable colors without hover/pressed fills. The app follows the Windows app light/dark preference and DWM accent color by default; users can disable this and choose a display mode and accent color in Advanced settings. Separate light/dark overlay opacity sliders default to the existing background alpha values. Advanced settings also provide a glow-effects switch, custom pointer highlight color, and separate intensity/size controls for pointer and icon glows. The reusable settings window opens modelessly so fences remain interactive, and sizes/centers itself within the active monitor's work area. Fence windows remain custom-drawn and translucent, with softly blurred text shadows, icon-colored glows derived from each icon's blurred pixels, a customizable cursor-following glass highlight, and persistent selection/drag feedback scaled from logical units for per-monitor DPI.

## Fence data and compatibility

The application continues to use `%LOCALAPPDATA%\NoFences` so existing layouts and contents remain available. It reads the original XML metadata format, JSON records in `Fences\<id>\fence.json`, and XML metadata in `Backup_XML_*` directories. Alternate-format sources are read without being changed. Removing a fence deletes its saved metadata in the XML and JSON locations and matching recovery-backup records.

Startup warnings for unreadable fence data are suppressed while the same set of paths remains skipped. The warning is shown again if the paths change, and its suppression state is cleared after a clean load. To inspect the files, open Advanced Settings and use the fence data folder. Successfully loading JSON or backup data does not trigger a recurring startup dialog.

## License and attribution

This repository is a personal fork of [Twometer's NoFences](https://github.com/Twometer/NoFences). The project is distributed under the MIT License. The original copyright and license notice are retained; changes in this fork are maintained by **rurudo**. See [LICENSE](LICENSE).
