param(
    [switch] $SkipAppBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$bin = Join-Path $root 'bin'
$dist = Join-Path $root 'dist'
$appExe = Join-Path $bin 'WinZoneTrigger.exe'
$installerSource = Join-Path $root 'tools\Installer.cs'
$installerExe = Join-Path $dist 'WinZoneTrigger_Setup.exe'
$readme = Join-Path $root 'README.md'
$icon = Join-Path $root 'assets\app.ico'

if (-not $SkipAppBuild) {
    & (Join-Path $root 'build.ps1')
}

if (-not (Test-Path $appExe)) {
    throw "App executable not found: $appExe"
}

if (-not (Test-Path $installerSource)) {
    throw "Installer source not found: $installerSource"
}

if (-not (Test-Path $icon)) {
    & (Join-Path $root 'tools\create-app-icon.ps1')
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)

$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw 'Could not find the .NET Framework C# compiler. Install .NET Framework 4.x or build on a standard Windows installation.'
}

$resourceArgs = @(
    "/resource:$appExe,WinZoneTrigger.exe"
)

if (Test-Path $readme) {
    $resourceArgs += "/resource:$readme,README.md"
}

& $csc `
    /nologo `
    /target:winexe `
    /optimize+ `
    /out:$installerExe `
    /win32icon:$icon `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $resourceArgs `
    $installerSource

if ($LASTEXITCODE -ne 0) {
    throw "Installer compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built installer $installerExe"
