<#
.SYNOPSIS
  RandomPlaylistMod 自动发布脚本
.DESCRIPTION
  1. 自动递增 patch 版本号 (manifest.json + csproj)
  2. 编译 Release DLL
  3. Git commit + tag + push
  4. 创建 GitHub Release 并上传 DLL
.PARAMETER Message
  Release 描述信息（可选）
.EXAMPLE
  .\release.ps1 "修复 VR HUD 显示问题"
#>

param(
    [string]$Message = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$ManifestPath = "$ProjectRoot\RandomPlaylistMod\manifest.json"
$CsprojPath = "$ProjectRoot\RandomPlaylistMod\RandomPlaylistMod.csproj"

# --- 1. 读取当前版本 ---
Write-Host "读取当前版本..." -ForegroundColor Cyan
$manifest = Get-Content $ManifestPath | ConvertFrom-Json
$oldVersion = $manifest.version
Write-Host "当前版本: $oldVersion" -ForegroundColor Yellow

# --- 2. 递增 patch 版本 ---
$parts = $oldVersion.Split('.')
if ($parts.Length -ne 3) { Write-Error "版本号格式错误: $oldVersion"; exit 1 }
$parts[2] = ([int]$parts[2] + 1).ToString()
$newVersion = "$($parts[0]).$($parts[1]).$($parts[2])"
Write-Host "新版本: $newVersion" -ForegroundColor Green

# --- 3. 更新文件 ---
Write-Host "更新 manifest.json..." -ForegroundColor Cyan
$manifest.version = $newVersion
$manifest | ConvertTo-Json -Compress:$false | Set-Content $ManifestPath -Encoding UTF8

Write-Host "更新 RandomPlaylistMod.csproj..." -ForegroundColor Cyan
$xml = [xml](Get-Content $CsprojPath)
$versionNode = $xml.SelectSingleNode("//Version")
if ($versionNode -eq $null) {
    $pg = $xml.SelectSingleNode("//PropertyGroup")
    $versionNode = $xml.CreateElement("Version")
    $pg.AppendChild($versionNode) | Out-Null
}
$versionNode.InnerText = $newVersion
$xml.Save($CsprojPath)

# --- 4. 编译 ---
Write-Host "编译 Release..." -ForegroundColor Cyan
dotnet build "$ProjectRoot\RandomPlaylistMod\RandomPlaylistMod.csproj" -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "编译失败"; exit 1 }
Write-Host "编译成功!" -ForegroundColor Green

# --- 5. Git commit + tag + push ---
$commitMsg = if ($Message) { "Release v$newVersion`n`n$Message" } else { "Release v$newVersion" }

Write-Host "Git commit..." -ForegroundColor Cyan
git -C $ProjectRoot add $ManifestPath $CsprojPath
git -C $ProjectRoot commit -m $commitMsg

Write-Host "Git tag v$newVersion..." -ForegroundColor Cyan
git -C $ProjectRoot tag "v$newVersion"

Write-Host "Git push..." -ForegroundColor Cyan
git -C $ProjectRoot push origin master --tags
if ($LASTEXITCODE -ne 0) { Write-Error "Git push 失败"; exit 1 }

# --- 6. 创建 GitHub Release 并上传 DLL ---
$dllPath = "$ProjectRoot\RandomPlaylistMod\bin\Release\RandomPlaylistMod.dll"
$releaseNotes = @"
## v$newVersion

$(if ($Message) { $Message } else { "Bug fixes and improvements." })
"@

Write-Host "创建 GitHub Release v$newVersion..." -ForegroundColor Cyan
gh -R xirain/RandomPlaylistMod release create "v$newVersion" `
    --title "v$newVersion" `
    --notes $releaseNotes `
    $dllPath `
    $ManifestPath

if ($LASTEXITCODE -ne 0) { Write-Error "GitHub Release 创建失败"; exit 1 }

Write-Host "`n=============================" -ForegroundColor Green
Write-Host " v$newVersion 发布成功!" -ForegroundColor Green
Write-Host " https://github.com/xirain/RandomPlaylistMod/releases/tag/v$newVersion" -ForegroundColor Cyan
Write-Host "=============================`n" -ForegroundColor Green
