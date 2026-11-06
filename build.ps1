# 编译 StutterFixZh 并部署到游戏的 Mods 目录
# 用法: .\build.ps1 [-NoDeploy]

param(
    [switch]$NoDeploy  # 只编译，不复制到游戏目录
)

$ErrorActionPreference = 'Stop'
$ProjectDir = $PSScriptRoot
$Configuration = 'Release'
$DllName = 'StutterFixZh.dll'

# 游戏 Mods 目录（与 csproj 中的 GameManaged 保持一致的游戏路径）
$GameModsDir = 'D:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\Mods\StutterFixZh'

Write-Host "==> 编译 $DllName ($Configuration)..." -ForegroundColor Cyan

dotnet build "$ProjectDir\StutterFixZh.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "==> 编译失败" -ForegroundColor Red
    exit 1
}

$DllPath = Join-Path $ProjectDir "bin\$Configuration\$DllName"
if (-not (Test-Path $DllPath)) {
    Write-Host "==> 未找到输出: $DllPath" -ForegroundColor Red
    exit 1
}

if ($NoDeploy) {
    Write-Host "==> 完成（未部署）: $DllPath" -ForegroundColor Green
    exit 0
}

Write-Host "==> 部署到 $GameModsDir..." -ForegroundColor Cyan
if (-not (Test-Path $GameModsDir)) {
    New-Item -ItemType Directory -Path $GameModsDir -Force | Out-Null
}
Copy-Item $DllPath $GameModsDir -Force

Write-Host "==> 完成: $GameModsDir\$DllName" -ForegroundColor Green
