# History Crash 구현 상세서 (Unity 테스트 버전, 2026-11-17 기준)

이 문서만 읽고 같은 게임을 처음부터 다시 만들 수 있도록, 현재 Unity 테스트 버전의 **게임 규칙·수치·연출·코드 구조·에셋 파이프라인**을 모두 적었습니다.
기획 의도는 `HistoryCrash_기획안_메인_v20.md`, 실제 구현값은 이 문서가 기준입니다(둘이 다르면 이 문서가 현재 동작).

---

## 1. 게임 한 줄 요약

감염된(언데드화된) 피라미드 신전에서, **고정된 보스 파라오의 탄막 5종(A~E)을 피하며**, 보스가 떨어뜨린 **스태프(보스 패턴)** 와 미라가 떨어뜨린 **속성 오브(불·물·전기·풀)** 를 주워 조합해 **같은 속성의 미라를 처치**하고, 처치된 미라의 **영혼이 파라오에게 날아가 피해**를 주는 3인칭 보스 공략 게임.

- 장르: 3인칭 액션 보스전 (레퍼런스: 엔터 더 건전의 고정 보스 탄막 + 패리맨의 오브젝트 상호작용 + 스타크래프트의 감염 건물 세계관)
- 핵심 재미: 보스의 공격을 주워서 보스(의 하수인)에게 되돌려 쓰기, 점프로 탄막·구덩이 회피
- 플레이 시간 목표: 스피드런 10분 / 일반 15분 (파라오 HP 1,200 ÷ 미라 1킬 20 = 60킬)

---

## 2. 개발 환경

| 항목 | 값 |
|---|---|
| 엔진 | Unity **6000.3.27f1** (Unity 6), **URP**, **Linear** 색공간 |
| 입력 | **새 Input System** (`Keyboard.current`, `Mouse.current`) |
| UI | UGUI (`com.unity.ugui` 2.0.0), 레거시 `Text` + 내장 `LegacyRuntime.ttf` |
| 트윈 라이브러리 | 없음 (DOTween 미사용, 모든 연출은 코루틴) |
| 물리 | 사용 안 함. 이동·충돌·피격은 전부 **타일 격자 + 거리 계산**으로 직접 처리 |
| 3D 툴 | Blender 5.2 (모델·리깅·애니메이션·Cloth·렌더) |
| 프로젝트 | `C:\Unity_C#Study\History_Crash_Test_Obj_MCP_TEST`, 씬 `Assets/01.Scenes/SampleScene.unity` |
| 스크립트 | `Assets/02.Scripts/<기능>/` (네임스페이스 `HistoryCrash.<기능>`), 1파일 1클래스 39개, 코딩 컨벤션 적용(15-1장) |
| 이름 규칙 | `<오브젝트>_<개념>` (예: `Orb_Fire_Glow`, `Player_PattenA_Sweep_fast`, `UI_Sketch_Orb_Water`) |

> "Patten"은 프로젝트 전체에서 "Pattern"의 의도된 표기입니다(에셋·클립·스크립트 이름이 모두 이 철자). 새로 만들 때도 맞춰 두면 참조가 깨지지 않습니다.

---

## 3. 씬 구성 (SampleScene 최상위)

```
Main Camera            Camera, ThirdPersonCamera
Directional Light      (밤 장면, 달빛)
Global Volume          URP 후처리(Bloom 등)
Enviorment/Temple_impe 신전 맵 (언데드화 버전 'impe'), 자식 7그룹
    Floor              바닥 타일 렌더러들 → TileGrid가 격자를 만듦 (무늬 12종 Tile_Crack_A~L)
    Pillars / Walls / Altar / Statues / Monuments / Entrance  → 장애물(이동 불가 칸)
Pattens                (7.00, 0.68, 18.74)
    Player             PlayerMover, PlayerHealth, PlayerAnimator, PlayerLoadout
    Patten_A_Copesh    Animator, AimConstraint   ← 보스 패턴 연출 오브젝트 5종
    Patten_B_SebekeTrap Animator, PositionConstraint
    Patten_C_AngkeGem  Animator, AimConstraint (보석 5개)
    Patten_D_LaSun     Animator, AimConstraint (태양 + 파편)
    Patten_E_ParaoStaff Animator, PositionConstraint (지팡이 5개)
VFX_TorchLight_impe    횃불 파티클 4개
Entity
    Pharaoh            PharaohHealth (+ 파라오 모델/Animator)
    MummySpawner       MummySpawner
BossPatternDirector    TileGrid, BossPatternDirector, HorusTileCollapse, StaffDropper, AnubisTeleport
UI_WeaponHUD           Canvas(Overlay), CanvasScaler(1920×1080), WeaponUIController
    Parchment          양피지 이미지 + 스태프/오브 그림 영역 2개
```

- **콜라이더가 없는 씬**입니다. 카메라 벽 충돌용 BoxCollider만 플레이 시작 때 장애물 렌더러에 자동으로 붙입니다(ThirdPersonCamera).
- 게임 시스템 컴포넌트 대부분이 `BossPatternDirector` 오브젝트 하나에 모여 있습니다.

