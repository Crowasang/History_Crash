using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.UI;

namespace HistoryCrash.Stage
{
    /// <summary>
    /// 아누비스 석상의 강제 이동(기획: 25초마다 Player를 강제 이동, 이동 중 무적 + 도착 후 0.5초 무적).
    /// 1) 호루스처럼 아누비스 석상의 눈이 빛난다.
    /// 2) 기둥 하나의 꼭대기에 불꽃이 타오르고 기둥이 발광한다.
    /// 3) 그 기둥 앞 칸(도착 칸)에 마법진이 그려진다.
    /// 4) 예고가 끝나면 Player가 불꽃 꼬리를 남기며 도착 칸으로 끌려간다.
    /// </summary>
    public class AnubisTeleport : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        [SerializeField, FormerlySerializedAs("player")] private Transform _player;
        [SerializeField, FormerlySerializedAs("pillars"), Tooltip("후보 기둥(비우면 Temple_impe/Pillars 중 전투장 안 기둥)")] private List<Transform> _pillars = new List<Transform>();

        [Header("주기(초)")]
        [SerializeField, FormerlySerializedAs("period")] private float _period = 25f;
        [SerializeField, FormerlySerializedAs("firstDelay")] private float _firstDelay = 12f;
        [SerializeField, FormerlySerializedAs("warnTime"), Tooltip("눈이 켜지고 기둥 불꽃·마법진이 보이는 예고 시간")] private float _warnTime = 3f;
        [SerializeField, FormerlySerializedAs("pullTime"), Tooltip("Player가 끌려가는 시간")] private float _pullTime = 0.8f;
        [SerializeField, FormerlySerializedAs("arriveInvul"), Tooltip("도착 후 추가 무적")] private float _arriveInvul = 0.5f;

        [Header("아누비스 눈(호루스와 같은 방식, 머리 위치는 호루스에서 z −13.03)")]
        [SerializeField, FormerlySerializedAs("eyePositions")] private Vector3[] _eyePositions = { new Vector3(-1.05f, 8.95f, 12.00f), new Vector3(-1.05f, 8.95f, 13.08f) };
        [SerializeField, FormerlySerializedAs("glowColor"), ColorUsage(false, true)] private Color _glowColor = new Color(0.55f, 1f, 0.12f) * 6f;
        [SerializeField, FormerlySerializedAs("eyeSize")] private float _eyeSize = 0.16f;
        [SerializeField, FormerlySerializedAs("eyeLightIntensity")] private float _eyeLightIntensity = 8f;
        [SerializeField, FormerlySerializedAs("eyeLightRange")] private float _eyeLightRange = 4f;

        [Header("기둥 불꽃")]
        [SerializeField, FormerlySerializedAs("pillarLightIntensity")] private float _pillarLightIntensity = 6f;
        [SerializeField, FormerlySerializedAs("pillarLightRange")] private float _pillarLightRange = 6f;
        [SerializeField, FormerlySerializedAs("pillarEmissionBoost"), Tooltip("기둥 재질 발광 배율(무늬 발광이 있는 재질만)")] private float _pillarEmissionBoost = 4f;

        [Header("마법진")]
        [SerializeField, FormerlySerializedAs("circleRadius")] private float _circleRadius = 0.6f;
        [SerializeField, FormerlySerializedAs("circleWidth")] private float _circleWidth = 0.035f;

        public string Status { get; private set; } = "";

        private readonly List<Renderer> _eyes = new List<Renderer>();
        private readonly List<Light> _eyeLights = new List<Light>();
        private Material _eyeMat, _lineMat;
        private PlayerMover _mover; private PlayerHealth _health;
        private HorusTileCollapse _horus;

        private void Start()
        {
            if (!_grid) _grid = FindFirstObjectByType<TileGrid>();
            _horus = FindFirstObjectByType<HorusTileCollapse>();
            if (_player) { _mover = _player.GetComponent<PlayerMover>(); _health = _player.GetComponent<PlayerHealth>(); }
            if (_pillars.Count == 0) CollectPillars();
            CreateEyes(); SetEyes(0f);
            StartCoroutine(Loop());
        }

        private void CollectPillars()
        {
            var _root = GameObject.Find("Temple_impe/Pillars");
            if (!_root || _grid == null) return;
            foreach (Transform _c in _root.transform)
            {
                if (_c.name.Contains("East")) continue;   // 제단 쪽(파라오 뒤) 기둥 제외
                _pillars.Add(_c);
            }
        }

