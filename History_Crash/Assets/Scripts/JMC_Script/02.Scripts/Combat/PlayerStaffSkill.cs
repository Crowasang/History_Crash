using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Combat
{
    /// <summary>
    /// 주운 스태프의 보스 패턴을 Player가 약화된 버전으로 쓰는 공격. 모양은 패턴 오브젝트, 색·효과는 오브 속성.
    /// 같은 속성의 머미만 맞는다(맞으면 즉사).
    /// - A: 코페쉬가 Player 주위를 돌며 근접 공격
    /// - B: Player 발밑에 함정 설치, 같은 속성 머미가 밟으면 솟아 공격
    /// - C: 바라보는 방향으로 보석 3연발(소환 중 화면을 돌리면 따라 돎)
    /// - D: Player 현재 위치에 범위를 줄인 태양 낙하
    /// - E: 바라보는 방향으로 창 2개 관통 사출(소환 중 화면을 돌리면 따라 돎)
    /// </summary>
    public class PlayerStaffSkill : MonoBehaviour
    {
        // ---- 조정값 ----
        public const float A_RADIUS = 1.4f, A_TIME = 1.6f, A_TURNS = 2f, A_HIT = 0.8f, A_SIZE = 0.55f;   // 맞히기 너무 어려워 회전 반경 1.1→1.4, 칼날 판정 0.6→0.8
        public const float B_TRIGGER = 2.3f, B_LIFE = 20f, B_SIZE = 2.8f;   // 함정 범위(0.55/0.7 → 1.8/2.2 → 2.8/3.4 너무 큼 → 2.3/2.8)
        public const int C_COUNT = 3; public const float C_INTERVAL = 0.18f, C_SPEED = 16f, C_RANGE = 14f, C_HIT = 0.5f, C_SIZE = 0.3f;
        public const float D_HEIGHT = 3f, D_FALL_TIME = 0.55f, D_RADIUS = 1.8f, D_SIZE = 0.7f;   // 맞히기 너무 어려워 타격 반경 1.4→1.8 (바닥 마법진도 같이 커짐)
        public const float E_SPEED = 20f, E_RANGE = 16f, E_HIT = 0.6f, E_SIZE = 2.4f;   // 창 길이. 파라오 E 창 최대 9.7 m(절반 4.85는 너무 커서 약 1/4)

        public const float SKILL_LIGHT_INTENSITY = 0.8f;   // 스킬 오브젝트에 붙는 속성색 점광원 세기
        private Element _element; private int _pattern; private Transform _player; private Vector3 _dir; private Vector3 _target; private TileGrid _grid; private Transform _visualSrc;
        private readonly HashSet<Mummy> _hit = new HashSet<Mummy>();

        /// <summary>공격 발동. 지점을 따로 정하지 않으면 플레이어 발밑(B)</summary>
        public static void Cast(int _pattern, Element _e, Transform _player, Vector3 _dir, TileGrid _grid, Transform _patternVisual)
        {
            Cast(_pattern, _e, _player, _dir, _player.position, _grid, _patternVisual);
        }

        /// <param name="_dir">C·E 발사 방향(클릭한 순간 고정, 시전 중 화면을 돌려도 바뀌지 않음)</param>
        /// <param name="_target">B 설치 지점(PlayerAttackAim이 정함)</param>
        public static void Cast(int _pattern, Element _e, Transform _player, Vector3 _dir, Vector3 _target, TileGrid _grid, Transform _patternVisual)
        {
            var _go = new GameObject("PlayerSkill_" + "ABCDE"[Mathf.Clamp(_pattern, 0, 4)] + "_" + ElementInfo.Name(_e));
            _go.transform.position = _player.position;
            var _s = _go.AddComponent<PlayerStaffSkill>();
            _s._pattern = _pattern; _s._element = _e; _s._player = _player; _s._dir = _dir; _s._target = _target; _s._grid = _grid; _s._visualSrc = _patternVisual;
            ElementFx.Burst(_e, _player.position + Vector3.up * 0.4f + _dir * 0.4f, 0.7f, 16);
            // B는 함정이 솟아 때릴 때 따로 냄. D는 착탄(1.30초)에 맞춰 조금 늦게
            if (_pattern != 1) HistoryCrash.Sound.GameSfx.Play("Player_Attack" + "ABCDE"[Mathf.Clamp(_pattern, 0, 4)], _pattern == 3 ? 0.2f : 0f);
            switch (_pattern)
            {
                case 0: _s.StartCoroutine(_s.RunA()); break;
                case 1: _s.StartCoroutine(_s.RunB()); break;
                case 2: _s.StartCoroutine(_s.RunC()); break;
                case 3: _s.StartCoroutine(_s.RunD()); break;
                default: _s.StartCoroutine(_s.RunE()); break;
            }
        }

        // ---------- 공통 ----------
        // 원래 재질에 속성색 발광만 더한 복사본(텍스처 유지). (원본 재질, 속성, 세기)별로 한 번만 만듦
        private static readonly Dictionary<string, Material> _tintCache = new Dictionary<string, Material>();
        private static Material TintMat(Material _src, Element _e, float _tint)
        {
            if (!_src || _tint <= 0f) return _src;
            string _key = _src.GetInstanceID() + "_" + _e + "_" + _tint;
            if (_tintCache.TryGetValue(_key, out var _m) && _m) return _m;
            _m = new Material(_src) { name = _src.name + "_" + _e };
            Color _ec = ElementInfo.Color(_e);
            // 원래 무늬색(예: 태양의 청록 핏줄)이 속성색을 덮지 않도록 베이스 색에 속성색을 일부 곱함(텍스처 명암은 유지)
            Color _baseTint = Color.Lerp(Color.white, _ec, Mathf.Clamp01(_tint * 0.75f));
            if (_m.HasProperty("baseColorFactor")) _m.SetColor("baseColorFactor", _m.GetColor("baseColorFactor") * _baseTint);
            else if (_m.HasProperty("_BaseColor")) _m.SetColor("_BaseColor", _m.GetColor("_BaseColor") * _baseTint);
            Color _c = _ec * _tint * 4f;   // 발광은 텍스처를 곱하므로 세기를 높게
            // 발광 텍스처가 없으면 베이스 텍스처를 발광 텍스처로 → 밝은 무늬만 속성색으로 빛나고 어두운 부분은 그대로
            if (_m.HasProperty("emissiveFactor"))   // glTF 셰이더(파라오 패턴)
            {
                if (_m.HasProperty("emissiveTexture") && !_m.GetTexture("emissiveTexture") && _m.HasProperty("baseColorTexture")) _m.SetTexture("emissiveTexture", _m.GetTexture("baseColorTexture"));
                _m.SetColor("emissiveFactor", _c);
            }
            if (_m.HasProperty("_EmissionColor"))   // URP Lit
            {
                if (_m.HasProperty("_EmissionMap") && !_m.GetTexture("_EmissionMap") && _m.HasProperty("_BaseMap")) _m.SetTexture("_EmissionMap", _m.GetTexture("_BaseMap"));
                _m.EnableKeyword("_EMISSION"); _m.SetColor("_EmissionColor", _c);
            }
            _tintCache[_key] = _m;
            return _m;
        }

        /// <param name="_tint">원래 재질에 더할 속성색 발광 세기(0 = 원래 재질 그대로)</param>
        /// <param name="_light">속성색 점광원 세기(음수 = 기본값)</param>
        /// <param name="_lightRange">점광원 범위(음수 = 크기에 맞춤)</param>
        private Transform Shape(Transform _parent, float _size, Vector3 _euler, float _tint = 0f, float _light = -1f, float _lightRange = -1f)
        {
            var _root = new GameObject("Shape").transform;
            _root.SetParent(_parent, false);
            var _mf = _visualSrc ? _visualSrc.GetComponent<MeshFilter>() : null;
            GameObject _vis;
            if (_mf && _mf.sharedMesh)
            {
                _vis = new GameObject("Mesh");
                _vis.AddComponent<MeshFilter>().sharedMesh = _mf.sharedMesh;
                _vis.AddComponent<MeshRenderer>();
                var _b = _mf.sharedMesh.bounds; var _rot = Quaternion.Euler(_euler);
                float _k = _size / Mathf.Max(_b.size.x, Mathf.Max(_b.size.y, _b.size.z), 1e-3f);
                _vis.transform.SetParent(_root, false);
                _vis.transform.localRotation = _rot; _vis.transform.localScale = Vector3.one * _k;
                _vis.transform.localPosition = -(_rot * _b.center) * _k;
            }
            else
            {
                _vis = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(_vis.GetComponent<Collider>());
                _vis.transform.SetParent(_root, false); _vis.transform.localScale = Vector3.one * _size * 0.5f;
            }
            var _r = _vis.GetComponent<Renderer>();
            var _srcR = _mf ? _visualSrc.GetComponent<Renderer>() : null;
            // 파라오 패턴처럼 원래 텍스처 재질을 그대로 쓰고, 속성은 은은한 점광원(빛)으로만 표현
            if (_srcR && _srcR.sharedMaterials.Length > 0)
            {
                var _mats = _srcR.sharedMaterials;
                for (int _i = 0; _i < _mats.Length; _i++) _mats[_i] = TintMat(_mats[_i], _element, _tint);
                _r.sharedMaterials = _mats;
            }
            else _r.sharedMaterial = ElementFx.GlowMat(_element, 1.5f);
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ElementFx.Glow(_element, _root, _light >= 0f ? _light : SKILL_LIGHT_INTENSITY,
                _lightRange > 0f ? _lightRange : Mathf.Clamp(_size * 0.6f, 0.8f, 1.6f));   // 범위가 넓으면 바닥을 물들임
            return _root;
        }

        /// <summary>pos 반경 radius 안의 같은 속성 머미를 맞힘. 이미 맞힌 머미는 건너뜀.</summary>
        private int HitAround(Vector3 _pos, float _radius, Vector3 _travel)
        {
            int _n = 0;
            foreach (var _m in Mummy.Alive.ToArray())
            {
                if (!_m || !_m.IsAlive || !_m.Active || _hit.Contains(_m)) continue;
                Vector3 _d = _m.transform.position - _pos; _d.y = 0;
                if (_d.magnitude > _radius) continue;
                if (_m.TryHit(_element, _travel.sqrMagnitude > 1e-4f ? _travel : _d)) { _hit.Add(_m); _n++; ElementFx.Burst(_element, _m.transform.position + Vector3.up * 0.5f, 0.9f, 18); }
            }
            return _n;
        }

        private bool Wall(Vector3 _p)
        {
            if (_grid == null) return false;
            var _c = _grid.WorldToCell(_p);
            return !_grid.InBounds(_c) || !_grid.IsWalkable(_c);
        }

        // 떠 있는 자리(좌우·뒤)가 벽 칸 안이거나 바로 앞(탄이 벽 판정을 시작하는 거리)이 벽이면 플레이어 쪽으로 당김.
        // 벽에 붙어 쏘면 옆 자리에서 출발한 탄이 0.6 m 만에 벽에 막히던 문제. 플레이어 정면이 막혔으면 그건 원래대로 막힘
        private Vector3 OutOfWall(Vector3 _p, Vector3 _f)
        {
            Vector3 _c = _player.position; _c.y = _p.y;
            for (int _i = 0; _i < 8 && (Wall(_p) || Wall(_p + _f * 0.8f)); _i++) _p = Vector3.Lerp(_p, _c, 0.35f);
            return Wall(_p) || Wall(_p + _f * 0.8f) ? _c : _p;
        }

        private float FloorY { get { return _grid != null ? _grid.FloorTopY : _player.position.y - 0.3f; } }

        private void Finish(ParticleSystem _trail = null)
        {
            if (_trail) { _trail.transform.SetParent(null, true); var _em = _trail.emission; _em.rateOverTime = 0; Destroy(_trail.gameObject, 0.8f); }
            Destroy(gameObject);
        }

        // ---------- A: 코페쉬 회전 ----------
        private IEnumerator RunA()
        {
            var _blade = Shape(transform, A_SIZE, Vector3.zero, 0.6f, 1.8f, 1.4f);   // 텍스처 + 속성 발광 틴트 + 강한 점광원
            var _trail = ElementFx.Trail(_element, _blade, 0.17f, 80f);
            float _startAng = Mathf.Atan2(_dir.x, _dir.z);
            for (float _t = 0; _t < A_TIME; _t += Time.deltaTime)
            {
                float _u = _t / A_TIME;
                float _r = A_RADIUS * Mathf.Min(1f, _u * 6f);   // 처음에 바깥으로 펼쳐짐
                float _ang = _startAng + _u * A_TURNS * Mathf.PI * 2f;
                Vector3 _c = _player.position + Vector3.up * 0.35f;
                Vector3 _prev = _blade.position;
                _blade.position = _c + new Vector3(Mathf.Sin(_ang), 0, Mathf.Cos(_ang)) * _r;
                _blade.Rotate(Vector3.up, 1440f * Time.deltaTime, Space.World);
                HitAround(_blade.position, A_HIT, _blade.position - _prev);
                yield return null;
            }
            ElementFx.Burst(_element, _blade.position, 0.8f, 14);
            Finish(_trail);
        }

        // ---------- B: 발밑 함정 ----------
        private IEnumerator RunB()
        {
            Vector3 _pos = _player.position; _pos.y = FloorY;
            if (_grid != null) _pos = _grid.CellCenter(_grid.WorldToCell(_player.position), FloorY);
            transform.position = _pos;
            var _trap = Shape(transform, B_SIZE, new Vector3(90, 0, 0), 0.5f, 1.6f, 2.0f);   // 바닥 빛은 함정 범위 표시를 겸함
            _trap.localPosition = new Vector3(0, -B_SIZE * 0.35f, 0);   // 바닥에 반쯤 묻힌 대기 상태
            _trap.localScale = new Vector3(1f, 0.35f, 1f);
            var _glow = ElementFx.Trail(_element, transform, 0.38f, 75f);
            _glow.transform.localPosition = new Vector3(0, 0.05f, 0);
            var _gm = _glow.main; _gm.gravityModifier = -0.2f;
            ElementFx.Impact(_pos, ElementInfo.Color(_element), new Color(0.3f, 0.27f, 0.2f, 0.4f), 2.0f);
            HistoryCrash.Sound.GameSfx.Play("Player_TrapSet");
            float _born = Time.time;
            while (Time.time - _born < B_LIFE)
            {
                // 같은 속성 머미가 밟으면 솟아오름
                foreach (var _m in Mummy.Alive)
                {
                    if (!_m || !_m.IsAlive || !_m.Active || _m.Element != _element) continue;
                    Vector3 _d = _m.transform.position - _pos; _d.y = 0;
                    if (_d.magnitude > B_TRIGGER) continue;
                    HistoryCrash.Sound.GameSfx.Play("Player_AttackB", _pos);
                    for (float _t = 0; _t < 0.12f; _t += Time.deltaTime)
                    {
                        float _u = _t / 0.12f;
                        _trap.localScale = new Vector3(1f, Mathf.Lerp(0.35f, 1.3f, _u), 1f);
                        _trap.localPosition = new Vector3(0, Mathf.Lerp(-B_SIZE * 0.35f, 0.1f, _u), 0);
                        yield return null;
                    }
                    ElementFx.Impact(_pos, ElementInfo.Color(_element), new Color(0.3f, 0.27f, 0.2f, 0.5f), 3.3f);
                    HitAround(_pos, B_TRIGGER + 0.15f, Vector3.up);
                    yield return new WaitForSeconds(0.35f);
                    Finish(_glow);
                    yield break;
                }
                yield return null;
            }
            Finish(_glow);
        }

        // ---------- C: 보석 3연발 ----------
        // 시전 시 Player 주위(좌·위·우)에 보석 3개가 사출돼 떠 있다가, 바라보는 방향으로 순차 발사
        private static readonly Vector3[] _c_Slots = { new Vector3(-0.8f, 1.05f, -0.2f), new Vector3(0f, 1.55f, -0.35f), new Vector3(0.8f, 1.05f, -0.2f) };
        public const float C_RISE = 0.22f, C_STAGGER = 0.07f, C_HOLD = 0.08f;
        /// <summary>사출~첫 발사까지 걸리는 시간. PlayerLoadout이 이만큼 먼저 Cast해서 첫 발사가 모션의 발사 시점과 맞게 함</summary>
        public const float C_SUMMON_LEAD = C_RISE + C_STAGGER * (C_COUNT - 1) + C_HOLD;

        private IEnumerator RunC()
        {
            var _gems = new Transform[C_COUNT];
            var _trails = new ParticleSystem[C_COUNT];
            var _arrived = new bool[C_COUNT];
            float _born = Time.time;
            int _fired = 0; float _nextFire = C_SUMMON_LEAD;
            while (_fired < C_COUNT)
            {
                float _t = Time.time - _born;
                Vector3 _f = _player ? FacingDir() : _dir;   // 소환 중에도 지금 화면 방향을 따라감
                var _rot = Quaternion.LookRotation(_f, Vector3.up);
                Vector3 _chest = _player.position + Vector3.up * 0.9f;
                for (int _i = _fired; _i < C_COUNT; _i++)
                {
                    float _ti = _t - _i * C_STAGGER;
                    if (_ti < 0f) continue;
                    if (!_gems[_i])
                    {
                        _gems[_i] = new GameObject("Gem" + _i).transform;
                        _gems[_i].SetParent(transform, false);
                        Shape(_gems[_i], C_SIZE, Vector3.zero);
                        _trails[_i] = ElementFx.Trail(_element, _gems[_i], C_SIZE * 0.4f, 35f);
                        ElementFx.Burst(_element, _chest, 0.4f, 8);
                    }
                    float _u = Mathf.Clamp01(_ti / C_RISE);
                    float _e = 1f - (1f - _u) * (1f - _u) * (1f - _u);                  // 빠르게 튀어나와 감속
                    Vector3 _slot = OutOfWall(_player.position + _rot * _c_Slots[_i], _f);
                    Vector3 _bob = Vector3.up * Mathf.Sin((_t + _i) * 6f) * 0.04f * _u;  // 떠 있는 흔들림
                    _gems[_i].position = Vector3.LerpUnclamped(_chest, _slot, _e) + _bob;
                    _gems[_i].rotation = _rot;
                    _gems[_i].localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, _e);
                    _gems[_i].GetChild(0).Rotate(Vector3.forward, 540f * Time.deltaTime, Space.Self);
                    if (_u >= 1f && !_arrived[_i]) { _arrived[_i] = true; ElementFx.Burst(_element, _gems[_i].position, 0.35f, 6); }
                }
                if (_t >= _nextFire && _gems[_fired])
                {
                    // 순차 발사: 떠 있던 자리에서 정면으로, 첫 1.5 m 동안 발사 높이로 내려옴
                    var _g = _gems[_fired];
                    if (_trails[_fired]) { _trails[_fired].transform.SetParent(null, true); var _em = _trails[_fired].emission; _em.rateOverTime = 0; Destroy(_trails[_fired].gameObject, 0.6f); }
                    ElementFx.Burst(_element, _g.position, 0.6f, 12);
                    StartCoroutine(Projectile(_g.position, _f, C_SPEED, C_RANGE, C_HIT, C_SIZE, Vector3.zero, false, 900f, _player.position.y + 0.35f));
                    Destroy(_g.gameObject);
                    _gems[_fired] = null; _fired++; _nextFire = _t + C_INTERVAL;   // 프레임이 튀어도 발사 간격 유지
                }
                yield return null;
            }
            yield return new WaitForSeconds(C_RANGE / C_SPEED + 0.2f);
            Destroy(gameObject);
        }

        private Vector3 FacingDir()
        {
            var _cam = Camera.main;
            Vector3 _d = _cam ? Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up) : _dir;
            return _d.sqrMagnitude > 1e-4f ? _d.normalized : _dir;
        }

        private IEnumerator Projectile(Vector3 _start, Vector3 _d, float _speed, float _range, float _hitR, float _size, Vector3 _euler, bool _pierce, float _spin, float _dropToY = float.NaN, float _hitAhead = 0f)
        {
            float _startY = _start.y;
            var _holder = new GameObject("Shot").transform;
            _holder.SetParent(transform, false);
            _holder.position = _start; _holder.rotation = Quaternion.LookRotation(_d, Vector3.up);
            var _shape = Shape(_holder, _size, _euler);
            var _trail = ElementFx.Trail(_element, _holder, Mathf.Min(_size * 0.4f, 0.2f), 45f);
            float _traveled = 0f;
            bool _stop = false;
            while (_traveled < _range && !_stop)
            {
                // 프레임이 튀어 한 번에 많이 이동해도 머미를 건너뛰지 않도록 판정 반경 이하 간격으로 나눠 이동·판정
                float _step = Mathf.Min(_speed * Time.deltaTime, _range - _traveled);
                int _sub = Mathf.Max(1, Mathf.CeilToInt(_step / Mathf.Max(_hitR, 0.1f)));
                for (int _k = 0; _k < _sub && !_stop; _k++)
                {
                    float _s = _step / _sub; _traveled += _s;
                    _holder.position += _d * _s;
                    if (!float.IsNaN(_dropToY)) { var _hp = _holder.position; _hp.y = Mathf.Lerp(_startY, _dropToY, Mathf.SmoothStep(0f, 1f, _traveled / 1.5f)); _holder.position = _hp; }
                    int _n = HitAround(_holder.position + _d * _hitAhead, _hitR, _d) + (_hitAhead > 0f ? HitAround(_holder.position, _hitR, _d) : 0);   // 긴 탄은 끝과 몸통 둘 다 판정
                    if (_n > 0 && !_pierce) _stop = true;
                    else if (_traveled > 0.6f && Wall(_holder.position)) _stop = true;
                }
                if (_spin != 0f) _shape.Rotate(Vector3.forward, _spin * Time.deltaTime, Space.Self);
                if (!_stop) yield return null;
            }
            ElementFx.Burst(_element, _holder.position, 0.9f, 18);
            if (_trail) { _trail.transform.SetParent(null, true); var _em = _trail.emission; _em.rateOverTime = 0; Destroy(_trail.gameObject, 0.8f); }
            Destroy(_holder.gameObject);
        }

        // ---------- D: 축소판 태양 낙하 ----------
        // 시전 위치(Player 현재 위치) 위에 오브가 생겨 잠시 떠 있는 동안, 바닥에 타격 범위(D_Radius)만 한 마법진이 그려지고
        // 이후 오브가 마법진 중앙으로 떨어져 범위 안 같은 속성 머미를 맞힘. 마법진 바깥 고리 = 실제 타격 범위.
        public const float D_GROW = 0.2f, D_HOVER = 0.55f, D_DRAW = 0.35f;

        private IEnumerator RunD()
        {
            Vector3 _ground = _player.position; _ground.y = FloorY;
            transform.position = _ground;
            Color _c = ElementInfo.Color(_element);
            SkillMagicCircle _circle = MagicCircle(transform, D_RADIUS, _c);
            var _sun = Shape(transform, D_SIZE, Vector3.zero, 0.7f, 2.2f, 1.8f);
            var _trail = ElementFx.Trail(_element, _sun, 0.22f, 60f);
            ElementFx.Burst(_element, _ground + Vector3.up * D_HEIGHT, 0.6f, 14);
            float _wait = D_GROW + D_HOVER;
            for (float _t = 0; _t < _wait; _t += Time.deltaTime)
            {
                // 오브: 커지며 나타나 떠 있음
                float _g = Mathf.Clamp01(_t / D_GROW);
                _sun.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, 1f - (1f - _g) * (1f - _g));
                _sun.localPosition = Vector3.up * (D_HEIGHT + Mathf.Sin(_t * 7f) * 0.05f);
                _sun.Rotate(Vector3.up, 240f * Time.deltaTime, Space.World);
                // 마법진: 바깥 고리가 한 바퀴 그려지고, 낙하가 가까울수록 밝게 깜빡임
                float _d = Mathf.Clamp01(_t / D_DRAW);
                float _pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_t * Mathf.Lerp(5f, 16f, _t / _wait)));
                _circle.Set(_d, _pulse, _t);
                yield return null;
            }
            for (float _t = 0; _t < D_FALL_TIME; _t += Time.deltaTime)
            {
                float _u = _t / D_FALL_TIME;
                _sun.localPosition = Vector3.up * Mathf.Lerp(D_HEIGHT, D_SIZE * 0.4f, _u * _u);
                _sun.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);
                _circle.Set(1f, 1f, _wait + _t);
                yield return null;
            }
            ElementFx.Impact(_ground, _c, new Color(0.3f, 0.27f, 0.2f, 0.55f), 1.6f);
            ElementFx.Burst(_element, _ground + Vector3.up * 0.3f, 1.8f, 40);
            HitAround(_ground, D_RADIUS, Vector3.down);
            Destroy(_sun.gameObject);
            // 착탄 후 마법진이 한 번 번쩍이고 사라짐
            for (float _t = 0; _t < 0.45f; _t += Time.deltaTime)
            {
                float _u = _t / 0.45f;
                _circle.Set(1f, Mathf.Lerp(1.6f, 0f, _u), 1f + _t, 1f + _u * 0.08f);
                yield return null;
            }
            Finish(_trail);
        }

        // ---------- 마법진(LineRenderer) ----------
        private static Material _lineMat;
        private static Material LineMat()
        {
            if (_lineMat) return _lineMat;
            _lineMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "PlayerSkill_CircleLine" };
            _lineMat.SetColor("_BaseColor", Color.white);
            _lineMat.SetFloat("_Surface", 1f); _lineMat.SetFloat("_Blend", 2f);
            _lineMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _lineMat.SetFloat("_ZWrite", 0f);
            _lineMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _lineMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _lineMat;
        }

        private SkillMagicCircle MagicCircle(Transform _parent, float _radius, Color _c)
        {
            SkillMagicCircle _cir = new SkillMagicCircle { Color = _c };
            // 텍스처 마법진(케프리와 태양 원반 · 네 속성 패): 바깥 고리 = 타격 범위가 되도록 지름을 맞춤
            var _tex = MagicCircleDecal.Load(MagicCircleDecal.PLAYER_D);
            if (_tex)
            {
                _cir.Decal = new MagicCircleDecal("MagicCircle", _tex, _parent, _radius * 2f / MagicCircleDecal.OUTER_RATIO);
                _cir.Root = _cir.Decal.Root;
                _cir.Set(0f, 0f, 0f);
                return _cir;
            }
            _cir.Root = new GameObject("MagicCircle").transform;
            _cir.Root.SetParent(_parent, false); _cir.Root.localPosition = Vector3.up * 0.03f;
            _cir.Spin = new GameObject("Spin").transform; _cir.Spin.SetParent(_cir.Root, false);
            const int SEG = 72;
            _cir.OuterPts = new Vector3[SEG + 1];
            for (int _i = 0; _i <= SEG; _i++) { float _a = _i * Mathf.PI * 2f / SEG; _cir.OuterPts[_i] = new Vector3(Mathf.Sin(_a), Mathf.Cos(_a), 0) * _radius; }
            _cir.Outer = Line(_cir.Root, "Outer", 0.06f, false);
            // 안쪽 고리
            _cir.Inner = Line(_cir.Spin, "Inner", 0.035f, true);
            float _ri = _radius * 0.78f; _cir.Inner.positionCount = SEG;
            for (int _i = 0; _i < SEG; _i++) { float _a = _i * Mathf.PI * 2f / SEG; _cir.Inner.SetPosition(_i, new Vector3(Mathf.Sin(_a), Mathf.Cos(_a), 0) * _ri); }
            // 육각별(두 삼각형을 한 선으로: 0-2-4-0 → 1-3-5-1)
            _cir.Star = Line(_cir.Spin, "Star", 0.03f, false);
            int[] _order = { 0, 2, 4, 0, 1, 3, 5, 1 };
            _cir.Star.positionCount = _order.Length;
            for (int _i = 0; _i < _order.Length; _i++) { float _a = _order[_i] * Mathf.PI / 3f; _cir.Star.SetPosition(_i, new Vector3(Mathf.Sin(_a), Mathf.Cos(_a), 0) * _ri); }
            // 두 고리 사이 눈금 12개(선 하나로 안쪽 고리→바깥→안쪽, 눈금 사이 이동은 안쪽 고리와 거의 겹침)
            _cir.Ticks = Line(_cir.Spin, "Ticks", 0.025f, false);
            const int TK = 12; _cir.Ticks.positionCount = TK * 3;
            for (int _i = 0; _i < TK; _i++)
            {
                float _a = _i * Mathf.PI * 2f / TK; var _dir = new Vector3(Mathf.Sin(_a), Mathf.Cos(_a), 0);
                _cir.Ticks.SetPosition(_i * 3, _dir * _ri); _cir.Ticks.SetPosition(_i * 3 + 1, _dir * _radius * 0.97f); _cir.Ticks.SetPosition(_i * 3 + 2, _dir * _ri);
            }
            _cir.Set(0f, 0f, 0f);
            return _cir;
        }

        private LineRenderer Line(Transform _parent, string _name, float _width, bool _loop)
        {
            var _go = new GameObject(_name); _go.transform.SetParent(_parent, false);
            var _lr = _go.AddComponent<LineRenderer>();
            _lr.useWorldSpace = false; _lr.loop = _loop; _lr.widthMultiplier = _width;
            _lr.alignment = LineAlignment.TransformZ;   // 점은 로컬 XY 평면 → X축 90° 회전으로 바닥(XZ)에 눕힘
            _go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _lr.sharedMaterial = LineMat();
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _lr.receiveShadows = false;
            _lr.numCornerVertices = 2; _lr.numCapVertices = 2;
            return _lr;
        }

        // ---------- E: 관통 창 ----------
        // C처럼: 시전 시 창 2개가 가슴에서 좌우로 하나씩 튀어나와 허리~가슴 높이에 떠서 정면을 겨눴다가, 살짝 당긴 뒤 좌→우 순서로 관통 사출
        private static readonly Vector3[] _e_Slots = { new Vector3(-0.6f, 1.0f, 0.05f), new Vector3(0.6f, 1.0f, 0.05f) };
        private static readonly Vector3 _e_Euler = new Vector3(-90, 0, 0);   // 창끝(메시 −Y, 넓은 머리)을 정면(Z)으로. +90은 거꾸로 나옴
        public const float E_RISE = 0.3f, E_HOLD = 0.18f, E_PULL = 0.2f, E_GAP = 0.08f;
        public const float E_CROSS_DIST = 4f;   // 좌우 창이 교차하는 지점(Player 정면 거리, m). 작을수록 교차 각도가 커짐(4 m ≈ 8.5°)
        /// <summary>소환~첫 사출까지 걸리는 시간. PlayerLoadout이 이만큼 먼저 Cast해서 사출을 모션 발사 시점에 맞춤</summary>
        public const float E_SUMMON_LEAD = E_RISE + E_HOLD;

        private IEnumerator RunE()
        {
            int _count = _e_Slots.Length;
            var _spears = new Transform[_count];
            var _trails = new ParticleSystem[_count];
            for (int _i = 0; _i < _count; _i++)
            {
                _spears[_i] = new GameObject("Spear" + _i).transform;
                _spears[_i].SetParent(transform, false);
                Shape(_spears[_i], E_SIZE, _e_Euler);
                _trails[_i] = ElementFx.Trail(_element, _spears[_i], 0.15f, 35f);
            }
            ElementFx.Burst(_element, _player.position + Vector3.up * 0.9f, 0.6f, 14);
            float _born = Time.time; int _fired = 0;
            while (_fired < _count)
            {
                float _t = Time.time - _born;
                Vector3 _f = _player ? FacingDir() : _dir;   // 소환 중에도 지금 화면 방향을 따라감
                var _rot = Quaternion.LookRotation(_f, Vector3.up);
                Vector3 _chest = _player.position + Vector3.up * 0.9f;
                float _u = Mathf.Clamp01(_t / E_RISE);
                float _e = 1f - (1f - _u) * (1f - _u) * (1f - _u);
                Vector3 _cross = _player.position + _f * E_CROSS_DIST;   // 좌우 창이 겨냥하는 교차점
                for (int _i = _fired; _i < _count; _i++)
                {
                    Vector3 _slot = OutOfWall(_player.position + _rot * _e_Slots[_i], _f);
                    Vector3 _aim = Vector3.ProjectOnPlane(_cross - _slot, Vector3.up).normalized;   // 안쪽으로 살짝 꺾인 방향
                    float _hold = E_HOLD + _i * E_GAP;   // 뒤에 쏘는 창은 그만큼 더 오래 당김
                    float _pull = _t > E_RISE ? Mathf.SmoothStep(0f, 1f, (_t - E_RISE) / _hold) * E_PULL : 0f;   // 사출 직전 뒤로 당김
                    _spears[_i].position = Vector3.LerpUnclamped(_chest, _slot, _e) - _aim * _pull + Vector3.up * Mathf.Sin(_t * 9f + _i * 2f) * 0.025f * _u;
                    _spears[_i].rotation = Quaternion.Slerp(_rot, Quaternion.LookRotation(_aim, Vector3.up), _e);
                    _spears[_i].localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, _e);
                }
                if (_t >= E_SUMMON_LEAD + _fired * E_GAP)
                {
                    var _sp = _spears[_fired]; Vector3 _start = _sp.position;
                    _f = Vector3.ProjectOnPlane(_cross - _start, Vector3.up).normalized;   // 교차점을 향해 사출
                    if (_trails[_fired]) { _trails[_fired].transform.SetParent(null, true); var _em = _trails[_fired].emission; _em.rateOverTime = 0; Destroy(_trails[_fired].gameObject, 0.6f); }
                    Destroy(_sp.gameObject);
                    ElementFx.Burst(_element, _start, 0.7f, 14);
                    StartCoroutine(Projectile(_start, _f, E_SPEED, E_RANGE, E_HIT, E_SIZE, _e_Euler, true, 0f, _player.position.y + 0.5f, E_SIZE * 0.4f));   // 판정은 창끝·몸통
                    _fired++;
                    continue;   // 같은 프레임에 다음 창 조건도 바로 확인
                }
                yield return null;
            }
            yield return new WaitForSeconds(E_RANGE / E_SPEED + 0.3f);
            Destroy(gameObject);
        }
    }
}
