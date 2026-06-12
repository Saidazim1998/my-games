# Launcherni bitta self-contained exe ga publish qiladi (foydalanuvchiga .NET kerak bo'lmaydi)
# PublishTrimmed QO'ShILMASIN — WPF .NET 6 da trimming ishlamaydi
$ErrorActionPreference = "Stop"
dotnet publish "$PSScriptRoot\..\src\Launcher" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw "Publish xato" }
$out = "$PSScriptRoot\..\src\Launcher\bin\Release\net6.0-windows\win-x64\publish\Launcher.exe"
Write-Host ""
Write-Host "Tayyor: $((Resolve-Path $out).Path)" -ForegroundColor Green
Write-Host "Shu bitta Launcher.exe ni tarqatasiz — config.json birinchi ishga tushishda o'zi yaraladi."
