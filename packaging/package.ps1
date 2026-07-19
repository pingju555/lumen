<#
.SYNOPSIS
  Lumen 便携版打包脚本：自包含发布 win-x64，扁平压缩为 ZIP（根目录即 lumen.exe）。

.DESCRIPTION
  - dotnet publish --self-contained（捆绑 .NET 8 运行时，用户免装）
  - 不压缩成单文件（避免 WPF/WinRT 裁剪问题），产物为扁平文件夹，lumen.exe 在根
  - 移除 *.pdb 减小体积（发布包不含调试符号）
  - 压缩 staging 目录内容，使解压后 exe 位于顶层
  - 输出 SHA256 便于发布校验

.NOTES
  产物：<repo>/dist/Lumen-<version>-win-x64.zip
  暂存：<repo>/artifacts/stage（可被 .gitignore 忽略）
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [string]$Version       = "",
    [string]$OutputDir     = "dist"
)

$ErrorActionPreference = "Stop"
$root    = Resolve-Path (Join-Path $PSScriptRoot "..")
$proj    = Join-Path $root "src/lumen/lumen.csproj"
$staging = Join-Path $root "artifacts/stage"

# 版本：优先参数，否则取 csproj 的 <Version>
if (-not $Version) {
    $csproj = Get-Content $proj -Raw
    if ($csproj -match '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>') {
        $Version = $Matches[1]
    } else {
        $Version = "1.0.0"
    }
}

$zipName = "Lumen-$Version-$Runtime.zip"
$zipPath = Join-Path (Join-Path $root $OutputDir) $zipName

# 清理旧暂存
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Write-Host "[1/4] Publishing self-contained $Runtime (v$Version) ..."
& dotnet publish "$proj" -c $Configuration -r $Runtime --self-contained true -o "$staging"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# 去除调试符号
Get-ChildItem $staging -Filter *.pdb -Recurse | Remove-Item -Force

# 确认入口存在
$exe = Join-Path $staging "lumen.exe"
if (-not (Test-Path $exe)) { throw "lumen.exe not found in publish output" }

# 压缩 staging 内容（exe 位于 zip 根）
$outDirFull = Split-Path $zipPath
if (-not (Test-Path $outDirFull)) { New-Item -ItemType Directory -Force -Path $outDirFull | Out-Null }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "[2/4] Zipping (flat layout) -> $zipPath ..."
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -Force

Write-Host "[3/4] Verifying zip root contains lumen.exe ..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$hasExe = $null -ne ($zip.Entries | Where-Object { $_.FullName -eq "lumen.exe" })
$zip.Dispose()
if (-not $hasExe) { throw "ZIP root missing lumen.exe" }

Write-Host "[4/4] SHA256 ..."
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)

Write-Host ""
Write-Host "OK  -> $zipPath"
Write-Host "Size-> $size MB"
Write-Host "SHA256: $hash"
