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
$taskName = 'WinZoneTrigger'

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
    if (Test-Path $runKeyPath) {
        Remove-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue
    }

    $taskXml = Join-Path $env:TEMP ('WinZoneTrigger.task.' + [Guid]::NewGuid().ToString('N') + '.xml')
    $userId = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $escapedUser = [System.Security.SecurityElement]::Escape($userId)
    $escapedExe = [System.Security.SecurityElement]::Escape($installExe)
    $escapedDir = [System.Security.SecurityElement]::Escape($installDir)
    $xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo><Author>$escapedUser</Author></RegistrationInfo>
  <Triggers><LogonTrigger><Enabled>true</Enabled><Delay>PT30S</Delay></LogonTrigger></Triggers>
  <Principals><Principal id="Author"><UserId>$escapedUser</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
  <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority><RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings>
  <Actions Context="Author"><Exec><Command>$escapedExe</Command><Arguments>--startup --minimized</Arguments><WorkingDirectory>$escapedDir</WorkingDirectory></Exec></Actions>
</Task>
"@
    try {
        Set-Content -LiteralPath $taskXml -Value $xml -Encoding Unicode
        & schtasks.exe /Delete /F /TN $taskName 2>$null | Out-Null
        & schtasks.exe /Create /F /TN $taskName /XML $taskXml | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "schtasks.exe exited with $LASTEXITCODE"
        }
    } catch {
        New-Item -Path $runKeyPath -Force | Out-Null
        Set-ItemProperty -Path $runKeyPath -Name $runValueName -Value ('"' + $installExe + '" --startup --minimized')
        Write-Warning "Scheduled task registration failed; Run registry fallback was registered. $($_.Exception.Message)"
    } finally {
        if (Test-Path $taskXml) {
            Remove-Item -LiteralPath $taskXml -Force
        }
    }
} else {
    & schtasks.exe /Delete /F /TN $taskName 2>$null | Out-Null
    if (Test-Path $runKeyPath) {
        Remove-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue
    }
}

Write-Host "Installed to $installDir"
Write-Host "Start Menu shortcut: $shortcutPath"
if (-not $NoStartup) {
    Write-Host "Startup command registered for current user."
}
