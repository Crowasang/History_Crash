# 코딩 컨벤션 (Unity C#)

팀원 모두가 같은 규칙으로 코드를 작성하기 위한 문서입니다.
애매한 경우에는 이 문서를 기준으로 하고, 규칙을 바꿀 때는 팀원과 합의 후 이 문서를 먼저 수정합니다.

---

## 1. 네이밍 규칙 요약

| 대상 | 규칙 | 예시 |
|------|------|------|
| 변수 (필드, 지역 변수, 매개변수) | camelCase (소문자로 시작) | `moveSpeed`, `currentHp`, `targetPos` |
| 클래스 | PascalCase (대문자로 시작) | `PlayerController`, `ItemData` |
| 메서드 | PascalCase (대문자로 시작) | `TakeDamage()`, `MovePlayer()` |
| 상수 | 전부 대문자, 단어 사이는 `_` | `MAX_HP`, `MAX_INVENTORY_SIZE` |

### 예시

```csharp
public class PlayerController : MonoBehaviour
{
    public const int MAX_HP = 100;

    [SerializeField] private float moveSpeed = 5f;

    private int currentHp;
    private bool isDead;
    private Rigidbody2D rigidbody2d;

    private void Awake()
    {
        rigidbody2d = GetComponent<Rigidbody2D>();
        currentHp = MAX_HP;
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead) return;

        currentHp -= damageAmount;
        if (currentHp <= 0)
        {
            isDead = true;
        }
    }
}
```

---

## 2. 파일 / 폴더 규칙

- 파일 이름은 클래스 이름과 똑같이 맞춥니다. (`PlayerController.cs` 안에는 `PlayerController` 클래스)
- 한 파일에는 클래스 하나만 작성합니다.
- 폴더 구조

```
Assets/
├── 01.Scenes/
├── 02.Scripts/
│   ├── Player/
│   ├── Enemy/
│   ├── UI/
│   ├── Manager/
│   └── Data/
├── 03.Prefabs/
├── 04.Art/
└── Resources/
```

> `Resources` 폴더는 이름이 정확히 `Resources`여야 `Resources.Load()`가 동작하므로 번호를 붙이지 않습니다.

---

## 3. 주석 규칙

- public 메서드에는 필요하면 `///` 요약 주석을 작성합니다.
- 할 일은 `// TODO: 내용 (작성자)` 형식으로 남깁니다.

```csharp
/// <summary>
/// 플레이어에게 데미지를 주고, 체력이 0 이하가 되면 사망 처리합니다.
/// </summary>
public void TakeDamage(int damageAmount) { ... }

// TODO: 넉백 처리 추가 (홍길동)
```

---

## 4. 커밋 메시지 규칙

형식: `타입: 내용` (내용은 한글로 간단하게)

| 타입 | 의미 | 예시 |
|------|------|------|
| `feat` | 새 기능 추가 | `feat: 플레이어 대시 기능 추가` |
| `fix` | 버그 수정 | `fix: 점프 중 이중 점프 되는 문제 수정` |
| `docs` | 문서 수정 | `docs: 코딩 컨벤션 수정` |

- 한 커밋에는 한 가지 작업만 담습니다.
- 커밋 전에 Unity에서 에러가 없는지 확인합니다.

---

## 5. 브랜치 규칙

| 브랜치 | 용도 |
|--------|------|
| `main` | 항상 실행 가능한 안정 버전 |
| `dev` | 개발 통합 브랜치 |
| `feature/기능이름` | 기능 개발 (예: `feature/player-dash`) |
| `fix/버그이름` | 버그 수정 (예: `fix/jump-bug`) |

- `main`과 `dev`에는 직접 푸시하지 않고 Pull Request로 합칩니다.
- `feature`, `fix` 브랜치는 `dev`로 합치고, `main`에는 `dev`에서만 합칩니다.
- 같은 씬 파일을 여러 명이 동시에 수정하지 않도록 작업 전에 미리 공유합니다. (씬 충돌은 해결이 어렵습니다.)
