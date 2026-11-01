<#
.SYNOPSIS
Builds the Release version, saves a build log and summary, and reports the result.

.DESCRIPTION
Streams MSBuild output to the console and to build-logs\build-<timestamp>.log, writes
build-logs\last-build.txt with a machine-readable summary, and shows a message box with the
outcome. After a failure the message box offers to open the log.

.PARAMETER Run
Starts the built executable after a successful build.

.PARAMETER NoPopup
Suppresses the result message box. It is also suppressed when the CI environment variable is set.

.EXAMPLE
.\build.ps1

.EXAMPLE
.\build.ps1 -Run

.EXAMPLE
.\build.ps1 -NoPopup
#>
param(
    [switch]$Run,
    [switch]$NoPopup
)

$appName = [string][char]0x7A97 + [string][char]0x683C
$solutionPath = Join-Path $PSScriptRoot "$appName.sln"
$exePath = Join-Path $PSScriptRoot "$appName\bin\Release\$appName.exe"
$logDirectory = Join-Path $PSScriptRoot 'build-logs'
$logPath = Join-Path $logDirectory ('build-{0:yyyyMMdd-HHmmss}.log' -f (Get-Date))
$summaryPath = Join-Path $logDirectory 'last-build.txt'
$buildStarted = Get-Date
$script:logReady = $false

function Write-Log {
    param(
        [string]$Message,
        [string]$Color = 'Gray'
    )

    Write-Host $Message -ForegroundColor $Color
    if ($script:logReady) {
        Add-Content -LiteralPath $logPath -Value $Message -Encoding UTF8
    }
}

function Show-BuildPopup {
    param(
        [string]$Message,
        [string]$Title,
        [bool]$IsError
    )

    if ($NoPopup -or $env:CI) {
        return
    }

    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        if ($IsError -and $script:logReady) {
            $result = [System.Windows.Forms.MessageBox]::Show(
                $Message + [Environment]::NewLine + [Environment]::NewLine + 'Open the build log?',
                $Title,
                [System.Windows.Forms.MessageBoxButtons]::YesNo,
                [System.Windows.Forms.MessageBoxIcon]::Error)
            if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
                Start-Process -FilePath 'notepad.exe' -ArgumentList ('"' + $logPath + '"')
            }
        }
        else {
            [void][System.Windows.Forms.MessageBox]::Show(
                $Message,
                $Title,
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Information)
        }
    }
    catch {
        # No UI session (or no WinForms): the console output and the log still carry the result.
    }
}

