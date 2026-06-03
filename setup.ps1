param(
    [switch] $SkipBuild,
    [switch] $NoStartup
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'build.ps1'
$sourceExe = Join-Path $root 'bin\WinZoneTrigger.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\WinZoneTrigger'
$installExe = Join-Path $installDir 'WinZoneTrigger.exe'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\WinZoneTrigger'
$shortcutPath = Join-Path $startMenuDir '위치 자동 실행.lnk'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValueName = 'WinZoneTrigger'

if (-not $SkipBuild) {
    & $buildScript
}

if (-not (Test-Path $sourceExe)) {
    throw "Executable not found: $sourceExe"
}

Get-Process WinZoneTrigger -ErrorAction SilentlyContinue | Stop-Process -Force

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installExe -Force

if (Test-Path (Join-Path $root 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $installDir 'README.md') -Force
}

New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installExe
$shortcut.Arguments = ''
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$installExe,0"
$shortcut.Description = 'WinZoneTrigger 위치 자동 실행'
$shortcut.Save()

$oldStartupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'WinZoneTrigger.lnk'
if (Test-Path $oldStartupShortcut) {
    Remove-Item -LiteralPath $oldStartupShortcut -Force
}

if (-not $NoStartup) {
    New-Item -Path $runKeyPath -Force | Out-Null
    Set-ItemProperty -Path $runKeyPath -Name $runValueName -Value ('"' + $installExe + '" --startup --minimized')
}

Write-Host "Installed to $installDir"
Write-Host "Start Menu shortcut: $shortcutPath"
if (-not $NoStartup) {
    Write-Host "Startup command registered for current user."
}
