# 코어 시스템 사용법

이미 만들어진 공통 기능 목록과 사용법입니다. **같은 역할의 기능을 새로 만들지 말고 여기 있는 것을 가져다 쓰세요.**
기능이 부족하면 새로 만들기 전에 담당자(KDH)와 먼저 이야기해서 기존 코드를 늘립니다.

- 스크립트 위치: `History_Crash/Assets/Scripts/` (아래 경로는 이 폴더 기준)
- 매니저 프리팹: `History_Crash/Assets/Prefabs/Managers.prefab`
- 예시 씬: `History_Crash/Assets/Scenes/KDH_Test.unity`

---

## 1. 공통 원칙

- **인터페이스로 받기**: 다른 스크립트는 구체 클래스(`GameManager`, `Health`) 대신 인터페이스(`IGameFlow`, `IDamageable`)로 받습니다.
  인스펙터에서는 `MonoBehaviour` 칸에 연결하고 `InterfaceRef.Resolve<T>()`로 바꿉니다.
  ```csharp
  [SerializeField] private MonoBehaviour _gameFlowSource;   // 인스펙터에 GameManager 연결
  IGameFlow _gameFlow = InterfaceRef.Resolve<IGameFlow>(_gameFlowSource, this, nameof(_gameFlowSource));
  ```
- **Update / FixedUpdate 쓰지 않기**: 입력은 New Input System 이벤트(`performed`), 값 변화는 R3 구독, 시간 대기는 UniTask, 부드러운 변화는 DOTween을 씁니다.
- 코드 스타일은 [CODING_CONVENTION.md](CODING_CONVENTION.md), 브랜치·커밋은 [GIT_CONVENTION.md](GIT_CONVENTION.md)를 따릅니다.

---

## 2. 매니저 (씬이 바뀌어도 유지)

시작 씬에 `Managers` 프리팹 하나만 둡니다. 다른 씬에 또 있으면 나중 것이 스스로 사라집니다.

| 매니저 | 파일 | 하는 일 | 쓰는 법 |
|---|---|---|---|
| SceneLoader | `Core/Scene/SceneLoader.cs` | 씬 전환 (페이드, 로딩 진행률) | `ISceneLoader.LoadScene("씬이름")`, `ReloadCurrent()` |
| SaveManager | `Core/Save/SaveManager.cs` | 자동 저장·불러오기 (JSON) | `ISaveService`, `IIntroProgress` (아래 6장) |
| SoundManager | `Core/Sound/SoundManager.cs` | 배경음·효과음, 볼륨 | `SoundManager.Instance.PlayBgm(clip)`, `PlaySfx(clip)`, `PlaySfx(clip, 위치)` |

씬마다 따로 두는 매니저: `GameManager`(게임 흐름), `PoolManager`(오브젝트 풀), `SpawnManager`(스폰).

---

## 3. 게임 흐름 — `GameManager` (`Core/Game/`)

- 상태: `Ready`(준비) → `Playing`(진행) → `Clear`(파라오 처치) / `Fail`(플레이어 사망)
- 읽기: `IGameFlow` — `CurrentState`(구독), `IsPlaying`, `ElapsedTime`, `ElapsedSeconds`(1초마다), `IsPaused`(구독)
- 진행 중일 때만 동작해야 하는 것(미라 리젠, 보스 패턴, 기믹)은 상태를 구독합니다.
  ```csharp
  _gameFlow.CurrentState
      .Subscribe(_state => SetActive(_state == GameStateType.Playing))
      .AddTo(this);
  ```
- 게임이 끝나면 끌 컴포넌트: `DisableOnGameEnd`에 등록 (플레이어 조작 등)

### 일시정지 — `IPauseControl`
- Esc(`Pause` 입력) → `PauseInputHandler`가 `TogglePause()` 호출. 시간 정지(timeScale 0), 준비·진행 상태에서만 가능
- **설정창은 일시정지를 따로 만들지 말고** `IsPaused`를 구독해서 열고 닫습니다.
  ```csharp
  _gameFlow.IsPaused.Subscribe(_paused => _settingsPanel.SetActive(_paused)).AddTo(this);
  // "계속하기" 버튼
  _pauseControl.Resume();
  ```
- 일시정지 중 끌 컴포넌트: `DisableWhilePaused`에 등록 (플레이어 시점·이동은 이미 등록됨 → 커서가 자동으로 보임)

---

## 4. 체력·피해 — `Health` (`Core/Combat/`)

플레이어(하트 5칸), 미라, 파라오 모두 `Health` 하나로 처리합니다.