---

## 4. 좌표계와 타일 격자 (TileGrid) — 모든 시스템의 기반

- 바닥 타일 렌더러(Floor 자식)의 bounds로 격자를 자동 생성. **한 칸 = 0.975m**, **38열 × 30행**.
- `Origin` = (0,0) 칸 중심의 월드 x,z. `WorldToCell`, `CellCenter(cell, y)`, `FloorTopY`(바닥 윗면 높이).
- **이동 가능(walkable)**: 장애물 그룹 렌더러의 bounds를 `obstacleShrink = 0.15m`만큼 줄여, 바닥에서 `1.5m` 이하에서 시작하는 장애물 아래 칸을 막음.
- **구멍(hole)**: 호루스 붕괴로 사라진 칸. `SetHole/IsHole`.
- `IsSolid(c)` = 이동 가능 && 구멍 아님. `IsSafe(c)` = 자신 + 주변 8칸 모두 Solid.
- `NearestSafe(from)` / `NearestSolid(from)`: 링 탐색으로 가장 가까운 안전 칸. 낙하 복귀·강제이동 도착에 사용.
- `[DefaultExecutionOrder(-100)]`로 다른 스크립트 Awake보다 먼저 격자를 만듦(순서가 어긋나면 칸 좌표가 밀림).
- 파라오는 맵 **좌측 끝 제단 위**에 고정. 보스 방향(=격자 +X 방향)으로 E 패턴 지팡이가 발사됨.

---

## 5. 플레이어

### 5-1. 조작

| 입력 | 동작 |
|---|---|
| WASD / 방향키 | 카메라가 보는 수평 방향 기준 이동 (대각 정규화) |
| Shift | 달리기 ×1.6 |
| Space | 점프 |
| 마우스 이동 | 시점 회전 (커서 잠금). Esc: 커서 풀기, 좌클릭: 다시 잠금 |
| 휠 | 카메라 거리 2~12m |
| E | 1칸 안의 가장 가까운 스태프/오브 줍기 |
| 좌클릭 | 스태프 + 오브를 모두 들고 있으면 공격 (둘 다 1회용으로 소모) |
| Z / C | 카메라 45° 스냅 |
| Tab | 보스 락온 켜기/끄기 (기본 꺼짐) |
| R | 보스 쪽으로 스냅, V: 원래 고정 시점 |
| 1~5 / P | (테스트) 다음 보스 패턴 지정 / 자동 진행 켜기·끄기 |

### 5-2. 이동 (PlayerMover)

| 항목 | 값 |
|---|---|
| 이동 속도 | **4 m/s** (약 4.1칸/초), 가속·감속 없음 |
| 달리기 | ×1.6 (6.4 m/s) |
| 공중 이동 | ×0.85 (airControl) |
| 점프 | 높이 **1.1m**, 중력 25 (체공 약 0.6초), 초기 속도 = √(2·g·h) |
| 충돌 반경 | 0.25m. 진행 방향 앞·좌·우 3점이 이동 불가 칸이면 그 축 이동만 막음(벽 따라 미끄러짐) |
| 기준 높이 | `GroundY` = 바닥 윗면 + 모델 반높이 |
| 낙하 | 구덩이 위에 서 있거나 구덩이로 착지하면 낙하 시작 → 1.6m 떨어지면 **1 데미지**, 마지막 안전 위치에서 가장 가까운 안전 칸 중심으로 복귀 + **1초 무적·낙하 면제** |
| 점프 회피 | 바닥에서 **0.45m 이상** 떠 있으면 B(함정)·E(지팡이) 판정 무시. 구덩이도 점프로 건넘 |
| `SnapToGround()` | 외부 강제 이동(아누비스) 직후 점프·낙하 상태를 끊고 바닥에 착지 |

### 5-3. 체력 (PlayerHealth)

- HP **5** (하트 5칸), 피격 후 **1초 무적**(깜빡임).
- `TakeDamage(amount, reason, ignoreInvulnerability)` — C 보석은 무적 무시하고 맞을 때마다 피해.
- 테스트용: HP 0이면 2초 뒤 가득 채움 (실패 처리 미구현).
- `GrantInvulnerability(sec)` — 아누비스 강제 이동, 낙하 복귀에 사용.

### 5-4. 모델과 애니메이션 (PlayerAnimator)

