; Script for å installere MOTRd på maskinen
; Krav er .NET og at du er admin
; Den spør om du vil installere som service eller ei

; HM NIS Edit Wizard helper defines
!define PRODUCT_NAME "Movies On The Run (MOTR)"
!define PRODUCT_VERSION "1.9beta"
!define PRODUCT_PUBLISHER "Lars Werner"
!define PRODUCT_WEB_SITE "http://lars.werner.no/motrd/"
!define PRODUCT_DIR_REGKEY "Software\MOTR\InstallerPath"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"
!define PRODUCT_INSTALLDIRECTORY "$PROGRAMFILES\Movies On The Run (MOTR)"
!define PRODUCT_STARTMENU "$SMPROGRAMS\Movies On The Run (MOTR)"
!define PRODUCT_OUTFILE "MOTRInstall"
!define PRODUCT_SCRIPTS "${PRODUCT_INSTALLDIRECTORY}\Scripts"

;Midlertidig variabel brukt for å føre data mellom funksjoner
!define TEMP $R0
!define TEMP2 $R1
!define TEMP3 $R4
!define VAL1 $R7
!define VAL2 $R8

!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\orange-install.ico"
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\orange-uninstall.ico"

;UAC require admin
RequestExecutionLevel admin ;Require admin rights on NT6+ (When UAC is turned on)

; Includes 
!include "MUI2.nsh"
!include "InstallOptions.nsh"
!include "ZipDLL.nsh"
!include "DotNetChecker.nsh"
!include "Ports.nsh"

;Pages
; MUI Settings
!define MUI_ABORTWARNING

; --- REKKEFØLGE AV SIDENE ---
; Welcome page
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "InstallerLicense.txt"

; Directory page
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_UNPAGE_CONFIRM

; Instfiles page
!insertmacro MUI_PAGE_INSTFILES

Page custom ServiceInstallerPre ServiceInstallerPost ; Custom page for installation of service

Page custom RunInitalPre RunInitalPost ; Custom page for inital setup

; Finish page
!define MUI_PAGE_CUSTOMFUNCTION_PRE DisableBackButton
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Browse login page"
!define MUI_FINISHPAGE_RUN_FUNCTION "LaunchLink"
!insertmacro MUI_PAGE_FINISH


; Uninstaller pages
!insertmacro MUI_UNPAGE_INSTFILES
;---------------------------------------

; Language files
!insertmacro MUI_LANGUAGE "English"


;Setter variabler
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "${PRODUCT_OUTFILE}-${PRODUCT_VERSION}.exe"
InstallDir "${PRODUCT_INSTALLDIRECTORY}"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show

;Init section
Function .onInit
	UserInfo::GetAccountType
	pop $0
	${If} $0 != "admin" ;Require admin rights on NT4+
		MessageBox MB_ICONSTOP "Movies On The Run requires admin rights to run. Right-click and choose 'Run as administrator'"
		SetErrorLevel 740 ;ERROR_ELEVATION_REQUIRED
		Quit
	${EndIf}

	;Check which dot.net we have installed
	call CheckAndInstallDotNet

	!insertmacro MUI_LANGDLL_DISPLAY
	!insertmacro INSTALLOPTIONS_EXTRACT  "serviceinstaller.ini"
	!insertmacro INSTALLOPTIONS_EXTRACT  "initialinstallation.ini"

  ReadRegStr $R0 ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString"
  StrCmp $R0 "" done
 
  MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \
  "${PRODUCT_NAME} is already installed. $\n$\nClick `OK` to remove the \
  previous version or `Cancel` to cancel this upgrade." \
  IDCANCEL no_remove_uninstaller IDOK UninstallOld

  UninstallOld:
  ClearErrors
  ExecWait '$R0 _?=$INSTDIR' ;Do not copy the uninstaller to a temp file
 
  IfErrors no_remove_uninstaller done
    ;You can either use Delete /REBOOTOK in the uninstaller or add some code
    ;here to remove the uninstaller. Use a registry key to check
    ;whether the user has chosen to uninstall. If you are using an uninstaller
    ;components page, make sure all sections are uninstalled.
  no_remove_uninstaller:
  MessageBox MB_ICONEXCLAMATION|MB_OK "${PRODUCT_NAME} needs to be uninstalled before upgrade, you are able to keep settings, files and databases during setup"
  goto QuitNow
  
  QuitNow:
    Quit
  done:	
