$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $csc)) {
    Write-Error "Microsoft .NET C# compiler not found."
    exit 1
}

Write-Host "[StreamClipMarker] Compiling standalone Windows executable with custom icon..." -ForegroundColor Cyan

$sources = @(
    "src\Program.cs",
    "src\MainForm.cs",
    "src\SettingsForm.cs",
    "src\Core\AppConfig.cs",
    "src\Core\AppBranding.cs",
    "src\Core\ClipMarker.cs",
    "src\Core\HotkeyManager.cs",
    "src\Core\JsonHelper.cs",
    "src\Core\RecordingDetector.cs",
    "src\Core\SessionData.cs",
    "src\Core\SessionStorage.cs",
    "src\Core\SessionTimer.cs",
    "src\Core\ToastFeedback.cs",
    "src\Properties\AssemblyInfo.cs"
)

$refs = "System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll"

& $csc /target:winexe /optimize+ /platform:anycpu /win32icon:app_icon.ico /out:StreamClipMarker.exe /r:$refs $sources

if ($LASTEXITCODE -eq 0) {
    Write-Host "[SUCCESS] StreamClipMarker.exe built successfully with custom branding!" -ForegroundColor Green
    $fileInfo = Get-Item "StreamClipMarker.exe"
    Write-Host ("Size: {0:N0} KB" -f ($fileInfo.Length / 1KB)) -ForegroundColor Gray
} else {
    Write-Error "[ERROR] Compilation failed."
}
