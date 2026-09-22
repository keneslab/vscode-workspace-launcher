# WorkspaceLauncher 빌드 스크립트
# .NET SDK 없이 윈도우에 기본 포함된 .NET Framework 컴파일러(csc.exe)로 빌드한다.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$src  = Join-Path $root 'src'
$bin  = Join-Path $root 'bin'
$exe  = Join-Path $bin  'WorkspaceLauncher.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw "csc.exe 를 찾을 수 없습니다. .NET Framework 4.x 가 설치되어 있어야 합니다."
}

if (-not (Test-Path $bin)) { New-Item -ItemType Directory -Path $bin | Out-Null }

# 아이콘 (없어도 빌드는 된다 — 기본 아이콘으로 나온다)
$icon = Join-Path $root 'workspace-icon.ico'
if (-not (Test-Path $icon)) { $icon = $null }

$manifest = Join-Path $root 'app.manifest'
$sources  = Get-ChildItem -Path $src -Filter *.cs | ForEach-Object { $_.FullName }

$cscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/langversion:5'
    '/codepage:65001'
    '/utf8output'
    '/warn:3'
    "/out:$exe"
    "/win32manifest:$manifest"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Web.Extensions.dll'
)
if ($icon) { $cscArgs += "/win32icon:$icon" }
$cscArgs += $sources

Write-Host "컴파일 중..." -ForegroundColor Cyan
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "컴파일 실패 (exit $LASTEXITCODE)" }

if ($icon) { Copy-Item $icon (Join-Path $bin 'workspace-icon.ico') -Force }

Write-Host "빌드 완료: $exe" -ForegroundColor Green
