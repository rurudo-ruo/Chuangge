# NoFences

Didn't want to pay 11€, made my own.

![Screenshot](screenshot.png "NoFences in action")

## Build

NoFences is a 64-bit Windows Forms application targeting **.NET Framework 4.8**.

### Prerequisites

- Windows
- Visual Studio 2022 Build Tools (or Visual Studio) with the **MSBuild** component
- The **.NET Framework 4.8 targeting pack**. In Visual Studio Installer, select it under **Individual components**. Alternatively, install the [.NET Framework 4.8 Developer Pack](https://aka.ms/msbuild/developerpacks).

### Build the executable

From the repository folder, run in PowerShell:

```powershell
.\build.ps1
```

The script checks for MSBuild and the .NET Framework 4.8 targeting pack before building. If PowerShell blocks running the script, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The Release executable is created at `NoFences\bin\Release\NoFences.exe`.

### Run

The target computer needs the **.NET Framework 4.8 runtime**. It is separate from the targeting pack used to build the application; install the runtime if it is not already available on the computer.