$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    New-Item -ItemType Directory -Force work | Out-Null
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    & $compiler /nologo /target:exe /main:Regression /out:work\Regression.exe /resource:assets\app.ico,FocusGarden.Icon /resource:assets\app.png,FocusGarden.Mark /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll FocusGarden.cs Island.cs Planning.cs Storage.cs tests\Regression.cs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    & ./work/Regression.exe
    if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed' }
} finally {
    Pop-Location
}