        private void CreateEyes()
        {
            _eyeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _eyeMat.SetColor("_BaseColor", _glowColor);
            for (int _i = 0; _i < _eyePositions.Length; _i++)
            {
                var _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(_go.GetComponent<Collider>());
                _go.name = "Anubis_EyeGlow_" + _i;
                _go.transform.SetParent(transform, false);
                _go.transform.position = _eyePositions[_i];
                _go.transform.localScale = Vector3.one * _eyeSize;
                var _r = _go.GetComponent<Renderer>(); _r.sharedMaterial = _eyeMat; _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _eyes.Add(_r);
                var _l = _go.AddComponent<Light>();
                _l.type = LightType.Point; _l.color = _glowColor / Mathf.Max(_glowColor.maxColorComponent, 1e-3f);
                _l.range = _eyeLightRange; _l.intensity = 0; _l.shadows = LightShadows.None;
                _eyeLights.Add(_l);
            }
        }

        private void SetEyes(float _k)
        {
            foreach (var _r in _eyes) { _r.enabled = _k > 0.01f; _r.transform.localScale = Vector3.one * _eyeSize * Mathf.Lerp(0.4f, 1f, _k); }
            foreach (var _l in _eyeLights) _l.intensity = _eyeLightIntensity * _k;
        }

        private IEnumerator Loop()
        {
            yield return new WaitForSeconds(_firstDelay);
            while (true)
            {
                yield return Trigger();
                float _wait = Mathf.Max(0f, _period - _warnTime - _pullTime);
                for (float _t = _wait; _t > 0f; _t -= Time.deltaTime) { Status = "Anubis: next in " + _t.ToString("F1") + "s"; yield return null; }
            }
        }

        /// <summary>강제 이동 1회(테스트용으로 바로 부를 수 있음)</summary>
        public IEnumerator Trigger()
        {
            if (!_player || _grid == null || _pillars.Count == 0 || _busy) yield break;
            _busy = true;
            try { yield return TriggerBody(); }
            finally { _busy = false; if (_mover && !_mover.enabled) _mover.enabled = true; }
        }
        private bool _busy;   // 강제 이동이 겹쳐 실행되면 이동 스크립트가 꺼진 채 남는 문제 방지