function Complete-Build {
    param(
        [string]$Status,
        [string]$Reason,
        [int]$ExitCode
    )

    $finished = Get-Date
    $duration = $finished - $buildStarted
    $durationText = '{0:hh\:mm\:ss}' -f $duration
    $errors = 0
    $warnings = 0

    if ($script:logReady -and (Test-Path -LiteralPath $logPath)) {
        try {
            $lines = Get-Content -LiteralPath $logPath -ErrorAction Stop
            $errors = @($lines | Select-String -Pattern ': error ' -SimpleMatch).Count
            $warnings = @($lines | Select-String -Pattern ': warning ' -SimpleMatch).Count
        }
        catch {
            # Counting is best effort; it must not change the build result.
        }
    }

    if ($script:logReady) {
        Add-Content -LiteralPath $logPath -Value '' -Encoding UTF8
        Add-Content -LiteralPath $logPath -Value ("=== {0} in {1} (errors: {2}, warnings: {3}) ===" -f $Status, $durationText, $errors, $warnings) -Encoding UTF8
        if ($Reason) {
            Add-Content -LiteralPath $logPath -Value "Reason: $Reason" -Encoding UTF8
        }

        $summary = @(
            "status=$Status"
            "started=$($buildStarted.ToString('o'))"
            "finished=$($finished.ToString('o'))"
            "duration=$durationText"
            "configuration=Release"
            "platform=Any CPU"
            "solution=$solutionPath"
            "executable=$exePath"
            "errors=$errors"
            "warnings=$warnings"
            "log=$logPath"
        )
        if ($Reason) {
            $summary += "reason=$Reason"
        }

        try {
            Set-Content -LiteralPath $summaryPath -Value ($summary -join [Environment]::NewLine) -Encoding UTF8
        }
        catch {
            Write-Host "Could not write the build summary: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    $logSection = ''
    if ($script:logReady) {
        $logSection = [Environment]::NewLine + [Environment]::NewLine + 'Log:' +
            [Environment]::NewLine + $logPath
    }

    if ($Status -eq 'Success') {
        $message = "Build succeeded in $durationText." +
            [Environment]::NewLine + [Environment]::NewLine + 'Executable:' +
            [Environment]::NewLine + $exePath + $logSection
        Show-BuildPopup -Message $message -Title "$appName build succeeded" -IsError $false
    }
    else {
        $message = "Build failed after $durationText." +
            [Environment]::NewLine + [Environment]::NewLine + "Reason: $Reason" + $logSection
        Show-BuildPopup -Message $message -Title "$appName build failed" -IsError $true
    }

    exit $ExitCode
}

try {
    if (-not (Test-Path -LiteralPath $logDirectory)) {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    }

    $script:logReady = $true

    # Housekeeping: keep the 50 most recent logs so the folder cannot grow without bound.
    try {
        Get-ChildItem -LiteralPath $logDirectory -Filter 'build-*.log' -File |
            Sort-Object LastWriteTime -Descending |
            Select-Object -Skip 50 |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
    catch {
        # Pruning is housekeeping only; never fail a build over it.
    }

    Add-Content -LiteralPath $logPath -Value ("=== {0} build started {1:yyyy-MM-dd HH:mm:ss} ===" -f $appName, $buildStarted) -Encoding UTF8
    Add-Content -LiteralPath $logPath -Value ("PowerShell: {0}" -f $PSVersionTable.PSVersion) -Encoding UTF8
    Add-Content -LiteralPath $logPath -Value ("Command line: {0}" -f $MyInvocation.Line) -Encoding UTF8
}
catch {
    Write-Host "Could not create the build log directory: $($_.Exception.Message)" -ForegroundColor Yellow
}

$targetingPackPaths = @()
if (${env:ProgramFiles(x86)}) {
    $targetingPackPaths += Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
}
if ($env:ProgramFiles) {
    $targetingPackPaths += Join-Path $env:ProgramFiles 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
}

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
    Complete-Build -Status 'Failed' -ExitCode 1 -Reason 'MSBuild was not found. Install Visual Studio Build Tools 2022 with the MSBuild component, or add MSBuild to PATH.'
}

if (-not ($targetingPackPaths | Where-Object { Test-Path $_ })) {
    Complete-Build -Status 'Failed' -ExitCode 1 -Reason 'The .NET Framework 4.8 targeting pack was not found. Install the ".NET Framework 4.8 targeting pack" component using Visual Studio Installer, or install the .NET Framework 4.8 Developer Pack from https://aka.ms/msbuild/developerpacks.'
}

Write-Log ("Using MSBuild: {0}" -f $msbuildPath)
Write-Log ("Build log: {0}" -f $logPath)

$normalizedExePath = [System.IO.Path]::GetFullPath($exePath)

try {
    $runningProcesses = @(Get-CimInstance Win32_Process -Filter "Name = '$appName.exe'" -ErrorAction Stop)
}
catch {
    Complete-Build -Status 'Failed' -ExitCode 1 -Reason "Could not enumerate running processes: $($_.Exception.Message)"
}

$matchingProcesses = @()
foreach ($process in $runningProcesses) {
    if ([string]::IsNullOrWhiteSpace($process.ExecutablePath)) {
        Complete-Build -Status 'Failed' -ExitCode 1 -Reason "Could not verify the executable path for process $($process.ProcessId). Close it manually before building."
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
            Complete-Build -Status 'Failed' -ExitCode 1 -Reason "Could not request a graceful close for NoFences process $($process.ProcessId). Close it manually before building."
        }
        Write-Log "Requested a normal close for NoFences (process $($process.ProcessId)); waiting for its fence data to save."
    }

    $shutdownDeadline = [DateTime]::UtcNow.AddSeconds(20)
    $remainingProcesses = @()
    do {
        Start-Sleep -Milliseconds 250
        try {
            $remainingProcesses = @(Get-CimInstance Win32_Process -Filter "Name = '$appName.exe'" -ErrorAction Stop |
                Where-Object {
                    (-not [string]::IsNullOrWhiteSpace($_.ExecutablePath)) -and
                    ([string]::Equals([System.IO.Path]::GetFullPath($_.ExecutablePath),
                        $normalizedExePath, [System.StringComparison]::OrdinalIgnoreCase))
                })
        }
        catch {
            Complete-Build -Status 'Failed' -ExitCode 1 -Reason "Could not check whether NoFences closed: $($_.Exception.Message)"
        }
    } while ($remainingProcesses.Count -gt 0 -and [DateTime]::UtcNow -lt $shutdownDeadline)

    if ($remainingProcesses.Count -gt 0) {
        Complete-Build -Status 'Failed' -ExitCode 1 -Reason 'NoFences did not close within 20 seconds. It may have blocked the close to protect unsaved data. The build was stopped; close the app manually and try again.'
    }
}

Write-Log ''
Write-Log ("Building {0} (Release|Any CPU)" -f $solutionPath) 'Cyan'

& $msbuildPath $solutionPath /p:Configuration=Release '/p:Platform=Any CPU' /v:m /nologo 2>&1 | ForEach-Object {
    $line = [string]$_
    Write-Host $line
    if ($script:logReady) {
        Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
    }
}

$buildExitCode = $LASTEXITCODE
if ($buildExitCode -ne 0) {
    Complete-Build -Status 'Failed' -ExitCode $buildExitCode -Reason "MSBuild exited with code $buildExitCode."
}

if (-not (Test-Path -LiteralPath $exePath)) {
    Complete-Build -Status 'Failed' -ExitCode 1 -Reason "Build reported success, but the expected executable was not found: $exePath"
}

if ($Run) {
    try {
        Start-Process -FilePath $exePath -ErrorAction Stop
        Write-Log "Started $appName for manual testing." 'Green'
    }
    catch {
        Write-Log "Could not start the executable: $($_.Exception.Message)" 'Yellow'
    }
}

Complete-Build -Status 'Success' -ExitCode 0 -Reason ''
