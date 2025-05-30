;~ MIT License

;~ Copyright (c) 2024 Michal Masek - masek@fortwana.sk

;~ Permission is hereby granted, free of charge, to any person obtaining a copy
;~ of this software and associated documentation files (the "Software"), to deal
;~ in the Software without restriction, including without limitation the rights
;~ to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
;~ copies of the Software, and to permit persons to whom the Software is
;~ furnished to do so, subject to the following conditions:

;~ The above copyright notice and this permission notice shall be included in all
;~ copies or substantial portions of the Software.

;~ THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
;~ IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
;~ FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
;~ AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
;~ LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
;~ OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
;~ SOFTWARE.

; Convert to exe after editing
; Exe conversion command
; cd "C:\Program Files (x86)\AutoIt3\Aut2Exe"
; .\Aut2Exe.exe /in "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect-Wrapper.au3" /out "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect-Wrapper.exe" /x86
; Place into C:\Program Files (x86)\CyberArk\PSM\Components\
; Add to applocker - generated exe and installed software
; EXE FILES:
;    <Application Name="WebConnect" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect.exe" Method="Hash" />
;    <Application Name="WebConnect-Wrapper" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect-Wrapper.exe" Method="Hash" />
;    <Application Name="WebConnect-Manager" Type="Exe" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\WebConnect\XaOenUHfxubjqknQD9EJKfKYvVkfgYI=\selenium-manager\windows\selenium-manager.exe" Method="Hash" />
; DLL FILES:
;    <Libraries Name="WebConnect-DLLs" Type="Dll" Path="C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect\WebConnect\XaOenUHfxubjqknQD9EJKfKYvVkfgYI=\*" Method="Path" />

; Edit login process as needed
#include "PSMGenericClientWrapper.au3"
#include "Constants.au3"
#include "ScreenCapture.au3"
#include "WindowsConstants.au3"

; #FUNCTION# ====================================================================================================================
; Name...........: PSMGenericClient_GetSessionProperty
; Description ...: Fetches properties required for the session
; Parameters ....: None
; Return values .: None
; ===============================================================================================================================
Func FetchSessionProperties()
    ; Cyberark Username field
        if (PSMGenericClient_GetSessionProperty("Username", $TargetUsername) <> $PSM_ERROR_SUCCESS) Then
        Error(PSMGenericClient_PSMGetLastErrorString())
        EndIf

    ; Cyberark Password field
        if (PSMGenericClient_GetSessionProperty("Password", $TargetPassword) <> $PSM_ERROR_SUCCESS) Then
        Error(PSMGenericClient_PSMGetLastErrorString())
        EndIf

    ; Cyberark Address field
        if (PSMGenericClient_GetSessionProperty("Address", $TargetAddress) <> $PSM_ERROR_SUCCESS) Then
        Error(PSMGenericClient_PSMGetLastErrorString())
        EndIf

    ; Cyberark Comment field
        if (PSMGenericClient_GetSessionProperty("Comment", $ChromeSettings) <> $PSM_ERROR_SUCCESS) Then
        Error(PSMGenericClient_PSMGetLastErrorString())
        EndIf                  
EndFunc

;=======================================
; Consts & Globals
;=======================================
Global $ConnectionClientPID = 0
Global $TargetUsername
Global $TargetPassword
Global $TargetAddress
Global $WebPrefix
Global $WebPort
Global $WebSuffix
Global $WebDomain
Global $WebIncognitoMode
Global $WebKioskMode
Global $WebCertificate
Global $ChromeSettings
Global Const $DISPATCHER_NAME = "Chrome Connect" ; CHANGE_ME - change only if you are using different browser
Global Const $ConnectionComponent_NAME = "Automatic web connector" ; CHANGE_ME - example = PVWA-web
Global Const $ERROR_MESSAGE_TITLE  = "PSM " & $DISPATCHER_NAME & " Dispatcher error message"
Global Const $LOG_MESSAGE_PREFIX = $DISPATCHER_NAME & " Dispatcher - "
Global Const $iScreenWidth = @DesktopWidth
Global Const $iScreenHeight = @DesktopHeight
; Timeout for application to turn on or for website to load
Global Const $AppTimeout = "30" ; CHANGE_ME
; Timeout for WinWait function
Global Const $WindowTimeout = "60" ; CHANGE_ME - not used in most use cases, but you may use it if needed, by default only $AppTimeout is used for turning on browser, loading website and login process

Global $connect = "C:\Program Files (x86)\CyberArk\PSM\Components\WebConnect.exe"

;=======================================
; Code
;=======================================
Exit Main()

;=======================================
; Main
;=======================================
Func Main()



;SplashTextOn($ConnectionComponent_NAME, $ConnectionComponent_NAME & " is starting, wait until autologin is completed...", $iScreenWidth, $iScreenHeight); Create a splash screen

; Init PSM Dispatcher utils wrapper
;ToolTip ("Initializing CyberArk session...")
if (PSMGenericClient_Init() <> $PSM_ERROR_SUCCESS) Then
Error(PSMGenericClient_PSMGetLastErrorString())
EndIf

LogWrite("INFO: Successfully initialized Dispatcher Utils Wrapper")
LogWrite("INFO: Variables set succesfully")

; Get the dispatcher parameters
FetchSessionProperties()


; Phase 1 start - execute client application
; Prepare variables:

LogWrite("INFO: Parsing $ChromeSettings")
; Parse ChromeSettings string into individual options
Local $options = StringSplit($ChromeSettings, "|")

