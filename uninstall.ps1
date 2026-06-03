$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\WinZoneTrigger'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\WinZoneTrigger'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValueName = 'WinZoneTrigger'
$oldStartupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'WinZoneTrigger.lnk'

Get-Process WinZoneTrigger -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $runKeyPath) {
    Remove-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue
}

if (Test-Path $oldStartupShortcut) {
    Remove-Item -LiteralPath $oldStartupShortcut -Force
}

if (Test-Path $startMenuDir) {
    Remove-Item -LiteralPath $startMenuDir -Recurse -Force
}

if (Test-Path $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

Write-Host 'WinZoneTrigger uninstalled for current user.'
