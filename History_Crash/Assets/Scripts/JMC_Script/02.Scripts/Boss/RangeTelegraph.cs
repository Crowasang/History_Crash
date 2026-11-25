using UnityEngine;

namespace HistoryCrash.Boss
{
    /// <summary>
    /// 파라오 공격 범위 예고 하나(원·사각·긴 띠). 테두리가 맞는 범위이고, 안에서 균열이 끝까지 퍼지면 공격이 들어온다.
    /// 모양은 Art/Resources/Telegraph/RangeTelegraph.shader. 씬·프리팹 없이 실행 중에 만든다.
    /// </summary>
    public class RangeTelegraph
    {
        private const string SHADER = "Telegraph/RangeTelegraph";
        private const float MARGIN = 0.8f, LIFT = 0.03f, FADE_IN = 0.15f, FADE_OUT = 0.4f;
        private static readonly int LINE = Shader.PropertyToID("_Line"), CRACK = Shader.PropertyToID("_Crack"), SIZE = Shader.PropertyToID("_Size"),
            HALF = Shader.PropertyToID("_Half"), R = Shader.PropertyToID("_R"), LANE = Shader.PropertyToID("_Lane"), P = Shader.PropertyToID("_P"),
            T = Shader.PropertyToID("_T"), FLASH = Shader.PropertyToID("_Flash"), FADE = Shader.PropertyToID("_Fade"), DIM = Shader.PropertyToID("_Dim"), SEED = Shader.PropertyToID("_Seed"), TAPER = Shader.PropertyToID("_Taper"), NEAR_FADE = Shader.PropertyToID("_NearFade");
        private const int DRAPE_ROWS = 46, DRAPE_COLS = 15;   // 높이를 따라가는 판: 시작 쪽 0.2m 간격 + 끝 한 줄
        private const float DRAPE_STEP = 0.2f;
        private static Mesh _quad;

        private readonly GameObject _go;
        private readonly Material _mat;
        private readonly System.Func<Vector3, float> _heightAt;   // 있으면 판이 이 높이(계단·제단)를 따라 덮인다
        private readonly Mesh _drape;
        private readonly Vector3[] _drapeV; private readonly Vector2[] _drapeUv;
        private float _age, _end = -1f, _hold;   // _end: 공격 순간부터 지난 시간(0 미만이면 예고 중)
        private bool _flash;

        public bool On { get { return _go && _go.activeSelf; } }
        /// <summary>공격이 들어와 사라지는 중</summary>
        public bool Ending { get { return _end >= 0f; } }

        /// <param name="_dim">면 밝기(여러 줄이 화면을 덮는 E는 낮게)</param>
        /// <param name="_height">바닥이 평평하지 않은 곳을 지나는 띠(A)의 높이 함수</param>
        public RangeTelegraph(string _name, Transform _parent, Color _line, Color _crack, float _dim = 1f, System.Func<Vector3, float> _height = null)
        {
            _heightAt = _height;
            if (!_quad)
            {
                _quad = new Mesh { name = "RangeTelegraphQuad" };
                _quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
                _quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _quad.RecalculateNormals(); _quad.RecalculateBounds();
            }
            _mat = new Material(Resources.Load<Shader>(SHADER));
            _mat.SetColor(LINE, _line); _mat.SetColor(CRACK, _crack); _mat.SetFloat(DIM, _dim);
            _go = new GameObject(_name);
            _go.transform.SetParent(_parent, false);
            if (_heightAt != null)
            {
                _drape = new Mesh { name = "RangeTelegraphDrape" };
                _drape.MarkDynamic();
                _drapeV = new Vector3[DRAPE_ROWS * DRAPE_COLS]; _drapeUv = new Vector2[_drapeV.Length];
                var _tri = new int[(DRAPE_ROWS - 1) * (DRAPE_COLS - 1) * 6];
                for (int _r = 0, _k = 0; _r < DRAPE_ROWS - 1; _r++)
                    for (int _c = 0; _c < DRAPE_COLS - 1; _c++)
                    {
                        int _i = _r * DRAPE_COLS + _c;
                        _tri[_k++] = _i; _tri[_k++] = _i + 1; _tri[_k++] = _i + DRAPE_COLS;
                        _tri[_k++] = _i + DRAPE_COLS; _tri[_k++] = _i + 1; _tri[_k++] = _i + DRAPE_COLS + 1;
                    }
                _drape.vertices = _drapeV; _drape.triangles = _tri;
            }
            _go.AddComponent<MeshFilter>().sharedMesh = _drape ? _drape : _quad;
            var _mr = _go.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = _mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _go.SetActive(false);
        }

        public void PlaceCircle(Vector3 _ground, float _radius)
        {
            _mat.SetFloat(R, _radius); _mat.SetFloat(LANE, 0f); _mat.SetFloat(NEAR_FADE, 0f); _mat.SetVector(TAPER, Vector4.zero);
            Place(_ground, 0f, _radius * 2f, _radius * 2f);
        }