| 하려는 것 | 쓰는 것 |
|---|---|
| 피해 주기 | `IDamageable.TakeDamage(new DamageInfo(양, ElementType.없으면None, 준오브젝트))` |
| 체력 보기·UI | `IHealthReadOnly` — `CurrentHp`(구독), `MaxHp`, `OnDamaged`, `OnDeath`, `IsAlive` |
| 잠깐 무적 | `IInvincible.SetInvincible(초)`, `IsInvincible` |

```csharp
if (_other.TryGetComponent(out IDamageable _target))
{
    _target.TakeDamage(new DamageInfo(1, ElementType.None, gameObject));
}
```
- 피격 후 무적 시간은 `Health` 인스펙터의 값(플레이어 1초)으로 자동 적용됩니다.
- 무적이어도 피해를 주려면(C 보석처럼 짧은 간격으로 계속 맞히는 공격) 마지막 인자를 `true`로:
  `new DamageInfo(1, ElementType.None, gameObject, true)`
- 체력 UI 예시: `UI/HeartView.cs`, `UI/HealthBarView.cs` (`Bind(IHealthReadOnly)`)

---

## 5. 플레이어 (`Player/`)

플레이어는 `PlayerController` 계열을 씁니다. 패턴·기믹은 아래 인터페이스로 플레이어에 접근합니다.

| 하려는 것 | 인터페이스 | 비고 |
|---|---|---|
| 점프로 피했는지 판정 (0.45m 이상) | `IJumpState.IsDodgingByJump()` | `IsGrounded`, `GetHeightAboveGround()`도 있음 |
| 이동 속도 바꾸기 (공격 중 60% 등) | `ISpeedModifier.SpeedMultiplier` | 1이 기본, 0이면 정지 |
| 조작 잠깐 막기 | `IMovementLock.Lock()` / `Unlock()` | 횟수 방식. **try/finally로 반드시 Unlock** |
| 즉시 옮기기 + 바닥에 착지 | `IPlayerTeleport.TeleportTo(위치)` | 점프·낙하 상태 끊음 |
| 아누비스 강제 이동 | `IForcedMovable.TryForceMove(도착 칸 바닥 위치)` | 0.8초 포물선, 무적 포함, 진행 중이면 무시 |
| 보는 방향·조준 지점 | `ILookDirection.Yaw`, `AimPoint` | 공격 방향 계산용 |
| 이동·점프·착지 알림 (애니·효과음) | `IPlayerMotion.OnMoveChanged`, `OnJumped`, `OnLanded` | |
| 보스 락온 상태 | `BossLockOn.IsLockedOn`, `OnLockOnChanged` | `Camera/BossLockOn.cs` |

```csharp
// 함정 판정: 점프 중이면 무시
if (_player.TryGetComponent(out IJumpState _jump) && _jump.IsDodgingByJump()) return;

// 아누비스: 예고가 끝나면 도착 칸으로 끌고 감
if (_player.TryGetComponent(out IForcedMovable _movable)) _movable.TryForceMove(_arrivalCellCenter);
```

### 구덩이 낙하
- 바닥 아래에 `Stage/FallZone.cs`(트리거)가 깔려 있고, 플레이어의 `PlayerFallHandler`가 1 피해 + 가까운 안전한 칸으로 복귀 + 1초 무적을 처리합니다.
- **구덩이를 만드는 쪽(호루스 등)은 바닥 타일 오브젝트를 끄기만 하면 됩니다.** 따로 부를 함수는 없습니다.

---

## 6. 저장 — `SaveManager` (`Core/Save/`)

- 시작할 때 자동으로 불러오고, 값을 바꾼 쪽에서 `Save()`를 부르면 저장됩니다. (저장 버튼 없음)
- 저장 위치: `Application.persistentDataPath/save.json`
- 처음 실행 소개(조작법·스토리):
  ```csharp
  IIntroProgress _intro = SaveManager.Instance;
  if (!_intro.HasSeenIntro) { /* 소개 페이지 보여 주기 */ }
  _intro.MarkIntroSeen();   // 마지막 페이지를 넘겼을 때 (바로 저장됨)
  ```
- 저장 항목 추가: `SaveData`에 `[SerializeField]` 필드 + 프로퍼티를 추가하고, 값을 바꾼 뒤 `SaveManager.Instance.Save()`

---

## 7. 오브젝트 풀 — `PoolManager` (`Core/Pool/`)