- 모델: 로브 마법사(키 약 1.07m), **오른손에 아누비스 지팡이**. Blender에서 리깅, Unity 컨트롤러 `Assets/04.Art/Model/Player_Robe/Player_Robe.controller`.
- **모델은 항상 카메라가 보는 수평 방향**을 바라보고, 이동 방향을 모델 기준으로 Animator에 넘김.
- Animator 파라미터: `MoveX`(좌− 우+), `MoveZ`(뒤− 앞+), `Speed`(m/s), `MoveAnimSpeed`(배속), `Grounded`(bool).
- **Locomotion 2D 블렌드트리**: Idle(0,0) · Run_Forward(0,1) · Run_Back(0,−1) · Step_StrafeLeft(−1,0) · Step_StrafeRight(1,0). 대각은 블렌드.
- 발 미끄러짐 방지: 재생 배속 = 이동 속도 ÷ 방향별 클립 보폭 속도 (전진 2.04, 후진 1.69, 옆 스텝 0.47 m/s), 0.5~3.5배로 제한.
- 그 외 상태: `Jump_Air`, `Hit_Small/Large_Robe`, `Pickup_Object_Robe`(서서 오른손으로 꽂힌 스태프 회수), `Pickup_Robe`(숙여서 왼손으로 오브 회수, 정지 전용), 공격 5종(아래 7-3).
- 로브: **Unity Cloth** (`Model_Robe/mesh_0_Fit`). 엉덩이 위는 고정(rootBone 공간 높이로 핀 가중치 계산), 밑단만 최대 10cm 흔들림. Bending 0.9, Damping 0.5, 외부 가속 (0,−4,0), 다리 캡슐 콜라이더 5개.

### 5-5. 카메라 (ThirdPersonCamera)

| 항목 | 값 |
|---|---|
| 거리 | 기본 5m, 2~12m (휠) |
| 피벗 높이 | 플레이어 위 0.35m |
| 피치 | 기본 18°, −10~70° |
| 마우스 감도 | 0.12 |
| 따라가기 스무딩 | 0.06초, 락온 시 yaw 스무딩 0.15초 |
| 벽 충돌 | 플레이어→카메라 SphereCast(반경 0.25, 여유 0.1), 막히면 앞으로 당김. 바닥 위 최소 0.25m |

---

## 6. 보스 파라오와 패턴 A~E (BossPatternDirector)

### 6-1. 기본 구조

- 파라오는 제단 위 고정, 플레이어 방향으로 **초당 120°** 회전. HP **1,200** (PharaohHealth, 상단 중앙 HP 바).
- **연출은 Blender에서 만든 Animator 클립을 그대로 재생**하고, 스크립트는 "어디로·언제 고정할지"만 정함. 에셋 단계의 Aim/Position Constraint는 시작 시 끔.
- 진행: 시작 2초 뒤 **A→B→C→D→E 순차 반복**, 패턴 사이 **1.5초** (기획 최종안은 랜덤 2:3:1:2:2, 미적용). 패턴 사이마다 `OnPatternGap` 이벤트 → 미라 소환.
- **tempo = 1.3**: 시전 애니메이션·예고·대기·투사체 속도 전부에 곱함(30% 빠르게). 아래 시간은 클립 기준 초.
- Cast 길이(클립): A 6.5 / B 6.17 / C 6.57 / D 6.5 / E 5.83초.
- 모든 패턴 피해 1. 탄막은 파라오에게 피해를 주지 않음.

### 6-2. 패턴별 규칙

| 패턴 | 오브젝트 | 전개 | 예고 | 판정 |
|---|---|---|---|---|
| **A 바르한 베기** | 코페쉬(쌍날 낫) | Cast 2.70초까지 플레이어를 조준하다 방향 고정, **20 m/s** 직선 투척. 플레이어 위치를 지나 최대 12m(오버슈트 6m) 날아가 바닥(0.05m 위)에서 깨짐 | 없음(조준 동작) | 반경 0.45m |
| **B 모래 칼날** | 세베크 함정(돌 칼날 3개) | 플레이어 주변 **4칸** 안에 함정 **3개**가 서로 3칸 이상 떨어져 솟음. 60% 확률로 1칸 이내를 노림. 함정 하나 약 2×2.5m | 연두색 바닥 균열 **0.9초** 먼저 표시 후 시전 | 반경 0.6m, 점프 회피 가능 |
| **C 모래폭풍** | 앙크 보석 5개 | 보석마다 젖히는 순간(Cast 2.18/2.70/3.25/3.82/4.37초)의 플레이어 위치로 조준, **28 m/s** 발사. 목표 지점 폭발 | 조준 동작 | 폭발 반경 1.1m, **무적 무시** |
| **D 메테오** | 라의 태양(금빛 구체) | 플레이어 1칸 안에 태양 생성, 3.67초 낙하 시작, 4.08초 바닥에서 깨짐. 반지름 약 2.9m. 파편은 바닥에 남았다가 5.0~6.3초 사이 투명해짐 | 파란 태양 문양 + 균열 + 그림자 (0.3초부터) | 반경 2.4m |
| **E 대검 빔** | 파라오 지팡이 5개 | 플레이어가 선 행 기준 **±2·±4행**에서 지팡이가 격자 +X 방향 직선 발사. 발사 6m 이후 벽·기둥 칸에 닿으면 깨짐 | 줄 정렬·조준 동작 | 반경 0.45m, 점프 회피 가능 |

- 바닥 예고(PatternTelegraph): 어두운 돌 위에 연두색 균열·상형 문양이 번지고 공격이 가까울수록 밝아짐. B는 Crack(크기 0.55→1로 퍼짐), D는 SunSeal(고리 문양 + 균열 + 낙하 그림자).
- 파괴 이펙트(PatternImpactFx): 투사체 재질로 그린 파편이 튀어 구르다 사라짐 + 패턴색 불꽃·섬광(가산) + 어둡고 옅은 먼지. 색: A 붉은 빛, C 초록 빛, E 파란 빛.

