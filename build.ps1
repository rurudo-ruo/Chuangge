$solutionPath = Join-Path $PSScriptRoot '窗格.sln'
$exePath = Join-Path $PSScriptRoot '窗格\bin\Release\窗格.exe'
$targetingPackPaths = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'),
    (Join-Path $env:ProgramFiles 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8')
)

$msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
$msbuildPath = if ($msbuildCommand) { $msbuildCommand.Source }

if (-not $msbuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

    if (Test-Path $vswhere) {
        $msbuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1
    }
}

if (-not $msbuildPath) {
    Write-Error 'MSBuild was not found. Install Visual Studio Build Tools 2022 with the MSBuild component, or add MSBuild to PATH.'
    exit 1
}

if (-not ($targetingPackPaths | Where-Object { Test-Path $_ })) {
    Write-Error 'The .NET Framework 4.8 targeting pack was not found. Install the ".NET Framework 4.8 targeting pack" component using Visual Studio Installer, or install the .NET Framework 4.8 Developer Pack from https://aka.ms/msbuild/developerpacks.'
    exit 1
}

$normalizedExePath = [System.IO.Path]::GetFullPath($exePath)
$runningProcesses = @(Get-CimInstance Win32_Process -Filter "Name = '窗格.exe'" -ErrorAction Stop)
$matchingProcesses = @()
foreach ($process in $runningProcesses) {
    if ([string]::IsNullOrWhiteSpace($process.ExecutablePath)) {
        Write-Error "Could not verify the executable path for process $($process.ProcessId). Close it manually before building."
        exit 1
    }

    if ([string]::Equals([System.IO.Path]::GetFullPath($process.ExecutablePath),
            $normalizedExePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        $matchingProcesses += $process
    }
}

if ($matchingProcesses.Count -gt 0) {
    if (-not ('NoFencesBuildShutdown' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class NoFencesBuildShutdown
{
    private const uint WM_CLOSE = 0x0010;
    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    public static int RequestClose(int targetProcessId)
    {
        var closeRequests = 0;
        EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            uint processId;
            GetWindowThreadProcessId(window, out processId);
            if (processId == (uint)targetProcessId && IsWindowVisible(window)
                && PostMessage(window, WM_CLOSE, IntPtr.Zero, IntPtr.Zero))
                closeRequests++;

            return true;
        }, IntPtr.Zero);
        return closeRequests;
    }
}
'@
    }

    foreach ($process in $matchingProcesses) {
        $closeRequests = [NoFencesBuildShutdown]::RequestClose($process.ProcessId)
        if ($closeRequests -eq 0) {
            Write-Error "Could not request a graceful close for NoFences process $($process.ProcessId). Close it manually before building."
            exit 1
        }
        Write-Host "Requested a normal close for NoFences (process $($process.ProcessId)); waiting for its fence data to save."
    }

    $shutdownDeadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $remainingProcesses = @(Get-CimInstance Win32_Process -Filter "Name = '窗格.exe'" -ErrorAction Stop |
            Where-Object {
                [string]::Equals([System.IO.Path]::GetFullPath($_.ExecutablePath),
                    $normalizedExePath, [System.StringComparison]::OrdinalIgnoreCase)
            })
    } while ($remainingProcesses.Count -gt 0 -and [DateTime]::UtcNow -lt $shutdownDeadline)

    if ($remainingProcesses.Count -gt 0) {
        Write-Error 'NoFences did not close within 20 seconds. It may have blocked the close to protect unsaved data. The build was stopped; close the app manually and try again.'
        exit 1
    }
}

& $msbuildPath $solutionPath /p:Configuration=Release '/p:Platform=Any CPU'

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path $exePath)) {
    Write-Error "Build reported success, but the expected executable was not found: $exePath"
    exit 1
}

Write-Host "Build succeeded: $exePath"
