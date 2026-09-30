; Legacy migration is deliberately limited to the application's historical path/command.
!ifndef WINZONE_LEGACY_DIR
!define WINZONE_LEGACY_DIR "$LOCALAPPDATA\Programs\WinZoneTrigger"
!endif
Var WinZoneLegacyMigration

!macro WINZONE_READ_INSTALL_LOCATION output
  Push $R9
  ReadRegStr ${output} SHCTX "${MANUPRODUCTKEY}" ""
  ${If} ${output} == ""
    ReadRegStr ${output} SHCTX "${UNINSTKEY}" "InstallLocation"
  ${EndIf}
  StrCpy $R9 ${output} 1
  ${If} $R9 == '$\"'
    StrCpy ${output} ${output} -1 1
  ${EndIf}
  Pop $R9
  ClearErrors
!macroend

!macro WINZONE_DETECT_LEGACY
  Push $R8
  Push $R9
  StrCpy $WinZoneLegacyMigration 0
  ReadRegStr $R8 SHCTX "${UNINSTKEY}" "InstallLocation"
  ReadRegStr $R9 SHCTX "${UNINSTKEY}" "UninstallString"
  ${If} $R8 == "${WINZONE_LEGACY_DIR}"
  ${AndIf} $R9 == '$\"${WINZONE_LEGACY_DIR}\WinZoneTrigger_Uninstall.exe$\" /uninstall'
    StrCpy $WinZoneLegacyMigration 1
  ${EndIf}
  Pop $R9
  Pop $R8
  ClearErrors
!macroend

!macro WINZONE_EXEC_UNINSTALL command result
  ; Optional registry reads may fail even when the remover exits successfully.
  ; Clear only those old errors immediately before launch; preserve real launch failures.
  ClearErrors
  ExecWait '${command}' ${result}
!macroend

!macro WINZONE_REINSTALL_PAGE
Function PageLeaveReinstall
  ; The legacy C# remover schedules a delayed recursive removal of its own folder.
  ; Upgrade its known installation in place so it cannot delete the new app later.
  !insertmacro WINZONE_DETECT_LEGACY
  ${If} $WinZoneLegacyMigration = 1
    Return
  ${EndIf}
  ${NSD_GetState} $R2 $R1

  ; If migrating from Wix, always uninstall
  ${If} $WixMode = 1
    Goto reinst_uninstall
  ${EndIf}

  ; In update mode, always proceeds without uninstalling
  ${If} $UpdateMode = 1
    Goto reinst_done
  ${EndIf}

  ; $R0 holds whether same(0)/upgrading(1)/downgrading(-1) version
  ; $R1 holds the radio buttons state:
  ;   1 => first choice was selected
  ;   0 => second choice was selected
  ${If} $R0 = 0 ; Same version, proceed
    ${If} $R1 = 1              ; User chose to add/reinstall
      Goto reinst_done
    ${Else}                    ; User chose to uninstall
      Goto reinst_uninstall
    ${EndIf}
  ${ElseIf} $R0 = 1 ; Upgrading
    ${If} $R1 = 1              ; User chose to uninstall
      Goto reinst_uninstall
    ${Else}
      Goto reinst_done         ; User chose NOT to uninstall
    ${EndIf}
  ${ElseIf} $R0 = -1 ; Downgrading
    ${If} $R1 = 1              ; User chose to uninstall
      Goto reinst_uninstall
    ${Else}
      Goto reinst_done         ; User chose NOT to uninstall
    ${EndIf}
  ${EndIf}

  reinst_uninstall:
    HideWindow
    ClearErrors

    ${If} $WixMode = 1
      ReadRegStr $R1 HKLM "$R6" "UninstallString"
      !insertmacro WINZONE_EXEC_UNINSTALL "$R1" $0
    ${Else}
      !insertmacro WINZONE_READ_INSTALL_LOCATION $4
      ReadRegStr $R1 SHCTX "${UNINSTKEY}" "UninstallString"
      ${IfThen} $UpdateMode = 1 ${|} StrCpy $R1 "$R1 /UPDATE" ${|} ; append /UPDATE
      ${IfThen} $PassiveMode = 1 ${|} StrCpy $R1 "$R1 /P" ${|} ; append /P
      StrCpy $R1 "$R1 _?=$4" ; append uninstall directory
      !insertmacro WINZONE_EXEC_UNINSTALL "$R1" $0
    ${EndIf}

    BringToFront

    ${IfThen} ${Errors} ${|} StrCpy $0 2 ${|} ; ExecWait failed, set fake exit code

    ${If} $0 <> 0
    ${OrIf} ${FileExists} "$INSTDIR\${MAINBINARYNAME}.exe"
      ; User cancelled wix uninstaller? return to select un/reinstall page
      ${If} $WixMode = 1
      ${AndIf} $0 = 1602
        Abort
      ${EndIf}

      ; User cancelled NSIS uninstaller? return to select un/reinstall page
      ${If} $0 = 1
        Abort
      ${EndIf}

      ; Other errors? show generic error message and return to select un/reinstall page
      MessageBox MB_ICONEXCLAMATION "$(unableToUninstall)"
      Abort
    ${EndIf}
  reinst_done:
FunctionEnd
!macroend