        public void PlaceBox(Vector3 _ground, float _sizeX, float _sizeZ)
        {
            _mat.SetFloat(R, 0f); _mat.SetFloat(LANE, 0f); _mat.SetFloat(NEAR_FADE, 0f); _mat.SetVector(TAPER, Vector4.zero);
            Place(_ground, 0f, _sizeX, _sizeZ);
        }

        /// <summary>_from에서 _to까지의 긴 띠(균열은 _from 쪽에서 퍼짐)</summary>
        /// <param name="_nearFade">시작 쪽 끝을 서서히 지우는 길이(m)</param>
        /// <param name="_startWidth">0보다 크면 시작 쪽 폭(_taper m에 걸쳐 _width로 넓어짐)</param>
        public void PlaceLane(Vector3 _from, Vector3 _to, float _width, float _nearFade, float _startWidth = 0f, float _taper = 0f)
        {
            _mat.SetFloat(NEAR_FADE, _nearFade); _mat.SetVector(TAPER, new Vector4(_startWidth * 0.5f, _taper));
            Vector3 _d = _to - _from; _d.y = 0f;
            _mat.SetFloat(R, 0f); _mat.SetFloat(LANE, 1f);
            Place((_from + _to) * 0.5f, -Mathf.Atan2(_d.z, _d.x) * Mathf.Rad2Deg, _d.magnitude, _width);
        }

        private void Place(Vector3 _ground, float _yaw, float _x, float _z)
        {
            _go.transform.SetPositionAndRotation(_ground + Vector3.up * LIFT, Quaternion.Euler(0f, _yaw, 0f));
            _go.transform.localScale = new Vector3(_x + MARGIN * 2f, 1f, _z + MARGIN * 2f);
            _mat.SetVector(SIZE, new Vector4(_x + MARGIN * 2f, _z + MARGIN * 2f));
            _mat.SetVector(HALF, new Vector4(_x * 0.5f, _z * 0.5f));
            if (_drape) Drape(_ground.y, _x + MARGIN * 2f);
        }

        // 판의 점을 바닥 높이에 맞춘다. 시작 쪽(제단·계단)만 촘촘하고 나머지는 평평한 바닥이라 끝 한 줄로 충분하다
        private void Drape(float _floorY, float _sizeX)
        {
            for (int _r = 0; _r < DRAPE_ROWS; _r++)
            {
                float _u = _r == DRAPE_ROWS - 1 ? 1f : Mathf.Min(1f, _r * DRAPE_STEP / _sizeX);
                for (int _c = 0; _c < DRAPE_COLS; _c++)
                {
                    float _v = _c / (DRAPE_COLS - 1f);
                    int _i = _r * DRAPE_COLS + _c;
                    Vector3 _p = new Vector3(_u - 0.5f, 0f, _v - 0.5f);
                    _p.y = _heightAt(_go.transform.TransformPoint(_p)) - _floorY;
                    _drapeV[_i] = _p; _drapeUv[_i] = new Vector2(_u, _v);
                }
            }
            _drape.vertices = _drapeV; _drape.uv = _drapeUv;
            _drape.bounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(1f, 8f, 1f));
        }

        /// <summary>예고 진행 0~1. 꺼져 있으면 켠다. 사라지는 중이면 무시한다.</summary>
        public void Set(float _progress)
        {
            if (!On)
            {
                if (!_go) return;
                _age = 0f; _end = -1f;
                _mat.SetVector(SEED, new Vector4(Random.Range(0f, 40f), Random.Range(0f, 40f)));
                _mat.SetFloat(FLASH, 0f); _mat.SetFloat(FADE, 0f);
                _go.SetActive(true);
            }
            if (!Ending) _mat.SetFloat(P, Mathf.Clamp01(_progress));
        }

        /// <summary>공격 순간: 번쩍이고 사라진다. _holdTime초 동안은 남아 있다(아직 지나가는 중인 공격).</summary>
        public void Strike(float _holdTime = 0f)
        {
            if (!On || Ending) return;
            _mat.SetFloat(P, 1f);
            _end = 0f; _hold = _holdTime; _flash = true;
        }

        /// <summary>번쩍임 없이 사라진다(공격이 도중에 끝난 경우).</summary>
        public void FadeOut()
        {
            if (!On || Ending) return;
            _end = 0f; _hold = 0f; _flash = false;
        }

        public void Hide() { _end = -1f; if (_go) _go.SetActive(false); }

        public void Tick(float _dt)
        {
            if (!On) return;
            _age += _dt;
            _mat.SetFloat(T, _age);
            float _fade = Mathf.Clamp01(_age / FADE_IN);
            if (Ending)
            {
                _end += _dt;
                if (_flash) _mat.SetFloat(FLASH, Mathf.Exp(-_end * 9f));
                float _u = (_end - _hold) / FADE_OUT;
                if (_u >= 1f) { Hide(); return; }
                _fade *= 1f - Mathf.Clamp01(_u);
            }
            _mat.SetFloat(FADE, _fade);
        }
    }
}