FunctionEnd

Section "MainSection" SEC01
  SetOutPath "$INSTDIR"

  ;Framework 4.6.1 is used on MOTRd
  !insertmacro CheckNetFramework 461

  SetOverwrite on
  File "motrd.zip"
  File "webfiles.zip"
  File "InstallerLicense.txt"
SectionEnd

Section -AdditionalIcons
	CreateDirectory $INSTDIR\Links	
	CreateDirectory "${PRODUCT_SCRIPTS}"
SectionEnd

Section -Post
  !insertmacro ZIPDLL_EXTRACT "$INSTDIR\motrd.zip" "$INSTDIR" "<ALL>"
  Delete "motrd.zip"
  !insertmacro ZIPDLL_EXTRACT "$INSTDIR\webfiles.zip" "$INSTDIR" "<ALL>"
  Delete "webfiles.zip"

  DetailPrint "Downloading tools"
  ;Download the tools for first time us
  nsExec::ExecToStack '"$INSTDIR\MOTRd.exe" -TOOLUPDATE'
  pop $0
  pop $1
  DetailPrint $1
  DetailPrint "Creating SSL certificate"
  
  ;Create certificate in the directory installed
  nsExec::ExecToStack '"$INSTDIR\MOTRd.exe" -CERT'
  pop $0
  pop $1
  DetailPrint $1
  nsExec::ExecToStack '"$INSTDIR\MOTRd.exe" -WAIT'
  
  WriteUninstaller "$INSTDIR\Uninstaller-MOTR.exe"
  WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\Uninstaller-MOTR.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayIcon" "$INSTDIR\MOTRd.exe"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
  WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  
  ;Create the start menu
  CreateDirectory "${PRODUCT_STARTMENU}"
  
  ;Creates a URL
  WriteIniStr "$INSTDIR\Links\${PRODUCT_NAME}.url" "InternetShortcut" "URL" "${PRODUCT_WEB_SITE}"

  ;Create start menu items
  CreateShortCut "${PRODUCT_STARTMENU}\Domain certification.lnk" "${PRODUCT_SCRIPTS}\domaincert.bat" "" "${PRODUCT_SCRIPTS}\domaincert.bat" 0
  CreateShortCut "${PRODUCT_STARTMENU}\Movies On The Run (console mode).lnk" 	"$INSTDIR\MOTRd.exe"  "" "$INSTDIR\MOTRd.exe" 0
  CreateShortCut "${PRODUCT_STARTMENU}\InstallerLicense.lnk" 					"$INSTDIR\InstallerLicense.txt" "" "$INSTDIR\InstallerLicense.txt" 0
  CreateShortCut "${PRODUCT_STARTMENU}\Scripts.lnk" 							"${PRODUCT_SCRIPTS}"  "" "${PRODUCT_SCRIPTS}" 0
  CreateShortCut "${PRODUCT_STARTMENU}\Visit MOTR online.lnk" 					"$INSTDIR\Links\${PRODUCT_NAME}.url" "" "$INSTDIR\link.ico" 0
  CreateShortCut "${PRODUCT_STARTMENU}\Uninstall.lnk" 							"$INSTDIR\Uninstaller-MOTR.exe" "" "$INSTDIR\Uninstaller-MOTR.exe" 0
SectionEnd


;--------------- Functions --------------------

Function openLinkNewWindow
  Push $3
  Exch
  Push $2
  Exch
  Push $1
  Exch
  Push $0
  Exch
 
  ReadRegStr $0 HKCR "http\shell\open\command" ""
# Get browser path
    DetailPrint $0
  StrCpy $2 '"'
  StrCpy $1 $0 1
  StrCmp $1 $2 +2 # if path is not enclosed in " look for space as final char
    StrCpy $2 ' '
  StrCpy $3 1
  loop:
    StrCpy $1 $0 1 $3
    DetailPrint $1
    StrCmp $1 $2 found
    StrCmp $1 "" found
    IntOp $3 $3 + 1
    Goto loop
 
  found:
    StrCpy $1 $0 $3
    StrCmp $2 " " +2
      StrCpy $1 '$1"'
 
  Pop $0
  Exec '$1 $0'
  Pop $0
  Pop $1
  Pop $2
  Pop $3
