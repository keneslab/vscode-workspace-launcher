# 빌드 + 시작 메뉴 바로가기 생성 + 점프 목록 등록을 한 번에 수행한다.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$exe  = Join-Path $root 'bin\WorkspaceLauncher.exe'

& (Join-Path $root 'build.ps1')

Write-Host "시작 메뉴 바로가기 생성 및 점프 목록 등록 중..." -ForegroundColor Cyan
& $exe --install | Out-Null

$lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
Write-Host ""
Write-Host "설치 완료." -ForegroundColor Green
Write-Host ""
Write-Host "작업 표시줄에 고정하기" -ForegroundColor Yellow
Write-Host "  1) 시작 -> 모든 앱 -> 'VS Code 워크스페이스' 를 찾습니다"
Write-Host "  2) 오른쪽 클릭 -> 자세히 -> 작업 표시줄에 고정"
Write-Host ""
Write-Host "바로가기 위치: $lnk"
Write-Host "설정 파일    : $env:APPDATA\WorkspaceLauncher\config.json"
