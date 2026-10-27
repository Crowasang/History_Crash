# 플러그인 설치 가이드

프로젝트를 처음 받았을 때 아래 순서대로 한 번만 하면 됩니다.
버전과 옵션이 다르면 컴파일이 깨질 수 있으니 **적힌 그대로** 맞춰 주세요.

---

## 1. 자동으로 설치되는 것 (할 일 없음)

pull 받고 Unity를 열면 알아서 받아집니다.

| 플러그인 | 버전 | 설치 방식 |
|------|------|------|
| UniTask | 2.5.10 | `Packages/manifest.json` (Git URL) |
| R3.Unity | 1.3.1 | `Packages/manifest.json` (Git URL) |
| NuGetForUnity | 4.5.0 | `Packages/manifest.json` (Git URL) |
| R3 (+ 의존 패키지) | 1.3.1 | `Assets/packages.config` → NuGetForUnity가 자동 복원 |

> 처음 열 때 R3 관련 컴파일 에러가 잠깐 보일 수 있습니다. NuGet 복원이 끝나면 사라집니다.
> 계속 남아 있으면 메뉴 **NuGet → Restore Packages** 를 눌러 주세요.

---

## 2. 직접 설치해야 하는 것: DOTween

| 플러그인 | 버전 |
|------|------|
| DOTween (HOTween v2) 무료 버전 | 1.3.045 |

1. 에셋 스토어에서 **DOTween (HOTween v2)** 를 "내 에셋에 추가"
2. Unity → **Window → Package Manager → My Assets** 에서 DOTween을 Download → Import
   - 경로는 기본값 그대로 `Assets/Plugins/Demigiant/` 에 둡니다.
3. 임포트 후 뜨는 **DOTween Utility Panel** 에서 **Setup DOTween...** 클릭
   - **Create ASMDEF 체크** (UniTask와 연동하려면 필요)
   - 나머지 모듈은 기본값 그대로 Apply
   - 창이 안 뜨면 메뉴 **Tools → Demigiant → DOTween Utility Panel**

---

## 3. 왜 DOTween / NuGet 폴더는 git에 안 올리나요?

- `Assets/Plugins/Demigiant/`, `Assets/Packages/` 는 `.gitignore` 에 들어 있습니다.
- NuGet 패키지는 `packages.config` 만 있으면 자동 복원되므로 올릴 필요가 없습니다.
- DOTween은 각자 에셋 스토어에서 받습니다. 그래서 위 버전과 Setup 옵션을 꼭 맞춰야 합니다.
- 플러그인 버전을 올릴 때는 팀원과 합의한 뒤 이 문서를 먼저 수정합니다.
