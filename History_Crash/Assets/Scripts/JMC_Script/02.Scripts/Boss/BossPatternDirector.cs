using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Boss
{
    /// <summary>
    /// 패턴 A~E 런타임 시뮬레이션.
    /// 연출(소환·회전·파티클·라이트)은 기존 Animator 클립을 그대로 쓰고,
    /// "어디로, 언제 고정할지"만 스크립트가 정한다. 에셋 단계의 Constraint/AimLock은 시작 시 끈다.
    ///
    /// A: 던지는 순간(2.70s)까지 Player를 조준, 그 순간 방향 고정 + 비행 거리를 Player 거리에 맞춤
    /// B: 시전 시작 시 Player 주변 4칸 안 무작위 걷기 가능 타일에서 솟음
    /// C: 보석마다 젖히는 순간(2.18~4.37s)의 Player 위치로 조준·거리 맞춤
    /// D: 시전 시작 시 Player 주변 2칸 안 무작위 타일 위에 생성, 4.08s 낙하
    /// E: 시전 시작 시 Player가 선 타일 줄(행) 기준 ±2·±4줄 직선 발사
    /// </summary>
    public class BossPatternDirector : MonoBehaviour
    {

        [Header("참조")]
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        [SerializeField, FormerlySerializedAs("player")] private Transform _player;
        [SerializeField, FormerlySerializedAs("playerModel")] private Transform _playerModel;
        [SerializeField, FormerlySerializedAs("pattensRoot")] private Transform _pattensRoot;
        [SerializeField, FormerlySerializedAs("pharaoh")] private Transform _pharaoh;
        [SerializeField, FormerlySerializedAs("pharaohAnimator")] private Animator _pharaohAnimator;
        [SerializeField, FormerlySerializedAs("staffDropper"), Tooltip("패턴이 끝난 자리에 스태프를 떨어뜨림(A 파괴 지점, B 함정 중 1곳, C 3번째 보석 파괴 지점, D 낙하 지점, E 마지막 지팡이가 Player X를 지나는 지점)")] private StaffDropper _staffDropper;
        /// <summary>패턴 하나가 끝나고 다음 패턴 전 대기에 들어갈 때</summary>
        public event System.Action OnPatternGap;

        [Header("진행")]
        [SerializeField, FormerlySerializedAs("autoLoop")] private bool _autoLoop = true;
        [SerializeField, FormerlySerializedAs("startDelay")] private float _startDelay = 2f;
        [SerializeField, FormerlySerializedAs("gapBetweenPatterns")] private float _gapBetweenPatterns = 1.5f;
        [SerializeField, FormerlySerializedAs("tempo"), Tooltip("패턴 전개 속도 배율. 시전 애니메이션(파라오·패턴), 예고·대기 시간, 투사체 비행 속도에 함께 곱한다(1.3 = 30% 빠르게)"), Range(0.5f, 2f)] private float _tempo = 1.3f;
        [SerializeField, FormerlySerializedAs("castLength"), Tooltip("A,B,C,D,E Cast 길이(초)")] private float[] _castLength = { 6.5f, 6.17f, 6.57f, 6.5f, 5.83f };
        [SerializeField, FormerlySerializedAs("pharaohTurnSpeed")] private float _pharaohTurnSpeed = 120f;

        [Header("타이밍(Cast 기준 초)")]
        [SerializeField, FormerlySerializedAs("aLockTime")] private float _aLockTime = 2.70f;
        [SerializeField, FormerlySerializedAs("cLockTimes")] private float[] _cLockTimes = { 2.18f, 2.70f, 3.25f, 3.82f, 4.37f };
        [SerializeField, FormerlySerializedAs("authoredHitDistance"), Tooltip("클립에서 투사체가 Player에 닿는 로컬 거리(A·C 공통)")] private float _authoredHitDistance = 33.54f;

        [Header("타일 규칙")]
        [SerializeField, FormerlySerializedAs("bRadius")] private int _bRadius = 4;
        [SerializeField, FormerlySerializedAs("bNearChance"), Tooltip("B가 Player 바로 주변(bNearRadius칸)을 노릴 확률"), Range(0, 1)] private float _bNearChance = 0.6f;
        [SerializeField, FormerlySerializedAs("bNearRadius")] private int _bNearRadius = 1;
        [SerializeField, FormerlySerializedAs("bTrapCount"), Tooltip("B 함정 개수(Player 주변에 동시에 솟음)")] private int _bTrapCount = 3;
        [SerializeField, FormerlySerializedAs("bMinSpacing"), Tooltip("B 함정끼리 최소 간격(칸). 함정 하나가 약 2×2.5m라 3칸이면 겹치지 않음")] private int _bMinSpacing = 3;
        [SerializeField, FormerlySerializedAs("dLandRadius"), Tooltip("D 태양이 떨어지는 칸: Player 칸에서 이 칸수 안(태양 반지름이 약 3칸이라 1이면 가만히 있으면 맞음)")] private int _dLandRadius = 1;

        [Header("바닥 예고 표시")]
        [SerializeField, FormerlySerializedAs("bWarnTime"), Tooltip("B 함정이 솟기 전에 예고만 보여 주는 시간(이 시간 뒤 시전 시작)")] private float _bWarnTime = 0.9f;
        [SerializeField, Tooltip("예고 테두리 색(샘플 나2 연두)"), ColorUsage(false, true)] private Color _rangeLineColor = new Color(0.40f, 1.5f, 0.10f);
        [SerializeField, Tooltip("예고 균열 색(테두리와 같은 계열의 밝은 색)"), ColorUsage(false, true)] private Color _rangeCrackColor = new Color(0.66f, 2.1f, 0.22f);
        [SerializeField, Tooltip("A 예고가 나타나는 시점(Cast 기준 초)")] private float _aWarnStart = 0.9f;
        [SerializeField, Tooltip("A 코페쉬가 발사되는 시점(Cast 기준 초, 클립 측정값)")] private float _aLaunchTime = 2.77f;
        [SerializeField, Tooltip("C 보석 5개가 발사되는 시점(Cast 기준 초, 클립 측정값)")] private float[] _cLaunchTimes = { 2.67f, 3.22f, 3.77f, 4.33f, 4.88f };
        [SerializeField, Tooltip("C 첫 보석 예고가 발사보다 앞서 나타나는 시간(나머지는 앞 보석이 발사될 때)")] private float _cWarnLead = 0.9f;
        [SerializeField, Tooltip("E 지팡이 5개가 발사되는 시점(Cast 기준 초, 클립 측정값)")] private float[] _eLaunchTimes = { 1.73f, 2.32f, 2.98f, 2.18f, 3.48f };
        [SerializeField, Tooltip("E 지팡이 5개의 줄(Player 줄 기준 칸, 클립 측정값)")] private int[] _eLaneCells = { -3, 0, 3, -6, 6 };
        [SerializeField, Tooltip("E 예고가 발사보다 앞서 나타나는 시간")] private float _eWarnLead = 1.2f;
        [SerializeField, Tooltip("E 지팡이 폭과 길이(m)")] private Vector2 _eStaffSize = new Vector2(2.34f, 9.74f);
        [SerializeField, FormerlySerializedAs("dShardFadeStart"), Tooltip("D 파편이 서서히 투명해지기 시작/끝 시점(Cast 기준 초)")] private float _dShardFadeStart = 5.0f;
        [SerializeField, FormerlySerializedAs("dShardFadeEnd")] private float _dShardFadeEnd = 6.3f;
        [SerializeField, FormerlySerializedAs("dWarnStart"), Tooltip("D 예고가 나타나기 시작하는 시점(Cast 기준 초)")] private float _dWarnStart = 0.3f;

        [Header("패턴 D 바닥 충돌(월드 m, 클립 측정값)")]
        [SerializeField, FormerlySerializedAs("dShardRestDepth"), Tooltip("파편이 쉬는 바닥이 La_Sun_PattenD_Root보다 이만큼 아래")] private float _dShardRestDepth = 1.391f;
        [SerializeField, FormerlySerializedAs("dSunBottomAtBreak"), Tooltip("깨지는 순간 구체 바닥이 La_Sun_PattenD_Root보다 이만큼 위")] private float _dSunBottomAtBreak = 0.089f;
        [SerializeField, FormerlySerializedAs("dFallStart")] private float _dFallStart = 3.67f;
        [SerializeField, FormerlySerializedAs("dBreakTime")] private float _dBreakTime = 4.08f;
        [SerializeField, FormerlySerializedAs("dShardBlend"), Tooltip("깨진 뒤 파편 시작 높이를 원래 궤적으로 되돌리는 시간")] private float _dShardBlend = 0.6f;

        [Header("피격 판정(m)")]
        [SerializeField, FormerlySerializedAs("projectileHitRadius")] private float _projectileHitRadius = 0.45f;
        [SerializeField, FormerlySerializedAs("sunHitRadius")] private float _sunHitRadius = 2.4f;
        [SerializeField, FormerlySerializedAs("trapHitRadius")] private float _trapHitRadius = 0.6f;
        [SerializeField, FormerlySerializedAs("dodgeHeight"), Tooltip("이 높이 이상 떠 있으면 B·E를 피함")] private float _dodgeHeight = 0.45f;
        [SerializeField, FormerlySerializedAs("cIgnoreInvulnerability"), Tooltip("C 보석은 피격 후 무적을 무시하고 맞을 때마다 데미지")] private bool _cIgnoreInvulnerability = true;

        // 런타임 상태
        private Animator[] _anims = new Animator[5];
        private Transform[] _roots = new Transform[5];
        private Transform _copesh, _sunRoot, _vfxD, _sun;
        private Transform[] _shards;
        private IJumpState _jumpState;   // 점프 회피 판정 (PlayerController)
        private Transform[] _gemPivots = new Transform[5], _gems = new Transform[5];
        private Transform[] _staffs = new Transform[5];
        private float _kA = 1f; private float[] _kC = { 1, 1, 1, 1, 1 };
        private bool _aLocked; private bool[] _cLocked = new bool[5];

        [Header("사출 속도(난이도). 발사 순간부터 스크립트가 직접 날림")]
        [SerializeField, FormerlySerializedAs("aFlightSpeed"), Tooltip("A 코페쉬 비행 속도(m/s). 클립 원래 속도는 약 7")] private float _aFlightSpeed = 20f;
        [SerializeField, FormerlySerializedAs("aOvershoot"), Tooltip("A가 Player 위치를 지나 더 날아가는 거리(m)")] private float _aOvershoot = 6f;
        [SerializeField, FormerlySerializedAs("cFlightSpeed"), Tooltip("C 보석 비행 속도(m/s). 클립 원래 속도는 약 17")] private float _cFlightSpeed = 28f;
        [SerializeField, FormerlySerializedAs("launchDetectZ"), Tooltip("발사로 판단하는 로컬 z 감소량(클립에서 앞으로 나가기 시작하는 순간)")] private float _launchDetectZ = 1.0f;

        [Header("착탄·파괴")]
        [SerializeField, FormerlySerializedAs("aFloorClearance"), Tooltip("A가 깨지는 바닥 위 높이(m)")] private float _aFloorClearance = 0.05f;
        [SerializeField, FormerlySerializedAs("aMaxPastPlayer"), Tooltip("A가 Player 위치를 지나 바닥까지 날아가는 최대 거리(m)")] private float _aMaxPastPlayer = 12f;
        [SerializeField, FormerlySerializedAs("cBlastRadius"), Tooltip("C 보석 폭발 반경(m). 목표 위치에서 이 안에 있으면 맞음")] private float _cBlastRadius = 1.1f;
        [SerializeField, FormerlySerializedAs("eArmDistance"), Tooltip("E 지팡이가 발사 뒤 이 거리(m)를 지나면 벽·기둥(걷기 불가 칸)에 닿을 때 깨짐(제단 위 출발 구간 제외)")] private float _eArmDistance = 6f;
        [SerializeField, FormerlySerializedAs("eTipOffset"), Tooltip("E 지팡이 끝까지의 거리(m)")] private float _eTipOffset = 0.6f;
        [SerializeField, FormerlySerializedAs("aImpactColor"), ColorUsage(false, true)] private Color _aImpactColor = new Color(2.4f, 0.55f, 0.3f);
        [SerializeField, FormerlySerializedAs("cImpactColor"), ColorUsage(false, true)] private Color _cImpactColor = new Color(0.6f, 2.2f, 0.5f);
        [SerializeField, FormerlySerializedAs("eImpactColor"), ColorUsage(false, true)] private Color _eImpactColor = new Color(0.4f, 0.95f, 2.6f);
        private readonly PatternFlight _flightA = new PatternFlight();
        private readonly PatternFlight[] _flightC = { new PatternFlight(), new PatternFlight(), new PatternFlight(), new PatternFlight(), new PatternFlight() };
        private int _current = -1; private float _castTime;
        private int _queued = -1;
        private bool[] _hitFlags = new bool[8];
        public int HitCount { get; private set; }
        private float _flashUntil;
        private MaterialPropertyBlock _mpb;
        private Renderer _playerRenderer;
        private string _lastHit = "";

        private static readonly string[] _rootNames = { "Patten_A_Copesh", "Patten_B_SebekeTrap", "Patten_C_AngkeGem", "Patten_D_LaSun", "Patten_E_ParaoStaff" };
        private static readonly string[] _pharaohStates = { "PattenA_Cast", "PattenB_Cast", "PattenC_Cast", "PattenD_Cast", "PattenE_Cast" };

        private void Awake()
        {
            for (int _i = 0; _i < 5; _i++)
            {
                _roots[_i] = _pattensRoot.Find(_rootNames[_i]);
                _anims[_i] = _roots[_i].GetComponent<Animator>();
            }
            _copesh = _roots[0].Find("Copesh_PattenA");
            _sunRoot = _roots[3].Find("La_Sun_PattenD_Root");
            _vfxD = _roots[3].Find("VFX_PattenD");
            _sun = _sunRoot.Find("La_Sun_PattenD");
            var _shardList = new System.Collections.Generic.List<Transform>();
            foreach (Transform _t in _sunRoot) if (_t.name.Contains("shard")) _shardList.Add(_t);
            _shards = _shardList.ToArray();
            _jumpState = _player.GetComponent<IJumpState>();
            for (int _i = 0; _i < 5; _i++)
            {
                string _suf = _i == 0 ? "" : ".00" + _i;
                _gemPivots[_i] = _roots[2].Find("Angke_Gem_PattenC_Aim" + _suf);
                _gems[_i] = _gemPivots[_i].Find("Angke_Gem_PattenC" + _suf);
                _staffs[_i] = _roots[4].Find("parao_Staff_PattenE" + _suf);
            }
            // B: 함정 bTrapCount개가 동시에 솟도록 패턴 B 루트를 통째로 복제한다(Animator·VFX 포함)
            _bRoots = new Transform[Mathf.Max(1, _bTrapCount)];
            _bAnims = new Animator[_bRoots.Length];
            _bRoots[0] = _roots[1]; _bAnims[0] = _anims[1];
            for (int _k = 1; _k < _bRoots.Length; _k++)
            {
                var _clone = Instantiate(_roots[1].gameObject, _roots[1].parent);
                _clone.name = _rootNames[1] + "_" + (_k + 1);
                _bRoots[_k] = _clone.transform; _bAnims[_k] = _clone.GetComponent<Animator>();
            }
            BuildHitBoxes();   // 보석·창·함정 참조를 잡은 뒤에 만든다

            // 에셋 단계의 Constraint 추적을 끄고 스크립트가 직접 정한다
            foreach (var _b in _pattensRoot.GetComponentsInChildren<Behaviour>(true))
                if (_b is IConstraint) Destroy(_b);
            foreach (var _a in AllAnimators())
            {
                int _li = _a.GetLayerIndex("AimLock");
                if (_li >= 0) _a.SetLayerWeight(_li, 0f);
                HoldIdle(_a);
            }
            // D는 대기 클립에서 구체가 보이는 크기(9.69)이고 파편도 시전 후 바닥에 남으므로, 시전 중에만 보이게 한다
            _sunRoot.gameObject.SetActive(false);
            if (_playerModel) _playerRenderer = _playerModel.GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void Start() { StartCoroutine(Loop()); }

        // ---------- 패턴 B: 함정 여러 개 ----------
        private Transform[] _bRoots; private Animator[] _bAnims;

        private System.Collections.Generic.IEnumerable<Animator> AllAnimators()
        {
            for (int _i = 0; _i < 5; _i++) if (_i != (int)BossPattern.B) yield return _anims[_i];
            foreach (var _a in _bAnims) yield return _a;
        }

        private Animator[] CastAnimators(int _p) { return _p == (int)BossPattern.B ? _bAnims : new[] { _anims[_p] }; }

        /// <summary>Player 주변에서 서로 bMinSpacing칸 이상 떨어진 걷기 가능 칸 count개. 하나는 bNearChance 확률로 Player 바로 옆.</summary>
        private System.Collections.Generic.List<Vector2Int> PickTrapCells(Vector2Int _pc, int _count)
        {
            var _picked = new System.Collections.Generic.List<Vector2Int>();
            var _pool = new System.Collections.Generic.List<Vector2Int>();
            for (int _dy = -_bRadius; _dy <= _bRadius; _dy++)
                for (int _dx = -_bRadius; _dx <= _bRadius; _dx++)
                {
                    var _c = new Vector2Int(_pc.x + _dx, _pc.y + _dy);
                    if (_grid.IsSolid(_c)) _pool.Add(_c);
                }
            if (_pool.Count == 0) { _picked.Add(_pc); return _picked; }
            // 첫 함정: Player 바로 주변(가만히 있으면 맞도록)
            if (Random.value < _bNearChance)
            {
                var _near = _pool.FindAll(_c => Mathf.Max(Mathf.Abs(_c.x - _pc.x), Mathf.Abs(_c.y - _pc.y)) <= _bNearRadius);
                if (_near.Count > 0) _picked.Add(_near[Random.Range(0, _near.Count)]);
            }
            for (int _spacing = _bMinSpacing; _spacing >= 1 && _picked.Count < _count; _spacing--)
            {
                for (int _tries = 0; _tries < 60 && _picked.Count < _count; _tries++)
                {
                    var _c = _pool[Random.Range(0, _pool.Count)];
                    bool _ok = true;
                    foreach (var _q in _picked) if (Mathf.Max(Mathf.Abs(_c.x - _q.x), Mathf.Abs(_c.y - _q.y)) < _spacing) { _ok = false; break; }
                    if (_ok) _picked.Add(_c);
                }
            }
            return _picked;
        }

        private static void HoldIdle(Animator _a)
        {
            for (int _l = 0; _l < _a.layerCount; _l++) _a.Play("Wait_Before", _l, 0f);
            _a.speed = 0f;
            _a.Update(0f);
        }

        private IEnumerator Loop()
        {
            yield return new WaitForSeconds(_startDelay);
            int _next = 0;
            while (true)
            {
                while (!_autoLoop && _queued < 0) yield return null;
                int _p = _queued >= 0 ? _queued : _next;
                _queued = -1;
                yield return Cast(_p);
                OnPatternGap?.Invoke();   // 패턴과 패턴 사이(머미 소환 등)
                _next = (_p + 1) % 5;
                float _wait = _gapBetweenPatterns;
                while (_wait > 0 && _queued < 0) { _wait -= Time.deltaTime * _tempo; yield return null; }
            }
        }

        private IEnumerator Cast(int _p)
        {
            for (int _i = 0; _i < _hitFlags.Length; _i++) _hitFlags[_i] = false;
            BeginTargeting(_p);

            // B: 솟을 칸을 먼저 보여 준 뒤 시전(클립은 0.48초에 바로 튀어나오므로 예고 시간을 앞에 둔다)
            if (_p == (int)BossPattern.B && _bWarnTime > 0f)
            {
                EnsureTelegraphs();
                HistoryCrash.Sound.GameSfx.Play("Pharaoh_CastB", _pharaoh);   // 예고가 시작될 때
                for (float _w = 0; _w < _bWarnTime; _w += Time.deltaTime * _tempo)
                {
                    WarnTraps(_w);
                    yield return null;
                }
            }

            _current = _p; _castTime = 0f;
            EnsureTelegraphs();
            var _castAnims = CastAnimators(_p);
            foreach (var _ca in _castAnims)
            {
                if (!_ca.gameObject.activeInHierarchy) continue;   // 칸이 모자라 이번에 쉬는 B 함정
                _ca.speed = _tempo;
                for (int _l = 0; _l < _ca.layerCount; _l++) _ca.Play("Cast", _l, 0f);
            }
            var _a = _castAnims[0];
            if (_p == (int)BossPattern.D)
            {
                _a.Update(0f); _sunRoot.gameObject.SetActive(true);
                var _mf = _sun.GetComponent<MeshFilter>();
                _sunRadius = _mf && _mf.sharedMesh ? _mf.sharedMesh.bounds.extents.x * Mathf.Abs(_sun.lossyScale.x) : 2.9f;
            }
            if (_pharaohAnimator) { _pharaohAnimator.speed = _tempo; _pharaohAnimator.CrossFadeInFixedTime(_pharaohStates[_p], 0.25f / _tempo); }

            PlayCastSfx(_p);

            while (_castTime < _castLength[_p]) yield return null;

            // B: 솟았던 함정 중 한 곳에 스태프
            if (_p == (int)BossPattern.B && _staffDropper)
            {
                var _used = new System.Collections.Generic.List<Transform>();
                foreach (var _r in _bRoots) if (_r.gameObject.activeSelf) _used.Add(_r);
                if (_used.Count > 0) _staffDropper.Drop(1, _used[Random.Range(0, _used.Count)].position, IconSource(1));
            }

            foreach (var _ca in _castAnims) if (_ca.gameObject.activeInHierarchy) HoldIdle(_ca);
            if (_p == (int)BossPattern.D) { RestoreShards(); _sunRoot.gameObject.SetActive(false); }
            foreach (var _w in _warns) _w.FadeOut();
            if (_pharaohAnimator) { _pharaohAnimator.speed = 1f; _pharaohAnimator.CrossFadeInFixedTime("Idle", 0.33f); }
            _current = -1;
        }

        // ---------- 소리 ----------
        private HistoryCrash.Sound.GameSfx.Voice _weaponSfx;
        // 무기 소리는 시전 시작부터의 길이로 만들어져 있음(tempo 1.3 기준). B의 시전 목소리는 예고 시작 때 따로 냄
        private void PlayCastSfx(int _p)
        {
            string _id = "ABCDE"[_p].ToString();
            if (_p != (int)BossPattern.B || _bWarnTime <= 0f) HistoryCrash.Sound.GameSfx.Play("Pharaoh_Cast" + _id, _pharaoh);
            Transform _at = _p == (int)BossPattern.A ? _copesh : _p == (int)BossPattern.B ? _bRoots[0] : _p == (int)BossPattern.D ? _sunRoot : _roots[_p];
            _weaponSfx = HistoryCrash.Sound.GameSfx.Play("Weapon_" + _id, _at, _p == (int)BossPattern.D ? 0.1f : 0f);
        }

        // ---------- 스태프 드랍 ----------
        private bool _dDropped, _eDropped;
        private int _eLaunchCount, _eLastIdx = -1;
        private readonly float[] _ePrevDX = new float[5];

        /// <summary>패턴 오브젝트(메시) 원본. 스태프 아이콘·플레이어 공격 모양에 쓴다.</summary>
        public Transform PatternVisual(int _p) { return IconSource(_p); }

        private Transform IconSource(int _p)
        {
            switch ((BossPattern)_p)
            {
                case BossPattern.A: return _copesh;
                case BossPattern.B: return _bRoots[0].Find("Sebeke_Trap_PattenB");
                case BossPattern.C: return _gems[0];
                case BossPattern.D: return _sun;
                default: return _staffs[0];
            }
        }

        private void DropStaff(int _p, Vector3 _world)
        {
            if (!_staffDropper) return;
            _staffDropper.Drop(_p, _world, IconSource(_p));
            if (_debugLog) Debug.Log(string.Format("[BossPattern] Staff drop {0} at {1}", (BossPattern)_p, _world.ToString("F2")));
        }

        private void BeginTargeting(int _p)
        {
            _dDropped = _eDropped = false; _warned = 0; _eLaunchCount = 0; _eLastIdx = -1;
            var _pc = _grid.WorldToCell(_player.position);
            switch ((BossPattern)_p)
            {
                case BossPattern.A:
                    _aLocked = false; _kA = 1f; _flightA.Reset(); break;
                case BossPattern.B:
                {
                    // Player 주변 bTrapCount곳(서로 겹치지 않게). 칸이 모자라면 남는 함정은 이번 시전에서 쉰다
                    var _cells = PickTrapCells(_pc, _bRoots.Length);
                    for (int _k = 0; _k < _bRoots.Length; _k++)
                    {
                        var _r = _bRoots[_k];
                        bool _use = _k < _cells.Count;
                        _r.gameObject.SetActive(_use);
                        if (_use) _r.position = _grid.CellCenter(_cells[_k], _r.position.y);
                    }
                    break;
                }
                case BossPattern.C:
                    for (int _i = 0; _i < 5; _i++) { _cLocked[_i] = false; _kC[_i] = 1f; _flightC[_i].Reset(); }
                    break;
                case BossPattern.D:
                {
                    _grid.TryRandomWalkableNear(_pc, _dLandRadius, out var _cell);
                    var _c = _grid.CellCenter(_cell, 0);
                    // 파편이 바닥 타일 위에 쉬도록 높이를 맞추고, VFX 충돌 평면도 바닥에 둔다
                    _sunRoot.position = new Vector3(_c.x, _grid.FloorTopY + _dShardRestDepth, _c.z);
                    _vfxD.position = new Vector3(_c.x, _grid.FloorTopY, _c.z);
                    break;
                }
                case BossPattern.E:
                {
                    var _r = _roots[4];
                    var _c = _grid.CellCenter(_pc, 0);
                    _r.position = new Vector3(_r.position.x, _r.position.y, _c.z);
                    _r.rotation = _pattensRoot.rotation; // 발사 방향 = 격자 +X
                    ResetStaffLanes();
                    break;
                }
            }
        }

        /// <summary>다음에 시전할 패턴 지정(0=A ~ 4=E). 키 1~5와 같음.</summary>
        public void RequestPattern(int _p) { _queued = Mathf.Clamp(_p, 0, 4); }
        public int CurrentPattern { get { return _current; } }
        public float CurrentCastTime { get { return _castTime; } }

        [Header("디버그")]
        [SerializeField, FormerlySerializedAs("debugLog")] private bool _debugLog = true;
        private bool _dBreakLogged, _dRestLogged;

        private static void AimAt(Transform _t, Vector3 _target)
        {
            Vector3 _d = _t.position - _target;   // 클립의 비행 방향은 로컬 -Z
            if (_d.sqrMagnitude > 0.0001f) _t.rotation = Quaternion.LookRotation(_d, Vector3.up);
        }

        private float LocalDistance(Transform _pivot)
        {
            float _s = Mathf.Abs(_pivot.lossyScale.y);
            return Vector3.Distance(_pivot.position, _player.position) / Mathf.Max(_s, 1e-4f);
        }

        private void Update()
        {
            var _kb = Keyboard.current;
            if (_kb != null)
            {
                if (_kb.digit1Key.wasPressedThisFrame) _queued = 0;
                if (_kb.digit2Key.wasPressedThisFrame) _queued = 1;
                if (_kb.digit3Key.wasPressedThisFrame) _queued = 2;
                if (_kb.digit4Key.wasPressedThisFrame) _queued = 3;
                if (_kb.digit5Key.wasPressedThisFrame) _queued = 4;
                if (_kb.pKey.wasPressedThisFrame) _autoLoop = !_autoLoop;
            }

            // 파라오는 Player 쪽으로 몸을 돌림
            if (_pharaoh)
            {
                Vector3 _d = _player.position - _pharaoh.position; _d.y = 0;
                if (_d.sqrMagnitude > 0.01f)
                    _pharaoh.rotation = Quaternion.RotateTowards(_pharaoh.rotation, Quaternion.LookRotation(_d), _pharaohTurnSpeed * Time.deltaTime);
            }

            if (_current < 0) return;
            _castTime += Time.deltaTime * _tempo;   // 모든 시전 시각(Cast 기준 초)은 클립 시간이라 같은 배율로 진행

            if (_current == (int)BossPattern.A && !_aLocked)
            {
                AimAt(_roots[0], _player.position);
                if (_castTime >= _aLockTime) { _aLocked = true; _kA = LocalDistance(_roots[0]) / _authoredHitDistance; }
            }
            if (_current == (int)BossPattern.C)
            {
                for (int _i = 0; _i < 5; _i++)
                {
                    if (_cLocked[_i]) continue;
                    AimAt(_gemPivots[_i], _player.position);
                    if (_castTime >= _cLockTimes[_i]) { _cLocked[_i] = true; _kC[_i] = LocalDistance(_gemPivots[_i]) / _authoredHitDistance; }
                }
            }
        }

        // ---------- 패턴 E: 지팡이마다 발사 순간의 Player 줄 ----------
        // 클립은 지팡이 5개를 루트 기준 줄(±3·±6칸 등)로 날린다. 루트는 시전 시작 때 Player 줄에 놓이지만,
        // 지팡이마다 발사되는 순간 Player가 서 있는 줄로 다시 맞추고, 발사 뒤에는 그 줄을 유지한다.
        [Header("패턴 E 줄 재조준")]
        [SerializeField, FormerlySerializedAs("eLaunchSpeed"), Tooltip("지팡이가 이 속도(m/s)를 넘으면 발사로 판단")] private float _eLaunchSpeed = 6f;
        private readonly Vector3[] _eLastRaw = new Vector3[5];
        private readonly bool[] _eLaunched = new bool[5];
        private readonly float[] _eShift = new float[5];
        private bool _eHasLast;

        private readonly Vector3[] _eLaunchPos = new Vector3[5];
        private readonly float[] _eDirSign = new float[5];
        private readonly bool[] _eBroken = new bool[5];

        private void ResetStaffLanes()
        {
            _eHasLast = false;
            for (int _i = 0; _i < 5; _i++) { _eLaunched[_i] = false; _eShift[_i] = 0f; _eBroken[_i] = false; }
        }

        // 발사된 지팡이 끝이 걷기 불가 칸(벽·기둥·탑문) 또는 바닥 밖에 닿으면 그 자리에서 깨진다.
        // 제단 위에서 출발하므로 발사 후 eArmDistance를 지나기 전에는 검사하지 않는다.
        private void CheckStaffBreak(int _i, Transform _s)
        {
            if (!_eLaunched[_i] || _eBroken[_i]) return;
            Vector3 _dir = _roots[4].forward * _eDirSign[_i];
            if (Vector3.Distance(_s.position, _eLaunchPos[_i]) < _eArmDistance) return;
            Vector3 _tip = _s.position + _dir * _eTipOffset;
            var _cell = _grid.WorldToCell(_tip);
            if (_grid.InBounds(_cell) && _grid.IsWalkable(_cell)) return;
            _eBroken[_i] = true;
            EnsureFx();
            _fxE.Burst(new Vector3(_tip.x, Mathf.Max(_tip.y, _grid.FloorTopY + 0.2f), _tip.z), -_dir, 1f, 9);
            if (_debugLog) Debug.Log(string.Format("[BossPattern] E staff {0} broke at {1} castT={2:F2}", _i, _tip.ToString("F2"), _castTime));
        }

        private void ApplyStaffLanes()
        {
            float _dt = Mathf.Max(Time.deltaTime, 1e-4f);
            // 줄 방향 = 격자 +X(루트 회전 = Pattens 회전), 줄 사이 = 월드 Z. Player가 선 칸의 줄 중심으로 맞춘다
            float _rowZ = _grid.CellCenter(_grid.WorldToCell(_player.position), 0).z;
            float _follow = _rowZ - _roots[4].position.z;
            for (int _i = 0; _i < 5; _i++)
            {
                var _s = _staffs[_i];
                if (!_s) continue;
                Vector3 _raw = _s.position;   // Animator가 쓴 이번 프레임 위치
                if (!_eLaunched[_i])
                {
                    Vector3 _step = _raw - _eLastRaw[_i];
                    Vector3 _lane = _roots[4].forward;   // 비행 방향(로컬 −Z 또는 +Z)과 같은 축의 이동만 발사로 본다
                    bool _alongLane = Mathf.Abs(Vector3.Dot(_step, _lane)) > 0.8f * _step.magnitude;
                    bool _visible = Mathf.Abs(_s.lossyScale.x) > 0.01f;
                    if (_eHasLast && _visible && _alongLane && _step.magnitude / _dt > _eLaunchSpeed)
                    {
                        _eLaunched[_i] = true;   // 직전 프레임의 줄로 고정
                        _eLaunchPos[_i] = _eLastRaw[_i] + Vector3.forward * _eShift[_i];
                        _eDirSign[_i] = Mathf.Sign(Vector3.Dot(_step, _lane));
                        _eLaunchCount++; _eLastIdx = _i; _ePrevDX[_i] = float.NaN;
                        if (_debugLog) Debug.Log(string.Format("[BossPattern] E staff {0} launch castT={1:F2} lane z={2:F2}", _i, _castTime, _roots[4].position.z + _eShift[_i]));
                    }
                    else _eShift[_i] = _follow;
                }
                _eLastRaw[_i] = _raw;
                _s.position = _raw + Vector3.forward * _eShift[_i];
                bool _wasBroken = _eBroken[_i];
                CheckStaffBreak(_i, _s);
                // E: 마지막으로 발사된 지팡이가 Player의 X를 지나는 순간, 그 X와 지팡이의 Z 위치에 스태프
                if (!_eDropped && _eLaunched[_i] && _i == _eLastIdx && _eLaunchCount >= _staffs.Length)
                {
                    float _dx = _s.position.x - _player.position.x;
                    bool _crossed = !float.IsNaN(_ePrevDX[_i]) && Mathf.Sign(_dx) != Mathf.Sign(_ePrevDX[_i]);
                    if (_crossed) { _eDropped = true; DropStaff(4, new Vector3(_player.position.x, 0f, _s.position.z)); }
                    else if (_eBroken[_i] && !_wasBroken) { _eDropped = true; DropStaff(4, _s.position); }
                    _ePrevDX[_i] = _dx;
                }
                if (_eBroken[_i]) _s.localScale = Vector3.zero;
            }
            _eHasLast = true;
        }

        // ---------- 바닥 예고 표시 ----------
        private RangeTelegraph[] _warns = new RangeTelegraph[0];   // 칸 하나 = 공격 하나(한 시전에 최대 5개 동시)
        private int _warned;   // 이번 시전에서 이미 공격이 들어온 예고(비트)
        private float _sunRadius = 2.9f;
        [SerializeField, FormerlySerializedAs("bWarnHideAt"), Tooltip("B 예고를 끄는 시점(Cast 기준 초, 함정이 바닥을 뚫는 순간)")] private float _bWarnHideAt = 0.56f;

        private void EnsureTelegraphs()
        {
            if (_warns.Length > 0) return;
            // B·C·D(B 함정 수가 5개를 넘으면 그만큼), A(제단·계단을 덮음), E 5줄(화면을 덮지 않게 면을 어둡게)
            int _n = Mathf.Max(5, _bRoots.Length);
            _warns = new RangeTelegraph[_n + 6];
            for (int _i = 0; _i < _n; _i++) _warns[_i] = new RangeTelegraph("RangeTelegraph_" + _i, transform, _rangeLineColor, _rangeCrackColor);
            SampleAltar();
            _warns[_n] = new RangeTelegraph("RangeTelegraph_A", transform, _rangeLineColor, _rangeCrackColor, 1f, AltarHeight);
            for (int _i = 0; _i < 5; _i++) _warns[_n + 1 + _i] = new RangeTelegraph("RangeTelegraph_E" + (_i + 1), transform, _rangeLineColor, _rangeCrackColor, 0.55f);
        }

        // 파라오 발밑에서 제단 아래까지의 높이(A 예고가 제단·계단 위를 덮게). 계단 턱을 따라 꺾이거나 무늬가 늘어나지 않게
        // "평평한 윗면 + 곧은 비탈 하나"로 만들어 어느 방향이든 같은 모양을 쓴다. 한 번만 잰다
        private const float ALTAR_STEP = 0.2f, ALTAR_SLOPE = 0.55f;
        private const int ALTAR_SAMPLES = 26;              // 5m까지만 잰다(그 밖에는 기둥이 있음)
        private readonly float[] _altar = new float[40];   // 파라오에서의 거리(칸 0.2m)별 높이
        private float _altarFoot;                          // 비탈이 끝나고 바닥이 시작되는 거리(m)

        private void SampleAltar()
        {
            var _hits = new RaycastHit[16];
            float _top = _pharaoh.position.y + 0.3f, _floor = _grid.FloorTopY;
            Vector3 _front = new Vector3(_grid.Origin.x + (_grid.Cols - 1) * 0.5f * _grid.TileSize - _pharaoh.position.x, 0f, _grid.Origin.y + (_grid.Rows - 1) * 0.5f * _grid.TileSize - _pharaoh.position.z).normalized;
            for (int _i = 0; _i < _altar.Length; _i++) _altar[_i] = _floor;
            for (int _i = 0; _i < ALTAR_SAMPLES; _i++)
                for (int _a = -15; _a <= 15; _a += 5)
                {
                    var _from = _pharaoh.position + Quaternion.Euler(0f, _a, 0f) * _front * (_i * ALTAR_STEP);
                    _from.y = _top;
                    int _c = Physics.RaycastNonAlloc(_from, Vector3.down, _hits, _top - _floor, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int _k = 0; _k < _c; _k++)
                    {
                        var _col = _hits[_k].collider;   // 움직이는 것(Player·미라 등)은 빼고 지형만
                        if (_col.attachedRigidbody || _col is CharacterController || _col.transform.IsChildOf(_pharaoh)) continue;
                        _altar[_i] = Mathf.Max(_altar[_i], _hits[_k].point.y);
                    }
                }
            for (int _i = _altar.Length - 2; _i >= 0; _i--) _altar[_i] = Mathf.Max(_altar[_i], _altar[_i + 1]);                                    // 움푹한 곳 없이
            for (int _i = 1; _i < _altar.Length; _i++) _altar[_i] = Mathf.Max(_altar[_i], _altar[_i - 1] - ALTAR_SLOPE * ALTAR_STEP);   // 비탈보다 가파르게 떨어지지 않게
            _altarFoot = System.Array.FindIndex(_altar, _h => _h <= _floor + 0.001f) * ALTAR_STEP;
        }

        private float AltarHeight(Vector3 _p)
        {
            float _d = new Vector2(_p.x - _pharaoh.position.x, _p.z - _pharaoh.position.z).magnitude / ALTAR_STEP;
            int _i = (int)_d;
            return _i >= _altar.Length - 1 ? _grid.FloorTopY : Mathf.Lerp(_altar[_i], _altar[_i + 1], _d - _i);
        }

        /// <summary>바닥 예고를 모두 바로 끈다(클리어 연출 시작 등).</summary>
        public void HideTelegraphs() { foreach (var _w in _warns) _w.Hide(); }

        private void OnDisable() { HideTelegraphs(); }

        private void Strike(int _i, float _hold = 0f)
        {
            if ((_warned & (1 << _i)) != 0) return;
            _warned |= 1 << _i;
            _warns[_i].Strike(_hold);
        }

        private Vector3 Ground(Vector3 _p) { return new Vector3(_p.x, _grid.FloorTopY, _p.z); }

        // ---------- 착탄·파괴 이펙트 ----------
        private PatternImpactFx _fxA, _fxC, _fxE;

        private void EnsureFx()
        {
            if (_fxA != null) return;
            var _meshes = new System.Collections.Generic.List<Mesh>();
            foreach (var _s in _shards)
            {
                var _mf = _s.GetComponent<MeshFilter>();
                if (_mf && _mf.sharedMesh && !_meshes.Contains(_mf.sharedMesh)) _meshes.Add(_mf.sharedMesh);
                if (_meshes.Count >= 4) break;
            }
            var _arr = _meshes.ToArray();
            Material M(Transform _t) { var _r = _t ? _t.GetComponent<Renderer>() : null; return _r ? _r.sharedMaterial : null; }
            _fxA = new PatternImpactFx("ImpactFx_PattenA", transform, M(_copesh), _arr, _aImpactColor, _grid.FloorTopY);
            _fxC = new PatternImpactFx("ImpactFx_PattenC", transform, M(_gems[0]), _arr, _cImpactColor, _grid.FloorTopY);
            _fxE = new PatternImpactFx("ImpactFx_PattenE", transform, M(_staffs[0]), _arr, _eImpactColor, _grid.FloorTopY);
        }

        // ---------- D 파편: 갑자기 사라지지 않고 서서히 투명해짐 ----------
        // glTF 재질은 불투명이라, 사라지는 구간에만 같은 텍스처의 URP Lit 투명 재질로 바꿔 알파를 내린다.
        private Material _shardFadeMat; private Material[] _shardOrigMats; private Renderer[] _shardRenderers;
        private Color _shardBaseColor = Color.white, _shardEmission = Color.black; private bool _shardFading;

        private void EnsureShardFade()
        {
            if (_shardFadeMat) return;
            _shardRenderers = new Renderer[_shards.Length]; _shardOrigMats = new Material[_shards.Length];
            for (int _i = 0; _i < _shards.Length; _i++) { _shardRenderers[_i] = _shards[_i].GetComponent<Renderer>(); _shardOrigMats[_i] = _shardRenderers[_i] ? _shardRenderers[_i].sharedMaterial : null; }
            var _src = _shardOrigMats[0];
            var _m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "La_Sun_PattenD_shard_fade" };
            if (_src)
            {
                if (_src.HasProperty("baseColorTexture")) _m.SetTexture("_BaseMap", _src.GetTexture("baseColorTexture"));
                if (_src.HasProperty("baseColorFactor")) _shardBaseColor = _src.GetColor("baseColorFactor");
                if (_src.HasProperty("normalTexture") && _src.GetTexture("normalTexture")) { _m.SetTexture("_BumpMap", _src.GetTexture("normalTexture")); _m.EnableKeyword("_NORMALMAP"); }
                if (_src.HasProperty("emissiveTexture") && _src.GetTexture("emissiveTexture")) _m.SetTexture("_EmissionMap", _src.GetTexture("emissiveTexture"));
                if (_src.HasProperty("emissiveFactor")) _shardEmission = _src.GetColor("emissiveFactor");
            }
            _m.EnableKeyword("_EMISSION");
            _m.SetFloat("_Metallic", 0f); _m.SetFloat("_Smoothness", 0.3f);
            _m.SetFloat("_Surface", 1f); _m.SetFloat("_Blend", 0f);
            _m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _m.SetFloat("_ZWrite", 0f);
            _m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _shardFadeMat = _m;
        }

        private void ApplyShardFade()
        {
            if (_castTime < _dShardFadeStart) return;
            EnsureShardFade();
            if (!_shardFading)
            {
                foreach (var _r in _shardRenderers) if (_r) _r.sharedMaterial = _shardFadeMat;
                _shardFading = true;
            }
            float _u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_dShardFadeStart, _dShardFadeEnd, _castTime));
            var _c = _shardBaseColor; _c.a = 1f - _u;
            _shardFadeMat.SetColor("_BaseColor", _c);
            _shardFadeMat.SetColor("_EmissionColor", _shardEmission * (1f - _u));
        }

        private void RestoreShards()
        {
            if (!_shardFading) return;
            for (int _i = 0; _i < _shardRenderers.Length; _i++) if (_shardRenderers[_i]) _shardRenderers[_i].sharedMaterial = _shardOrigMats[_i];
            _shardFading = false;
        }

        private void UpdateTelegraphs()
        {
            foreach (var _w in _warns) _w.Tick(Time.deltaTime);
            switch (_current)
            {
                case (int)BossPattern.A: WarnA(); break;
                case (int)BossPattern.B: WarnTraps(_bWarnTime + _castTime); break;
                case (int)BossPattern.C: WarnC(); break;
                case (int)BossPattern.D: WarnD(); break;
                case (int)BossPattern.E: WarnE(); break;
            }
        }

        // A: 파라오 발밑에서 코페쉬가 떨어질 자리까지. 발사 전에는 파라오를 중심으로 Player를 따라 돌고, 발사되면 그 방향으로 고정
        private void WarnA()
        {
            if (_castTime < _aWarnStart) return;
            int _w = _warns.Length - 6;
            if (_flightA.Done || (_flightA.Active && _flightA.Traveled >= _flightA.HitDist)) { Strike(_w); return; }
            Vector3 _from = _flightA.Active ? _flightA.Start : _copesh.position, _dir = _flightA.Dir;
            float _total = _flightA.Total;
            if (!_flightA.Active)
            {
                Vector3 _d = _player.position - _from;
                _dir = _d.normalized;
                _total = FlightTotal(_from, _dir, _d.magnitude, _aOvershoot, out _);
            }
            var _mf = _copesh.GetComponent<MeshFilter>();
            float _radius = _mf.sharedMesh.bounds.extents.magnitude * Mathf.Abs(_copesh.lossyScale.x);   // 빙글빙글 도는 날의 반지름
            Vector3 _flat = new Vector3(_dir.x, 0f, _dir.z).normalized;
            Vector3 _end = Ground(_from + _dir * _total) + _flat * _radius;
            Vector3 _start = Ground(_pharaoh.position);
            _start += (_end - _start).normalized * 0.4f;
            // 발밑은 좁게(제단 위는 코페쉬가 높이 떠 있어 맞지 않음), 바닥에 닿을 즈음 실제 폭으로
            _warns[_w].PlaceLane(_start, _end, _radius * 2f, 0.6f, 1.2f, Mathf.Min(_altarFoot - 0.4f, (_end - _start).magnitude * 0.6f));
            _warns[_w].Set(_flightA.Active ? 0.7f + 0.3f * _flightA.Traveled / _flightA.HitDist : 0.7f * Mathf.InverseLerp(_aWarnStart, _aLaunchTime, _castTime));
        }

        // B: 함정이 솟을 자리. _t = 예고가 시작된 뒤의 시간(시전 전 예고 + 시전)
        private void WarnTraps(float _t)
        {
            for (int _k = 0; _k < _bRoots.Length; _k++)
            {
                if (!_bRoots[_k].gameObject.activeSelf) continue;   // 칸이 모자라 쉬는 함정
                if (_t >= _bWarnTime + _bWarnHideAt) { Strike(_k); continue; }
                if ((_warned & (1 << _k)) != 0) continue;
                if (!_warns[_k].On || _warns[_k].Ending)
                {
                    var _b = _bRoots[_k].Find("Sebeke_Trap_PattenB").GetComponent<Renderer>().bounds;
                    _warns[_k].Hide();
                    // 함정은 세워진 채 솟아 바닥 폭이 얇으므로(0.9m) Player 몸 크기까지 더해 보여 준다
                    _warns[_k].PlaceBox(Ground(_bRoots[_k].position), _b.size.x + _playerRadius * 2f, _b.size.z + _playerRadius * 2f);
                }
                _warns[_k].Set(_t / (_bWarnTime + _bWarnHideAt));
            }
        }

        // C: 보석이 터질 자리. 발사 전에는 Player 발밑, 발사되면 그때 위치로 고정
        private void WarnC()
        {
            for (int _i = 0; _i < 5; _i++)
            {
                float _from = _i == 0 ? _cLaunchTimes[0] - _cWarnLead : _cLaunchTimes[_i - 1];
                PatternFlight _f = _flightC[_i];
                if (_castTime < _from) continue;
                if (_f.Done) { Strike(_i); continue; }
                _warns[_i].PlaceCircle(Ground(_f.Active ? _f.Start + _f.Dir * _f.Total : _player.position), _cBlastRadius);
                _warns[_i].Set(_f.Active ? 0.5f + 0.5f * _f.Traveled / _f.Total : 0.5f * Mathf.InverseLerp(_from, _cLaunchTimes[_i], _castTime));
            }
        }

        // D: 태양이 떨어질 자리(태양 반지름)
        private void WarnD()
        {
            if (_castTime < _dWarnStart) return;
            if (_castTime >= _dBreakTime) { Strike(0); return; }
            _warns[0].PlaceCircle(Ground(_sunRoot.position), _sunRadius);
            _warns[0].Set(Mathf.InverseLerp(_dWarnStart, _dBreakTime, _castTime));
        }

        private readonly Vector3[] _eShown = new Vector3[5], _eVel = new Vector3[5];   // 화면에 그리는 줄(x = 시작 x, y = 끝 x, z = 줄 z)

        // E: 지팡이가 지나갈 줄. 발사 전에는 Player를 따라가고, 발사된 지팡이는 그 줄로 고정.
        // 실제 줄은 칸 단위로 바뀌므로 그대로 그리면 툭툭 끊긴다 → 발사 전에는 Player의 실제 위치를 부드럽게 따라가고, 발사되면 실제 줄로 빠르게 맞춘다
        private void WarnE()
        {
            int _first = _warns.Length - 5;
            for (int _i = 0; _i < 5; _i++)
            {
                int _w = _first + _i;
                float _from = _eLaunchTimes[_i] - _eWarnLead;
                if (_castTime < _from || (_warned & (1 << _w)) != 0) continue;
                if (_eBroken[_i]) { _warned |= 1 << _w; _warns[_w].FadeOut(); continue; }
                float _z = _eLaunched[_i] ? _staffs[_i].position.z : _player.position.z + _eLaneCells[_i] * _grid.TileSize;
                if (!StaffLane(_z, out float _x0, out float _x1)) { _warns[_w].FadeOut(); continue; }   // 바닥 밖 줄
                Vector3 _goal = new Vector3(_x0, _x1, _z);
                Vector3 _lane = _eShown[_i];
                if (_warns[_w].On)   // 줄은 바로 따라가고, 기둥에 막혀 바뀌는 길이는 천천히
                {
                    _lane.x = Mathf.SmoothDamp(_lane.x, _goal.x, ref _eVel[_i].x, 0.2f);
                    _lane.y = Mathf.SmoothDamp(_lane.y, _goal.y, ref _eVel[_i].y, 0.2f);
                    _lane.z = Mathf.SmoothDamp(_lane.z, _goal.z, ref _eVel[_i].z, _eLaunched[_i] ? 0.04f : 0.08f);
                }
                else { _lane = _goal; _eVel[_i] = Vector3.zero; }
                _eShown[_i] = _lane;
                _warns[_w].PlaceLane(new Vector3(_lane.x, _grid.FloorTopY, _lane.z), new Vector3(_lane.y, _grid.FloorTopY, _lane.z), _eStaffSize.x, (_lane.y - _lane.x) * 0.14f);
                if (!_eLaunched[_i]) { _warns[_w].Set(0.6f * Mathf.InverseLerp(_from, _eLaunchTimes[_i], _castTime)); continue; }
                // 지팡이 끝이 Player 앞까지 오면 공격, 꼬리가 지나갈 때까지 남는다
                float _tip = _staffs[_i].position.x + _eDirSign[_i] * _roots[4].forward.x * _eStaffSize.y * 0.5f;
                float _u = Mathf.InverseLerp(_eLaunchPos[_i].x, _player.position.x, _tip);
                if (_u >= 1f) Strike(_w, _eStaffSize.y / 16f / _tempo);   // 지팡이 속도 약 16m/s(클립)
                else _warns[_w].Set(0.6f + 0.4f * _u);
            }
        }

        // E 지팡이가 날아갈 줄에서 바닥이 있는 구간(제단 앞 ~ 처음 막히는 벽·기둥)
        private bool StaffLane(float _z, out float _x0, out float _x1)
        {
            float _tile = _grid.TileSize, _origin = _roots[4].position.x;
            _x0 = _x1 = 0f; bool _has = false;
            for (float _d = 0f; _d < 80f; _d += _tile)
            {
                var _cell = _grid.WorldToCell(new Vector3(_origin + _d, 0f, _z));
                bool _ok = _grid.IsWalkable(_cell);
                if (_ok && !_has) { _has = true; _x0 = _grid.CellCenter(_cell, 0f).x - _tile * 0.5f; }
                if (_ok) _x1 = _grid.CellCenter(_cell, 0f).x + _tile * 0.5f;
                else if (_has && _d > _eArmDistance) break;   // 발사 직후 구간은 막힘을 보지 않음(CheckStaffBreak와 같음)
            }
            return _has && _x1 - _x0 > 1f;
        }

        // D 태양은 비볼록 MeshCollider라 Player가 구체 안쪽에 들어가면 겹침으로 잡히지 않는다.
        // 낙하~깨짐 구간에는 구 판정(중심 거리 < 반지름 + Player 반지름)을 함께 쓴다.
        private void CheckSunVolumeHit()
        {
            if (_current != (int)BossPattern.D || _castTime < _dFallStart || _castTime > _dBreakTime + 0.1f) return;
            var _r = _sun.GetComponent<Renderer>();
            if (!_r || !_r.enabled || Mathf.Abs(_sun.lossyScale.x) < 0.01f) return;
            if (Vector3.Distance(_player.position, _r.bounds.center) < _sunRadius + _playerRadius) { Hit(0, true, "D Sun"); return; }
            // 깨지는 순간: 예고 원(태양 반지름) 안이면 열기 폭발로 피격(공중에 떠 있어도 dodgeHeight 위면 피함)
            if (_castTime >= _dBreakTime && _castTime < _dBreakTime + 0.1f && !Airborne())
            {
                Vector3 _c = _r.bounds.center; Vector3 _p = _player.position;
                if (new Vector2(_p.x - _c.x, _p.z - _c.z).magnitude < _sunRadius) Hit(0, true, "D Sun Blast");
            }
        }

        // Animator가 위치를 쓴 뒤에 비행 거리(로컬 z)를 Player 거리에 맞게 늘이고 줄인다
        private void LateUpdate()
        {
            if (_current == (int)BossPattern.A && _aLocked)
            {
                if (!UpdateFlight(_flightA, _copesh, _aFlightSpeed, _aOvershoot, false)) ScaleZ(_copesh, _kA);
                if (_flightA.Done && !_flightA.Notified)
                {
                    // A: 바닥에 닿은 자리에서 깨짐(바닥까지 못 가고 끝나면 그 자리에서 진행 방향 반대로)
                    _flightA.Notified = true;
                    HistoryCrash.Sound.GameSfx.FadeOut(_weaponSfx, 0.25f);   // 코페쉬가 깨지면 회전 소리를 걷음
                    EnsureFx();
                    Vector3 _p = _flightA.Start + _flightA.Dir * _flightA.Total;
                    if (_flightA.Straight) _fxA.Burst(new Vector3(_p.x, _grid.FloorTopY + 0.03f, _p.z), Vector3.up, 1f, 12);
                    else _fxA.Burst(_p, -_flightA.Dir, 0.8f, 8);
                    DropStaff(0, _p);   // A: 코페쉬가 깨진 지점
                }
            }
            if (_current == (int)BossPattern.C)
                for (int _i = 0; _i < 5; _i++)
                {
                    if (_cLocked[_i] && !UpdateFlight(_flightC[_i], _gems[_i], _cFlightSpeed, 0f, true)) ScaleZ(_gems[_i], _kC[_i]);
                    PatternFlight _f = _flightC[_i];
                    if (_f.Done && !_f.Notified)
                    {
                        // C: 목표 위치에 닿으면 터지고, 폭발 범위 안의 Player가 맞는다
                        _f.Notified = true;
                        EnsureFx();
                        Vector3 _p = _f.Start + _f.Dir * _f.Total;
                        _fxC.Burst(_p, Vector3.up, 1.2f, 8);
                        HistoryCrash.Sound.GameSfx.Play("Weapon_C_GlassBreak", _p, null, 0f, false, false, 0.04f);
                        if (Vector3.Distance(_player.position, _p) < _cBlastRadius + _playerRadius) Hit(_i, true, "C Gem " + (_i + 1) + " Blast", _p);
                        if (_i == 2) DropStaff(2, _p);   // C: 3번째 보석이 터진 지점
                    }
                }
            if (_current == (int)BossPattern.D)
            {
                ApplySunGroundImpact(); ApplyShardFade();
                if (!_dDropped && _castTime >= _dBreakTime) { _dDropped = true; DropStaff(3, _sunRoot.position); }   // D: 낙하 지점
            }
            if (_current == (int)BossPattern.E) ApplyStaffLanes();
            UpdateTelegraphs();   // 투사체 위치가 정해진 뒤
            CheckHits();
            CheckSunVolumeHit();

            if (_playerRenderer)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                _playerRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", Time.time < _flashUntil ? new Color(1f, 0.25f, 0.2f) : Color.white);
                _playerRenderer.SetPropertyBlock(_mpb);
            }
        }

        // 클립은 파편이 쉬는 바닥보다 (dShardRestDepth + dSunBottomAtBreak)만큼 위에서 구체가 깨진다.
        // 낙하 구간에 그만큼 더 떨어뜨려 구체 바닥이 타일에 닿는 순간 깨지게 하고,
        // 파편은 그 낮아진 위치에서 시작해 원래 궤적(바닥 착지)으로 자연스럽게 돌아가게 한다.
        private void ApplySunGroundImpact()
        {
            float _extra = _dShardRestDepth + _dSunBottomAtBreak;
            float _t = _castTime;
            if (_t > _dFallStart)
            {
                float _f = _t >= _dBreakTime ? 1f : Mathf.Pow((_t - _dFallStart) / (_dBreakTime - _dFallStart), 2f);
                _sun.position -= Vector3.up * _extra * _f;
            }
            if (_t >= _dBreakTime && _t < _dBreakTime + _dShardBlend)
            {
                float _u = (_t - _dBreakTime) / _dShardBlend;
                float _g = 1f - _u * _u * (3f - 2f * _u);
                foreach (var _s in _shards) _s.position -= Vector3.up * _extra * _g;
            }
            if (!_debugLog) return;
            if (_t < _dFallStart) { _dBreakLogged = _dRestLogged = false; return; }
            if (!_dBreakLogged && _t >= _dBreakTime - 0.02f)
            {
                _dBreakLogged = true;
                Debug.Log(string.Format("[BossPattern] D break t={0:F2}: sun bottom y={1:F3}, floor={2:F3}", _t, _sun.GetComponent<Renderer>().bounds.min.y, _grid.FloorTopY));
            }
            if (!_dRestLogged && _t >= 6.0f)
            {
                _dRestLogged = true;
                float _minY = float.MaxValue;
                foreach (var _s in _shards) { var _r = _s.GetComponent<Renderer>(); if (_r && _s.localScale.x > 0.01f) _minY = Mathf.Min(_minY, _r.bounds.min.y); }
                Debug.Log(string.Format("[BossPattern] D rest t={0:F2}: lowest shard bottom y={1:F3}, floor={2:F3}", _t, _minY, _grid.FloorTopY));
            }
        }

        /// <summary>
        /// 조준이 고정된 투사체를, 클립이 앞으로 나가기 시작하는 순간(로컬 z가 launchDetectZ만큼 줄어든 순간)부터
        /// "그 순간의 Player 위치"를 향해 지정 속도로 직선 비행시킨다. 비행 중이면 true.
        /// A: Player를 지나 overshoot만큼 더 날아간 뒤 사라짐. C(impact=true): Player 위치에 닿으면 잠깐 커졌다가 사라짐.
        /// </summary>
        private bool UpdateFlight(PatternFlight _f, Transform _t, float _speed, float _overshoot, bool _impact)
        {
            if (!_f.Armed) { _f.Armed = true; _f.ZRef = _t.localPosition.z; return false; }
            if (!_f.Active && !_f.Done)
            {
                if (_t.localPosition.z > _f.ZRef - _launchDetectZ) { _f.ZRef = Mathf.Max(_f.ZRef, _t.localPosition.z); return false; }
                _f.Active = true;
                _f.Start = _t.position;
                Vector3 _target = _player.position;
                Vector3 _d = _target - _f.Start;
                _f.HitDist = _d.magnitude;
                _f.Dir = _f.HitDist > 1e-3f ? _d / _f.HitDist : Vector3.forward;
                _f.Traveled = 0;
                _f.Total = FlightTotal(_f.Start, _f.Dir, _f.HitDist, _overshoot, out bool _straight);
                _f.Straight = _straight;
                if (_debugLog) Debug.Log(string.Format("[BossPattern] Launch {0} castT={1:F2} dist={2:F1}m speed={3}m/s eta={4:F2}s", _t.name, _castTime, _f.HitDist, _speed, _f.HitDist / _speed));
            }
            if (_f.Active)
            {
                _f.Traveled += _speed * _tempo * Time.deltaTime;
                if (_f.Traveled >= _f.Total) { _f.Active = false; _f.Done = true; _f.DoneTime = Time.time; _f.Traveled = _f.Total; }
            }
            if (_f.Straight || _f.Traveled <= _f.HitDist) _t.position = _f.Start + _f.Dir * _f.Traveled;
            else
            {
                // Player 위치를 지난 뒤에는 수평으로 계속 날아가 바닥 아래로 파고들지 않게 한다
                Vector3 _flat = new Vector3(_f.Dir.x, 0, _f.Dir.z);
                _flat = _flat.sqrMagnitude > 1e-4f ? _flat.normalized : _f.Dir;
                _t.position = _f.Start + _f.Dir * _f.HitDist + _flat * (_f.Traveled - _f.HitDist);
            }
            // 착탄 후에는 숨긴다(깨지는 이펙트는 LateUpdate에서 한 번만 낸다)
            if (_f.Done) _t.localScale = Vector3.zero;
            return true;
        }

        // A: 아래로 비스듬히 날아가므로, Player 위치를 지나 그대로 바닥에 닿을 때까지 직선으로 날아가 깨진다
        private float FlightTotal(Vector3 _start, Vector3 _dir, float _hitDist, float _overshoot, out bool _straight)
        {
            _straight = false;
            if (_overshoot > 0f && _dir.y < -0.05f)
            {
                float _toFloor = (_start.y - (_grid.FloorTopY + _aFloorClearance)) / -_dir.y;
                if (_toFloor > _hitDist) { _straight = true; return Mathf.Min(_toFloor, _hitDist + _aMaxPastPlayer); }
            }
            return _hitDist + _overshoot;
        }

        private static void ScaleZ(Transform _t, float _k)
        {
            var _p = _t.localPosition; _p.z *= _k; _t.localPosition = _p;
        }
        private readonly System.Collections.Generic.List<PatternHitBox> _hitBoxes = new System.Collections.Generic.List<PatternHitBox>();
        private readonly Collider[] _overlapBuf = new Collider[16];
        const int HIT_LAYER = 2; // Ignore Raycast: 카메라 충돌 검사(DefaultRaycastLayers)에 걸리지 않음
        private float _playerRadius = 0.29f;
        private IDamageable _health;   // 플레이어 체력 (Health)
        private IKnockback _knockback; // 플레이어 넉백 (PlayerController)
        private float _knockbackUntil; // 밀리는 중에는 새 넉백을 겹치지 않음(보석 몸통+폭발, 지팡이 연속 등)

        [Header("피격 넉백(옆에서 날아오는 A·C·E만. 밑에서 솟는 B·위에서 떨어지는 D는 피해만)")]
        [SerializeField, Tooltip("밀려나는 거리(m)")] private float _knockbackDistance = 0.6f;
        [SerializeField, Tooltip("밀려나는 시간(초)")] private float _knockbackTime = 0.15f;

        private void BuildHitBoxes()
        {
            AddHitBox(_copesh, 0, 0, "A Copesh");
            for (int _k = 0; _k < _bRoots.Length; _k++)
                AddHitBox(_bRoots[_k].Find("Sebeke_Trap_PattenB"), 1, _k, "B Trap " + (_k + 1));
            for (int _i = 0; _i < 5; _i++) AddHitBox(_gems[_i], 2, _i, "C Gem " + (_i + 1));
            AddHitBox(_sun, 3, 0, "D Sun");
            for (int _i = 0; _i < 5; _i++) AddHitBox(_staffs[_i], 4, _i, "E Staff " + (_i + 1));
            if (_playerModel)
            {
                var _r = _playerModel.GetComponent<Renderer>();
                if (_r) _playerRadius = _r.bounds.extents.y;
            }
            _health = _player.GetComponent<IDamageable>();
            _knockback = _player.GetComponent<IKnockback>();
        }

        private void AddHitBox(Transform _t, int _pattern, int _slot, string _label)
        {
            if (!_t) return;
            var _mf = _t.GetComponent<MeshFilter>();
            if (!_mf || !_mf.sharedMesh) return;
            var _mc = _t.GetComponent<MeshCollider>();
            if (_mc == null) _mc = _t.gameObject.AddComponent<MeshCollider>();   // ??는 유니티의 가짜 null을 걸러내지 못함
            _mc.sharedMesh = _mf.sharedMesh;
            _mc.convex = false;
            // 판정은 OverlapSphere로만 하므로 물리 접촉은 끔(켜 두면 날아오는 투사체가 플레이어를 밀어 미끄러지게 함)
            _mc.excludeLayers = ~0;
            _mc.enabled = false;
            _t.gameObject.layer = HIT_LAYER;
            _hitBoxes.Add(new PatternHitBox { Col = _mc, T = _t, Pattern = _pattern, Slot = _slot, Label = _label });
        }

        private void CheckHits()
        {
            // 현재 패턴의 보이는 투사체만 콜라이더를 켠다(크기 0이면 끔)
            foreach (PatternHitBox _hb in _hitBoxes)
            {
                bool _on = _hb.Pattern == _current && Mathf.Abs(_hb.T.lossyScale.x) > 0.01f && _hb.T.gameObject.activeInHierarchy;
                if (_hb.Col.enabled != _on) _hb.Col.enabled = _on;
            }
            if (_current < 0) return;
            Physics.SyncTransforms();
            int _n = Physics.OverlapSphereNonAlloc(_player.position, _playerRadius, _overlapBuf, 1 << HIT_LAYER, QueryTriggerInteraction.Collide);
            for (int _k = 0; _k < _n; _k++)
                foreach (PatternHitBox _hb in _hitBoxes)
                    if (_hb.Col == _overlapBuf[_k] && _hb.Pattern == _current) Hit(_hb.Slot, true, _hb.Label, _hb.Col.bounds.center);
        }

        private static float Flat(Vector3 _v) { _v.y = 0; return _v.magnitude; }
        private bool Airborne() { return _jumpState != null && _jumpState.GetHeightAboveGround() > _dodgeHeight; }

        /// <summary>_from: 맞힌 투사체(폭발) 위치. 옆에서 날아오는 패턴(A·C·E)이면 그 반대쪽으로 조금 밀려남</summary>
        private void Hit(int _slot, bool _cond, string _label, Vector3? _from = null)
        {
            if (!_cond || _hitFlags[_slot]) return;
            _hitFlags[_slot] = true;
            HitCount++;
            _lastHit = _label;
            _flashUntil = Time.time + 0.35f;
            // C 보석은 0.55초 간격이라 무적(1초)에 막히지 않게, 맞을 때마다 1씩 깎는다
            bool _ignoreInvul = _current == (int)BossPattern.C && _cIgnoreInvulnerability;
            if (_health != null) _health.TakeDamage(new DamageInfo(1, ElementType.None, gameObject, _ignoreInvul));
            bool _sideHit = _current == (int)BossPattern.A || _current == (int)BossPattern.C || _current == (int)BossPattern.E;
            if (_sideHit && _from.HasValue && _knockback != null && Time.time >= _knockbackUntil)
            {
                _knockback.Knockback(_player.position - _from.Value, _knockbackDistance, _knockbackTime);
                _knockbackUntil = Time.time + _knockbackTime + 0.1f;
            }
            Debug.Log("[BossPattern] Hit: " + _label + " (total " + HitCount + ", ignoreInvul=" + _ignoreInvul + ")");
        }

        private void OnGUI()
        {
            if (HistoryCrash.UI.PauseSettingsUI.IsOpen) return;   // 설정창이 열리면 OnGUI 글자는 그리지 않음(항상 UI 위에 그려지므로)
            string _pat = _current >= 0 ? ((BossPattern)_current) + "  " + _castTime.ToString("F2") + "s" : "-";
            float _top = HistoryCrash.UI.DebugHudLayout.Top;   // 좌측 상단 HP 나침반 아래부터
            GUI.Box(new Rect(10, _top + 10, 360, 150), "");
            GUI.Label(new Rect(20, _top + 16, 340, 20), "Pattern: " + _pat + "   Auto: " + (_autoLoop ? "ON" : "OFF"));
            GUI.Label(new Rect(20, _top + 36, 340, 20), "Hits: " + HitCount + (_lastHit != "" ? "   (last: " + _lastHit + ")" : ""));
            GUI.Label(new Rect(20, _top + 60, 340, 20), "Mouse look / WASD move / Shift run / Space jump");
            GUI.Label(new Rect(20, _top + 78, 340, 20), "Wheel zoom / Q,E 45deg snap / R face boss");
            GUI.Label(new Rect(20, _top + 96, 340, 20), "Tab lock-on / V overview / Esc free cursor, click lock");
            GUI.Label(new Rect(20, _top + 114, 340, 20), "1-5 cast A-E / P auto loop on-off");
        }
    }
}