        private IEnumerator TriggerBody()
        {
            // 1) 기둥과 도착 칸 고르기: 기둥 아래 칸에서 가장 가까운 안전한 칸
            //    호루스 붕괴로 사라졌거나 곧 사라질 칸(경고·붕괴·수복 중)은 제외
            Transform _pillar = _pillars[Random.Range(0, _pillars.Count)];
            Bounds _pb = Bounds(_pillar);
            var _pillarCell = _grid.WorldToCell(new Vector3(_pb.center.x, 0, _pb.center.z));
            var _dest = PickDest(_pillarCell);
            Vector3 _destPos = _grid.CellCenter(_dest, _grid.FloorTopY);

            // 2) 예고: 눈 + 기둥 불꽃 + 마법진
            var _fx = new GameObject("Anubis_Warn").transform; _fx.SetParent(transform, false);
            var _top = new Vector3(_pb.center.x, _pb.max.y + 0.05f, _pb.center.z);
            var _flame = PillarFlame(_fx, _top, Mathf.Min(_pb.size.x, _pb.size.z) * 0.35f);
            var _pl = new GameObject("Anubis_PillarLight").AddComponent<Light>();
            _pl.transform.SetParent(_fx, false); _pl.transform.position = _top + Vector3.up * 0.6f;
            _pl.type = LightType.Point; _pl.color = _glowColor / Mathf.Max(_glowColor.maxColorComponent, 1e-3f); _pl.range = _pillarLightRange; _pl.intensity = 0; _pl.shadows = LightShadows.None;
            var _prs = _pillar.GetComponentsInChildren<Renderer>();
            var _circle = MagicCircle(_fx, _destPos + Vector3.up * 0.02f);

            for (float _t = 0; _t < _warnTime; _t += Time.deltaTime)
            {
                float _u = _t / _warnTime;
                float _k = Mathf.SmoothStep(0, 1, Mathf.Min(1f, _u * 3f));
                SetEyes(_k * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f)));
                _pl.intensity = _pillarLightIntensity * _k * (0.85f + 0.15f * Mathf.Sin(Time.time * 13f));
                SetPillarGlow(_prs, 1f + (_pillarEmissionBoost - 1f) * _k);
                UpdateCircle(_circle, _u, Time.time);
                // 예고 도중 호루스가 도착 칸(주변 포함)을 붕괴 대상으로 고르면 마법진을 안전한 칸으로 옮김
                if (!IsGoodDest(_dest)) { _dest = PickDest(_pillarCell); _destPos = _grid.CellCenter(_dest, _grid.FloorTopY); MoveCircle(_circle, _destPos + Vector3.up * 0.02f); }
                Status = "Anubis: pull in " + (_warnTime - _t).ToString("F1") + "s";
                yield return null;
            }

            // 3) 강제 이동(이동 중 무적 + 도착 후 0.5초 무적)
            while (_mover && _mover.IsFalling) yield return null;
            if (!IsGoodDest(_dest)) { _dest = PickDest(_pillarCell); _destPos = _grid.CellCenter(_dest, _grid.FloorTopY); MoveCircle(_circle, _destPos + Vector3.up * 0.02f); }
            Status = "Anubis: pulling";
            if (_health) _health.GrantInvulnerability(_pullTime + _arriveInvul);
            if (_mover) _mover.enabled = false;
            // 점프 중이어도 도착은 바닥 높이로(공중 높이를 유지한 채 굳지 않게)
            Vector3 _from = _player.position; Vector3 _to = new Vector3(_destPos.x, _mover ? _mover.GroundY : _from.y, _destPos.z);
            var _trail = TrailFx(_player);
            BurstAt(_from + Vector3.up * 0.3f);
            for (float _t = 0; _t < _pullTime; _t += Time.deltaTime)
            {
                float _u = _t / _pullTime; float _e = _u * _u * (3f - 2f * _u);
                _player.position = Vector3.Lerp(_from, _to, _e) + Vector3.up * Mathf.Sin(_u * Mathf.PI) * 0.6f;
                yield return null;
            }
            _player.position = _to;
            BurstAt(_to + Vector3.up * 0.3f);
            if (_mover) { _mover.enabled = true; _mover.SnapToGround(); }
            if (_trail) { var _em = _trail.emission; _em.rateOverTime = 0; _trail.transform.SetParent(null, true); Destroy(_trail.gameObject, 1f); }
            Debug.Log("[Anubis] Pulled player to cell " + _dest + " (pillar " + _pillar.name + ")");

            // 4) 정리
            for (float _t = 0; _t < 0.5f; _t += Time.deltaTime)
            {
                float _k = 1f - _t / 0.5f;
                SetEyes(_k); _pl.intensity = _pillarLightIntensity * _k; SetPillarGlow(_prs, 1f + (_pillarEmissionBoost - 1f) * _k);
                foreach (var _lr in _circle) { var _c = _lr.startColor; _c.a = _k; _lr.startColor = _lr.endColor = _c; }
                yield return null;
            }
            SetEyes(0f); SetPillarGlow(_prs, 1f);
            var _fem = _flame.emission; _fem.rateOverTime = 0; _flame.transform.SetParent(null, true); Destroy(_flame.gameObject, 1.5f);
            Destroy(_fx.gameObject);
            Status = "";
        }

        // ---------- 도착 칸 ----------
        /// <summary>도착 칸과 주변 8칸이 모두 서 있을 수 있고, 호루스 붕괴 대상(경고·붕괴·수복 중)이 아님</summary>
        private bool IsGoodDest(Vector2Int _c)
        {
            if (!_grid.IsSafe(_c)) return false;
            if (!_horus) return true;
            for (int _dy = -1; _dy <= 1; _dy++)
                for (int _dx = -1; _dx <= 1; _dx++)
                    if (_horus.IsDoomed(new Vector2Int(_c.x + _dx, _c.y + _dy))) return false;
            return true;
        }

        /// <summary>기둥 아래 칸에서 가장 가까운 좋은 도착 칸(없으면 기존 NearestSafe)</summary>
        private Vector2Int PickDest(Vector2Int _from)
        {
            if (IsGoodDest(_from)) return _from;
            for (int _r = 1; _r < Mathf.Max(_grid.Cols, _grid.Rows); _r++)
            {
                Vector2Int _best = _from; float _bestD = float.MaxValue;
                for (int _dy = -_r; _dy <= _r; _dy++)
                    for (int _dx = -_r; _dx <= _r; _dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(_dx), Mathf.Abs(_dy)) != _r) continue;
                        var _c = new Vector2Int(_from.x + _dx, _from.y + _dy);
                        if (!IsGoodDest(_c)) continue;
                        float _d = _dx * _dx + _dy * _dy;
                        if (_d < _bestD) { _bestD = _d; _best = _c; }
                    }
                if (_bestD < float.MaxValue) return _best;
            }
            return _grid.NearestSafe(_from);
        }

        private void MoveCircle(List<LineRenderer> _lines, Vector3 _pos)
        {
            if (_lines.Count == 0) return;
            _lines[0].transform.parent.position = _pos;
            BurstAt(_pos + Vector3.up * 0.1f);
        }

        private static Bounds Bounds(Transform _t)
        {
            var _rs = _t.GetComponentsInChildren<Renderer>();
            Bounds _b = _rs.Length > 0 ? _rs[0].bounds : new Bounds(_t.position, Vector3.one);
            foreach (var _r in _rs) _b.Encapsulate(_r.bounds);
            return _b;
        }

        // 기둥 재질 발광 세기(무늬 발광이 있는 재질만): 원래 발광색 × 배율
        private readonly Dictionary<Renderer, Color[]> _baseEmission = new Dictionary<Renderer, Color[]>();
        private void SetPillarGlow(Renderer[] _rs, float _mul)
        {
            foreach (var _r in _rs)
            {
                var _mats = _r.sharedMaterials;
                if (!_baseEmission.TryGetValue(_r, out var _baseCols))
                {
                    _baseCols = new Color[_mats.Length];
                    for (int _i = 0; _i < _mats.Length; _i++) _baseCols[_i] = _mats[_i] && _mats[_i].HasProperty("_EmissionColor") ? _mats[_i].GetColor("_EmissionColor") : Color.black;
                    _baseEmission[_r] = _baseCols;
                }
                for (int _i = 0; _i < _mats.Length; _i++)
                {
                    if (_baseCols[_i].maxColorComponent <= 0f) continue;
                    var _mpb = new MaterialPropertyBlock(); _r.GetPropertyBlock(_mpb, _i);
                    if (_mul <= 1.0001f) _mpb.Clear(); else _mpb.SetColor("_EmissionColor", _baseCols[_i] * _mul);
                    _r.SetPropertyBlock(_mpb, _i);
                }
            }
        }

        // ---------- 연출 ----------
        private Material LineMat()
        {
            if (_lineMat) return _lineMat;
            _lineMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Anubis_Line" };
            _lineMat.SetFloat("_Surface", 1f); _lineMat.SetFloat("_Blend", 2f);
            _lineMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _lineMat.SetFloat("_ZWrite", 0f); _lineMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _lineMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _lineMat.SetColor("_BaseColor", Color.white);
            return _lineMat;
        }

        private LineRenderer Line(Transform _parent, string _name, int _count, bool _loop)
        {
            var _go = new GameObject(_name); _go.transform.SetParent(_parent, false);
            var _lr = _go.AddComponent<LineRenderer>();
            _lr.sharedMaterial = LineMat(); _lr.useWorldSpace = false; _lr.loop = _loop; _lr.positionCount = _count;
            _lr.widthMultiplier = _circleWidth; _lr.alignment = LineAlignment.TransformZ;
            _go.transform.localRotation = Quaternion.Euler(90, 0, 0);   // 바닥에 눕힘(로컬 XY = 바닥)
            var _c = _glowColor / Mathf.Max(_glowColor.maxColorComponent, 1e-3f) * 2.5f; _c.a = 0f;
            _lr.startColor = _lr.endColor = _c;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return _lr;
        }

        private List<LineRenderer> MagicCircle(Transform _parent, Vector3 _pos)
        {
            var _root = new GameObject("Anubis_MagicCircle").transform; _root.SetParent(_parent, false); _root.position = _pos;
            var _list = new List<LineRenderer>();
            const int SEG = 64;
            var _outer = Line(_root, "Outer", SEG, true); var _inner = Line(_root, "Inner", SEG, true);
            for (int _i = 0; _i < SEG; _i++)
            {
                float _a = _i * Mathf.PI * 2f / SEG;
                _outer.SetPosition(_i, new Vector3(Mathf.Cos(_a), Mathf.Sin(_a), 0) * _circleRadius);
                _inner.SetPosition(_i, new Vector3(Mathf.Cos(_a), Mathf.Sin(_a), 0) * _circleRadius * 0.74f);
            }
            // 오각별(아누비스 문양 대신 단순 별)
            var _star = Line(_root, "Star", 6, false);
            for (int _i = 0; _i < 6; _i++)
            {
                float _a = Mathf.PI * 0.5f + _i * Mathf.PI * 4f / 5f;
                _star.SetPosition(_i, new Vector3(Mathf.Cos(_a), Mathf.Sin(_a), 0) * _circleRadius * 0.72f);
            }
            _list.Add(_outer); _list.Add(_inner); _list.Add(_star);
            return _list;
        }

        private void UpdateCircle(List<LineRenderer> _lines, float _u, float _time)
        {
            if (_lines.Count == 0) return;
            var _root = _lines[0].transform.parent;
            _root.Rotate(Vector3.up, 50f * Time.deltaTime, Space.World);
            // 바깥 고리 → 안쪽 고리 → 별 순서로 드러나고, 이동이 가까울수록 빠르게 깜빡임
            float _blink = _u < 0.6f ? 1f : 0.65f + 0.35f * Mathf.Sin(_time * Mathf.Lerp(8f, 26f, (_u - 0.6f) / 0.4f));
            for (int _i = 0; _i < _lines.Count; _i++)
            {
                float _show = Mathf.Clamp01((_u - _i * 0.12f) / 0.25f);
                var _c = _lines[_i].startColor; _c.a = _show * _blink;
                _lines[_i].startColor = _lines[_i].endColor = _c;
                _lines[_i].widthMultiplier = _circleWidth * (1f + 0.4f * _u);
            }
        }

        private ParticleSystem MakePs(string _name, Transform _parent, Vector3 _pos, int _max)
        {
            var _go = new GameObject(_name); _go.transform.SetParent(_parent, false); _go.transform.position = _pos;
            var _ps = _go.AddComponent<ParticleSystem>(); _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var _m = _ps.main; _m.maxParticles = _max; _m.simulationSpace = ParticleSystemSimulationSpace.World; _m.playOnAwake = false;
            var _r = _go.GetComponent<ParticleSystemRenderer>(); _r.sharedMaterial = ElementFx.ParticleMat(); _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return _ps;
        }

        private ParticleSystem PillarFlame(Transform _parent, Vector3 _top, float _radius)
        {
            Color _c = _glowColor / Mathf.Max(_glowColor.maxColorComponent, 1e-3f);
            var _ps = MakePs("Anubis_PillarFlame", _parent, _top, 400);
            var _m = _ps.main; _m.loop = true;
            _m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            _m.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.4f);
            _m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            _m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_c, Color.white, 0.4f), _c);
            _m.gravityModifier = -0.2f;
            var _em = _ps.emission; _em.rateOverTime = 140f;
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Circle; _sh.radius = _radius; _sh.rotation = new Vector3(-90, 0, 0);
            var _sz = _ps.sizeOverLifetime; _sz.enabled = true; _sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var _col = _ps.colorOverLifetime; _col.enabled = true;
            var _g = new Gradient(); _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            _col.color = _g;
            var _noise = _ps.noise; _noise.enabled = true; _noise.strength = 0.5f; _noise.frequency = 1.3f;
            _ps.Play();
            return _ps;
        }

        private ParticleSystem TrailFx(Transform _follow)
        {
            Color _c = _glowColor / Mathf.Max(_glowColor.maxColorComponent, 1e-3f);
            var _ps = MakePs("Anubis_PullTrail", _follow, _follow.position + Vector3.up * 0.35f, 300);
            var _m = _ps.main; _m.loop = true; _m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            _m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.4f); _m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            _m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_c, Color.white, 0.4f), _c);
            var _em = _ps.emission; _em.rateOverTime = 160f;
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Sphere; _sh.radius = 0.25f;
            var _sz = _ps.sizeOverLifetime; _sz.enabled = true; _sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            _ps.Play();
            return _ps;
        }

        private void BurstAt(Vector3 _pos)
        {
            Color _c = _glowColor / Mathf.Max(_glowColor.maxColorComponent, 1e-3f);
            ElementFx.Burst(_c, _pos, 1.2f, 30, "Anubis");
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(Status)) GUI.Label(new Rect(20, 104, 400, 20), Status);
        }
    }
}