### 6-3. 스태프 드랍 (StaffDropper)

패턴마다 정해진 순간에 해당 패턴의 스태프가 떨어집니다.

| 패턴 | 드랍 순간·위치 |
|---|---|
| A | 코페쉬가 깨지는 지점 |
| B | 시전 종료 시 함정 3개 중 1곳 |
| C | 3번째 보석이 깨지는 지점 |
| D | 태양이 깨지는 지점 |
| E | 마지막 지팡이가 플레이어의 X 위치를 지날 때 그 지점 |

- 연출: 12m 높이에서 초속 18, 중력 90으로 빠르게 떨어져 **칸 정중앙에 수직으로 박힘**(깊이 0.12m). 박히는 순간 0.12m 더 들어갔다 0.22초에 튕겨 올라옴 + 충격 파티클, 10°로 0.35초 흔들림.
- 박힌 스태프는 **반투명(알파 0.8)**, 머리 위 0.28m에 **패턴 오브젝트의 작은 복제(0.32m)** 가 떠서 위아래(0.06m)로 움직이며 초당 70° 회전 → 무슨 패턴인지 표시.
- 같은 칸에 이미 있으면 가장 가까운 빈 칸. **10초 뒤 1초간 흐려지며 사라짐**(테스트값, 기획은 무제한).
- 스태프 모델은 플레이어가 드는 **아누비스 지팡이**(머리에 오브를 끼우는 고리가 있음).

---

## 7. 전투 루프: 미라 · 오브 · 공격

### 7-1. 속성 (Element)

| 속성 | enum | 대표색 (RGB, 오브·이펙트·UI 공통) |
|---|---|---|
| 불 Fire | 0 | (1.00, 0.22, 0.02) |
| 물 Water | 1 | (0.02, 0.32, 1.00) |
| 전기 Electric | 2 | (1.00, 0.78, 0.04) |
| 풀 Grass | 3 | (0.12, 0.85, 0.08) |

색은 `ElementInfo.Color(e)` 한 곳에서만 정의하고, 오브·공격·미라 표시·UI가 모두 이 값을 씁니다.

### 7-2. 미라 (MummySpawner, Mummy)

- **소환**: 패턴과 패턴 사이(`OnPatternGap`)마다 **1마리**, 동시에 **최대 5마리**. 파라오 제단 **계단 아래 칸**(파라오에서 가장 가까운 걷기 가능 칸 주변 1칸) 중 하나.
- 소환 연출: 속성색 불꽃이 0.9초 피어오른 뒤 0.6초에 걸쳐 바닥에서 솟아오름.
- **성별(남/여) · 속성 무작위**(속성 각 25%). 머리 위 작은 속성 표시(0.09m, 높이 0.28m), 속성색 발광은 미라 자신에게만.
- **이동**: 플레이어 이동속도의 **25%(약 1 m/s)** 로 플레이어에게 다가감. 걷기 불가 칸·구덩이 회피. 다른 미라와 0.85m 안이면 서로 밀어내 겹치지 않음. 0.45m 안이면 멈춰 붙음. 걷기 재생 배속 = 실제 속도 ÷ 0.16 (최대 6.5배).
- **접촉 피해 1**.
- **오브 드랍**: 살아 있는 동안 **5초마다** 플레이어 주변 **3칸** 안 바닥에 자기 속성 오브. 오브 수명도 **5초**(미라 1마리당 바닥에 오브 1개만 → 우후죽순 방지).
- **처치**: **같은 속성 공격 1회에 즉사, 다른 속성은 무효**. 뒤에서 맞으면 앞으로(Dying), 앞에서 맞으면 뒤로(Death) 쓰러짐(1.5배속) → 2초 유지 → 1.4초간 0.9m 녹아내리며 바닥으로 꺼짐 → **속성색 영혼**이 1.4m 떠올라(0.7초) 파라오에게 날아가(1.0초) **20 피해**.
- 모델: 남/여 각 FBX + 프리팹 + 전용 컨트롤러(`Assets/04.Art/Model/Mummy`), Walk / Death / Dying 클립.

### 7-3. 획득과 공격 (PickupItem, PlayerLoadout, PlayerStaffSkill)

- **획득(E)**: 플레이어 칸과 주변 8칸 안의 가장 가까운 스태프/오브. 같은 종류는 새로 주운 것으로 교체. 획득 모션 재생, 카메라는 강제로 돌지 않음.
- **오브 장착 표시**: 오브를 주우면 손에 든 아누비스 지팡이 머리 고리(로컬 (0, 0.19, 0), 지름 0.088)에 그 오브가 박혀 초당 90° 회전. 공격으로 소비하면 터지며 사라짐.
- **공격(좌클릭)**: 스태프와 오브를 모두 들고 있을 때만. **1회 사용 후 둘 다 소모**. 발사 방향 = 카메라 수평 방향.
- 시전 모션(Blender 제작, 시전 배속 1.6): A `PattenA_Sweep_fast`(1.20초에 발동) · B `PattenB_Trap_fast`(2.10) · C `PattenC_Volley_v3`(0.97) · D `PattenD_SunDrop_fast`(1.30) · E `PattenE_Spear_fast_Robe`(1.40). 서서 쓰면 시전 동안 정지, 이동 중이면 속도 60%, 발동 후 0.35초 뒤 이동 복귀.
- **스태프별 약화 공격** (모양 = 패턴 오브젝트, 색·효과 = 오브 속성, **같은 속성 미라만 맞음**):

