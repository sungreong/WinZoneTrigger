"""Execute NSIS migration macros with disposable registry keys; never touch installation data."""
import os
from pathlib import Path
import subprocess
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[1]
NSIS = Path(os.environ['LOCALAPPDATA']) / 'tauri' / 'NSIS' / 'makensis.exe'
key = 'Software\\WinZoneTriggerInstallerTests\\' + uuid.uuid4().hex
with tempfile.TemporaryDirectory(prefix='winzone-installer-test-') as folder:
    root = Path(folder)
    script = r'''
Unicode true
!include LogicLib.nsh
!define MANUPRODUCTKEY "@@KEY@@\Vendor"
!define UNINSTKEY "@@KEY@@\Uninstall"
!define WINZONE_LEGACY_DIR "$EXEDIR\legacy"
!include "@@INCLUDE@@"
Name "WinZone installer regression"
OutFile "test.exe"
RequestExecutionLevel user
SilentInstall silent
!macro Verify expression message
  ${If} ${expression}
  ${Else}
    FileWrite $9 "FAIL: ${message}$\r$\n"
    StrCpy $8 1
  ${EndIf}
!macroend
Section
  SetShellVarContext current
  StrCpy $8 0
  FileOpen $9 "$EXEDIR\result.txt" w
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "${WINZONE_LEGACY_DIR}"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '$\"${WINZONE_LEGACY_DIR}\WinZoneTrigger_Uninstall.exe$\" /uninstall'
  !insertmacro WINZONE_READ_INSTALL_LOCATION $4
  !insertmacro Verify '$4 == "${WINZONE_LEGACY_DIR}"' "legacy InstallLocation fallback"
  !insertmacro WINZONE_DETECT_LEGACY
  !insertmacro Verify '$WinZoneLegacyMigration == 1' "known legacy recognized for in-place migration"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$EXEDIR\unrelated.exe" /uninstall'
  !insertmacro WINZONE_DETECT_LEGACY
  !insertmacro Verify '$WinZoneLegacyMigration == 0' "unrelated remover not bypassed"
  WriteRegStr HKCU "${MANUPRODUCTKEY}" "" '$\"$EXEDIR\new app$\"'
  !insertmacro WINZONE_READ_INSTALL_LOCATION $4
  !insertmacro Verify '$4 == "$EXEDIR\new app"' "quoted NSIS path normalized"
  ClearErrors
  ReadRegStr $4 HKCU "@@KEY@@\Missing" ""
  StrCpy $R1 '$\"$SYSDIR\cmd.exe$\" /c exit 0'
  !insertmacro WINZONE_EXEC_UNINSTALL "$R1" $0
  !insertmacro Verify '$0 == 0' "successful exit code"
  ${If} ${Errors}
    FileWrite $9 "FAIL: stale registry error leaked$\r$\n"
    StrCpy $8 1
  ${EndIf}
  StrCpy $R1 '$\"$SYSDIR\cmd.exe$\" /c exit 7'
  !insertmacro WINZONE_EXEC_UNINSTALL "$R1" $0
  !insertmacro Verify '$0 == 7' "real failure code retained"
  StrCpy $R1 '$\"$EXEDIR\missing.exe$\"'
  !insertmacro WINZONE_EXEC_UNINSTALL "$R1" $0
  ${IfNot} ${Errors}
    FileWrite $9 "FAIL: real launch failure hidden$\r$\n"
    StrCpy $8 1
  ${EndIf}
  DeleteRegKey HKCU "@@KEY@@"
  ${If} $8 == 0
    FileWrite $9 "PASS: legacy migration, quoted paths, stale error, real exit and launch errors$\r$\n"
  ${EndIf}
  FileClose $9
  SetErrorLevel $8
SectionEnd
'''.replace('@@KEY@@', key).replace('@@INCLUDE@@', str(ROOT / 'src-tauri' / 'installer-upgrade.nsh'))
    (root / 'test.nsi').write_text(script, encoding='utf-8-sig')
    subprocess.run([str(NSIS), '/V2', str(root / 'test.nsi')], check=True, timeout=60)
    result = subprocess.run([str(root / 'test.exe')], timeout=30)
    report = (root / 'result.txt').read_text(encoding='utf-8-sig')
    print(report)
    assert result.returncode == 0 and report.startswith('PASS:'), report
