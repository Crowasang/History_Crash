using UnityEngine;
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
    /// 바닥 예고 표시(패턴 B·D). impe 바닥과 같은 언어로 보이게 만든다:
    /// 어두운 돌 위에 연두색 균열과 상형 문양이 은은하게 번지고, 공격이 가까울수록 밝아진다.
    /// - Crack(B): 칸 중심에서 갈라져 나가는 균열. 예고가 진행되며 균열이 퍼진다(크기 0.55 → 1).
    /// - SunSeal(D): 태양 원반을 닮은 고리 문양 + 중심에서 뻗는 균열 + 떨어지는 태양의 그림자.
    /// </summary>
    public class PatternTelegraph
    {

        private static Texture2D _crackTex, _sealTex, _shadowTex, _heatTex;
        private static Mesh _quad;

        private readonly TelegraphStyle _style;
        private readonly Transform _root;
        private readonly Material _glowMat, _shadowMat, _heatMat;
        private readonly Transform _glowT, _heatT;
        private readonly Color _glowColor, _heatColor;
        private float _size, _spin;

        /// <param name="_glow">균열·문양 발광색(HDR). impe 균열색과 맞춘다.</param>
        /// <param name="_heat">중심 열기 색(HDR, D만). 검은 색이면 쓰지 않음.</param>
        public PatternTelegraph(string _name, TelegraphStyle _style, Color _glow, Color _heat, Transform _parent)
        {
            EnsureShared();
            this._style = _style;
            _glowColor = _glow; _heatColor = _heat;
            _root = new GameObject(_name).transform;
            _root.SetParent(_parent, false);

            if (_style == TelegraphStyle.SunSeal)
            {
                _shadowMat = MakeMaterial(_shadowTex, false);
                MakeLayer("Shadow", _shadowMat, 0.010f, 1.0f);
                _heatMat = MakeMaterial(_heatTex, true);
                _heatT = MakeLayer("Heat", _heatMat, 0.016f, 0.7f);
            }
            _glowMat = MakeMaterial(_style == TelegraphStyle.Crack ? _crackTex : _sealTex, true);
            _glowT = MakeLayer("Glow", _glowMat, 0.022f, 1.0f);
            _root.gameObject.SetActive(false);
        }

        private Transform MakeLayer(string _n, Material _m, float _lift, float _scale)
        {
            var _go = new GameObject(_n);
            _go.transform.SetParent(_root, false);
            _go.transform.localPosition = Vector3.up * _lift;
            _go.transform.localScale = new Vector3(_scale, 1f, _scale);
            _go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var _mr = _go.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = _m;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            return _go.transform;
        }

        /// <summary>바닥 위치와 지름(m)으로 켠다. 균열 방향은 매번 무작위로 돌린다.</summary>
        public void Show(Vector3 _groundPos, float _diameter)
        {
            _size = _diameter;
            _root.position = _groundPos;
            _spin = Random.Range(0f, 360f);
            _root.rotation = Quaternion.Euler(0f, _style == TelegraphStyle.Crack ? Mathf.Round(_spin / 90f) * 90f : _spin, 0f);
            _root.localScale = Vector3.one * _diameter;
            Set(0f, 0f);
            _root.gameObject.SetActive(true);
        }

        /// <param name="_k">발광 세기 0~1(1이면 바닥 균열 세기 정도)</param>
        /// <param name="_progress">예고 진행 0~1. B는 균열이 퍼지고, D는 그림자가 짙어지고 문양이 천천히 돈다.</param>
        public void Set(float _k, float _progress)
        {
            _progress = Mathf.Clamp01(_progress);
            var _c = _glowColor * Mathf.Max(0f, _k); _c.a = Mathf.Clamp01(_k);
            _glowMat.SetColor("_BaseColor", _c);
            if (_style == TelegraphStyle.Crack)
            {
                float _s = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, _progress));
                _glowT.localScale = new Vector3(_s, 1f, _s);
            }
            else
            {
                _glowT.localRotation = Quaternion.Euler(0f, _progress * 40f, 0f);
                _shadowMat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.15f + 0.55f * _progress * _progress));
                float _h = Mathf.Clamp01(_k) * _progress * _progress;
                var _hc = _heatColor * _h; _hc.a = _h;
                _heatMat.SetColor("_BaseColor", _hc);
                float _hs = Mathf.Lerp(0.35f, 0.75f, _progress);
                _heatT.localScale = new Vector3(_hs, 1f, _hs);
            }
        }

        public void Hide() { if (_root) _root.gameObject.SetActive(false); }

        // ---------- 공용 텍스처 ----------
        private static void EnsureShared()
        {
            if (_quad != null) return;
            _quad = new Mesh { name = "TelegraphQuad" };
            _quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.RecalculateNormals(); _quad.RecalculateBounds();

            var _rng = new System.Random(1137);
            _crackTex = BuildCrack(256, _rng);
            _sealTex = BuildSeal(512, _rng);
            _shadowTex = BuildSoftDisc(128, 2.2f);
            _heatTex = BuildSoftDisc(128, 3.0f);
        }

        // 바닥 타일 균열처럼 중심에서 갈라져 나가는 가는 선. 가장자리로 갈수록 가늘고 옅다.
        private static Texture2D BuildCrack(int _n, System.Random _rng)
        {
            var _a = new float[_n * _n];
            float _c = (_n - 1) * 0.5f;
            int _main = 6;
            for (int _b = 0; _b < _main; _b++)
            {
                float _ang = (_b + (float)_rng.NextDouble() * 0.6f) / _main * Mathf.PI * 2f;
                Branch(_a, _n, new Vector2(_c, _c), _ang, _n * 0.52f, 4.2f, _rng, 2);
            }
            // 중심의 깨진 자리: 짧은 잔금
            for (int _b = 0; _b < 8; _b++)
                Branch(_a, _n, new Vector2(_c, _c), (float)_rng.NextDouble() * Mathf.PI * 2f, _n * 0.16f, 2.6f, _rng, 0);
            // 균열 아래로 은은하게 번지는 빛(타일 모양을 따라 모서리가 둥근 사각, 가장자리는 0)
            for (int _y = 0; _y < _n; _y++)
                for (int _x = 0; _x < _n; _x++)
                {
                    float _dx = Mathf.Abs(_x - _c) / _c, _dy = Mathf.Abs(_y - _c) / _c;
                    float _e = Mathf.Pow(Mathf.Pow(_dx, 4f) + Mathf.Pow(_dy, 4f), 0.25f);
                    float _fill = 0.16f * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.35f, 0.98f, _e));
                    int _i = _y * _n + _x; _a[_i] = Mathf.Max(_a[_i], _fill);
                }
            return ToTexture(_a, _n, (_x, _y) =>
            {
                float _r = Vector2.Distance(new Vector2(_x, _y), new Vector2(_c, _c)) / _c;
                return Mathf.Clamp01(1.25f - 0.6f * _r) * Mathf.Clamp01((1.02f - Mathf.Max(Mathf.Abs(_x - _c), Mathf.Abs(_y - _c)) / _c) * 12f);
            }, 0.16f);
        }

        // 태양 원반 문양: 바깥 이중 고리, 고리 사이 상형 눈금(길고 짧게 번갈아), 중심에서 뻗는 균열
        private static Texture2D BuildSeal(int _n, System.Random _rng)
        {
            var _a = new float[_n * _n];
            float _c = (_n - 1) * 0.5f;
            for (int _y = 0; _y < _n; _y++)
                for (int _x = 0; _x < _n; _x++)
                {
                    float _dx = (_x - _c) / _c, _dy = (_y - _c) / _c;
                    float _r = Mathf.Sqrt(_dx * _dx + _dy * _dy);
                    float _ang = Mathf.Atan2(_dy, _dx);
                    float _v = 0f;
                    _v += Line(_r, 0.955f, 0.010f) * 0.9f;            // 바깥 고리
                    _v += Line(_r, 0.885f, 0.006f) * 0.6f;            // 안쪽 고리
                    // 고리 사이 눈금 36개: 3개마다 긴 눈금(태양 광선처럼)
                    float _seg = _ang / (Mathf.PI * 2f) * 36f;
                    float _f = Mathf.Abs(_seg - Mathf.Round(_seg));
                    bool _longTick = ((int)Mathf.Round(_seg) % 3 + 3) % 3 == 0;
                    float _inner = _longTick ? 0.80f : 0.90f;
                    if (_r > _inner && _r < 0.95f) _v += Mathf.Clamp01(1f - _f / (_longTick ? 0.07f : 0.045f)) * (_longTick ? 0.85f : 0.5f);
                    // 이음 고리(긴 눈금 끝)
                    _v += Line(_r, 0.80f, 0.004f) * 0.35f;
                    _a[_y * _n + _x] = Mathf.Max(_a[_y * _n + _x], _v);
                }
            for (int _b = 0; _b < 7; _b++)
            {
                float _ang = (_b + (float)_rng.NextDouble() * 0.7f) / 7f * Mathf.PI * 2f;
                Branch(_a, _n, new Vector2(_c, _c), _ang, _n * 0.37f, 2.6f, _rng, 2);
            }
            return ToTexture(_a, _n, null, 0.06f);
        }

        private static float Line(float _v, float _center, float _width)
        {
            float _d = Mathf.Abs(_v - _center) / _width;
            return Mathf.Clamp01(1f - _d * _d * 0.5f);
        }

        // 지그재그로 꺾이며 나아가는 균열. depth가 남아 있으면 곁가지를 친다.
        private static void Branch(float[] _a, int _n, Vector2 _p, float _ang, float _len, float _width, System.Random _rng, int _depth)
        {
            float _traveled = 0f, _step = 2.5f;
            while (_traveled < _len)
            {
                _ang += ((float)_rng.NextDouble() - 0.5f) * 0.55f;
                Vector2 _q = _p + new Vector2(Mathf.Cos(_ang), Mathf.Sin(_ang)) * _step;
                float _w = Mathf.Lerp(_width, 0.6f, _traveled / _len);
                Stamp(_a, _n, _q, _w);
                _p = _q; _traveled += _step;
                if (_depth > 0 && _rng.NextDouble() < 0.035)
                    Branch(_a, _n, _p, _ang + (_rng.NextDouble() < 0.5 ? -1f : 1f) * (0.5f + (float)_rng.NextDouble() * 0.5f), (_len - _traveled) * 0.55f, _w * 0.75f, _rng, _depth - 1);
            }
        }

        private static void Stamp(float[] _a, int _n, Vector2 _p, float _w)
        {
            int _r = Mathf.CeilToInt(_w + 2);
            for (int _y = (int)_p.y - _r; _y <= (int)_p.y + _r; _y++)
                for (int _x = (int)_p.x - _r; _x <= (int)_p.x + _r; _x++)
                {
                    if (_x < 0 || _y < 0 || _x >= _n || _y >= _n) continue;
                    float _d = Vector2.Distance(new Vector2(_x, _y), _p);
                    float _v = Mathf.Clamp01(1f - Mathf.Max(0f, _d - _w * 0.5f) / 1.6f);
                    int _i = _y * _n + _x; if (_v > _a[_i]) _a[_i] = _v;
                }
        }

        // halo: 선 주변에 은은하게 번지는 빛(블러 대신 거리 기반 확장)
        private static Texture2D ToTexture(float[] _a, int _n, System.Func<int, int, float> _fade, float _halo)
        {
            var _blur = BoxBlur(_a, _n, 3);
            _blur = BoxBlur(_blur, _n, 3);
            var _t = new Texture2D(_n, _n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var _px = new Color[_n * _n];
            for (int _y = 0; _y < _n; _y++)
                for (int _x = 0; _x < _n; _x++)
                {
                    int _i = _y * _n + _x;
                    float _v = Mathf.Clamp01(_a[_i] + _blur[_i] * _halo * 6f);
                    if (_fade != null) _v *= _fade(_x, _y);
                    _px[_i] = new Color(1, 1, 1, _v);
                }
            _t.SetPixels(_px); _t.Apply(true);
            return _t;
        }

        private static float[] BoxBlur(float[] _a, int _n, int _r)
        {
            var _o = new float[_n * _n];
            for (int _y = 0; _y < _n; _y++)
                for (int _x = 0; _x < _n; _x++)
                {
                    float _s = 0; int _k = 0;
                    for (int _yy = Mathf.Max(0, _y - _r); _yy <= Mathf.Min(_n - 1, _y + _r); _yy += 1)
                        for (int _xx = Mathf.Max(0, _x - _r); _xx <= Mathf.Min(_n - 1, _x + _r); _xx += 1) { _s += _a[_yy * _n + _xx]; _k++; }
                    _o[_y * _n + _x] = _s / _k;
                }
            return _o;
        }

        private static Texture2D BuildSoftDisc(int _n, float _power)
        {
            var _t = new Texture2D(_n, _n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float _c = (_n - 1) * 0.5f;
            for (int _y = 0; _y < _n; _y++)
                for (int _x = 0; _x < _n; _x++)
                {
                    float _r = Vector2.Distance(new Vector2(_x, _y), new Vector2(_c, _c)) / _c;
                    _t.SetPixel(_x, _y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - _r), _power)));
                }
            _t.Apply();
            return _t;
        }

        private static Material MakeMaterial(Texture _tex, bool _additive)
        {
            var _m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            _m.SetTexture("_BaseMap", _tex);
            _m.SetColor("_BaseColor", Color.clear);
            _m.SetFloat("_Surface", 1f);
            _m.SetFloat("_Blend", _additive ? 2f : 0f);
            _m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _m.SetFloat("_DstBlend", (float)(_additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            _m.SetFloat("_ZWrite", 0f);
            _m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _m;
        }
    }
}