| 스태프 | 공격 | 수치 |
|---|---|---|
| A 코페쉬 | 코페쉬가 플레이어 주위를 2바퀴 돌며 근접 공격 | 반경 1.1m, 1.6초, 판정 0.6m |
| B 세베크 함정 | 발밑에 함정 설치, 같은 속성 미라가 밟으면 솟아 공격 | 감지 2.3m, 20초 유지 |
| C 앙크 보석 | 바라보는 방향으로 보석 3연발 | 간격 0.18초, 16 m/s, 사거리 14m, 판정 0.5m |
| D 라의 태양 | 플레이어 위치에 작은 태양 낙하 | 높이 3m, 0.55초 낙하, 반경 1.4m |
| E 파라오 지팡이 | 창 1개 전방 관통 사출 | 20 m/s, 사거리 16m, 판정 0.6m, 길이 2.4m |

- 속성 이펙트(ElementFx): 에셋 없이 코드로 만든 가산 파티클·발광 재질. Flame(소환 불꽃), Burst(피격·영혼 도착 섬광), Trail, Glow, OrbMat.

---

## 8. 스테이지 기믹

### 8-1. 호루스 석상 — 타일 붕괴 (HorusTileCollapse)

- **15초 주기**, 첫 발동 5초. 바닥 무늬 **12종(Tile_Crack_A~L) 중 한 종류**의 칸 전체가 대상(직전과 다른 종류).
- 경고 **2초**: 호루스 석상 눈(지정 좌표 2개)에 연두 발광 + 포인트 라이트, 해당 타일이 은은하게(최대 0.2) 빛나고 균열 발광 4배, 점점 빠르게 깜빡임.
- 붕괴: 사암 파편 6·자갈 12·먼지 1개/칸, 0.25초에 0.35m 꺼지며 사라짐 → **7.5초간 구덩이**(깊이 4m 심연, 구덩이 벽, 바닥 희미한 초록 빛, 올라오는 불씨).
- 수복: 0.5초에 아래에서 올라오며 커짐, 구멍 해제.
- 경고·붕괴·수복 중인 칸은 `IsDoomed(cell)`로 공개 → 아누비스 도착 칸에서 제외.
- 경고색은 바닥 균열 발광색(연두, hue≈90)과 통일.

### 8-2. 아누비스 석상 — 강제 이동 (AnubisTeleport)

- **25초 주기**, 첫 발동 12초.
- 예고 **3초**: 아누비스 석상 눈 발광(호루스와 같은 방식) + 전투장 안 기둥(제단 쪽 제외) 중 하나의 **꼭대기에 초록 불꽃** + 기둥 무늬 발광 4배 + **도착 칸에 마법진**(외곽 원·내곽 원·오각별, 반지름 0.6m)이 차례로 그려지고 회전, 끝무렵 빠르게 깜빡임.
- 도착 칸: 기둥 아래 칸에서 가장 가까운 칸 중 **자신 + 주변 8칸이 모두 서 있을 수 있고 호루스 붕괴 대상이 아닌 칸**. 예고 도중 대상이 되면 마법진을 안전한 칸으로 옮김.
- 강제 이동: 낙하 중이면 끝날 때까지 기다림 → **0.8초**간 불꽃 꼬리를 남기며 포물선(최고 0.6m)으로 끌려감 → 도착하면 바닥에 착지(점프 중이어도). **끌려가는 동안 + 도착 후 0.5초 무적**.
- 중복 발동 방지(진행 중이면 다음 호출 무시), 끝나면 이동 스크립트를 항상 다시 켬.

---

## 9. UI — 양피지 획득 표시 (WeaponUIController)

- 화면 **하단 중앙 양피지 1장**(560×219, 기준 해상도 1920×1080). 왼쪽 = 주운 스태프, 오른쪽 = 장착한 속성 오브. 칸 테두리·구분선·제목 없음.
- 양피지 텍스처 `Assets/04.Art/UI/Parchment/UI_Parchment.png` (1024×400, 코드로 생성): 찢어진 가장자리, 종이 결·얼룩·비네트, **얇은 그을음 + 가장 바깥에 드문드문 은은한 초록 불씨**(바닥 균열 발광색 계열).
- 그림: 렌더 아이콘을 손그림처럼 가공한 `UI_Sketch_*` (11-2 참조). 그림 아래 영어 이름(기울임, 짙은 갈색).
- **연출(InkDraw)**: 새로 주우면
  1. 이전 그림이 0.22초간 번지며(살짝 커지며) 지워짐
  2. 같은 그림의 짙은 갈색 실루엣(먹선)이 **왼쪽→오른쪽으로 0.55초** 펜으로 그리듯 채워짐(펜 떨림 ±1.2°)
  3. 채색이 **0.45초** 스며들고(양피지 톤 틴트) 이름이 써짐. 먹선은 18% 농도로 남아 윤곽 역할
  - 공격으로 소모하면 지워지고 빈 양피지. `PlayerLoadout` 상태를 매 프레임 읽어 바뀐 쪽만 연출(직접 호출 불필요).
  - 대안 연출 `CoinFlip`(Y축 한 바퀴 회전 교체)도 코드에 남아 있음.
