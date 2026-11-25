using UnityEngine;
using HistoryCrash.Sound;

namespace HistoryCrash.Stage
{
    /// <summary>
    /// 제드 기둥 파괴·수복 연출(확정 샘플: 파괴 4, 수복 1). 조각 모델(Animator)과 같은 오브젝트에 붙는다.
    /// 움직임은 Animator의 Break/Rebuild 클립(AnubisTeleport가 트리거), 이 스크립트는 그 시간에 맞춰 증발·발광·입자·소리를 낸다.
    /// 평소에는 원본 기둥만 보이고, 부서져 있는 동안만 조각을 켠다(하단부는 남음).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ZedtopBreak : MonoBehaviour
    {
        [SerializeField, Tooltip("온전한 기둥")] private Renderer _intact;
        [SerializeField, Tooltip("조각 46개(뼈마다 하나)")] private SkinnedMeshRenderer _pieces;
        [SerializeField, Tooltip("입자·섬광 묶음. 쉬는 파티클의 경계는 월드 원점이라 켜 두면 아누비스·타일 격자의 기둥 위치 계산이 틀어지므로 평소에는 끔")] private GameObject _fx;
        [SerializeField] private ParticleSystem _mote;
        [SerializeField] private ParticleSystem _dust;
        [SerializeField] private ParticleSystem _rock;
        [SerializeField] private Light _flash;

        private static readonly int BREAK = Animator.StringToHash("Break"), REBUILD = Animator.StringToHash("Rebuild");
        private const float BURST = 0.9f, ROCK_SIZE = 2.2f;   // 허리가 터지는 때 / 파편 메시(약 0.3m)를 1m 기준으로
        private static readonly (float t, string name)[] BREAK_SFX = { (0f, "Zedtop_Warning"), (BURST, "Zedtop_Collapse"), (3.23f, "Zedtop_Evaporate") };
        private static readonly (float t, string name)[] REBUILD_SFX = { (0f, "Zedtop_Restore"), (2.97f, "Zedtop_TierLand"), (3.37f, "Zedtop_TierLand"), (3.77f, "Zedtop_TierLand"), (4.17f, "Zedtop_TierLand"), (4.43f, "Zedtop_RestoreDone") };
        private static readonly float[] TIER_Y = { 4.17f, 4.5f, 4.9f, 5.4f };
        private static readonly Color DUST = new Color(0.30f, 0.27f, 0.22f, 0.42f), WISP = new Color(0.16f, 0.36f, 0.12f, 0.30f), GREEN = new Color(0.5f, 1f, 0.22f, 1f);

        // 발광(원본 = 1배)·단면 줄·섬광: 샘플의 키 그대로
        private static readonly AnimationCurve B_EMI = C(0, 1, .23f, 1, .43f, 2.2f, .53f, 1.5f, .87f, 2.6f, .97f, 3, 1.73f, 1.2f, 3.2f, .45f);
        private static readonly AnimationCurve B_IN = C(0, 0, .9f, 1.6f, 1.17f, 1.2f, 2.2f, .4f, 3.6f, .1f);
        private static readonly AnimationCurve B_FL = C(0, 0, .3f, 0, .87f, .17f, .97f, 1, 1.2f, .27f, 1.9f, 0);
        private static readonly AnimationCurve R_EMI = C(0, .45f, .3f, .45f, .83f, 1.1f, 1.5f, .67f, 2.93f, .67f, 3f, 1.4f, 3.23f, .75f, 3.33f, .67f, 3.4f, 1.4f, 3.63f, .75f, 3.73f, .67f, 3.8f, 1.4f, 4.03f, .75f, 4.13f, .67f, 4.2f, 1.4f, 4.43f, .83f, 4.7f, 2.5f, 5.43f, 1);
        private static readonly AnimationCurve R_IN = C(0, .1f, .8f, 1.3f, 2.7f, 1.2f, 4.3f, .6f, 5.1f, 0);
        private static readonly AnimationCurve R_FL = C(0, 0, .25f, 0, .83f, .2f, 1.6f, .1f, 2.93f, .05f, 3f, .3f, 3.3f, .05f, 3.4f, .3f, 3.7f, .05f, 3.8f, .3f, 4.1f, .05f, 4.2f, .3f, 4.43f, .08f, 4.7f, .6f, 5.4f, 0);
        private static AnimationCurve C(params float[] _k)
        {
            var _ks = new Keyframe[_k.Length / 2];
            for (int i = 0; i < _ks.Length; i++) _ks[i] = new Keyframe(_k[i * 2], _k[i * 2 + 1]);
            return new AnimationCurve(_ks);
        }

        private Animator _anim;
        private Material[] _mats;
        private Transform[] _bones;
        private Matrix4x4[] _binds;
        private Vector3[] _verts;
        private int[] _boneOf;
        private Vector2[] _evap, _app;
        private int[] _stumpTop;
        private Vector3[] _prev, _vel;   // 조각이 부딪혀 멈추는 순간(먼지)을 찾기 위한 직전 값
        private int _mode;               // 0 온전함, 1 파괴, 2 수복
        private float _last;
        private bool _mute;
        private float _flashMax;
        private static int _rebuildFrame = -1;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _mats = _pieces.materials;
            _bones = _pieces.bones;
            var _m = _pieces.sharedMesh;
            _binds = _m.bindposes; _verts = _m.vertices;
            var _w = _m.boneWeights; _boneOf = new int[_w.Length];
            for (int i = 0; i < _w.Length; i++) _boneOf[i] = _w[i].boneIndex0;
            _evap = _m.uv2; _app = _m.uv3;
            var _rest = _m.uv4; var _top = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _verts.Length; i++) if (_evap[i].x > 100f && _rest[i].y > 1.45f) _top.Add(i);
            _stumpTop = _top.ToArray();
            _prev = new Vector3[_bones.Length]; _vel = new Vector3[_bones.Length];
            _flashMax = _flash.intensity; _flash.intensity = 0f;
        }

        private void OnDestroy() { if (_mats != null) foreach (var _m in _mats) Destroy(_m); }

        private void LateUpdate()
        {
            var _s = _anim.GetCurrentAnimatorStateInfo(0);
            int _now = _s.shortNameHash == BREAK ? 1 : _s.shortNameHash == REBUILD ? 2 : 0;
            if (_now != _mode) Enter(_now);
            if (_mode == 0) return;

            float _t = Mathf.Min(_s.normalizedTime, 1f) * _s.length;
            var _sfx = _mode == 1 ? BREAK_SFX : REBUILD_SFX;
            foreach (var (_at, _name) in _sfx) if (!_mute && Hit(_at, _t)) GameSfx.Play(_name, transform.position + Vector3.up * 2f);
            foreach (var _m in _mats)
            {
                _m.SetFloat("_T", _t);
                _m.SetFloat("_EmissionMul", (_mode == 1 ? B_EMI : R_EMI).Evaluate(_t));
                _m.SetFloat("_InnerGlow", (_mode == 1 ? B_IN : R_IN).Evaluate(_t));
            }
            _flash.intensity = _flashMax * (_mode == 1 ? B_FL : R_FL).Evaluate(_t);
            if (_t > _last) { Stops(); if (_mode == 1) BreakFx(_t); else RebuildFx(_t); }
            _last = _t;
        }

        private bool Hit(float _at, float _t) => _last <= _at && _t > _at;

        private void Enter(int _m)
        {
            _mode = _m; _last = 0f;
            _intact.enabled = _m == 0;
            _pieces.gameObject.SetActive(_m != 0);
            _fx.SetActive(_m != 0); _flash.intensity = 0f;
            foreach (var _mt in _mats) _mt.SetFloat("_Mode", _m);
            _mute = _m == 2 && _rebuildFrame == Time.frameCount;   // 6개가 함께 세워질 때 같은 소리가 겹쳐 커지지 않게 한 기둥만 소리를 냄
            if (_m == 2) _rebuildFrame = Time.frameCount;
            for (int i = 0; i < _bones.Length; i++) { _prev[i] = _bones[i].position; _vel[i] = Vector3.zero; }
        }

        // 빠르게 움직이던 조각이 갑자기 멈춘 자리에 먼지·부스러기
        private void Stops()
        {
            float _dt = Time.deltaTime; if (_dt < 1e-4f) return;
            for (int i = 0; i < _bones.Length; i++)
            {
                Vector3 _p = _bones[i].position, _v = (_p - _prev[i]) / _dt;
                if (_vel[i].sqrMagnitude > 4f && _v.sqrMagnitude < _vel[i].sqrMagnitude * 0.2f)
                {
                    for (int k = 0; k < (_mode == 1 ? 4 : 2); k++) Dust(_p, 1.2f, _mode == 1 ? 1f : 0.45f);
                    for (int k = 0; k < 2; k++) Rock(_p, 1.4f, 0.05f);
                }
                _prev[i] = _p; _vel[i] = _v;
            }
        }

        private void BreakFx(float _t)
        {
            Vector3 _o = transform.position;
            if (_t < BURST && Random.value < 22f * (_t - _last)) Rock(_o + Ring(0.5f) + Vector3.up * Random.Range(2.3f, 5.4f), 0.3f, 0.035f);
            if (Hit(BURST, _t))
            {
                for (int i = 0; i < 60; i++) Rock(_o + Ring(0.45f) + Vector3.up * Random.Range(1.9f, 2.6f), 3.4f, 0.07f);
                for (int i = 0; i < 26; i++) Dust(_o + Ring(0.4f) + Vector3.up * Random.Range(1.8f, 2.6f), 2f, 0.9f);
            }
            for (int i = Count(260f, _t); i > 0; i--)
                if (Pick(_x => { float _k = (_t - _evap[_x].x) / _evap[_x].y; return _k > 0.03f && _k < 0.9f; }, out var _p))
                {
                    Mote(_p, new Vector3(0.1f, Random.Range(0.5f, 1.2f), 0.05f) + Random.insideUnitSphere * 0.25f, Random.Range(0.9f, 1.6f));
                    if (Random.value < 0.16f) Wisp(_p);
                }
        }

        private void RebuildFx(float _t)
        {
            Vector3 _o = transform.position;
            if (_t < 1.5f)
                for (int i = Count(230f, _t); i > 0; i--) In(Point(_stumpTop[Random.Range(0, _stumpTop.Length)]), Random.Range(1.4f, 3.4f), Random.Range(0.8f, 1.3f));
            for (int i = Count(300f, _t); i > 0; i--)
                if (Pick(_x => _t > _app[_x].x - 0.5f && _t < _app[_x].x + 0.6f, out var _p)) In(_p, Random.Range(0.6f, 1.7f), Random.Range(0.5f, 0.9f));
            for (int k = 0; k < 4; k++)
                if (Hit(2.97f + 0.4f * k, _t))
                    for (int i = 0; i < 14; i++) { Dust(_o + Ring(0.5f) + Vector3.up * TIER_Y[k], 1.5f, 0.45f); Rock(_o + Ring(0.55f) + Vector3.up * TIER_Y[k], 1f, 0.03f); }
        }

        // ---------- 입자 ----------
        private int Count(float _rate, float _t) { float _n = _rate * (_t - _last); return (int)_n + (Random.value < _n - (int)_n ? 1 : 0); }
        private static Vector3 Ring(float _r) { float _a = Random.value * Mathf.PI * 2f; return new Vector3(Mathf.Cos(_a) * _r, 0f, Mathf.Sin(_a) * _r); }
        private Vector3 Point(int _i) { int _b = _boneOf[_i]; return (_bones[_b].localToWorldMatrix * _binds[_b]).MultiplyPoint3x4(_verts[_i]); }
        private bool Pick(System.Func<int, bool> _ok, out Vector3 _p)
        {
            for (int k = 0; k < 6; k++) { int _i = Random.Range(0, _verts.Length); if (_evap[_i].x < 100f && _ok(_i)) { _p = Point(_i); return true; } }
            _p = default; return false;
        }
        private static void Emit(ParticleSystem _ps, Vector3 _p, Vector3 _v, float _life, float _size, Color _c)
            => _ps.Emit(new ParticleSystem.EmitParams { position = _p, velocity = _v, startLifetime = _life, startSize = _size, startColor = _c, rotation = Random.Range(0f, 360f) }, 1);
        private void Mote(Vector3 _p, Vector3 _v, float _life) => Emit(_mote, _p, _v, _life, Random.Range(0.07f, 0.17f), GREEN);
        private void In(Vector3 _p, float _r, float _life) { Vector3 _d = Random.onUnitSphere * _r; _d.y = Mathf.Abs(_d.y) * 0.8f; Mote(_p + _d, -_d / _life, _life); }
        private void Dust(Vector3 _p, float _speed, float _size) { Vector3 _v = Ring(_speed * Random.Range(0.4f, 1f)); _v.y = Random.Range(0f, 0.5f); Emit(_dust, _p + Vector3.up * 0.1f, _v, Random.Range(1.2f, 2.2f), _size * Random.Range(0.7f, 1.2f), DUST); }
        private void Wisp(Vector3 _p) => Emit(_dust, _p, new Vector3(Random.Range(-0.1f, 0.2f), Random.Range(0.7f, 1.4f), 0f), Random.Range(1.3f, 2f), Random.Range(0.5f, 0.9f), WISP);
        private void Rock(Vector3 _p, float _speed, float _size) { Vector3 _v = Ring(_speed * Random.Range(0.4f, 1f)); _v.y = Random.Range(0f, _speed * 0.8f); Emit(_rock, _p, _v, Random.Range(1.2f, 2f), _size * ROCK_SIZE * Random.Range(0.5f, 1.2f), Color.white); }
    }
}