FunctionEnd
 
!macro _OpenURL URL
Push "${URL}"
Call openLinkNewWindow
!macroend
 
!define OpenURL '!insertmacro "_OpenURL"'

;Run at end of the installer
Function RunInitalPre
		;Sets parameters in system to the NSIS installer %programdata% is then correct as $APPDATA 
		SetShellVarContext all
		${If} ${FileExists} '$APPDATA\MOTRd\config\*.*'
			goto NoRunPre
		${EndIf}

         !insertmacro MUI_HEADER_TEXT "Inital setup" "Setup MOTR in the inital web setup"
         !insertmacro INSTALLOPTIONS_DISPLAY "initialinstallation.ini"
	NoRunPre:
FunctionEnd

Function RunInitalPost
	!insertmacro INSTALLOPTIONS_READ $R0 "initialinstallation.ini" "Field 2" "State"
	
	;No run
	IntCmp $R0 0 nostart
	
	;Start the browser with correct port
	 ${OpenURL} "http://localhost:$R1/" ;URL
	
	nostart:
FunctionEnd

;Disable back button
Function DisableBackButton
	GetDlgItem $R0 $HWNDPARENT 3
	EnableWindow $R0 0
FunctionEnd

;Install service
Function ServiceInstallerPre
		;Now show the service window
         !insertmacro MUI_HEADER_TEXT "Service installer" "Install MOTR as a system service or console"
         !insertmacro INSTALLOPTIONS_DISPLAY "serviceinstaller.ini"
FunctionEnd

Function ServiceInstallerPost
        !insertmacro INSTALLOPTIONS_READ $R0 "serviceinstaller.ini" "Field 2" "State"
		!insertmacro INSTALLOPTIONS_READ $R1 "serviceinstaller.ini" "Field 4" "State"
		!insertmacro INSTALLOPTIONS_READ $R2 "serviceinstaller.ini" "Field 7" "State"
		 
		;Create the bat-scripts needed
		call CreateServiceInstallerBat
		call CreateServiceUnInstallerBat
		call CreateCertDomainBat
		
		;Check if the port selected is available
		 ${If} ${TCPPortOpen} $R1
			MessageBox MB_ICONEXCLAMATION|MB_OK "http port is already open, please select another port to continue"
			abort
		${EndIf}}
		${If} $R2 > 0
			${If} ${TCPPortOpen} $R2
				MessageBox MB_ICONEXCLAMATION|MB_OK "https port is already open, please select another port to continue"
				abort
			${EndIf}}		
		${EndIf}}
		${If} $R1 > 65535
				MessageBox MB_ICONEXCLAMATION|MB_OK "http port cannot be greater than 65535, please select another port to continue"
				abort		
		${EndIf}}
		${If} $R2 > 65535
				MessageBox MB_ICONEXCLAMATION|MB_OK "https port cannot be greater than 65535, please select another port to continue"
				abort		
		${EndIf}}

		 ;Disable the back button
		GetDlgItem $0 $HWNDPARENT 3
		EnableWindow $0 0
		
		;Recreate the menu since the ports have changed (otherwise the service will update the motrd.config file with the ports)
		CreateShortCut "${PRODUCT_STARTMENU}\Movies On The Run (console mode).lnk" "$INSTDIR\MOTRd.exe"  "-http=$R1 -https=$R2" "$INSTDIR\MOTRd.exe" 0

         ;Check if we are going to install the wrapper
         IntCmp $R0 0 noinstall		
		
		;Now install the service
         ExecWait "${PRODUCT_SCRIPTS}\serviceinstaller.bat"
         return
noinstall: ;As console		
		;Run the app with port parameter
        Exec "$INSTDIR\MOTRd.exe -http=$R1 -https=$R2"
FunctionEnd

;Launched after finished page
Function LaunchLink
	ExecShell "" "$INSTDIR\Links\MOTRd in HTTP local.url"
FunctionEnd

;---------------------------------------------
; Uninstaller
Function un.onUninstSuccess
  HideWindow
  MessageBox MB_ICONINFORMATION|MB_OK "$(^Name) was successfully removed from your computer."