- 데이터: `WeaponData`(ScriptableObject) 9개 + `WeaponDatabase` (`Assets/04.Art/UI/WeaponData`).

| 에셋 | 이름 표기 | 아이콘 |
|---|---|---|
| WD_A_Copesh | Copesh | UI_Sketch_Patten_A_Copesh |
| WD_B_Sebeke | Sebeke Trap | UI_Sketch_Patten_B_Sebeke |
| WD_C_Angke | Ankh Gem | UI_Sketch_Patten_C_Angke |
| WD_D_Sun | Sun of Ra | UI_Sketch_Patten_D_Sun |
| WD_E_Staff | Pharaoh's Staff | UI_Sketch_Patten_E_Staff |
| WD_Core_Fire/Water/Electric/Grass | Fire/Water/Electric/Grass Orb | UI_Sketch_Orb_* |

- 임시 디버그 UI(OnGUI): 좌상단 패턴·조작 안내·HP·호루스/아누비스 상태, 화면 중앙 아래 "[E] 줍기" 안내. 정식 하트 HUD는 미구현.

---

## 10. 비주얼 스타일

- **밤의 언데드 신전**: 어두운 돌, 검푸른 수정, 벽·바닥·기둥의 **연두색 균열·상형문자 발광**이 기본 톤. 모든 경고·기믹 발광은 이 연두색 계열로 통일(예외: D 태양은 파랑, A는 붉은 빛).
- 패턴 오브젝트는 청록 파티나 + 금·구리 장식의 이집트 공예품 질감(Blender 제작, base color / normal / ORM / emission 텍스처).
- **속성 오브**: 지름 약 0.26m 구체. 메시 하나에 서브메시 3개:
  - 눈 판 `Orb_<E>_Shell`(불투명, 호루스의 눈 무늬 텍스처, 바탕 0.6 회색으로 어둡게)
  - 안쪽 구 `Orb_<E>_Glow`(1.18 크기, 일반 Alpha 블렌드, 알파 0.55, 발광 = 속성색 ×0.6)
  - 바깥 껍질 `Orb_<E>_Core`(1.33 크기, 알파 0.55, 어두운 속성색, 반사·스펙큘러 끔)
  - 중요: URP 투명 재질의 **Preserve Specular를 끄고**(`_BlendModePreserveSpecular = 0`) 일반 Alpha 블렌드로 둬야 발광이 투명도만큼 줄어 Blender처럼 진한 색이 나옴. 켜져 있으면 하얗게 뜸. 그리기 순서 Glow 3000 → Core 3001.
  - 프리팹 `Assets/Resources/Orbs/Orb_<Element>.prefab` (`Resources.Load("Orbs/Orb_"+이름)`), 위아래 0.05m로 떠다니며 회전(OrbMotion).

---

## 11. 에셋 파이프라인

### 11-1. Blender → Unity

