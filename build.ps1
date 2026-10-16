param(
  [switch]$SkipBuild,     # 跳过编译，直接打包装
  [switch]$Run,           # 打包完顺手启动
  [string]$Dist = ''      # 输出目录，默认 <仓库>\dist
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src'
$out  = Join-Path $proj 'bin\Release\net9.0-windows7.0'
if (-not $Dist) { $Dist = Join-Path $root 'dist' }

if (-not $SkipBuild) {
  Push-Location $proj
  $log = dotnet build -c Release -v q -nowarn:CS8632,CS0649 2>&1
  Pop-Location
  $err = $log | Select-String -Pattern ': error'
  if ($err) { Write-Host 'BUILD FAILED'; $err | Select-Object -First 25 | ForEach-Object { Write-Host $_ }; exit 1 }
  Write-Host 'build ok'
}

# 输出目录已经是自包含的：exe/dll + art\（由 csproj 的 Content 规则拷进来）
if (-not (Test-Path $out)) { Write-Host "找不到输出目录：$out"; exit 1 }
New-Item -ItemType Directory -Force -Path $Dist | Out-Null
Get-ChildItem $out -File | Where-Object { $_.Extension -in '.exe','.dll','.json' } | ForEach-Object {
  Copy-Item $_.FullName $Dist -Force
}
# 托盘图标：程序从 exe 同级目录读 icon.ico
$ico = Join-Path $root 'icon.ico'
if (Test-Path $ico) { Copy-Item $ico $Dist -Force }
$artSrc = Join-Path $out 'art'
if (Test-Path $artSrc) {
  $artDst = Join-Path $Dist 'art'
  if (Test-Path $artDst) { Remove-Item $artDst -Recurse -Force }
  Copy-Item $artSrc $artDst -Recurse -Force
}
$files = Get-ChildItem $Dist -Recurse -File
Write-Host ("package -> $Dist  ({0} 个文件, {1} MB)" -f $files.Count, [Math]::Round((($files | Measure-Object Length -Sum).Sum)/1MB, 2))

if ($Run) {
  Get-Process -Name 'SC-touhoubox' -ErrorAction SilentlyContinue | Stop-Process -Force
  Start-Sleep -Milliseconds 400
  Start-Process -FilePath (Join-Path $Dist 'SC-touhoubox.exe') -WorkingDirectory $Dist
  Write-Host 'started'
}
