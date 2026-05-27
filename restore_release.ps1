# Restore Release Script - 一键还原到 v1.0.0 可游玩版本
# 用法：在 PowerShell 中运行  .\restore_release.ps1

$ErrorActionPreference = "Stop"

$ReleaseVersion = "v1.3.1"
$Repo           = "xirain/RandomPlaylistMod"
$AssetName      = "RandomPlaylistMod.dll"

$TargetDirs = @(
    "F:\paly\BSManager\BSInstances\1.40.8\Plugins",
    "F:\paly\BSManager\BSInstances\1.42.2\Plugins"
)

$TmpDir = Join-Path $PSScriptRoot "release_cache"
New-Item -ItemType Directory -Force -Path $TmpDir | Out-Null
$LocalDll = Join-Path $TmpDir $AssetName

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Restore Release $ReleaseVersion" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 下载 Release DLL（如果本地缓存没有）
if (-not (Test-Path $LocalDll)) {
    Write-Host "Step 1: Downloading $AssetName from $ReleaseVersion ..."
    $url = "https://github.com/$Repo/releases/download/$ReleaseVersion/$AssetName"
    try {
        Invoke-WebRequest -Uri $url -OutFile $LocalDll -ErrorAction Stop
        Write-Host "  Downloaded to: $LocalDll" -ForegroundColor Green
    } catch {
        Write-Host "  ERROR: Download failed! $_" -ForegroundColor Red
        Write-Host "  Please check your network or manually download from:" -ForegroundColor Yellow
        Write-Host "  https://github.com/$Repo/releases/tag/$ReleaseVersion" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "Step 1: Using cached $AssetName (already downloaded)"
}

Write-Host ""

# 2. 部署到各 BS 实例
Write-Host "Step 2: Deploying to Beat Saber instances..."
foreach ($dir in $TargetDirs) {
    Write-Host "  Target: $dir"
    if (-not (Test-Path $dir)) {
        Write-Host "  WARNING: Directory not found, skipping." -ForegroundColor Yellow
        continue
    }
    try {
        Copy-Item -Path $LocalDll -Destination $dir -Force
        Write-Host "  OK: Deployed!" -ForegroundColor Green
    } catch {
        Write-Host "  ERROR: Copy failed! $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Restore Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Release DLL cached at: $LocalDll"
Write-Host "Restart Beat Saber to use the stable version."
