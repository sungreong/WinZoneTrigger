$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$out = Join-Path $root 'bin'

New-Item -ItemType Directory -Force -Path $out | Out-Null

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)

$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw 'Could not find the .NET Framework C# compiler. Install .NET Framework 4.x or build on a standard Windows installation.'
}

$sources = Get-ChildItem -Path $src -Filter '*.cs' | ForEach-Object { $_.FullName }
$exe = Join-Path $out 'WinZoneTrigger.exe'
$icon = Join-Path $root 'assets\app.ico'

if (-not (Test-Path $icon)) {
    & (Join-Path $root 'tools\create-app-icon.ps1')
}

& $csc `
    /nologo `
    /target:winexe `
    /optimize+ `
    /out:$exe `
    /win32icon:$icon `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Management.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Windows.Forms.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built $exe"
