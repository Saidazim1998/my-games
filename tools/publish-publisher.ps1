# Release Publisher'ni bitta self-contained exe ga publish qiladi.
# PublishTrimmed QO'ShILMASIN — WPF .NET 6 da trimming ishlamaydi.
$ErrorActionPreference = "Stop"
dotnet publish "$PSScriptRoot\..\src\Publisher" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) { throw "Publish xato" }
$out = "$PSScriptRoot\..\src\Publisher\bin\Release\net6.0-windows\win-x64\publish\Publisher.exe"
Write-Host ""
Write-Host "Tayyor: $((Resolve-Path $out).Path)" -ForegroundColor Green
Write-Host "Bu Publisher.exe ni GameLauncher repo papkasida (yoki uning ichida) ishga tushiring."
Write-Host "Birinchi ishga tushishda repo papkasini va gh.exe yo'lini avtomatik topadi."
