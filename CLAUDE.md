# History Crash — Claude 작업 안내

팀 Unity 게임 프로젝트입니다. 이 저장소에서 코드를 짜기 전에 아래 규칙을 따르세요.

- Unity 6000.3, URP, New Input System. Unity 프로젝트 폴더: `History_Crash/`
- 스크립트: `History_Crash/Assets/Scripts/`
- 문서: `docs/` (기획서 `HistoryCrash_기획안_메인_v20.md`, `HistoryCrash_구현상세_v20.md`)
- 답변·주석·커밋 메시지는 한국어로 씁니다.

## 가장 중요한 규칙: 이미 있는 기능을 새로 만들지 않기

**새 매니저, 체력, 플레이어 이동, 게임 흐름, 일시정지, 저장, 오브젝트 풀, 씬 전환, 사운드를 새로 만들지 마세요.**
이미 `Scripts/Core`, `Scripts/Player`, `Scripts/Camera`에 있습니다. 작업 전에 반드시 [docs/CORE_SYSTEM.md](docs/CORE_SYSTEM.md)를 읽고 거기 있는 것을 씁니다.

| 필요한 것 | 쓸 것 |
|---|---|
| 게임 상태(준비·진행·클리어·실패), 경과 시간 | `IGameFlow` (`GameManager`) |
| 일시정지 / 설정창 열고 닫기 | `IGameFlow.IsPaused` 구독, `IPauseControl.Resume()` |
| 피해 주기 / 체력 읽기 / 무적 | `IDamageable` / `IHealthReadOnly` / `IInvincible` (`Health`) |
| 플레이어 점프 회피 판정 | `IJumpState.IsDodgingByJump()` |
| 플레이어 강제 이동 (아누비스) | `IForcedMovable.TryForceMove()` |
| 플레이어 순간이동·조작 잠금·속도 | `IPlayerTeleport` / `IMovementLock` / `ISpeedModifier` |
| 구덩이 | 바닥 타일만 끄기 (`FallZone`이 낙하·복귀 처리) |
| 저장·불러오기, 첫 실행 소개 | `ISaveService`, `IIntroProgress` (`SaveManager`) |
| 자주 생기고 사라지는 오브젝트 | `IPoolService` + `PooledObject` |
| 씬 전환 / 사운드 | `ISceneLoader` / `SoundManager.Instance` |
| 시작 씬의 매니저 | `Assets/Prefabs/Managers.prefab` 하나만 놓기 |

- 기능이 부족하면 비슷한 것을 새로 만들지 말고, **사용자에게 "기존 코드에 이런 기능 추가가 필요하다"고 먼저 알리세요.**
- `Scripts/Core`, `Scripts/Player`, `Scripts/Camera`는 KDH 담당입니다. 고쳐야 하면 사용자에게 먼저 확인합니다.
- 플레이어는 `PlayerController` 계열을 씁니다. `JMC_Script/02.Scripts/Player`의 `PlayerMover`·`PlayerHealth`는 테스트용이므로 새 코드에서 쓰지 않습니다. (바꾸는 방법: CORE_SYSTEM.md 9장)

## 코드 규칙 (자세히: [docs/CODING_CONVENTION.md](docs/CODING_CONVENTION.md))

- 필드·지역 변수·매개변수: `_camelCase` / 클래스·메서드·프로퍼티: `PascalCase` / 상수: `UPPER_SNAKE_CASE`
- **Update / FixedUpdate를 쓰지 않습니다.** 입력은 Input System `performed` 이벤트, 값 변화는 R3 구독, 시간 대기는 UniTask, 부드러운 변화는 DOTween
- 다른 스크립트는 구체 클래스 대신 인터페이스로 받고, 인스펙터 연결은 `MonoBehaviour` 칸 + `InterfaceRef.Resolve<T>()`
- 자주 만들고 지우는 오브젝트는 `Instantiate/Destroy` 대신 오브젝트 풀

## Git 규칙 (자세히: [docs/GIT_CONVENTION.md](docs/GIT_CONVENTION.md))

- 브랜치: `dev`에서 새로 만들기. `feat/이름`, `fix/이름`, `doc/이름`
- 커밋: `feat: 한글 내용`, `fix: 한글 내용`, `doc: 한글 내용` (한 커밋에 한 가지 작업)
- 올리지 않는 것: `ProjectSettings` 변경, `Assets/Art`·`Assets/Resources`(에셋은 따로 전달), DOTween·NuGet 플러그인 폴더
- 같은 씬을 여러 명이 동시에 고치지 않습니다. 각자 자기 테스트 씬에서 작업합니다.
- 커밋·푸시·머지는 사용자가 요청할 때만 합니다.