FunctionEnd

Function un.onInit
	UserInfo::GetAccountType
	pop $0
	${If} $0 != "admin" ;Require admin rights on NT4+
		MessageBox mb_iconstop "Movies On The Run unstallation requires admin rights to run. Right-click and choose 'Run as administrator'"
		SetErrorLevel 740 ;ERROR_ELEVATION_REQUIRED
		Quit
	${EndIf}

  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "You are about to uninstall ${PRODUCT_NAME}. Are you sure you want to ?" IDYES +2
  Abort
FunctionEnd

Section Uninstall
  ExecWait "${PRODUCT_SCRIPTS}\serviceuninstaller.bat"

  ;Remove the StartMenu directory
  RMDir /r "${PRODUCT_STARTMENU}"
  
  ;Remove the Installationdir completely
  RMDir /r /REBOOTOK "$INSTDIR"

  ;REmove the key in registry
  DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
  DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"

  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Do you want to keep the users, settings and download tools in programdata?" IDNO 0 IDYES UninstEnd
  SetShellVarContext all
  RMDir /r /REBOOTOK "$APPDATA\MOTRd"
    
  UninstEnd:
  SetAutoClose true  
SectionEnd
;--------------- UNINSTALLER EndIf}

;---------------------------------------------------------
; BATCH SCRIPTS
;Helper function for the Batch creators
Function GetRoot
  Exch $0
  Push $1
  Push $2
  Push $3
  Push $4

  StrCpy $1 $0 2
  StrCmp $1 "\\" UNC
    StrCpy $0 $1
    Goto done

UNC:
  StrCpy $2 3
  StrLen $3 $0
  loop:
    IntCmp $2 $3 "" "" loopend
    StrCpy $1 $0 1 $2
    IntOp $2 $2 + 1
    StrCmp $1 "\" loopend loop
  loopend:
    StrCmp $4 "1" +3
      StrCpy $4 1
      Goto loop
    IntOp $2 $2 - 1
    StrCpy $0 $0 $2

done:
  Pop $4
  Pop $3
  Pop $2
  Pop $1
  Exch $0
FunctionEnd

Function CreateServiceInstallerBat
         Push $WINDIR
         Call GetRoot
         Pop ${VAL1}

         FileOpen $0 "${PRODUCT_SCRIPTS}\serviceinstaller.bat" "w"
         FileWrite $0 "@ECHO OFF$\r$\n"
         FileWrite $0 ${VAL1}$\r$\n
         FileWrite $0 "cd $\"$WINDIR\Microsoft.NET\Framework\v4.0.30319$\"$\r$\n"
         FileWrite $0 "installutil.exe /http=$R1 /https=$R2 $\"$INSTDIR\MOTRd.exe$\"$\r$\n"
		 FileWrite $0 "sc failure MOTRd reset= 0 actions= restart/60000$\r$\n"
         FileWrite $0 "ECHO Wait 2 seconds for starting the service$\r$\n"
         FileWrite $0 "ping 127.0.0.1 -n 5 -w 2000 >nul$\r$\n"
         FileWrite $0 "net start MOTRd$\r$\n"
         ;FileWrite $0 "pause"
         FileClose $0
		 
		 ;Creates a URL to be launched to login site
		WriteIniStr "$INSTDIR\Links\MOTRd in HTTP local.url" "InternetShortcut" "URL" "http://localhost:$R1/"
		WriteIniStr "$INSTDIR\Links\MOTRd in HTTPS local.url" "InternetShortcut" "URL" "https://localhost:$R2/"
		CreateShortCut "${PRODUCT_STARTMENU}\MOTR login HTTP.lnk" "$INSTDIR\Links\MOTRd in HTTP local.url" "" "$INSTDIR\login.ico" 0
		CreateShortCut "${PRODUCT_STARTMENU}\MOTR login HTTPS.lnk" "$INSTDIR\Links\MOTRd in HTTPS local.url" "" "$INSTDIR\login.ico" 0
FunctionEnd

