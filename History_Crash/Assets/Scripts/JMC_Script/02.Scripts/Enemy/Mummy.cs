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
    /// - 쓰러진 뒤 머리 위에 속성 구체가 생기고, 시체가 구체로 빨려 들어간 다음 구체가 파라오에게 날아가 체력을 깎는다.
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
        // 플레이어·파라오 연결 (KDH 코어 인터페이스, 소환기가 채움)
        private IJumpState _playerJump;
        public IJumpState PlayerJump { get => _playerJump; set => _playerJump = value; }
        private float _playerMoveSpeed = 4f;
        public float PlayerMoveSpeed { get => _playerMoveSpeed; set => _playerMoveSpeed = value; }
        private IDamageable _playerHealth;
        public IDamageable PlayerHealth { get => _playerHealth; set => _playerHealth = value; }
        [SerializeField, FormerlySerializedAs("pharaoh")] private Transform _pharaoh;
        public Transform Pharaoh { get => _pharaoh; set => _pharaoh = value; }
        private IDamageable _pharaohHealth;
        public IDamageable PharaohHealth { get => _pharaohHealth; set => _pharaohHealth = value; }
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
        [SerializeField, FormerlySerializedAs("pharaohDamage")] private int _pharaohDamage = 20;
        /// <summary>처치 시 파라오에게 주는 피해 (소환기가 채움)</summary>
        public int PharaohDamage { get => _pharaohDamage; set => _pharaohDamage = value; }
        [SerializeField, FormerlySerializedAs("spiritFlyTime")] private float _spiritFlyTime = 1.0f;

        [Header("영혼 흡수 (시체가 구체로 빨려 들어감)")]
        [SerializeField, Tooltip("구체가 생기는 높이 (바닥 기준 m)")] private float _soulHeight = 1.5f;
        [SerializeField, Tooltip("구체가 커지며 나타나는 시간 (초)")] private float _soulFormTime = 0.4f;
        [SerializeField, Tooltip("시체가 구체로 빨려 들어가는 시간 (초)")] private float _absorbTime = 0.8f;
        [SerializeField, Tooltip("다 빨아들인 뒤 구체가 한 번 부풀었다 돌아오는 시간 (초)")] private float _soulPulseTime = 0.25f;
        [SerializeField, Tooltip("빨려 드는 동안 시체가 도는 각도")] private float _absorbSpin = 540f;
        [SerializeField, Tooltip("시체에서 구체로 흘러가는 입자 수 (초당)")] private float _absorbParticleRate = 160f;

        public bool IsAlive { get; private set; } = true;
        public bool Active { get; private set; }
        private float _nextOrb;

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
            Color _c = ElementInfo.Color(_element);
            // 미라 모습은 속성과 관계없이 기본형 그대로(속성색 에너지 껍질·불티·머리 위 구슬 없음) — 속성색은 몸 테두리(아웃라인)로만 표시
            if (_shellRenderer) _shellRenderer.enabled = false;
            AddOutline(_c);
        }

        [Header("속성 아웃라인")]
        [SerializeField, Tooltip("몸 테두리 두께(m)")] private float _outlineWidth = 0.012f;
        [SerializeField, Tooltip("테두리 색 밝기(1보다 작으면 속성색보다 어둡게, 1보다 크면 블룸으로 빛남)")] private float _outlineIntensity = 0.7f;
        private const string OUTLINE_SHADER = "HistoryCrash_Outline";   // Assets/Shaders/Resources/
        private static Shader _outlineShader;
        private Material _outlineMat;

        // 몸 렌더러의 머티리얼 칸 뒤에 아웃라인 머티리얼을 하나 더 붙임(원래 머티리얼은 그대로)
        private void AddOutline(Color _c)
        {
            if (!_outlineShader) _outlineShader = Resources.Load<Shader>(OUTLINE_SHADER);
            if (!_outlineShader) { Debug.LogWarning("[Mummy] 아웃라인 셰이더를 찾지 못했습니다: Resources/" + OUTLINE_SHADER, this); return; }
            _outlineMat = new Material(_outlineShader) { name = "MummyOutline_" + ElementInfo.Name(_element) };
            _outlineMat.SetColor("_OutlineColor", _c * _outlineIntensity);
            _outlineMat.SetFloat("_OutlineWidth", _outlineWidth);
            foreach (var _r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (_r == _shellRenderer) continue;
                var _mats = _r.sharedMaterials;
                System.Array.Resize(ref _mats, _mats.Length + 1);
                _mats[_mats.Length - 1] = _outlineMat;
                _r.sharedMaterials = _mats;
            }
        }

        private void OnDestroy() { if (_outlineMat) Destroy(_outlineMat); }

        /// <summary>소환 연출(바닥에서 솟아오름)이 끝나면 호출</summary>
        public void Activate()
        {
            Active = true;
            _nextOrb = Time.time + _orbInterval;
            if (_animator) _animator.Play("Walk", 0, Random.value);
            _walkSfx = HistoryCrash.Sound.GameSfx.Play("Mummy_Walk", transform, 0f, true);
        }
        private HistoryCrash.Sound.GameSfx.Voice _walkSfx;   // 걷는 동안 반복

        private void Update()
        {
            if (!IsAlive || !Active || !_player) return;
            Vector3 _p = transform.position;
            Vector3 _to = _player.position - _p; _to.y = 0;
            float _dist = _to.magnitude;
            float _speed = _playerMoveSpeed * _speedRatio;
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
            HistoryCrash.Sound.GameSfx.SetGain(_walkSfx, _moved > 0.05f ? 1f : 0f);
            if (_animator) _animator.speed = Mathf.Clamp(_moved / Mathf.Max(_walkClipSpeed, 0.01f), 0.6f, _maxWalkAnimSpeed);

            // 접촉 피해(기획: 1). PlayerHealth의 피격 무적이 연속 피해를 막는다.
            if (_contactDamage > 0 && _playerHealth != null && _dist < _radius + 0.3f && _playerJump != null && _playerJump.GetHeightAboveGround() < 0.3f)
                _playerHealth.TakeDamage(new DamageInfo(_contactDamage, ElementType.None, gameObject));

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
            HistoryCrash.Sound.GameSfx.FadeOut(_walkSfx, 0.15f);
            HistoryCrash.Sound.GameSfx.Play("Mummy_Death", transform.position);
            // 공격이 머미의 등 쪽에서 와서 앞으로 진행하면 뒤에서 맞은 것 → 앞으로 쓰러짐(Dying)
            bool _fromBehind = Vector3.Dot(transform.forward, _travelDir.normalized) > 0f;
            string _state = _fromBehind ? "Dying" : "Death";
            ElementFx.Burst(_element, transform.position + Vector3.up * 0.6f, 1.2f, 30);
            if (_animator) { _animator.speed = _deathAnimSpeed; _animator.CrossFadeInFixedTime(_state, 0.08f); }
            Debug.Log("[Mummy] " + _gender + " " + ElementInfo.Name(_element) + " hit from " + (_fromBehind ? "behind → Dying" : "front → Death"));
            yield return new WaitForSeconds(_deathHold);

            // 1) 쓰러진 시체 위에 속성 구체가 커지며 나타남
            if (_animator) _animator.speed = 0f;   // 쓰러진 자세 그대로 멈춤
            var _bodyRenderers = GetComponentsInChildren<Renderer>();
            Bounds _body = BodyBounds(_bodyRenderers);
            float _floorY = _grid ? _grid.FloorTopY : transform.position.y;
            Vector3 _soulPos = new Vector3(_body.center.x, _floorY + _soulHeight, _body.center.z);

            var _spirit = new GameObject("Mummy_Spirit_" + ElementInfo.Name(_element));
            _spirit.transform.position = _soulPos;
            var _core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_core.GetComponent<Collider>());
            _core.transform.SetParent(_spirit.transform, false); _core.transform.localScale = Vector3.zero;
            _core.GetComponent<Renderer>().sharedMaterial = ElementFx.GlowMat(_element, 6f);
            ElementFx.Trail(_element, _spirit.transform, 0.22f, 90f);
            var _light = ElementFx.Glow(_element, _spirit.transform, 0f, 3f);

            // 파라오 피격 소리는 1.2초쯤 가장 커지므로, 영혼이 닿는 순간에 맞춰 먼저 시작
            float _untilHit = _soulFormTime + _absorbTime + _soulPulseTime + _spiritFlyTime;
            if (_pharaoh) HistoryCrash.Sound.GameSfx.Play("Pharaoh_Attacked", _pharaoh, Mathf.Max(0f, _untilHit - 1.2f), false, true);

            const float CORE_SIZE = 0.16f;
            for (float _t = 0; _t < _soulFormTime; _t += Time.deltaTime)
            {
                float _u = _t / _soulFormTime; _u = 1f - (1f - _u) * (1f - _u);
                _core.transform.localScale = Vector3.one * CORE_SIZE * 0.7f * _u;
                _light.intensity = 1.5f * _u;
                yield return null;
            }

            // 2) 시체가 돌면서 구체 쪽으로 늘어나며 빨려 들어감 + 속성색으로 빛남 + 입자가 구체로 흘러감
            // 흡수음: 0.8초(다 삼키는 순간)에 가장 커지도록 편집된 파일
            HistoryCrash.Sound.GameSfx.Play("Mummy_SoulAbsorb", _soulPos);
            var _stream = AbsorbStream(_body);
            var _glowMats = StartBodyGlow(_bodyRenderers);
            Color _glowColor = ElementInfo.Color(_element);
            Vector3 _pos0 = transform.position, _scale0 = transform.localScale;
            Quaternion _rot0 = transform.rotation;
            Vector3 _offset = _body.center - _pos0;   // 발밑 기준점에서 몸 중심까지
            for (float _t = 0; _t < _absorbTime; _t += Time.deltaTime)
            {
                float _u = _t / _absorbTime; float _e = _u * _u;   // 점점 빨라지게 빨려 듦
                float _xz = 1f - _e, _y = (1f + 1.2f * _e) * (1f - _e);
                Quaternion _spin = Quaternion.Euler(0f, _absorbSpin * _e, 0f);
                Vector3 _o = _spin * _offset;
                Vector3 _center = Vector3.Lerp(_body.center, _soulPos, _e);
                transform.rotation = _spin * _rot0;
                transform.localScale = new Vector3(_scale0.x * _xz, _scale0.y * _y, _scale0.z * _xz);
                transform.position = _center - new Vector3(_o.x * _xz, _o.y * _y, _o.z * _xz);
                foreach (var _m in _glowMats) _m.SetColor("_EmissionColor", _glowColor * (4f * _u));
                _core.transform.localScale = Vector3.one * CORE_SIZE * Mathf.Lerp(0.7f, 1.3f, _u);
                _light.intensity = Mathf.Lerp(1.5f, 3.5f, _u);
                PullParticles(_stream, _soulPos, _u);
                yield return null;
            }
            foreach (var _r in _bodyRenderers) if (_r) _r.enabled = false;
            foreach (var _m in _glowMats) Destroy(_m);
            var _streamEm = _stream.emission; _streamEm.rateOverTime = 0f;

            // 3) 다 삼키면 한 번 크게 부풀었다가 원래 크기로
            ElementFx.Burst(_element, _soulPos, 0.8f, 20);
            for (float _t = 0; _t < _soulPulseTime; _t += Time.deltaTime)
            {
                float _u = _t / _soulPulseTime;
                // 흡수 끝 크기(1.3배)에서 이어서 부풀었다가 원래 크기로
                _core.transform.localScale = Vector3.one * CORE_SIZE * (Mathf.Lerp(1.3f, 1f, _u) + 1.0f * Mathf.Sin(_u * Mathf.PI));
                _light.intensity = Mathf.Lerp(3.5f, 2.5f, _u);
                PullParticles(_stream, _soulPos, 1f);
                yield return null;
            }
            _core.transform.localScale = Vector3.one * CORE_SIZE;
            Destroy(_stream.gameObject);

            // 4) 파라오에게 날아감
            Vector3 _b = _soulPos;
            Vector3 _target = _pharaoh ? PharaohCenter() : _b + Vector3.up;
            Vector3 _mid = (_b + _target) * 0.5f + Vector3.up * 1.6f;
            // 비행음: 비행 시간(1.0초)과 같은 길이로 편집, 구체를 따라감
            HistoryCrash.Sound.GameSfx.Play("Mummy_SoulFly", _spirit.transform);
            for (float _t = 0; _t < _spiritFlyTime; _t += Time.deltaTime)
            {
                float _u = _t / _spiritFlyTime; _u = _u * _u * (3f - 2f * _u);
                if (_pharaoh) _target = PharaohCenter();
                Vector3 _q = Vector3.Lerp(Vector3.Lerp(_b, _mid, _u), Vector3.Lerp(_mid, _target, _u), _u);
                _spirit.transform.position = _q;
                yield return null;
            }
            ElementFx.Burst(_element, _target, 1.6f, 40);
            HistoryCrash.Sound.GameSfx.Play("Mummy_SoulHit", _target);
            if (_pharaohHealth != null) _pharaohHealth.TakeDamage(new DamageInfo(_pharaohDamage, ElementType.None, gameObject));   // 미라 1킬 = 파라오 피해 (기획 v20: 20, 소환기에서 조정)
            var _trail = _spirit.GetComponentInChildren<ParticleSystem>();
            if (_trail) { _trail.transform.SetParent(null, true); var _em = _trail.emission; _em.rateOverTime = 0; Destroy(_trail.gameObject, 1f); }
            Destroy(_spirit);
            Destroy(gameObject);
        }

        private static Bounds BodyBounds(Renderer[] _rs)
        {
            Bounds _b = new Bounds();
            bool _has = false;
            foreach (var _r in _rs)
            {
                if (!_r || !_r.enabled) continue;
                if (_has) _b.Encapsulate(_r.bounds); else { _b = _r.bounds; _has = true; }
            }
            return _b;
        }

        // 몸 머티리얼을 복사해 발광을 켬 (원본 공용 머티리얼은 그대로). 다 쓰면 호출한 쪽에서 Destroy
        private static System.Collections.Generic.List<Material> StartBodyGlow(Renderer[] _rs)
        {
            var _list = new System.Collections.Generic.List<Material>();
            foreach (var _r in _rs)
            {
                if (!_r || !_r.enabled) continue;
                foreach (var _m in _r.materials)
                {
                    _list.Add(_m);
                    if (!_m.HasProperty("_EmissionColor")) continue;
                    _m.EnableKeyword("_EMISSION");
                    _m.SetColor("_EmissionColor", Color.black);
                }
            }
            return _list;
        }

        // 시체 둘레에서 속성색 입자가 생겨 구체로 흘러감 (속도는 PullParticles가 매 프레임 구체 쪽으로 맞춤)
        private ParticleSystem AbsorbStream(Bounds _body)
        {
            Color _c = ElementInfo.Color(_element);
            var _go = new GameObject("Fx_SoulAbsorb_" + _element);
            _go.transform.position = _body.center;
            var _ps = _go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var _main = _ps.main;
            _main.maxParticles = 400; _main.loop = true; _main.playOnAwake = false;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.startLifetime = 2f; _main.startSpeed = 0f;
            _main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            _main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_c, Color.white, 0.5f), _c);
            var _em = _ps.emission; _em.rateOverTime = _absorbParticleRate;
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Box; _sh.scale = _body.size;
            var _r = _go.GetComponent<ParticleSystemRenderer>();
            _r.sharedMaterial = ElementFx.ParticleMat();
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ps.Play();
            return _ps;
        }

        private static ParticleSystem.Particle[] _absorbBuffer;

        // 입자를 구체 쪽으로 끌어당김. 빨려 드는 후반(_u→1)일수록 빨라지고, 구체에 닿으면 사라짐
        private static void PullParticles(ParticleSystem _ps, Vector3 _soul, float _u)
        {
            if (_absorbBuffer == null || _absorbBuffer.Length < _ps.main.maxParticles) _absorbBuffer = new ParticleSystem.Particle[_ps.main.maxParticles];
            int _n = _ps.GetParticles(_absorbBuffer);
            for (int _i = 0; _i < _n; _i++)
            {
                Vector3 _d = _soul - _absorbBuffer[_i].position;
                float _dist = _d.magnitude;
                float _age = _absorbBuffer[_i].startLifetime - _absorbBuffer[_i].remainingLifetime;
                float _speed = Mathf.Lerp(1.2f, 7f, Mathf.Clamp01(_age * 1.5f + _u * 0.5f));
                // 이번 프레임에 구체까지 닿을 거리면 지나치지 않게 여기서 사라지게 함
                if (_dist < 0.12f + _speed * Time.deltaTime) { _absorbBuffer[_i].remainingLifetime = 0f; continue; }
                // 살짝 휘감아 들어가도록 옆 방향 성분을 섞음
                Vector3 _side = Vector3.Cross(_d / _dist, Vector3.up) * 0.6f;
                _absorbBuffer[_i].velocity = (_d / _dist + _side).normalized * _speed;
            }
            _ps.SetParticles(_absorbBuffer, _n);
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