자주 생기고 사라지는 것(미라, 탄막, 오브, 이펙트)은 `Instantiate/Destroy` 대신 풀을 씁니다.

- 프리팹 루트에 `PooledObject` 컴포넌트를 붙입니다.
- 꺼내기: `IPoolService.Spawn(프리팹, 위치, 회전)` / 반납: `PooledObject.Release()` 또는 `ReleaseAfter(초)`
- 꺼내질 때·반납될 때 초기화가 필요하면 `IPoolable`(`OnSpawned`, `OnDespawned`)을 구현합니다. `Health`는 꺼내질 때 체력이 자동으로 초기화됩니다.
- 주기적 생성: `Core/Spawn/SpawnManager.cs` (`SpawnEntry`로 프리팹·간격·최대 수 설정, 진행 상태에서만 동작)

---

## 8. 테스트 도구

- `Debug/DamageDebugger.cs`: 인스펙터 ⋮ 메뉴로 보스·플레이어에 피해, 즉시 처치 (공격 기능 없이 승패 확인)
- `SaveManager` ⋮ 메뉴: 저장 데이터 초기화 (처음 실행 상태로)
- `Tools/Snap To Ground` (End 키): 선택한 오브젝트를 바닥에 붙이기

---

## 9. 팀원 스크립트를 코어로 바꿀 때 (JMC_Script)

`JMC_Script`의 일부 스크립트는 `PlayerMover`, `PlayerHealth`, `PlayerAnimator`를 직접 찾습니다.
실제 게임에서는 `PlayerController` 계열을 쓰므로 아래처럼 바꿉니다.

### 바꾼 것 (KDH_Test에서 KDH 플레이어로 동작 확인)

| 파일 | 바꾼 내용 |
|---|---|
| `Boss/BossPatternDirector.cs` | 점프 회피 → `IJumpState.GetHeightAboveGround()`, 피해 → `IDamageable` (C 보석은 무적 무시) |
| `Stage/Gimmick/AnubisTeleport.cs` | 플레이어 직접 이동 → `IForcedMovable.TryForceMove(도착 칸)` (예고·효과는 그대로) |
| `Stage/Gimmick/HorusTileCollapse.cs` | 타일이 무너질 때 콜라이더도 끄고, 수복할 때 켬 (빠지면 `FallZone`이 처리) |

- 패턴 오브젝트(`Pattens`, `BossPatternDirector`)는 KDH_Test에 배치되어 있습니다.
- 이제 TEST_JMC의 `PlayerMover` 플레이어는 패턴에 맞아도 피해가 들어가지 않습니다.

### 아직 바꿀 것

| 파일 | 할 일 |
|---|---|
| `Enemy/Mummy.cs`, `Enemy/MummySpawner.cs` | `PlayerMover`·`PlayerHealth` 참조를 아래 표대로 변경 |
| `Player/PlayerLoadout.cs` | 같은 방식으로 변경 |
| `UI/PlayerHPCompassUI.cs` | 체력 읽기를 `IHealthReadOnly.CurrentHp` 구독으로 변경 |
| 보스 패턴·아누비스·호루스 | **게임이 끝나도(클리어·실패) 계속 돌아감** → `IGameFlow.CurrentState`가 `Playing`일 때만 동작하게 변경 |

### 바꾸는 방법

| 지금 쓰는 것 | 바꿀 것 |
|---|---|
| `PlayerHealth.TakeDamage(...)` | `IDamageable.TakeDamage(new DamageInfo(...))` |
| `TakeDamage(..., ignoreInvulnerability: true)` | `new DamageInfo(양, ElementType.None, gameObject, true)` |
| `PlayerHealth`의 무적 (`GrantInvulnerability`) | `IInvincible.SetInvincible(초)` |
| `PlayerHealth`의 HP 읽기 (UI) | `IHealthReadOnly.CurrentHp` 구독 |
| `PlayerMover`의 점프 회피 높이 판정 | `IJumpState.IsDodgingByJump()` |
| `PlayerMover.SnapToGround()` / 위치 직접 이동 | `IPlayerTeleport.TeleportTo(위치)` |
| 아누비스 끌고 가기 (이동 스크립트 끄고 직접 옮기기) | `IForcedMovable.TryForceMove(도착 칸)` |
| `PlayerMover`의 이동 막기 | `IMovementLock.Lock()` / `Unlock()` |
| `PlayerAnimator`의 상태 읽기 | `IPlayerMotion` 이벤트 |
| 구덩이 낙하 처리 | 타일 콜라이더만 끄면 `FallZone`이 처리 |