Function CreateServiceUnInstallerBat
         Push $WINDIR
         Call GetRoot
         Pop ${VAL1}

         FileOpen $0 "${PRODUCT_SCRIPTS}\serviceuninstaller.bat" "w"
         FileWrite $0 "@ECHO OFF$\r$\n"
		 FileWrite $0 "net stop motrd$\r$\n"
         FileWrite $0 "ECHO Wait 2 seconds before uninstalling the service$\r$\n"
         FileWrite $0 "ping 127.0.0.1 -n 5 -w 2000 >nul$\r$\n"
         FileWrite $0 ${VAL1}$\r$\n
         FileWrite $0 "cd $\"$WINDIR\Microsoft.NET\Framework\v4.0.30319$\"$\r$\n"
         FileWrite $0 "installutil.exe /u $\"$INSTDIR\MOTRd.exe$\"$\r$\n"
         FileClose $0
FunctionEnd


Function CreateCertDomainBat
         FileOpen $0 "${PRODUCT_SCRIPTS}\domaincert.bat" "w"
         FileWrite $0 "@ECHO OFF$\r$\n"
         FileWrite $0 "REM Check if command prompt is elevated$\r$\n"
         FileWrite $0 "net session >nul 2>&1$\r$\n"
         FileWrite $0 "if %errorLevel% == 0 ($\r$\n"
         FileWrite $0 "		goto STARTING$\r$\n"
         FileWrite $0 ") else ($\r$\n"
         FileWrite $0 "		ECHO Warning: This script needs to be runned as administrator.$\r$\n"
         FileWrite $0 "		pause$\r$\n"
         FileWrite $0 "		goto THEEND$\r$\n"
         FileWrite $0 ")$\r$\n"
         FileWrite $0 ":STARTING$\r$\n"
		 FileWrite $0 "cd $\"${PRODUCT_INSTALLDIRECTORY}$\"$\r$\n"
         FileWrite $0 "ECHO Please enter top-level domainname.$\r$\n"
         FileWrite $0 "ECHO Example: site.com$\r$\n"
         FileWrite $0 "ECHO (Do not enter sub domains like motr.site.com)$\r$\n"
         FileWrite $0 "ping 127.0.0.1 -n 5 -w 2000 >nul$\r$\n"
         FileWrite $0 "ECHO. $\r$\n"
         FileWrite $0 "ECHO Please stop MOTRd before you proceed to gain access to keyfile$\r$\n"
         FileWrite $0 "ping 127.0.0.1 -n 5 -w 2000 >nul$\r$\n"
         FileWrite $0 "ECHO. $\r$\n"
         FileWrite $0 "ECHO Enter domain and press ENTER to continue or CTRL+C to abort$\r$\n"
         FileWrite $0 "ECHO. $\r$\n"
         FileWrite $0 "set /p id=$\"Domain: $\"$\r$\n"
		 FileWrite $0 "MOTRd.exe -CERT %id%$\r$\n"
         FileWrite $0 "ECHO. $\r$\n"
         FileWrite $0 "ECHO Please restart MOTRd if the cert-file creation was OK$\r$\n"
		 FileWrite $0 "pause$\r$\n"
         FileWrite $0 ":THEEND$\r$\n"
         FileWrite $0 "exit$\r$\n"
         FileClose $0
FunctionEnd


Function CheckAndInstallDotNet
    ; Magic numbers from http://msdn.microsoft.com/en-us/library/ee942965.aspx
    ClearErrors
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"

    IfErrors NotDetected

	; 4.7.2 on all Windows version that support it
    ${If} $0 >= 461814

        DetailPrint "Microsoft .NET Framework 4.7.2 is installed ($0)"
    ${Else}
    NotDetected:
        DetailPrint "Installing Microsoft .NET Framework 4.7.2"
        SetDetailsPrint listonly
        ExecWait '"$INSTDIR\Tools\dotNetFx45_Full_setup.exe" /passive /norestart' $0
        ${If} $0 == 3010 
        ${OrIf} $0 == 1641
            DetailPrint "Microsoft .NET Framework 4.7.2 installer requested reboot"
            SetRebootFlag true
        ${EndIf}
        SetDetailsPrint lastused
        DetailPrint "Microsoft .NET Framework 4.7.2 installer returned $0"
    ${EndIf}

FunctionEnd


;-------------- END BAT SCRIPTS