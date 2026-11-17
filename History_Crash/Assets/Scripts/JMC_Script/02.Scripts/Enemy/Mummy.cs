using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Enemy
{
    /// <summary>
    /// 소환된 머미.
    /// - Player를 향해 Player 이동속도의 speedRatio(60%)로 달려든다(걷기 불가 칸·구멍은 피함).
    /// - 살아 있는 동안 orbInterval초마다 Player 주변 orbRadius칸 안 바닥에 자기 속성 오브를 떨어뜨린다.
    /// - 같은 속성 공격 1회에 사망: 뒤에서 맞으면 Dying(앞으로 쓰러짐), 앞에서 맞으면 Death(뒤로 쓰러짐).
    /// - 쓰러진 뒤 녹아내리듯 바닥으로 꺼지고, 영혼이 떠올라 파라오에게 날아가 체력을 깎는다.
    /// </summary>
    public class Mummy : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("gender")] private MummyGender _gender;
        [SerializeField, FormerlySerializedAs("element")] private Element _element;
        public Element Element { get => _element; set => _element = value; }

        [Header("참조(소환기가 채움)")]
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        public TileGrid Grid { get => _grid; set => _grid = value; }
        [SerializeField, FormerlySerializedAs("player")] private Transform _player;
        public Transform Player { get => _player; set => _player = value; }
        [SerializeField, FormerlySerializedAs("playerMover")] private PlayerMover _playerMover;
        public PlayerMover PlayerMover { get => _playerMover; set => _playerMover = value; }
        [SerializeField, FormerlySerializedAs("playerHealth")] private PlayerHealth _playerHealth;
        public PlayerHealth PlayerHealth { get => _playerHealth; set => _playerHealth = value; }
        [SerializeField, FormerlySerializedAs("pharaoh")] private Transform _pharaoh;
        public Transform Pharaoh { get => _pharaoh; set => _pharaoh = value; }
        [SerializeField, FormerlySerializedAs("pharaohHealth")] private PharaohHealth _pharaohHealth;
        public PharaohHealth PharaohHealth { get => _pharaohHealth; set => _pharaohHealth = value; }
        [SerializeField, FormerlySerializedAs("animator")] private Animator _animator;
        public Animator Animator { get => _animator; set => _animator = value; }
        [SerializeField, FormerlySerializedAs("shellRenderer"), Tooltip("속성 발광을 입힐 에너지 껍질 렌더러")] private Renderer _shellRenderer;

        [Header("이동")]
        [SerializeField, FormerlySerializedAs("speedRatio"), Tooltip("Player 이동속도 대비 배율. 걷기 클립 보폭(0.16 m/s)과 맞추려고 0.6→0.25(약 1.0 m/s)"), Range(0.1f, 1.5f)] private float _speedRatio = 0.25f;
        [SerializeField, FormerlySerializedAs("turnSpeed")] private float _turnSpeed = 540f;
        [SerializeField, FormerlySerializedAs("radius")] private float _radius = 0.25f;
        [SerializeField, FormerlySerializedAs("walkClipSpeed"), Tooltip("걷기 클립 1배속 이동속도(m/s) — Mummy_*_Walk 측정값 0.16")] private float _walkClipSpeed = 0.16f;
        [SerializeField, FormerlySerializedAs("maxWalkAnimSpeed"), Tooltip("걷기 재생 배속 상한. 1.0 m/s ÷ 0.16 ≈ 6.3배")] private float _maxWalkAnimSpeed = 6.5f;
        [SerializeField, FormerlySerializedAs("stopDistance"), Tooltip("이 거리 안이면 멈춰서 붙어 있음")] private float _stopDistance = 0.45f;
        [SerializeField, FormerlySerializedAs("contactDamage")] private int _contactDamage = 1;
        [SerializeField, FormerlySerializedAs("separation"), Tooltip("다른 머미와 이 거리(m) 안이면 서로 밀어냄")] private float _separation = 0.85f;
        [SerializeField, FormerlySerializedAs("separationWeight")] private float _separationWeight = 2.2f;

        [Header("오브 드랍")]
        [SerializeField, FormerlySerializedAs("orbInterval")] private float _orbInterval = 5f;
        [SerializeField, FormerlySerializedAs("orbRadius")] private int _orbRadius = 3;

        [Header("사망")]
        [SerializeField, FormerlySerializedAs("deathAnimSpeed")] private float _deathAnimSpeed = 1.5f;
        [SerializeField, FormerlySerializedAs("deathHold")] private float _deathHold = 2.0f;
        [SerializeField, FormerlySerializedAs("meltTime")] private float _meltTime = 1.4f;
        [SerializeField, FormerlySerializedAs("meltDepth")] private float _meltDepth = 0.9f;
        [SerializeField, FormerlySerializedAs("pharaohDamage")] private int _pharaohDamage = 20;
        [SerializeField, FormerlySerializedAs("spiritRise")] private float _spiritRise = 1.4f;
        [SerializeField, FormerlySerializedAs("spiritRiseTime")] private float _spiritRiseTime = 0.7f;
        [SerializeField, FormerlySerializedAs("spiritFlyTime")] private float _spiritFlyTime = 1.0f;

        public bool IsAlive { get; private set; } = true;
        public bool Active { get; private set; }
        private float _nextOrb;
        private MaterialPropertyBlock _mpb;

        private static readonly System.Collections.Generic.List<Mummy> _alive = new System.Collections.Generic.List<Mummy>();
        public static System.Collections.Generic.List<Mummy> Alive => _alive;

        private void OnEnable() { _alive.Add(this); }
        private void OnDisable() { _alive.Remove(this); }

        public void Init(MummyGender _g, Element _e)
        {
            _gender = _g; _element = _e;
            ApplyElementLook();
        }

        private void ApplyElementLook()
        {
            if (!_shellRenderer) return;
            _mpb = _mpb ?? new MaterialPropertyBlock();
            _shellRenderer.GetPropertyBlock(_mpb);
            // 주변을 비추는 광원 없이, 머미 자신에게만 속성색을 입힌다
            Color _c = ElementInfo.Color(_element);
            _mpb.SetColor("_EmissionColor", _c * 5f);
            _mpb.SetColor("_BaseColor", new Color(_c.r, _c.g, _c.b, 0.42f));
            _shellRenderer.SetPropertyBlock(_mpb);
            // 붕대(몸)도 속성색으로 물들임
            foreach (var _r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (_r == _shellRenderer) continue;
                var _b = new MaterialPropertyBlock(); _r.GetPropertyBlock(_b);
                _b.SetColor("_BaseColor", Color.Lerp(Color.white, _c, 0.45f));
                _r.SetPropertyBlock(_b);
            }
            // 몸 주위에서 피어오르는 속성 불티
            var _aura = ElementFx.Trail(_element, transform, 0.11f, 35f);
            _aura.transform.localPosition = new Vector3(0, 0.45f, 0);
            var _am = _aura.main; _am.gravityModifier = -0.35f; _am.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            var _ash = _aura.shape; _ash.radius = 0.22f;
            // 머리 위 속성 표식(작게 빛나는 구슬)
            _head = FindDeep(transform, "Head");
            _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Destroy(_marker.GetComponent<Collider>());
            _marker.name = "ElementMarker"; _marker.SetParent(transform, true);
            _marker.localScale = Vector3.one * _markerSize;
            _marker.GetComponent<Renderer>().sharedMaterial = ElementFx.GlowMat(_element, 6f);
            _marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        [Header("속성 표시")]
        [SerializeField, FormerlySerializedAs("markerSize")] private float _markerSize = 0.09f;
        [SerializeField, FormerlySerializedAs("markerHeight")] private float _markerHeight = 0.28f;
        private Transform _head, _marker;

        private static Transform FindDeep(Transform _t, string _name)
        {
            if (_t.name == _name) return _t;
            foreach (Transform _c in _t) { var _r = FindDeep(_c, _name); if (_r) return _r; }
            return null;
        }

        private void LateUpdate()
        {
            if (!_marker) return;
            Vector3 _basePos = _head ? _head.position : transform.position + Vector3.up * 1f;
            _marker.position = _basePos + Vector3.up * (_markerHeight + Mathf.Sin(Time.time * 3f + GetInstanceID()) * 0.03f);
            if (!IsAlive) _marker.gameObject.SetActive(false);
        }

        /// <summary>소환 연출(바닥에서 솟아오름)이 끝나면 호출</summary>
        public void Activate()
        {
            Active = true;
            _nextOrb = Time.time + _orbInterval;
            if (_animator) _animator.Play("Walk", 0, Random.value);
        }

        private void Update()
        {
            if (!IsAlive || !Active || !_player) return;
            Vector3 _p = transform.position;
            Vector3 _to = _player.position - _p; _to.y = 0;
            float _dist = _to.magnitude;
            float _speed = (_playerMover ? _playerMover.MoveSpeed : 4f) * _speedRatio;
            float _moved = 0f;
            // 다른 머미와 겹치지 않게: 가까우면 서로 밀어내는 방향을 더한다
            Vector3 _sep = Vector3.zero;
            foreach (var _o in _alive)
            {
                if (_o == this || !_o || !_o.IsAlive) continue;
                Vector3 _d = _p - _o.transform.position; _d.y = 0;
                float _m = _d.magnitude;
                if (_m < _separation && _m > 1e-4f) _sep += _d / _m * (1f - _m / _separation);
                else if (_m <= 1e-4f) _sep += Quaternion.Euler(0, GetInstanceID() % 360, 0) * Vector3.forward;
            }
            if (_dist > _stopDistance || _sep.sqrMagnitude > 1e-4f)
            {
                Vector3 _dir = _dist > _stopDistance ? _to / _dist : Vector3.zero;
                Vector3 _steer = _dir + _sep * _separationWeight;
                if (_steer.sqrMagnitude > 1f) _steer.Normalize();
                Vector3 _delta = _steer * _speed * Time.deltaTime;
                if (_dir == Vector3.zero) _dir = _to.sqrMagnitude > 1e-4f ? _to.normalized : transform.forward;
                // 축별로 막힘 검사(걷기 불가 칸·구멍이면 그 축 이동 취소) → 벽을 따라 미끄러짐
                Vector3 _px = _p + new Vector3(_delta.x, 0, 0);
                if (Blocked(_px, Mathf.Sign(_delta.x), 0)) _delta.x = 0;
                Vector3 _pz = _p + new Vector3(_delta.x, 0, _delta.z);
                if (Blocked(_pz, 0, Mathf.Sign(_delta.z))) _delta.z = 0;
                transform.position = _p + _delta;
                _moved = _delta.magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
                var _target = Quaternion.LookRotation(_dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _target, _turnSpeed * Time.deltaTime);
            }
            if (_animator) _animator.speed = Mathf.Clamp(_moved / Mathf.Max(_walkClipSpeed, 0.01f), 0.6f, _maxWalkAnimSpeed);

            // 접촉 피해(기획: 1). PlayerHealth의 피격 무적이 연속 피해를 막는다.
            if (_contactDamage > 0 && _playerHealth && _dist < _radius + 0.3f && _playerMover && _playerMover.HeightAboveGround < 0.3f)
                _playerHealth.TakeDamage(_contactDamage, "Mummy " + ElementInfo.Name(_element));

            if (Time.time >= _nextOrb) { _nextOrb = Time.time + _orbInterval; DropOrbNearPlayer(); }
        }

        private bool Blocked(Vector3 _pos, float _sx, float _sz)
        {
            if (!_grid) return false;
            Vector3 _probe = _pos + new Vector3(_sx * _radius, 0, _sz * _radius);
            var _c = _grid.WorldToCell(_probe);
            return !_grid.IsSolid(_c);
        }

        private void DropOrbNearPlayer()
        {
            if (!_grid) return;
            var _pc = _grid.WorldToCell(_player.position);
            var _list = new System.Collections.Generic.List<Vector2Int>();
            for (int _dy = -_orbRadius; _dy <= _orbRadius; _dy++)
                for (int _dx = -_orbRadius; _dx <= _orbRadius; _dx++)
                {
                    var _c = new Vector2Int(_pc.x + _dx, _pc.y + _dy);
                    if (_grid.IsSolid(_c) && !PickupItem.CellTaken(_c)) _list.Add(_c);
                }
            if (_list.Count == 0) return;
            var _cell = _list[Random.Range(0, _list.Count)];
            // 오브 수명 = 드랍 주기: 머미 한 마리당 바닥에 오브가 하나씩만 있게(우후죽순 방지)
            ElementOrb.Spawn(_element, _cell, _grid, _orbInterval);
        }

        /// <summary>공격체가 닿았을 때. 속성이 같을 때만 맞고 true.</summary>
        public bool TryHit(Element _attack, Vector3 _travelDir)
        {
            if (!IsAlive || !Active || _attack != _element) return false;
            IsAlive = false;
            StartCoroutine(Die(_travelDir));
            return true;
        }

        private IEnumerator Die(Vector3 _travelDir)
        {
            _travelDir.y = 0;
            // 공격이 머미의 등 쪽에서 와서 앞으로 진행하면 뒤에서 맞은 것 → 앞으로 쓰러짐(Dying)
            bool _fromBehind = Vector3.Dot(transform.forward, _travelDir.normalized) > 0f;
            string _state = _fromBehind ? "Dying" : "Death";
            ElementFx.Burst(_element, transform.position + Vector3.up * 0.6f, 1.2f, 30);
            if (_animator) { _animator.speed = _deathAnimSpeed; _animator.CrossFadeInFixedTime(_state, 0.08f); }
            Debug.Log("[Mummy] " + _gender + " " + ElementInfo.Name(_element) + " hit from " + (_fromBehind ? "behind → Dying" : "front → Death"));
            yield return new WaitForSeconds(_deathHold);

            // 녹아내림: 바닥 아래로 꺼지며 납작해지고, 속성색 불꽃이 바닥에서 피어오름
            ElementFx.Flame(_element, new Vector3(transform.position.x, _grid ? _grid.FloorTopY : transform.position.y, transform.position.z), _meltTime, 0.4f);
            Vector3 _start = transform.position; Vector3 _scale0 = transform.localScale;
            for (float _t = 0; _t < _meltTime; _t += Time.deltaTime)
            {
                float _u = _t / _meltTime; float _e = _u * _u;
                transform.position = _start - Vector3.up * _meltDepth * _e;
                transform.localScale = new Vector3(_scale0.x * (1f + 0.25f * _e), _scale0.y * (1f - 0.7f * _e), _scale0.z * (1f + 0.25f * _e));
                yield return null;
            }

            // 영혼: 떠오른 뒤 파라오에게 날아감
            var _spirit = new GameObject("Mummy_Spirit_" + ElementInfo.Name(_element));
            _spirit.transform.position = new Vector3(_start.x, (_grid ? _grid.FloorTopY : _start.y) + 0.3f, _start.z);
            var _core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_core.GetComponent<Collider>());
            _core.transform.SetParent(_spirit.transform, false); _core.transform.localScale = Vector3.one * 0.16f;
            _core.GetComponent<Renderer>().sharedMaterial = ElementFx.GlowMat(_element, 6f);
            ElementFx.Trail(_element, _spirit.transform, 0.22f, 90f);
            ElementFx.Glow(_element, _spirit.transform, 2.5f, 3f);
            foreach (var _r in GetComponentsInChildren<Renderer>()) _r.enabled = false;

            Vector3 _a = _spirit.transform.position, _b = _a + Vector3.up * _spiritRise;
            for (float _t = 0; _t < _spiritRiseTime; _t += Time.deltaTime)
            {
                float _u = _t / _spiritRiseTime; _u = 1f - (1f - _u) * (1f - _u);
                _spirit.transform.position = Vector3.Lerp(_a, _b, _u) + new Vector3(Mathf.Sin(_t * 9f) * 0.05f, 0, 0);
                yield return null;
            }
            Vector3 _target = _pharaoh ? PharaohCenter() : _b + Vector3.up;
            Vector3 _mid = (_b + _target) * 0.5f + Vector3.up * 1.6f;
            for (float _t = 0; _t < _spiritFlyTime; _t += Time.deltaTime)
            {
                float _u = _t / _spiritFlyTime; _u = _u * _u * (3f - 2f * _u);
                if (_pharaoh) _target = PharaohCenter();
                Vector3 _q = Vector3.Lerp(Vector3.Lerp(_b, _mid, _u), Vector3.Lerp(_mid, _target, _u), _u);
                _spirit.transform.position = _q;
                yield return null;
            }
            ElementFx.Burst(_element, _target, 1.6f, 40);
            if (_pharaohHealth) _pharaohHealth.Damage(_pharaohDamage, "Mummy spirit " + ElementInfo.Name(_element));
            var _trail = _spirit.GetComponentInChildren<ParticleSystem>();
            if (_trail) { _trail.transform.SetParent(null, true); var _em = _trail.emission; _em.rateOverTime = 0; Destroy(_trail.gameObject, 1f); }
            Destroy(_spirit);
            Destroy(gameObject);
        }

        private Vector3 PharaohCenter()
        {
            var _rs = _pharaoh.GetComponentsInChildren<Renderer>();
            if (_rs.Length == 0) return _pharaoh.position + Vector3.up;
            Bounds _b = _rs[0].bounds; foreach (var _r in _rs) _b.Encapsulate(_r.bounds);
            return _b.center;
        }
    }
}