; Initialize variables by extracting values from each option
Global $WebPrefix = StringTrimLeft($options[1], 3)
Global $WebSuffix = StringTrimLeft($options[2], 3)
Global $WebPort = StringTrimLeft($options[3], 3)
Global $WebDomain = StringTrimLeft($options[4], 3)
Global $WebIncognitoMode = StringTrimLeft($options[5], 3)
Global $WebKioskMode = StringTrimLeft($options[6], 3)
Global $WebCertificate = StringTrimLeft($options[7], 3)
LogWrite("INFO: WebPrefix = " & $WebPrefix)
LogWrite("INFO: WebSuffix = " & $WebSuffix)
LogWrite("INFO: WebPort = " & $WebPort)
LogWrite("INFO: WebDomain = " & $WebDomain)
LogWrite("INFO: WebIncognitoMode = " & $WebIncognitoMode)
LogWrite("INFO: WebKioskMode = " & $WebKioskMode)
LogWrite("INFO: WebCertificate = " & $WebCertificate)

LogWrite("INFO: starting client application - " & $DISPATCHER_NAME)
Global Const $CLIENT_EXECUTABLE = $connect & " --USR " & $TargetUsername & " --PSW " & $TargetPassword & " --DOM " & $WebDomain & " --INCOGNITO " & $WebIncognitoMode & " --KIOSK " & $WebKioskMode & " --CERT " & $WebCertificate & " --URL " & $WebPrefix & $TargetAddress & $WebSuffix

$ConnectionClientPID = Run($CLIENT_EXECUTABLE,  "", @SW_SHOWNORMAL)
LogWrite("INFO: ConnectionClientPID = " & $ConnectionClientPID)

; StringReplace makes sure that password value is not logged in case you are using basic authentication sending password in URL
$logMessage = StringReplace($CLIENT_EXECUTABLE, $TargetPassword, "****") 
LogWrite("INFO: Executed " & $logMessage)
; Phase 1 end

; Phase 2 start - perform autologin using WebConnect.exe
if ($ConnectionClientPID == 0) Then
Error(StringFormat("Failed to execute process [%s]", $connect, @error))
EndIf

; Wait until the login is completed
LogWrite("INFO: Waiting until WebConnect.exe process is completed")
ProcessWaitClose($ConnectionClientPID)
LogWrite("INFO: WebConnect.exe process is completed")

LogWrite("INFO: Getting PID for PSM")
;Wait for window to open and send PID to cyberark
    Opt("WinTitleMatchMode", 2) ; substring match
	local $hWnd = WinWait(" - Google Chrome", "", 0)
	local $iPID = WinGetProcess($hWnd) 
	LogWrite("The CCPID is: " & $ConnectionClientPID ) 
	LogWrite("The new PID is: " & $iPID) 
	$ConnectionClientPID = $iPID 

; Send PID to PSM as early as possible so recording/monitoring can begin
LogWrite("INFO: sending PID to PSM")
if (PSMGenericClient_SendPID($ConnectionClientPID) <> $PSM_ERROR_SUCCESS) Then
Error(PSMGenericClient_PSMGetLastErrorString())
EndIf
LogWrite("INFO: Connection component actions completed and PID was sent to PSM")

; After login steps
;SplashOff()

; Terminate PSM Dispatcher utils wrapper
LogWrite("INFO: Terminating Dispatcher Utils Wrapper")
LogWrite("INFO: COMPONENT ACTIONS - finished")
PSMGenericClient_Term()

Return $PSM_ERROR_SUCCESS
EndFunc

;==================================
; Functions
;==================================
; #FUNCTION# ====================================================================================================================
; Name...........: Error
; Description ...: An exception handler - displays an error message and terminates the dispatcher
; Parameters ....: $ErrorMessage - Error message to display
;   $Code - [Optional] Exit error code
; ===============================================================================================================================
Func Error($ErrorMessage, $Code = -1)

; If the dispatcher utils DLL was already initialized, write an error log message and terminate the wrapper
if (PSMGenericClient_IsInitialized()) Then
LogWrite($ErrorMessage, True)
PSMGenericClient_Term()
EndIf

Local $MessageFlags = BitOr(0, 16, 262144) ; 0=OK button, 16=Stop-sign icon, 262144=MsgBox has top-most attribute set

MsgBox($MessageFlags, $ERROR_MESSAGE_TITLE, $ErrorMessage)

; If the connection component was already invoked, terminate it
if ($ConnectionClientPID <> 0) Then
LogWrite("Terminating Dispatcher Utils Wrapper")
ProcessClose($ConnectionClientPID)
$ConnectionClientPID = 0
EndIf

Exit $Code
EndFunc

; #FUNCTION# ====================================================================================================================
; Name...........: LogWrite
; Description ...: Write a PSMWinSCPDispatcher log message to standard PSM log file
; Parameters ....: $sMessage - [IN] The message to write
;         $LogLevel - [Optional] [IN] Defined if the message should be handled as an error message or as a trace messge
; Return values .: $PSM_ERROR_SUCCESS - Success, otherwise error - Use PSMGenericClient_PSMGetLastErrorString for details.
; ===============================================================================================================================
Func LogWrite($sMessage, $LogLevel = $LOG_LEVEL_TRACE)
Return PSMGenericClient_LogWrite($LOG_MESSAGE_PREFIX & $sMessage, $LogLevel)
EndFunc