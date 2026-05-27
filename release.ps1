<#
.SYNOPSIS
  RandomPlaylistMod auto-release script
.DESCRIPTION
  1. Auto-increment patch version (manifest.json + csproj)
  2. Build Release DLL
  3. Git commit + tag + push
  4. Create GitHub Release and upload DLL
.PARAMETER Message
  Release description (optional)
.EXAMPLE
  .\release.ps1 "Fix difficulty selection logic"
#>

param(
    [string]$Message = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ManifestPath = "$ProjectRoot\RandomPlaylistMod\manifest.json"
$CsprojPath = "$ProjectRoot\RandomPlaylistMod\RandomPlaylistMod.csproj"

# --- 1. Read current version ---
Write-Host "Reading current version..." -ForegroundColor Cyan
$text = Get-Content $ManifestPath -Encoding UTF8 -Raw
if ($text -notmatch '"version"\s*:\s*"(\d+\.\d+\.\d+)"') {
    Write-Error "Cannot read version from manifest.json"
    exit 1
}
$oldVersion = $Matches[1]
Write-Host "Current version: $oldVersion" -ForegroundColor Yellow

# --- 2. Increment patch version ---
$parts = $oldVersion.Split('.')
if ($parts.Length -ne 3) { Write-Error "Invalid version format: $oldVersion"; exit 1 }
$parts[2] = ([int]$parts[2] + 1).ToString()
$newVersion = "$($parts[0]).$($parts[1]).$($parts[2])"
Write-Host "New version: $newVersion" -ForegroundColor Green

# --- 3. Update files ---
Write-Host "Updating manifest.json..." -ForegroundColor Cyan
$newText = $text -replace '"version"\s*:\s*"\d+\.\d+\.\d+"', "`"version`": `"$newVersion`""
Set-Content $ManifestPath -Value $newText -Encoding UTF8

Write-Host "Updating RandomPlaylistMod.csproj..." -ForegroundColor Cyan
$xml = [xml](Get-Content $CsprojPath)
$versionNode = $xml.SelectSingleNode("//Version")
if ($versionNode -eq $null) {
    $pg = $xml.SelectSingleNode("//PropertyGroup")
    $versionNode = $xml.CreateElement("Version")
    $pg.AppendChild($versionNode) | Out-Null
}
$versionNode.InnerText = $newVersion
$xml.Save($CsprojPath)

# --- 4. Build ---
Write-Host "Building Release..." -ForegroundColor Cyan
dotnet build "$ProjectRoot\RandomPlaylistMod\RandomPlaylistMod.csproj" -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
Write-Host "Build OK!" -ForegroundColor Green

# --- 5. Git commit + tag + push ---
if ($Message) {
    $commitMsg = "Release v$newVersion`n`n$Message"
} else {
    $commitMsg = "Release v$newVersion"
}

Write-Host "Git commit..." -ForegroundColor Cyan
git -C $ProjectRoot add $ManifestPath $CsprojPath
git -C $ProjectRoot commit -m $commitMsg

Write-Host "Git tag v$newVersion..." -ForegroundColor Cyan
git -C $ProjectRoot tag "v$newVersion"

Write-Host "Git push..." -ForegroundColor Cyan
git -C $ProjectRoot push origin master --tags
if ($LASTEXITCODE -ne 0) { Write-Error "Git push failed"; exit 1 }

# --- 6. Create GitHub Release and upload DLL ---
$dllPath = "$ProjectRoot\RandomPlaylistMod\bin\Release\RandomPlaylistMod.dll"
if ($Message) {
    $rn = "## v$newVersion`n`n$Message"
} else {
    $rn = "## v$newVersion`n`nBug fixes and improvements."
}

Write-Host "Creating GitHub Release v$newVersion..." -ForegroundColor Cyan
gh -R xirain/RandomPlaylistMod release create "v$newVersion" `
    --title "v$newVersion" `
    --notes "$rn" `
    "$dllPath" `
    "$ManifestPath"

if ($LASTEXITCODE -ne 0) { Write-Error "GitHub Release failed"; exit 1 }

Write-Host "=============================" -ForegroundColor Green
Write-Host " v$newVersion released!" -ForegroundColor Green
Write-Host " https://github.com/xirain/RandomPlaylistMod/releases/tag/v$newVersion" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Green
