<#
.SYNOPSIS
  Unity build -> release zip + SHA-256 + games.json yozuvi.

.EXAMPLE
  .\publish-game.ps1 -BuildDir 'E:\UnityProjects\Builds\RacingGame' -GameId racing-game -GameName 'Racing Game' -Version 1.1.1 -ExeName 'Racing Game.exe' -GamesJsonPath .\games.json -Owner Saidazim1998 -Repo my-games -Changelog "Version 1.1.0 ga o'zgardi"

  # Lokal test (GitHub'siz): -LocalZip bilan zipUrl lokal fayl yo'li bo'ladi
  .\publish-game.ps1 -BuildDir ... -LocalZip -GamesJsonPath ..\testdata\games.local.json
#>
param(
    [Parameter(Mandatory)][string]$BuildDir,
    [Parameter(Mandatory)][string]$GameId,
    [Parameter(Mandatory)][string]$GameName,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$ExeName,
    [string]$GamesJsonPath,
    [string]$Owner = "OWNER",
    [string]$Repo = "REPO",
    [string]$Changelog = "",
    [string]$Description = "",
    [string]$CoverUrl = "",
    [string]$OutDir = "$PSScriptRoot\..\releases",
    [switch]$LocalZip
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

# 1. Tekshiruv
$exePath = Join-Path $BuildDir $ExeName
if (-not (Test-Path $exePath)) { throw "Exe topilmadi: $exePath" }

# 2. Staging — Unity'ning ship qilinmaydigan papkalarisiz
$staging = Join-Path $env:TEMP "gl-staging-$GameId"
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory $staging | Out-Null
robocopy $BuildDir $staging /E /XD "*_BurstDebugInformation_DoNotShip" "*_BackUpThisFolder_ButDontShipItWithYourGame" | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Robocopy xato kodi: $LASTEXITCODE" }

# 3. Zip — Compress-Archive EMAS (PS 5.1 da 2GB limit); build IChIDAGILARI arxiv ildizida
New-Item -ItemType Directory -Force $OutDir | Out-Null
$zipName = "$GameId-$Version.zip"
$zipPath = Join-Path (Resolve-Path $OutDir) $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zipPath)
Remove-Item $staging -Recurse -Force

# 4. Hash
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash

# 5. Katalog yozuvi
$tag = "$GameId-v$Version"
if ($LocalZip) { $zipUrl = $zipPath }
else { $zipUrl = "https://github.com/$Owner/$Repo/releases/download/$tag/$zipName" }

$entry = [ordered]@{
    id = $GameId; name = $GameName; version = $Version
    zipUrl = $zipUrl; sha256 = $hash; exe = $ExeName
    coverUrl = $CoverUrl; changelog = $Changelog; description = $Description
}

if ($GamesJsonPath) {
    if (Test-Path $GamesJsonPath) {
        $catalog = Get-Content $GamesJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $existing = $catalog.games | Where-Object { $_.id -eq $GameId }
        # coverUrl/description berilmagan bo'lsa eskisini saqlab qolamiz
        if ($existing -and -not $CoverUrl -and $existing.coverUrl) { $entry.coverUrl = $existing.coverUrl }
        if ($existing -and -not $Description -and $existing.description) { $entry.description = $existing.description }
        $games = @($catalog.games | Where-Object { $_.id -ne $GameId })
    } else {
        $games = @()
    }
    $games += [pscustomobject]$entry
    $json = [pscustomobject]@{ games = $games } | ConvertTo-Json -Depth 5
    $resolvedPath = if ([System.IO.Path]::IsPathRooted($GamesJsonPath)) { $GamesJsonPath }
                    else { Join-Path (Get-Location).Path $GamesJsonPath }
    [System.IO.File]::WriteAllText($resolvedPath, $json, (New-Object System.Text.UTF8Encoding $false))
    Write-Host "games.json yangilandi: $resolvedPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== TAYYOR ===" -ForegroundColor Green
Write-Host "Zip:    $zipPath"
Write-Host "SHA256: $hash"
Write-Host "Tag:    $tag"
Write-Host ""
if (-not $LocalZip) {
    Write-Host "Keyingi qadamlar:" -ForegroundColor Yellow
    Write-Host "  gh release create $tag `"$zipPath`" --title `"$GameName $Version`" --notes `"$Changelog`""
    Write-Host "  git add games.json; git commit -m `"$GameId $Version`"; git push"
    Write-Host "(Eslatma: raw.githubusercontent.com ~5 daqiqa kesh qiladi)"
}
exit 0
