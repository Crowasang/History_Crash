# History Crash — Claude guide

Team Unity game. Unity 6000.3, URP, New Input System.
- Unity project: `History_Crash/`, scripts: `History_Crash/Assets/Scripts/`
- Docs: `docs/` (design: `HistoryCrash_기획안_메인_v20.md`, `HistoryCrash_구현상세_v20.md`)
- **Write replies, code comments and commit messages in Korean.**

## Rule #1: never rebuild existing systems

Do NOT create new managers, health, player movement, game flow, pause, save, object pool, scene loading or sound.
They exist in `Scripts/Core`, `Scripts/Player`, `Scripts/Camera`. Read [docs/CORE_SYSTEM.md](docs/CORE_SYSTEM.md) before coding and use what is there.

| Need | Use |
|---|---|
| Game state (Ready/Playing/Clear/Fail), elapsed time | `IGameFlow` (`GameManager`) |
| Pause / open-close settings | subscribe `IGameFlow.IsPaused`, `IPauseControl.Resume()` |
| Deal damage / read HP / invincibility | `IDamageable` / `IHealthReadOnly` / `IInvincible` (`Health`) |
| Player HP / boss HP for UI | `IGameFlow.PlayerHealth` / `BossHealth` |
| Jump-dodge check | `IJumpState.IsDodgingByJump()` |
| Forced move (Anubis) | `IForcedMovable.TryForceMove()` |
| Teleport / input lock / speed | `IPlayerTeleport` / `IMovementLock` / `ISpeedModifier` |
| Pits | just disable the floor tile (`FallZone` handles fall + respawn) |
| Save/load, first-run intro | `ISaveService`, `IIntroProgress` (`SaveManager`) |
| Frequently spawned objects | `IPoolService` + `PooledObject` |
| Scene change / sound | `ISceneLoader` (`SceneLoader.Instance` after a scene change) / `SoundManager.Instance` |
| Managers in a scene | place only `Assets/Prefabs/Managers.prefab` |

- Missing a feature? Don't build a look-alike. **Tell the user first: "existing code needs this feature added".**
- `Scripts/Core`, `Scripts/Player`, `Scripts/Camera` are owned by KDH. Ask the user before changing them.
- Player = `PlayerController` family. `JMC_Script/02.Scripts/Player` `PlayerMover`/`PlayerHealth` are test-only; don't use them in new code (migration: CORE_SYSTEM.md ch. 9).
- Scenes (`Assets/Scenes/`): `MainMenu` (start) → `Game` → `KDH_Test` (KDH test scene).

## Code convention ([docs/CODING_CONVENTION.md](docs/CODING_CONVENTION.md))

| Target | Rule | Example |
|---|---|---|
| Fields, locals, parameters | `_camelCase` | `_moveSpeed`, `_damageAmount` |
| Classes, methods, properties | `PascalCase` | `PlayerController`, `TakeDamage()` |
| Constants | `UPPER_SNAKE_CASE` | `MAX_HP` |

- **No `Update` / `FixedUpdate`.** Input: Input System `performed` events. Value changes: R3 subscriptions. Waiting: UniTask. Smooth changes: DOTween.
- Take other scripts as interfaces, not concrete classes. Inspector link: `MonoBehaviour` field + `InterfaceRef.Resolve<T>()`.
- Frequently created/destroyed objects: object pool, not `Instantiate/Destroy`.
- File name = class name, one class per file.
- `///` summary on public methods when useful. TODO format: `// TODO: 내용 (작성자)`.
- Check Unity has no errors before committing.

## Git convention ([docs/GIT_CONVENTION.md](docs/GIT_CONVENTION.md))

- Branches: `main` (stable), `dev` (integration). Always branch from `dev`: `feat/name`, `fix/name`, `doc/name` (lowercase, hyphens, e.g. `feat/player-dash`).
- Commits: `feat: 한글 내용`, `fix: 한글 내용`, `doc: 한글 내용` (space after colon, Korean, one task per commit).
- `feat`/`fix`/`doc` branches merge into `dev`; `main` merges only from `dev`.
- Never commit: `ProjectSettings` changes, `Assets/Art`, `Assets/Resources` (assets are shared separately), DOTween / NuGet plugin folders.
- Don't edit the same scene with others at the same time; work in your own test scene.
- Commit, push and merge only when the user asks.
