$solutionPath = Join-Path $PSScriptRoot 'NoFences.sln'
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

& $msbuildPath $solutionPath /p:Configuration=Release '/p:Platform=Any CPU'

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exePath = Join-Path $PSScriptRoot 'NoFences\bin\Release\NoFences.exe'

if (-not (Test-Path $exePath)) {
    Write-Error "Build reported success, but the expected executable was not found: $exePath"
    exit 1
}

Write-Host "Build succeeded: $exePath"
