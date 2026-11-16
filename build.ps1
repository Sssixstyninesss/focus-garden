$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found. Enable .NET Framework 4.8 or build with Visual Studio.' }
& $compiler /nologo /target:winexe /optimize+ /out:FocusGarden.exe /win32icon:assets/app.ico /resource:assets/app.ico,FocusGarden.Icon /resource:assets/app.png,FocusGarden.Mark /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll FocusGarden.cs Island.cs Planning.cs Storage.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host 'Built FocusGarden.exe'
} finally {
    Pop-Location
}
