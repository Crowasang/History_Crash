using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Player
{
    /// <summary>
    /// Player가 든 스태프·오브와 공격.
    /// - E: Player 칸 기준 pickupRange칸 안의 가장 가까운 스태프/오브 줍기(같은 종류는 새로 주운 것으로 교체)
    /// - 좌클릭: 스태프와 오브를 모두 들고 있으면 공격(둘 다 1회용으로 소모). 공격체 색 = 오브 속성색
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        [SerializeField, Tooltip("플레이어 모델 (비우면 Animator가 붙은 자식)")] private Transform _model;
        [SerializeField, FormerlySerializedAs("animator")] private Animator _animator;
        [SerializeField, FormerlySerializedAs("pickupRange"), Tooltip("획득 가능 범위(칸). 1이면 Player 칸과 주변 8칸")] private int _pickupRange = 1;
        [SerializeField, FormerlySerializedAs("pickupState"), Tooltip("획득 시 재생할 Animator 상태")] private string _pickupState = "Pickup_Object_Robe";

        [Header("공격")]
        [SerializeField, FormerlySerializedAs("boltSpeed")] private float _boltSpeed = 16f;
        [SerializeField, FormerlySerializedAs("boltRange")] private float _boltRange = 14f;
        [SerializeField, FormerlySerializedAs("boltHitRadius")] private float _boltHitRadius = 0.55f;
        [SerializeField, FormerlySerializedAs("muzzleHeight"), Tooltip("발사 높이(Player 원점 기준)")] private float _muzzleHeight = 0.35f;

        public int HeldStaff { get; private set; } = -1;          // 패턴 0=A~4=E
        public bool HasOrb { get; private set; }
        public Element HeldOrb { get; private set; }
        public bool CanAttack { get { return HeldStaff >= 0 && HasOrb; } }

        private static readonly string[] _staffNames = { "A", "B", "C", "D", "E" };
        private BossPatternDirector _director;
        // KDH 플레이어 연결: 이동 속도(물리), 시전 중 감속, 카메라
        private Rigidbody _rb;
        private ISpeedModifier _speed;
        private Transform _camera;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _speed = GetComponent<ISpeedModifier>();
            // 타일 격자가 붙은 패턴 오브젝트는 게임 진행 전에는 꺼져 있으므로(ActiveOnlyWhilePlaying) 꺼진 것도 찾음
            if (!_grid) _grid = FindFirstObjectByType<TileGrid>(FindObjectsInactive.Include);
            if (!_animator) _animator = GetComponentInChildren<Animator>();
            if (!_model && _animator) _model = _animator.transform;
            if (Camera.main) _camera = Camera.main.transform;
        }

        // 시전 중 이동 속도 배율 (KDH PlayerController의 ISpeedModifier)
        private float SpeedScale { set { if (_speed != null) _speed.SpeedMultiplier = value; } }

        private Vector3 Velocity => _rb ? _rb.linearVelocity : Vector3.zero;

        private void Update()
        {
            if (HistoryCrash.UI.PauseSettingsUI.IsOpen) return;   // 설정창을 누르는 클릭이 공격·줍기로 새지 않게
            var _kb = Keyboard.current;
            if (_kb != null && _kb.eKey.wasPressedThisFrame) TryPickup();
            var _ms = Mouse.current;
            if (_ms != null && _ms.leftButton.wasPressedThisFrame && CanAttack) Attack();
        }

        public PickupItem FindPickable()
        {
            if (_grid == null) _grid = FindFirstObjectByType<TileGrid>(FindObjectsInactive.Include);
            if (_grid == null) return null;
            var _pc = _grid.WorldToCell(transform.position);
            PickupItem _best = null; float _bestD = float.MaxValue;
            foreach (var _it in PickupItem.All)
            {
                if (!_it || !_it.Ready) continue;
                if (Mathf.Max(Mathf.Abs(_it.Cell.x - _pc.x), Mathf.Abs(_it.Cell.y - _pc.y)) > _pickupRange) continue;
                Vector3 _off = _it.transform.position - transform.position; _off.y = 0;
                if (_off.sqrMagnitude < _bestD) { _bestD = _off.sqrMagnitude; _best = _it; }
            }
            return _best;
        }

        public bool TryPickup()
        {
            var _it = FindPickable();
            if (!_it) return false;
            if (_it.Kind == PickupKind.Staff) { HeldStaff = _it.Value; Debug.Log("[Loadout] Staff " + _staffNames[HeldStaff]); }
            else { HasOrb = true; HeldOrb = _it.ElementValue; SetSocketOrb(HeldOrb); Debug.Log("[Loadout] Orb " + ElementInfo.Name(HeldOrb)); }
            ElementFx.Burst(_it.Kind == PickupKind.Orb ? _it.ElementValue : Element.Electric, _it.transform.position + Vector3.up * 0.4f, 0.5f, 10);
            HistoryCrash.Sound.GameSfx.Play(_it.Kind == PickupKind.Staff ? "Pickup_Weapon" : "Pickup_Orb");
            _it.Collect();
            if (_animator && !string.IsNullOrEmpty(_pickupState)) _animator.CrossFadeInFixedTime(_pickupState, 0.1f);
            return true;
        }

        [Header("공격 모션(Blender에서 만든 A~E 패턴 모션)")]
        [SerializeField, FormerlySerializedAs("castStates")] private string[] _castStates = { "PattenA_Sweep_fast", "PattenB_Trap_fast", "PattenC_Volley_v3", "PattenD_SunDrop_fast", "PattenE_Spear_fast_Robe" };
        [SerializeField, FormerlySerializedAs("releaseTimes"), Tooltip("클립에서 손을 휘두르는 순간(초, 1배속 기준)")] private float[] _releaseTimes = { 1.20f, 2.10f, 0.97f, 1.30f, 1.40f };
        [SerializeField, FormerlySerializedAs("castAnimSpeed")] private float _castAnimSpeed = 1.6f;
        [SerializeField, FormerlySerializedAs("castRecovery"), Tooltip("발동 후 이동을 다시 허용하기까지(초)")] private float _castRecovery = 0.35f;
        public bool IsCasting { get; private set; }

        /// <summary>테스트용: 스태프·오브를 바로 쥐여 줌</summary>
        public void DebugGive(int _staff, Element _orb) { HeldStaff = _staff; HasOrb = true; HeldOrb = _orb; SetSocketOrb(_orb); }

        // ---------- 지팡이 머리 고리(홀)에 박힌 오브 ----------
        [Header("지팡이 홀 오브")]
        [SerializeField, FormerlySerializedAs("socketLocal"), Tooltip("Player_Staff 메시 기준 고리 중심(높이 0.15~0.23 구멍의 가운데)")] private Vector3 _socketLocal = new Vector3(0f, 0.19f, 0f);
        [SerializeField, FormerlySerializedAs("socketSize"), Tooltip("고리 안 오브 지름(메시 단위, 구멍 지름 약 0.096)")] private float _socketSize = 0.088f;
        [SerializeField, FormerlySerializedAs("socketSpin")] private float _socketSpin = 90f;
        private Transform _staffTf, _socketOrb;

        private Transform StaffTransform()
        {
            if (_staffTf) return _staffTf;
            var _root = _model ? _model : transform;
            foreach (var _t in _root.GetComponentsInChildren<Transform>(true)) if (_t.name == "Player_Staff") { _staffTf = _t; break; }
            return _staffTf;
        }

        private void SetSocketOrb(Element _e)
        {
            ClearSocketOrb(false);
            var _staff = StaffTransform();
            if (!_staff) return;
            // 바닥 오브와 같은 에너지 코어(빛으로만 된 오브). Resources 프리팹은 쓰지 않음
            GameObject _go = ElementOrb.MakeEnergyCore(_e);
            _go.name = "SocketOrb_" + ElementInfo.Name(_e);
            _socketOrb = _go.transform;
            _socketOrb.SetParent(_staff, false);
            _socketOrb.localPosition = _socketLocal;
            _socketOrb.localRotation = Quaternion.identity;
            StartCoroutine(InsertOrb(_socketOrb, _e));
        }

        private System.Collections.IEnumerator InsertOrb(Transform _t, Element _e)
        {
            // 고리에 박히는 연출: 커지며 자리 잡고 속성 불티
            for (float _u = 0; _u < 1f && _t; _u += Time.deltaTime / 0.25f)
            {
                float _k = 1f - (1f - _u) * (1f - _u);
                _t.localScale = Vector3.one * _socketSize * Mathf.Lerp(1.6f, 1f, _k) * Mathf.Min(1f, _u * 3f);
                yield return null;
            }
            if (!_t) yield break;
            _t.localScale = Vector3.one * _socketSize;
            ElementFx.Burst(_e, _t.position, 0.35f, 10);
        }

        private void ClearSocketOrb(bool _burst)
        {
            if (!_socketOrb) return;
            if (_burst) ElementFx.Burst(_heldOrbForFx, _socketOrb.position, 0.6f, 16);
            Destroy(_socketOrb.gameObject);
            _socketOrb = null;
        }
        private Element _heldOrbForFx = Element.Fire;

        private void LateUpdate()
        {
            if (!_socketOrb) return;
            _socketOrb.Rotate(Vector3.up, _socketSpin * Time.deltaTime, Space.Self);
            OrbMotion.FaceCamera(_socketOrb);   // 에너지 코어 빛 판이 화면을 보게
        }
        public void TryAttack() { if (CanAttack) Attack(); }

        private void Attack()
        {
            if (IsCasting) return;
            StartCoroutine(CastRoutine(HeldStaff, HeldOrb));
            HeldStaff = -1; HasOrb = false;   // 1회용
        }

        [Header("이동 중 시전")]
        [SerializeField, FormerlySerializedAs("moveCastStates"), Tooltip("이동 중 시전 모션(앞/뒤/왼쪽/오른쪽) — Blender Player_CastC_Move_*")] private string[] _moveCastStates = { "CastC_Move_Forward_Robe", "CastC_Move_Back_Robe", "CastC_Move_StrafeLeft", "CastC_Move_StrafeRight" };
        [SerializeField, FormerlySerializedAs("castMoveSpeedScale"), Tooltip("시전 중 이동 속도 배율")] private float _castMoveSpeedScale = 0.6f;
        [SerializeField, FormerlySerializedAs("castEndNormalized"), Tooltip("서서 시전할 때 모션을 이 지점(normalized)까지 재생한 뒤 Locomotion으로")] private float _castEndNormalized = 0.95f;

        /// <summary>현재 이동 방향(모델 기준)에 맞는 이동 시전 상태. 움직이지 않으면 null</summary>
        private string MoveCastState()
        {
            if (_moveCastStates == null || _moveCastStates.Length < 4) return null;
            Vector3 _v = Velocity; _v.y = 0f;
            if (_v.sqrMagnitude < 0.04f) return null;   // 움직이지 않으면(이동 입력 없음) 서서 시전
            Vector3 _local = _model ? _model.InverseTransformDirection(_v) : _v;
            if (Mathf.Abs(_local.z) >= Mathf.Abs(_local.x)) return _local.z >= 0f ? _moveCastStates[0] : _moveCastStates[1];
            return _local.x < 0f ? _moveCastStates[2] : _moveCastStates[3];
        }

        private bool InState(string _s) { return _animator && _animator.GetCurrentAnimatorStateInfo(0).IsName(_s); }

        private System.Collections.IEnumerator CastRoutine(int _staff, Element _orb)
        {
            IsCasting = true;
            HistoryCrash.Sound.GameSfx.Play("Player_Cast");
            SpeedScale = _castMoveSpeedScale;   // 시전 중에도 이동 가능(느리게)
            float _wait = 0.1f;
            string _standState = _animator && _staff >= 0 && _staff < _castStates.Length ? _castStates[_staff] : null;
            string _playing = null;
            if (_animator && _standState != null)
            {
                _animator.speed = _castAnimSpeed;
                _playing = MoveCastState() ?? _standState;
                _animator.CrossFadeInFixedTime(_playing, 0.1f);
                _wait = _releaseTimes[_staff] / _castAnimSpeed;
                // C는 보석 3개를 주위에 사출한 뒤 발사하므로, 사출 시간만큼 먼저 시전해 첫 발사를 모션 발사 시점에 맞춤
                if (_staff == 2) _wait = Mathf.Max(0.05f, _wait - PlayerStaffSkill.C_SUMMON_LEAD);
                if (_staff == 4) _wait = Mathf.Max(0.05f, _wait - PlayerStaffSkill.E_SUMMON_LEAD);   // E도 창 소환 시간만큼 먼저
            }
            ElementFx.Trail(_orb, transform, 0.1f, 40f).transform.localPosition = Vector3.up * 0.35f;   // 시전 중 속성 기운

            // 발동까지: 이동 여부·방향이 바뀌면 이동 시전 모션으로 전환(같은 진행도 유지)
            for (float _t = 0f; _t < _wait; _t += Time.deltaTime)
            {
                if (_animator && _standState != null)
                {
                    string _want = MoveCastState() ?? (_playing == _standState ? _standState : _playing);   // 멈추면 이동 시전 모션은 그대로 마저 재생
                    if (_want != _playing)
                    {
                        float _n = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
                        _animator.CrossFade(_want, 0.15f, 0, _n);
                        _playing = _want;
                    }
                }
                yield return null;
            }
            FireSkill(_staff, _orb);
            yield return new WaitForSeconds(_castRecovery);
            foreach (var _ps in GetComponentsInChildren<ParticleSystem>()) if (_ps.name.StartsWith("Fx_Trail")) { var _em = _ps.emission; _em.rateOverTime = 0; _ps.transform.SetParent(null, true); Destroy(_ps.gameObject, 0.6f); }
            IsCasting = false;   // 다음 공격 허용

            // 모션 마무리(잘림 방지): 시전 모션을 끝까지 재생한 뒤 Locomotion으로
            //  - 서서 시전: 끝까지 재생. 도중에 이동 입력이 들어오면 이동 시전 모션으로 바꿔 마저 재생
            //  - 이동 시전: 끝까지 재생(방향이 바뀌면 그 방향 모션으로), 다리 속도에 맞춰 이동 속도는 줄인 채 유지
            if (_animator && _playing != null)
            {
                float _guard = 4f;
                while (_guard > 0f && !IsCasting)
                {
                    var _st = _animator.GetCurrentAnimatorStateInfo(0);
                    bool _inCast = InState(_playing) || _animator.IsInTransition(0);
                    if (!_inCast || _st.normalizedTime >= _castEndNormalized) break;
                    string _want = MoveCastState();
                    if (_want != null && _want != _playing)
                    {
                        _animator.CrossFade(_want, 0.15f, 0, _st.normalizedTime % 1f);
                        _playing = _want;
                    }
                    SpeedScale = _playing == _standState ? 1f : _castMoveSpeedScale;
                    _guard -= Time.deltaTime;
                    yield return null;
                }
                if (!IsCasting)
                {
                    SpeedScale = 1f;
                    if (InState(_playing) || _animator.IsInTransition(0)) { _animator.speed = 1f; _animator.CrossFadeInFixedTime("Locomotion", 0.25f); }
                }
            }
            else SpeedScale = 1f;
        }

        private void FireSkill(int _heldStaff, Element _heldOrb)
        {
            // 공격으로 오브를 소비: 지팡이 홀의 오브가 터지며 사라짐(시전 중 새 오브를 주웠다면 그대로 둠)
            if (!HasOrb) { _heldOrbForFx = _heldOrb; ClearSocketOrb(true); }

            // 주운 스태프의 패턴을 약화판으로 사용(모양 = 패턴 오브젝트, 색·효과 = 오브 속성)
            // 방향 = 발사되는 순간의 화면 방향, B·D 위치 = 플레이어 자리
            Vector3 _dir = _model ? _model.forward : transform.forward;
            if (_camera) { _dir = Vector3.ProjectOnPlane(_camera.forward, Vector3.up); }
            _dir.y = 0; _dir = _dir.sqrMagnitude > 1e-4f ? _dir.normalized : Vector3.forward;
            if (!_director) _director = FindFirstObjectByType<BossPatternDirector>();
            PlayerStaffSkill.Cast(_heldStaff, _heldOrb, transform, _dir, _grid, _director ? _director.PatternVisual(_heldStaff) : null);
            Debug.Log("[Loadout] Attack staff " + _staffNames[_heldStaff] + " + orb " + ElementInfo.Name(_heldOrb));
        }

        private void OnGUI()
        {
            if (HistoryCrash.UI.PauseSettingsUI.IsOpen) return;   // 설정창이 열리면 OnGUI 글자는 그리지 않음(항상 UI 위에 그려지므로)
            var _it = FindPickable();
            if (_it)
            {
                var _style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                // 설정창 Language에 따라 한국어/영어
                string _what = _it.Kind == PickupKind.Staff
                    ? HistoryCrash.UI.GameLanguage.T("무기 " + _staffNames[_it.Value], "Weapon " + _staffNames[_it.Value])
                    : HistoryCrash.UI.GameLanguage.T(ElementInfo.NameKo(_it.ElementValue) + " 오브", ElementInfo.Name(_it.ElementValue) + " Orb");
                string _msg = HistoryCrash.UI.GameLanguage.T("[E] " + _what + " 줍기", "[E] Pick up " + _what);
                GUI.Label(new Rect(Screen.width / 2 - 160, Screen.height * 0.62f, 320, 30), _msg, _style);
            }
            string _staff = HeldStaff >= 0 ? _staffNames[HeldStaff] : "-";
            string _orb = HasOrb ? ElementInfo.NameKo(HeldOrb) : "-";
            GUI.Label(new Rect(20, HistoryCrash.UI.DebugHudLayout.Top + 84, 360, 20), "Staff: " + _staff + "   Orb: " + _orb + (CanAttack ? "   [좌클릭] 공격" : ""));
        }
    }
}
