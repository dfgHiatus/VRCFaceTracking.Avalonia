;--------------------------------
; Includes

  !include "MUI2.nsh"
  !include "logiclib.nsh"
  
;--------------------------------
; Custom defines
  !define NAME "VRCFaceTracking.Avalonia"
  !define APPFILE "VRCFaceTracking.Avalonia.Desktop.exe"
  !define VERSION "1.0.0.0"
  !define SLUG "${NAME} v${VERSION}"
  
;--------------------------------
; General

  Name "${NAME}"
  OutFile "${NAME} Setup.exe"
  ; Default install directory in user's AppData folder
  InstallDir "$LOCALAPPDATA\\${NAME}"
  InstallDirRegKey HKCU "Software\${NAME}" ""
  RequestExecutionLevel admin
  
;--------------------------------
; UI
  
  !define MUI_ICON "logo_32.ico"
  !define MUI_HEADERIMAGE
  ;!define MUI_WELCOMEFINISHPAGE_BITMAP "assets\MUI_WELCOMEFINISHPAGE_BITMAP.bmp"
  ;!define MUI_HEADERIMAGE_BITMAP "assets\MUI_HEADERIMAGE_BITMAP.bmp"
  !define MUI_ABORTWARNING
  !define MUI_WELCOMEPAGE_TITLE "${SLUG} Setup"
  
;--------------------------------
; Pages
  
  ; Installer pages
  !insertmacro MUI_PAGE_WELCOME
  !insertmacro MUI_PAGE_LICENSE "license.txt"
  !insertmacro MUI_PAGE_COMPONENTS
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES
  !insertmacro MUI_PAGE_FINISH

  ; Uninstaller pages
  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES
  
  ; Set UI language
  !insertmacro MUI_LANGUAGE "English"
  
;--------------------------------
; Section - Install App

  Section "-hidden app"
    SectionIn RO
    SetOutPath "$INSTDIR"
    File /r "net8.0-windows10.0.17763.0\*.*"
    WriteRegStr HKCU "Software\${NAME}" "" $INSTDIR
    WriteUninstaller "$INSTDIR\Uninstall.exe"
  SectionEnd
  
;--------------------------------
; Section - Shortcut

  Section "Desktop Shortcut" DeskShort
    CreateShortCut "$DESKTOP\${NAME}.lnk" "$INSTDIR\${APPFILE}"
  SectionEnd
  
;--------------------------------
; Descriptions

  ;Language strings
  LangString DESC_DeskShort ${LANG_ENGLISH} "Create Shortcut on Dekstop."

  ;Assign language strings to sections
  !insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${DeskShort} $(DESC_DeskShort)
  !insertmacro MUI_FUNCTION_DESCRIPTION_END
  
;--------------------------------
; Remove empty parent directories

  Function un.RMDirUP
    !define RMDirUP '!insertmacro RMDirUPCall'

    !macro RMDirUPCall _PATH
          push '${_PATH}'
          Call un.RMDirUP
    !macroend

    ; $0 - current folder
    ClearErrors

    Exch $0
    ;DetailPrint "ASDF - $0\.."
    RMDir "$0\.."

    IfErrors Skip
    ${RMDirUP} "$0\.."
    Skip:

    Pop $0

  FunctionEnd
  
;--------------------------------
; Section - Uninstaller

Section "Uninstall"

  ;Delete Shortcut
  Delete "$DESKTOP\${NAME}.lnk"

  ;Delete Uninstall
  Delete "$INSTDIR\Uninstall.exe"

  ;Delete Folder
  RMDir /r "$INSTDIR"
  ${RMDirUP} "$INSTDIR"

  DeleteRegKey /ifempty HKCU "Software\${NAME}"

SectionEnd