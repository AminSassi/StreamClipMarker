@echo off
setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] Microsoft .NET C# compiler not found.
    pause
    exit /b 1
)

echo [StreamClipMarker] Compiling standalone Windows executable with custom icon...
"%CSC%" /target:winexe /optimize+ /platform:anycpu /win32icon:app_icon.ico /out:StreamClipMarker.exe /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll src\Program.cs src\MainForm.cs src\SettingsForm.cs src\Core\AppConfig.cs src\Core\AppBranding.cs src\Core\ClipMarker.cs src\Core\HotkeyManager.cs src\Core\JsonHelper.cs src\Core\RecordingDetector.cs src\Core\SessionData.cs src\Core\SessionStorage.cs src\Core\SessionTimer.cs src\Core\ToastFeedback.cs src\Properties\AssemblyInfo.cs

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] StreamClipMarker.exe built successfully with custom branding!
) else (
    echo [ERROR] Compilation failed.
)

endlocal
