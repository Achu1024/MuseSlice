# 构建 Release 版并打包成安装程序。
#
#   powershell -ExecutionPolicy Bypass -File build-installer.ps1
#
# 产物：dist\MuseSlice-Setup-<版本>.exe
# 前提：已装 .NET SDK 与 Inno Setup 6（winget install JRSoftware.InnoSetup）

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

$inno = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $inno) {
    throw "找不到 ISCC.exe。装一个：winget install JRSoftware.InnoSetup"
}

# icon\MuseSlice.ico 已入库，正常构建不需要动它。
# 品牌图改版时才跑 tools\make_icon.py 重新生成（需要 Python + Pillow）。

Write-Host "==> 编译 Release" -ForegroundColor Cyan
dotnet build MuseSlice.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "编译失败" }

Write-Host "==> 打包安装程序" -ForegroundColor Cyan
& $inno "installer\MuseSlice.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup 编译失败" }

Write-Host "`n完成，产物在 dist\：" -ForegroundColor Green
Get-ChildItem dist\*.exe | Select-Object Name, @{n = "大小"; e = { "{0:N0} KB" -f ($_.Length / 1KB) } }