- Blender 파일: `Desktop\Blender\History_crash\Object\`
  - `Entity\Player.blend`, `Entity\mummy.blend` (캐릭터·애니메이션)
  - `Enviorment\Enviorment_Obj.blend` (패턴 오브젝트 5종, 아누비스 지팡이, elemental orb 4종; 컬렉션 Patten / Item)
  - 텍스처: `Object\Item\<이름>\`, `Object\Patten\<이름>\` (`_base_color`, `_normal`, `_orm`, `_emission`, `_coat`)
- 내보내기: 백그라운드 실행 `blender.exe -b 파일.blend --python 스크립트.py`로 FBX 출력.
- 오브 속성 변형: 불 오브를 복제하고 Hue/Saturation 노드로 색만 바꿔 물·전기·풀 제작.
- 플레이어 애니메이션: Mixamo 클립(달리기, 줍기 등) + Blender에서 만든 공격 모션 5종, 이동 클립은 같은 모델 하나에 대한 애니메이션으로 통일(옆걸음은 자연스러운 옆 스텝으로 수정).

### 11-2. UI 아이콘 만들기

1. Blender 렌더 전용 임시 씬(원래 씬 설정을 건드리지 않음): EEVEE, **투명 배경 512×512**, 직교 카메라, 키·필·림 에어리어 라이트, AgX.
2. 패턴 5종(정면에서 약간 위), 오브 4종(정면, 호루스의 눈이 보이게) 렌더 → `Assets/04.Art/UI/Icon/UI_Patten_*`, `UI_Orb_*`.
3. Unity 에디터 코드로 손그림 버전 생성 → `UI_Sketch_*`:
   - 밝기·외곽 Sobel 에지로 짙은 갈색 먹선(펄린 노이즈로 굵기 흔들림)
   - 채도 35% 낮춤 + 세피아 35% + 색 6단계 양자화
   - 어두운 곳에 7px 간격 사선 해칭
   - 칠 가장자리를 노이즈로 살짝 덜 채움(물감 번짐)
4. 임포트 설정(9종 공통): Sprite(Single, **Full Rect**), sRGB, **Alpha Is Transparency**, **밉맵 끔**, Clamp, Bilinear, 최대 512, High Quality(PC **BC7**). 아틀라스 `UI_Icon_Atlas.spriteatlas`(회전·Tight 패킹 끔, 여백 4).

---

## 12. 스크립트 목록 (Assets/02.Scripts, 1파일 1클래스)

| 폴더 (네임스페이스) | 파일 | 역할 |
|---|---|---|
| Player (`HistoryCrash.Player`) | PlayerMover | 이동·달리기·점프·낙하 |
| | PlayerHealth | HP·무적 |
| | PlayerAnimator | 방향별 이동 애니메이션, 모델 방향 |
| | PlayerLoadout | 획득·공격·지팡이 홀 오브 |
| Camera (`HistoryCrash.CameraRig`) | ThirdPersonCamera | 3인칭 카메라 |
| Boss (`HistoryCrash.Boss`) | BossPatternDirector | 패턴 A~E 진행·조준·피격 판정, 드랍·소환 신호 |
| | BossPattern | 패턴 enum (A~E) |
| | PatternFlight | A·C 투사체 직선 비행 상태 |
| | PatternHitBox | 패턴 피격 판정 상자 정보 |
| | PatternTelegraph / TelegraphStyle | 바닥 예고(B 균열, D 태양 문양) / 예고 종류 enum |
| | PatternImpactFx | 투사체 파괴 이펙트 |
| | StaffDropper / StaffDropEntry | 스태프 낙하·박힘·아이콘·소멸 / 떨어진 스태프 1개 상태 |
| | PharaohHealth | 파라오 HP·HP 바 |
| Enemy (`HistoryCrash.Enemy`) | Mummy / MummyGender | 미라 AI·오브 드랍·죽음·영혼 / 성별 enum |
| | MummySpawner | 미라 소환 |
| | ElementOrb | 속성 오브 바닥 생성(Resources 프리팹) |
| | OrbMotion | 오브 떠다님·회전·수명 |
| Stage (`HistoryCrash.Stage`) | TileGrid | 격자 생성, 이동 가능·구멍·안전 칸 조회 |
| Stage/Gimmick | HorusTileCollapse / HorusTile | 호루스 타일 붕괴 / 붕괴 대상 타일 1칸 정보 |
| | AnubisTeleport | 아누비스 강제 이동 |
| Combat (`HistoryCrash.Combat`) | PickupItem / PickupKind | 바닥 아이템·칸 점유 / 스태프·오브 구분 enum |
| | PlayerStaffSkill / SkillMagicCircle | 스태프별 플레이어 공격 A~E / B 함정 마법진 연출 |
| | ElementFx | 속성 파티클·발광 재질(코드 생성) |
| | PlayerBolt | (미사용) 초기 직선 공격체 |
| Data (`HistoryCrash.Data`) | Element / ElementInfo | 속성 enum / 속성 색·이름 |
| | WeaponData / WeaponDatabase | UI 아이콘·이름·대표색 데이터 / 9종 조회 |
| | WeaponGroup / PatternId | 무기 분류 enum / 패턴 무기 번호 enum |
| UI (`HistoryCrash.UI`) | WeaponUIController | 양피지 획득 표시 UI |
| | WeaponUISlot / WeaponUIStyle | 그림 영역 1칸 참조 / 연출 종류 enum |

### 12-1. 에셋 폴더 구조

```
Assets/
├── 01.Scenes/        SampleScene(전투), Temple_defalt
├── 02.Scripts/       Player, Camera, Boss, Enemy, Stage(/Gimmick), Combat, Data, UI
├── 03.Prefabs/
├── 04.Art/           Model(Player_Robe, Mummy, Orb, Patten, Enviorment …), UI(Icon, Parchment, WeaponData), Particle, Map_HDRI
├── Resources/Orbs/   Orb_Fire/Water/Electric/Grass 프리팹 (Resources.Load 경로라 번호 없음)
└── HistoryCrash_Bake/, Settings/  (베이크·URP 설정)
```

---

## 13. 처음부터 다시 만드는 순서 (권장)

1. Unity 6 URP 프로젝트(Linear, 새 Input System). 신전 맵을 바닥 타일 그룹 + 장애물 그룹으로 나눠 배치.
2. **TileGrid** — 바닥 렌더러로 격자, 장애물로 막힘 칸. 디버그로 칸 표시해서 검증.
3. **PlayerMover + ThirdPersonCamera** — 카메라 기준 이동, 벽 미끄러짐, 점프.
4. **PlayerAnimator** — 2D 블렌드트리와 보폭 맞춤 배속.
5. **BossPatternDirector** — 패턴 하나씩(A부터) "클립 재생 + 조준 고정 + 직선 비행 + 거리 판정". tempo 1.3.
6. **PatternTelegraph / PatternImpactFx** — 예고와 파괴 연출.
7. **PlayerHealth** 연결, 점프 회피(0.45m) 규칙.
8. **HorusTileCollapse** — 구멍 + 낙하 복귀.
9. **StaffDropper + PickupItem + PlayerLoadout** — 드랍·획득.
10. **MummySpawner + Mummy + 오브** — 패턴 사이 소환, 오브 드랍.
11. **PlayerStaffSkill** — 같은 속성만 맞는 약화 공격, 영혼 → **PharaohHealth**.
12. **AnubisTeleport** — 호루스 붕괴 칸 회피, 점프 중 착지 처리.
13. **UI** — 아이콘 렌더 → 손그림 가공 → 양피지 HUD.
14. 남은 일(14장).

---

## 14. 미구현 / 확정 필요

- 실패(플레이어 HP 0)·클리어(파라오 HP 0) 처리, 정화 연출(피라미드 복원·소환 몹 소멸·모래바람 해제)
- 모래바람(화면 가림 + 모래 패턴)
- 하트 HP·경과 시간 등 정식 HUD (현재 OnGUI 디버그)
- 우클릭 오브 제거
- 패턴 랜덤 비율 2:3:1:2:2 적용 여부
- 스태프 유지 시간(기획 무제한 vs 테스트 10초), 오브 유지 시간(기획 12초 vs 테스트 5초)
- 공격 조준 방식(기획: 마우스 커서 / 현재: 카메라 방향)
- 아누비스 예고(기획 v19: 경고 제거 / 현재: 마법진 3초 예고)

---

## 15. 개발 중 알아둘 함정

- **플레이 중 스크립트 재컴파일 금지**: 도메인 리로드 후 시전 상태가 멈추거나 머티리얼 블록이 null이 됨. 항상 플레이를 끄고 수정 → 컴파일 → 다시 플레이.
- **실행 순서**: TileGrid는 반드시 다른 스크립트보다 먼저(`DefaultExecutionOrder(-100)`).
- **Cloth 핀 가중치**는 rootBone 공간 높이로 계산해야 함(월드/로컬 메시 공간으로 하면 엉뚱한 곳이 고정됨).
- **파티클 velocityOverLifetime**은 x·y·z 세 축의 커브 모드를 같게(TwoConstants 등) 둬야 에러가 안 남.
- **URP 투명 재질 발광**은 Preserve Specular를 끄지 않으면 투명도와 무관하게 더해져 하얗게 뜸(10장).
- **Blender 텍스처 폴더를 옮기면** blend 안의 이미지 경로가 끊겨 분홍색으로 렌더됨 → 경로를 새 위치로 재지정 후 저장.
- **강제 이동처럼 이동 스크립트를 끄는 코드**는 중복 실행 방지와 "항상 다시 켜기"를 같이 넣어야 함(겹치면 꺼진 채 굳음).
- 오브 발광은 "밝게"보다 **색 농도**가 핵심: 발광을 올리면 파스텔로 뜨고, 바깥 껍질 알파로 진하기를 조절.

### 15-1. 코딩 컨벤션 (2026-11-17 리팩토링 적용)

- **1파일 1클래스**: 중첩 클래스·enum도 모두 별도 파일로 분리(소속을 알 수 있는 이름으로, 예: `Mummy.Gender` → `MummyGender`). 파일 이름 = 클래스 이름.
- **변수(필드·지역 변수·매개변수)**: `_camelCase` (예: `_moveSpeed`, `_targetPos`, 매개변수 `_damageAmount`).
- **클래스·메서드·프로퍼티**: PascalCase. **상수**: `UPPER_SNAKE` (예: `A_RADIUS`, `C_SUMMON_LEAD`).
- **필드는 모두 private**. 인스펙터 노출은 `[SerializeField] private`. 다른 클래스가 쓰는 값은 같은 이름의 PascalCase 프로퍼티로 공개(예: `_moveSpeed` ↔ `MoveSpeed`).
- 접근 제한자 생략 금지 (`private void Update()`).
- 이름을 바꾼 직렬화 필드에는 `[FormerlySerializedAs("옛이름")]`을 달아 씬·에셋에 저장된 값을 유지. 씬·에셋을 한 번 저장하면 새 이름으로 다시 기록됨.
- 요약 주석은 `/// <summary>`, 할 일은 `// TODO: 내용 (작성자)`.
- 리팩토링 방법: Roslyn(Unity 내장 `Microsoft.CodeAnalysis`)으로 심볼 단위 이름 변경·클래스 분리를 자동 처리 → 원본·결과 모두 컴파일 오류 0 확인 → 스크립트는 `AssetDatabase.MoveAsset`으로 옮겨 GUID(씬의 컴포넌트 연결)를 유지.
