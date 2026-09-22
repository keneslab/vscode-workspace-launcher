# VS Code 워크스페이스 런처

[English](README.en.md) · 한국어

윈도우 11 작업 표시줄에 고정해서 쓰는 VS Code 런처.

- **왼쪽 클릭** → VS Code 빈 새 창 (`code -n`)
- **오른쪽 클릭** → 점프 목록(숏컷 팝업)에서 `.code-workspace` 선택 → 해당 워크스페이스로 VS Code 실행
- 등록 개수 **제한 없음**, 순서는 드래그 / ▲▼ 버튼으로 변경

`.NET SDK 설치가 필요 없다.` 윈도우에 기본 포함된 .NET Framework 컴파일러(`csc.exe`)로 빌드한다.

## 요구 사항

| | |
|---|---|
| OS | 윈도우 10 / 11 |
| 런타임 | .NET Framework 4.x — 윈도우 10/11에 기본 포함 |
| 빌드 도구 | 없음 (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 사용) |
| VS Code | 설치되어 있어야 함 (설치본 / Insiders / PATH 자동 탐색) |

---

## 설치

PowerShell 에서:

```powershell
git clone https://github.com/keneslab/vscode-workspace-launcher.git
cd vscode-workspace-launcher
.\install.ps1
```

빌드 → 시작 메뉴 바로가기 생성 → 점프 목록 등록까지 한 번에 진행된다.

그 다음 **작업 표시줄에 고정**:

1. 시작 → 모든 앱 → `VS Code 워크스페이스`
2. 오른쪽 클릭 → 자세히 → **작업 표시줄에 고정**

> 고정은 반드시 이 바로가기로 해야 한다. 바로가기에 AppUserModelID
> (`DevWorkspace.VSCodeWorkspaceLauncher`) 가 박혀 있고 점프 목록이 그 ID 에 붙는다.
> `bin\WorkspaceLauncher.exe` 를 직접 드래그해서 고정하면 점프 목록이 안 붙을 수 있다.

첫 실행 시 스캔 폴더를 자동으로 찾지 못하면 목록이 비어 있다. 점프 목록 →
`워크스페이스 관리 / 전체 목록…` → **설정** 탭에서 `.code-workspace` 가 들어 있는
폴더를 지정하면 된다.

---

## 사용

### 작업 표시줄 아이콘

| 동작 | 결과 |
|---|---|
| 왼쪽 클릭 | VS Code 빈 새 창 (설정에서 특정 폴더 열기로 바꿀 수 있음) |
| 오른쪽 클릭 | 점프 목록 → 워크스페이스 선택 시 해당 `.code-workspace` 로 VS Code 실행 |

점프 목록 아래쪽 **작업** 영역에는 항상 다음 세 개가 있다.

- `새 창 (빈 프로젝트)`
- `워크스페이스 관리 / 전체 목록…` — 관리 창 열기
- `점프 목록 새로 고침` — 폴더 재스캔 후 목록 갱신

### 관리 창

점프 목록의 `워크스페이스 관리 / 전체 목록…` 또는 `WorkspaceLauncher.exe --manage`.

**워크스페이스 탭**

- 체크박스 = 점프 목록에 표시할지 여부
- **드래그** 또는 **▲ 위로 / ▼ 아래로 / 맨 위로 / 맨 아래로** 로 순서 변경 (다중 선택 가능)
- 더블클릭 또는 `Enter` → 바로 열기
- 검색창에 입력하면 이름·경로·그룹으로 필터링 (검색 중에는 순서 변경 비활성화)
- `워크스페이스 추가…` / `폴더 추가…` 로 스캔 폴더 밖의 항목도 직접 등록
- `이름 변경…` 으로 점프 목록 표시 이름 지정, `그룹 지정…` 으로 카테고리 분류
- 회색 = 점프 목록 미표시, 빨간색 = 경로가 사라진 항목
- 창을 닫으면 자동 저장 + 점프 목록 재적용

**설정 탭**

| 항목 | 설명 |
|---|---|
| VS Code 실행 파일 | 비워두면 자동 탐색 (설치본 / Insiders / PATH) |
| 스캔 폴더 | 한 줄에 하나 |
| 하위 폴더 탐색 깊이 | `.code-workspace` 를 찾을 깊이 (기본 3) |
| 실행할 때마다 자동 재스캔 | 새 워크스페이스를 만들면 다음 클릭 때 자동 등록 |
| 점프 목록 최대 표시 개수 | `0` = 윈도우가 허용하는 최대치까지 |
| 그룹별로 나눠서 표시 | `Group` 값별로 점프 목록 카테고리 분리 |
| 왼쪽 클릭 동작 | 빈 새 창 / 지정한 폴더 열기 |

### 명령줄

