; Stop only WinZoneTrigger processes before replacing this application's binaries.
!macro NSIS_HOOK_PREINSTALL
  nsExec::ExecToLog 'taskkill /F /IM WinZoneTrigger.Engine.exe'
  Pop $0
  nsExec::ExecToLog 'taskkill /F /IM WinZoneTrigger.exe'
  Pop $0
!macroend

!macro NSIS_HOOK_POSTINSTALL
  nsExec::ExecToLog 'schtasks /Delete /TN WinZoneTrigger /F'
  Pop $0
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "WinZoneTrigger" '"$INSTDIR\WinZoneTrigger.exe" --startup --minimized'
  Delete "$SMSTARTUP\WinZoneTrigger.lnk"
  IfFileExists "$SMPROGRAMS\WinZoneTrigger\위치 자동 실행.lnk" 0 +2
    CreateShortCut "$SMPROGRAMS\WinZoneTrigger\위치 자동 실행.lnk" "$INSTDIR\WinZoneTrigger.exe"
!macroend

!macro NSIS_HOOK_PREUNINSTALL
  nsExec::ExecToLog 'taskkill /F /IM WinZoneTrigger.Engine.exe'
  Pop $0
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "WinZoneTrigger"
  nsExec::ExecToLog 'schtasks /Delete /TN WinZoneTrigger /F'
  Pop $0
!macroend
