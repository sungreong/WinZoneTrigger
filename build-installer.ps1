param([switch] $SkipAppBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    if (-not $SkipAppBuild) { & (Join-Path $root 'build.ps1') }
    if (-not (Test-Path 'node_modules/@tauri-apps/cli')) {
        & npm.cmd ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    }
    & npm.cmd run tauri -- build --bundles nsis
    if ($LASTEXITCODE -ne 0) { throw 'Tauri build failed.' }
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'dist') | Out-Null
    $version = (Get-Content 'src-tauri/tauri.conf.json' -Raw | ConvertFrom-Json).version
    $setup = Join-Path $root "src-tauri/target/release/bundle/nsis/WinZoneTrigger_${version}_x64-setup.exe"
    if (-not (Test-Path $setup)) { throw "Tauri installer missing: $setup" }
    Copy-Item -LiteralPath 'src-tauri/target/release/WinZoneTrigger.exe' -Destination 'bin/WinZoneTrigger.exe' -Force
    Copy-Item -LiteralPath $setup -Destination 'dist/WinZoneTrigger_Setup.exe' -Force
    Write-Host "Built Tauri app and installer: dist\WinZoneTrigger_Setup.exe"
} finally { Pop-Location }