```
WorkspaceLauncher.exe                 빈 VS Code 창
WorkspaceLauncher.exe --open <경로>   해당 워크스페이스/폴더 열기
WorkspaceLauncher.exe --manage        관리 창
WorkspaceLauncher.exe --refresh [-v]  재스캔 + 점프 목록 갱신 (-v 는 결과 표시)
WorkspaceLauncher.exe --install       시작 메뉴 바로가기 + 점프 목록 등록
WorkspaceLauncher.exe --clear         점프 목록 삭제
```

---

## 개수 제한에 대해

등록할 수 있는 워크스페이스 개수 자체에는 제한이 없다. 다만 **윈도우 점프 목록이 한 번에
화면에 뿌려주는 줄 수는 운영체제가 정한다** (기본 10개 안팎, 스크롤 없음). 이건 앱이 아니라
셸의 제약이라 우회할 수 없어서 두 가지로 대응한다.

1. **넘치는 항목은 관리 창에서 전부 본다.** 점프 목록의 `워크스페이스 관리 / 전체 목록…` 을
   누르면 검색 가능한 전체 목록이 뜬다. 여기엔 개수 제한이 없다.
2. **윈도우 한도 자체를 올린다.** 설정 탭 → `윈도우 점프 목록 표시 개수 늘리기…` 는
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` 의
   `JumpListItems_Maximum` 값을 바꾸고 탐색기를 재시작한다. 20~30 정도가 무난하다.
   (열려 있는 탐색기 창이 모두 닫히므로 확인 대화상자가 먼저 뜬다. 화면 높이보다 많은 항목은
   여전히 잘리므로 무한정 늘어나지는 않는다.)

자주 쓰는 워크스페이스를 목록 위쪽으로 올려두면 점프 목록에 먼저 들어간다.

---

## 설정 파일

`%APPDATA%\WorkspaceLauncher\config.json` — 직접 편집해도 된다.
`Items` 배열의 **순서가 곧 점프 목록 순서**다.

```json
{
  "Name": "내 프로젝트",
  "Path": "D:\\projects\\my-project\\my-project.code-workspace",
  "Show": true,
  "Group": "업무",
  "ExtraArgs": ""
}
```

| 키 | 의미 |
|---|---|
| `Name` | 점프 목록 표시 이름. `null` 이면 파일 이름 사용 |
| `Path` | `.code-workspace` 파일 또는 폴더 경로 |
| `Show` | 점프 목록 표시 여부 |
| `Group` | `GroupByCategory` 가 `true` 일 때의 카테고리 이름 |
| `ExtraArgs` | `code.exe` 에 추가로 넘길 인자 |

편집 후 `WorkspaceLauncher.exe --refresh` 로 반영한다.

오류가 생기면 `%APPDATA%\WorkspaceLauncher\launcher.log` 가 만들어진다
(정상 동작 중에는 생성되지 않는다).

---

## 빌드

```powershell
.\build.ps1
```

`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 를 사용한다. 소스는 이 컴파일러에
맞춰 **C# 5** 문법으로 작성되어 있다 (문자열 보간, `?.`, `nameof` 등 사용 불가).

`.csproj` 가 없는 것은 의도적이다 — 윈도우만 있으면 추가 설치 없이 바로 빌드된다.

### 구조

| 파일 | 역할 |
|---|---|
| `src/Interop.cs` | `ICustomDestinationList` · `IShellLinkW` · `IPropertyStore` COM 인터롭 |
| `src/JumpList.cs` | 점프 목록 생성, AppUserModelID 가 박힌 바로가기 생성 |
| `src/Config.cs` | 설정 JSON, 폴더 스캔 및 병합 |
| `src/ManagerForm.cs` | 관리 창 (목록 · 순서 변경 · 설정) |
| `src/VsCode.cs` | VS Code 실행 |
| `src/Program.cs` | 명령줄 분기, 단일 인스턴스 |

---

## 다른 사람에게 공유하기

`bin\WorkspaceLauncher.exe` **한 파일만** 주면 된다. 아이콘은 exe 에 내장되어 있고
설정 파일은 첫 실행 때 자동 생성된다. 받는 쪽에서 한 번만:

```
WorkspaceLauncher.exe --install
```

서명되지 않은 실행 파일이라 메일이나 다운로드로 받으면 SmartScreen 경고가 뜬다
(`추가 정보` → `실행`).

---

## 제거

1. 작업 표시줄 아이콘 오른쪽 클릭 → 작업 표시줄에서 제거
2. `WorkspaceLauncher.exe --clear` — 점프 목록 삭제
3. `%APPDATA%\Microsoft\Windows\Start Menu\Programs\VS Code 워크스페이스.lnk` 삭제
4. `%APPDATA%\WorkspaceLauncher\` 폴더 삭제
5. `bin\` 폴더 삭제

레지스트리를 건드리는 곳은 `JumpListItems_Maximum` 하나뿐이고, 설정 탭에서 직접
실행했을 때만 바뀐다.

---

## 라이선스

[MIT](LICENSE)
