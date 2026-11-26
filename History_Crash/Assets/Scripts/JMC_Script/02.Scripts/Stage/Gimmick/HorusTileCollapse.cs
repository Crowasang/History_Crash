using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// 15초 주기로 호루스 석상의 눈이 빛나고, 바닥 타일 12종(Tile_Crack_A~L) 중 한 종류가
    /// 경고(타일 전체가 은은하게 빛남) 뒤 사암이 부서지듯 깨져 7.5초간 사라졌다가 다시 수복된다.
    /// 깨진 칸은 아래 맵 대신 어두운 심연(구덩이 벽, 바닥의 희미한 빛, 올라오는 불씨)으로 보인다.
    /// 깨진 칸은 TileGrid의 구멍(hole)이 되어, 점프로는 건널 수 있지만 서 있으면 떨어진다(PlayerMover).
    /// </summary>
    public class HorusTileCollapse : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        [SerializeField, FormerlySerializedAs("floorRoot"), Tooltip("Temple_impe/Floor")] private Transform _floorRoot;
        [SerializeField, Tooltip("플레이어가 못 가는 구역(WallClimbBlockers의 투명 상자). 중심이 이 안에 있는 타일은 무너지지 않음")] private Collider[] _unreachableAreas;

        [Header("주기(초)")]
        [SerializeField, FormerlySerializedAs("period")] private float _period = 15f;
        [SerializeField, FormerlySerializedAs("firstDelay")] private float _firstDelay = 5f;
        [SerializeField, FormerlySerializedAs("warnTime"), Tooltip("눈이 빛나고 타일이 은은하게 빛나는 경고 시간")] private float _warnTime = 2f;
        [SerializeField, FormerlySerializedAs("brokenTime"), Tooltip("깨져서 사라져 있는 시간")] private float _brokenTime = 7.5f;
        [SerializeField, FormerlySerializedAs("breakAnimTime")] private float _breakAnimTime = 0.25f;
        [SerializeField, FormerlySerializedAs("restoreAnimTime")] private float _restoreAnimTime = 0.5f;
        [SerializeField, FormerlySerializedAs("sinkDepth")] private float _sinkDepth = 0.35f;

        [Header("호루스 눈")]
        [SerializeField, FormerlySerializedAs("eyePositions"), Tooltip("눈 위치(월드). 머리 메시 단면에서 잡은 값")] private Vector3[] _eyePositions = { new Vector3(-1.05f, 8.95f, 25.03f), new Vector3(-1.05f, 8.95f, 26.11f) };
        [SerializeField, FormerlySerializedAs("eyeColor"), ColorUsage(false, true)] private Color _eyeColor = new Color(0.55f, 1f, 0.12f) * 6f;
        [SerializeField, FormerlySerializedAs("eyeSize")] private float _eyeSize = 0.16f;
        [SerializeField, FormerlySerializedAs("eyeLightIntensity")] private float _eyeLightIntensity = 8f;
        [SerializeField, FormerlySerializedAs("eyeLightRange")] private float _eyeLightRange = 4f;

        [Header("경고: 타일 전체 발광")]
        [SerializeField, FormerlySerializedAs("warnGlowColor"), Tooltip("impe 균열 발광색(연두, hue≈90)과 통일")] private Color _warnGlowColor = new Color(0.55f, 1f, 0.12f);
        [SerializeField, FormerlySerializedAs("warnGlowMax"), Tooltip("경고 끝 무렵 발광 세기(0~1). 사각 덮개는 은은하게만 두고 균열 발광으로 알린다")] private float _warnGlowMax = 0.2f;
        [SerializeField, FormerlySerializedAs("warnCrackEmission"), Tooltip("균열 발광도 함께 올림(배율)")] private float _warnCrackEmission = 4f;

        [Header("구멍(심연)")]
        [SerializeField, FormerlySerializedAs("pitDepth")] private float _pitDepth = 4f;
        [SerializeField, FormerlySerializedAs("pitTopColor")] private Color _pitTopColor = new Color(0.075f, 0.055f, 0.035f);
        [SerializeField, FormerlySerializedAs("pitGlowColor")] private Color _pitGlowColor = new Color(0.05f, 0.22f, 0.02f);
        [SerializeField, FormerlySerializedAs("abyssEmberColor")] private Color _abyssEmberColor = new Color(0.45f, 1f, 0.15f);
        [SerializeField, FormerlySerializedAs("abyssEmberRate")] private float _abyssEmberRate = 45f;

        [Header("붕괴 파티클(패턴 B 재질 재사용)")]
        [SerializeField, FormerlySerializedAs("rockMaterial")] private Material _rockMaterial;
        [SerializeField, FormerlySerializedAs("chunkMeshes")] private Mesh[] _chunkMeshes;
        [SerializeField, FormerlySerializedAs("dustMaterial")] private Material _dustMaterial;
        [SerializeField, FormerlySerializedAs("chunksPerTile")] private int _chunksPerTile = 6;
        [SerializeField, FormerlySerializedAs("gravelPerTile")] private int _gravelPerTile = 12;
        [SerializeField, FormerlySerializedAs("dustPerTile")] private int _dustPerTile = 1;
        [SerializeField, FormerlySerializedAs("dustColor"), Tooltip("붕괴 먼지 색(조명 안 받는 셰이더라 밤 톤으로 어둡게)")] private Color _dustColor = new Color(0.22f, 0.19f, 0.15f, 0.22f);
        private readonly Dictionary<char, List<HorusTile>> _byType = new Dictionary<char, List<HorusTile>>();
        private readonly List<char> _types = new List<char>();
        private readonly List<Renderer> _eyeRenderers = new List<Renderer>();
        private readonly List<Light> _eyeLights = new List<Light>();
        private MaterialPropertyBlock _mpb;
        private Material _eyeMat, _glowMat, _pitMat, _emberMat;
        private Mesh _quadMesh, _pitMesh;
        private ParticleSystem _chunkPS, _gravelPS, _dustPS, _emberPS;
        private float _chunkMeshSize = 1f;
        private List<HorusTile> _brokenTiles;

        public string Status { get; private set; } = "";
        private char _lastType = ' ';
        private float _phaseEnd; private string _phaseName = "wait"; private char _currentType = ' ';

        // 물리로 움직이는 플레이어(PlayerController)가 실제로 구멍에 빠지도록 타일 콜라이더도 끄고 켬
        // (빠지면 바닥 아래 FallZone이 피해·복귀 처리)
        private static void SetTileCollider(HorusTile _tile, bool _on)
        {
            var _col = _tile.R ? _tile.R.GetComponent<Collider>() : null;
            if (_col) _col.enabled = _on;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _grid.EnsureBuilt();   // 격자보다 먼저 실행되면 칸 좌표가 원점(0,0) 기준으로 밀린다
            foreach (Transform _t in _floorRoot)
            {
                var _r = _t.GetComponentInChildren<Renderer>();
                var _mf = _r ? _r.GetComponent<MeshFilter>() : null;
                if (!_r || !_mf || !_mf.sharedMesh) continue;
                if (IsUnreachable(_r.bounds.center)) continue;   // 석상·제단 사이처럼 못 가는 곳은 무너뜨리지 않음
                string _n = _mf.sharedMesh.name;              // Tile_Crack_A_impe
                int _k = _n.IndexOf("Tile_Crack_");
                if (_k < 0 || _n.Length <= _k + 11) continue;
                char _type = _n[_k + 11];
                if (!_byType.ContainsKey(_type)) { _byType[_type] = new List<HorusTile>(); _types.Add(_type); }
                var _cell = _grid.WorldToCell(_r.bounds.center);
                _byType[_type].Add(new HorusTile { Mesh = _r.transform, R = _r, Cell = _cell, BasePos = _r.transform.localPosition, BaseScale = _r.transform.localScale, Center = _grid.CellCenter(_cell, _grid.FloorTopY) });
            }
            _types.Sort();
            CreateEyes();
            SetEyes(0f);
            CreateSharedVisuals();
            CreateParticles();
        }

        private CancellationTokenSource _cycleCts;

        private void Start()
        {
            _cycleCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CycleAsync(_cycleCts.Token).Forget();
        }

        // 꺼지면(게임 종료 등) 코루틴처럼 주기도 멈춘다
        private void OnDisable()
        {
            if (_cycleCts == null) return;
            _cycleCts.Cancel(); _cycleCts.Dispose(); _cycleCts = null;
        }

        // 다음 프레임까지 대기. LastUpdate = 모든 Update 뒤라 코루틴(yield return null)과 같은 순서. 취소되면 true
        private static UniTask<bool> NextFrame(CancellationToken _token)
        {
            return UniTask.Yield(PlayerLoopTiming.LastUpdate, _token).SuppressCancellationThrow();
        }

        // 위에서 봤을 때 못 가는 구역 상자 안에 있는 점인지
        private bool IsUnreachable(Vector3 _p)
        {
            if (_unreachableAreas == null) return false;
            foreach (var _area in _unreachableAreas)
            {
                if (!_area) continue;
                var _b = _area.bounds;
                if (_p.x > _b.min.x && _p.x < _b.max.x && _p.z > _b.min.z && _p.z < _b.max.z) return true;
            }
            return false;
        }

        // ---------- 공용 재질/메시 ----------
        private void CreateSharedVisuals()
        {
            // 경고 발광: 모서리가 부드러운 사각 텍스처 + 가산 합성
            var _glowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int _y = 0; _y < 64; _y++)
                for (int _x = 0; _x < 64; _x++)
                {
                    float _dx = Mathf.Abs(_x - 31.5f) / 31.5f, _dy = Mathf.Abs(_y - 31.5f) / 31.5f;
                    float _edge = Mathf.Max(_dx, _dy);
                    float _a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, _edge)) * (0.75f + 0.25f * (1f - _edge));
                    _glowTex.SetPixel(_x, _y, new Color(1, 1, 1, _a));
                }
            _glowTex.Apply();
            _glowMat = MakeAdditive(_glowTex, Color.clear);
            _quadMesh = BuildQuad();

            // 심연 불씨: 둥근 부드러운 점(경고 발광과 재질을 나눔)
            var _dotTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int _y = 0; _y < 32; _y++)
                for (int _x = 0; _x < 32; _x++)
                {
                    float _d = Vector2.Distance(new Vector2(_x, _y), new Vector2(15.5f, 15.5f)) / 15.5f;
                    _dotTex.SetPixel(_x, _y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - _d), 2.2f)));
                }
            _dotTex.Apply();
            _emberMat = MakeAdditive(_dotTex, Color.white);

            // 구멍: 안쪽을 향한 열린 상자(벽 + 바닥). 위는 사암색, 아래로 갈수록 검고 바닥은 희미한 빛
            var _pitTex = new Texture2D(4, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int _y = 0; _y < 128; _y++)
            {
                float _v = _y / 127f; // 0=바닥, 1=위
                Color _c = Color.Lerp(Color.black, _pitTopColor, Mathf.Pow(_v, 3.5f)) + _pitGlowColor * Mathf.Pow(1f - _v, 6f);
                _c.a = 1;
                for (int _x = 0; _x < 4; _x++) _pitTex.SetPixel(_x, _y, _c);
            }
            _pitTex.Apply();
            _pitMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _pitMat.SetTexture("_BaseMap", _pitTex);
            _pitMat.SetColor("_BaseColor", Color.white);
            if (_pitMat.HasProperty("_Cull")) _pitMat.SetFloat("_Cull", 0f);
            _pitMesh = BuildOpenBox();
        }

        /// <summary>URP 파티클 Unlit 셰이더로 가산 합성(SrcAlpha, One) 투명 재질을 만든다. 정점색(파티클 색)도 곱해진다.</summary>
        private static Material MakeAdditive(Texture _tex, Color _color)
        {
            var _m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            _m.SetTexture("_BaseMap", _tex);
            _m.SetColor("_BaseColor", _color);
            _m.SetFloat("_Surface", 1f);
            _m.SetFloat("_Blend", 2f);
            _m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _m.SetFloat("_ZWrite", 0f);
            _m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _m;
        }

        private static Mesh BuildQuad()
        {
            var _m = new Mesh { name = "TileGlowQuad" };
            _m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            _m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            _m.colors = new[] { Color.white, Color.white, Color.white, Color.white };   // 파티클 셰이더는 정점색을 곱하므로 흰색 필요
            _m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _m.RecalculateNormals(); _m.RecalculateBounds();
            return _m;
        }

        /// <summary>윗면이 열린 단위 상자(x,z −0.5~0.5, y −1~0). v는 위 1 → 아래 0.</summary>
        private static Mesh BuildOpenBox()
        {
            var _v = new List<Vector3>(); var _uv = new List<Vector2>(); var _tri = new List<int>();
            Vector3[] _c = { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            for (int _i = 0; _i < 4; _i++)
            {
                Vector3 _a = _c[_i], _b = _c[(_i + 1) % 4];
                int _s = _v.Count;
                _v.Add(_a); _v.Add(_b); _v.Add(_b + Vector3.down); _v.Add(_a + Vector3.down);
                _uv.Add(new Vector2(0, 1)); _uv.Add(new Vector2(1, 1)); _uv.Add(new Vector2(1, 0)); _uv.Add(new Vector2(0, 0));
                _tri.AddRange(new[] { _s, _s + 2, _s + 1, _s, _s + 3, _s + 2 });
            }
            int _bs = _v.Count;
            foreach (var _p in _c) { _v.Add(_p + Vector3.down); _uv.Add(new Vector2(0.5f, 0)); }
            _tri.AddRange(new[] { _bs, _bs + 2, _bs + 1, _bs, _bs + 3, _bs + 2 });
            var _m = new Mesh { name = "TilePitBox" };
            _m.SetVertices(_v); _m.SetUVs(0, _uv); _m.SetTriangles(_tri, 0);
            _m.RecalculateNormals(); _m.RecalculateBounds();
            return _m;
        }

        private GameObject MakeChild(string _name, Mesh _mesh, Material _mat, Vector3 _pos, Vector3 _scale)
        {
            var _go = new GameObject(_name);
            _go.transform.SetParent(transform, false);
            _go.transform.position = _pos; _go.transform.localScale = _scale;
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var _r = _go.AddComponent<MeshRenderer>(); _r.sharedMaterial = _mat;
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _r.receiveShadows = false;
            _go.SetActive(false);
            return _go;
        }

        private GameObject GlowOf(HorusTile _t)
        {
            if (!_t.Glow) _t.Glow = MakeChild("TileWarnGlow", _quadMesh, _glowMat, _t.Center + Vector3.up * 0.015f, new Vector3(_grid.TileSize * 1.02f, 1, _grid.TileSize * 1.02f));
            return _t.Glow;
        }

        private GameObject PitOf(HorusTile _t)
        {
            if (!_t.Pit) _t.Pit = MakeChild("TilePit", _pitMesh, _pitMat, _t.Center + Vector3.down * 0.005f, new Vector3(_grid.TileSize, _pitDepth, _grid.TileSize));
            return _t.Pit;
        }

        // ---------- 파티클 ----------
        private ParticleSystem MakePS(string _name, Material _mat, bool _mesh, float _gravity, int _max)
        {
            var _go = new GameObject(_name);
            _go.transform.SetParent(transform, false);
            var _ps = _go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var _main = _ps.main;
            _main.playOnAwake = false; _main.loop = true; _main.duration = 1f;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.scalingMode = ParticleSystemScalingMode.Local;
            _main.gravityModifier = _gravity; _main.maxParticles = _max;
            _main.startRotation3D = _mesh;
            var _em = _ps.emission; _em.enabled = false;
            var _sh = _ps.shape; _sh.enabled = false;
            var _r = _go.GetComponent<ParticleSystemRenderer>();
            _r.sharedMaterial = _mat;
            if (_mesh && _chunkMeshes != null && _chunkMeshes.Length > 0) { _r.renderMode = ParticleSystemRenderMode.Mesh; _r.SetMeshes(_chunkMeshes); }
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ps.Play();
            return _ps;
        }

        private void CreateParticles()
        {
            if (_chunkMeshes != null && _chunkMeshes.Length > 0 && _chunkMeshes[0])
                _chunkMeshSize = Mathf.Max(_chunkMeshes[0].bounds.size.x, _chunkMeshes[0].bounds.size.y, _chunkMeshes[0].bounds.size.z);
            if (_rockMaterial)
            {
                _chunkPS = MakePS("TileBreak_Chunks", _rockMaterial, true, 2.2f, 3000);
                _gravelPS = MakePS("TileBreak_Gravel", _rockMaterial, true, 2.4f, 4000);
                foreach (var _ps in new[] { _chunkPS, _gravelPS })
                {
                    var _sol = _ps.sizeOverLifetime; _sol.enabled = true;
                    _sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.75f, 1), new Keyframe(1, 0)));
                }
            }
            if (_dustMaterial)
            {
                _dustPS = MakePS("TileBreak_Dust", _dustMaterial, false, -0.05f, 1500);
                var _col = _dustPS.colorOverLifetime; _col.enabled = true;
                var _g = new Gradient();
                _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
                _col.color = _g;
                var _sol = _dustPS.sizeOverLifetime; _sol.enabled = true;
                _sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 1.4f)));
                var _vel = _dustPS.limitVelocityOverLifetime; _vel.enabled = true; _vel.limit = 1.0f; _vel.dampen = 0.2f;
                // 카메라에 가까운 먼지가 화면을 덮지 않게(가까울수록 투명): 소프트 파티클 대신 최대 화면 크기 제한
                var _dr = _dustPS.GetComponent<ParticleSystemRenderer>(); _dr.maxParticleSize = 0.15f;
            }
            _emberPS = MakePS("Abyss_Embers", _emberMat, false, -0.15f, 800);
            {
                var _col = _emberPS.colorOverLifetime; _col.enabled = true;
                var _g = new Gradient();
                _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0, 1) });
                _col.color = _g;
            }
        }

        private void EmitBreak(HorusTile _t)
        {
            Vector3 _c = _t.Center;
            float _half = _grid.TileSize * 0.45f;
            var _ep = new ParticleSystem.EmitParams();
            if (_chunkPS)
            {
                for (int _i = 0; _i < _chunksPerTile; _i++)
                {
                    _ep.position = _c + new Vector3(Random.Range(-_half, _half), 0.02f, Random.Range(-_half, _half));
                    _ep.velocity = new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(2.4f, 4.4f), Random.Range(-1.4f, 1.4f));
                    _ep.startSize3D = new Vector3(Random.Range(0.2f, 0.42f), Random.Range(0.12f, 0.3f), Random.Range(0.2f, 0.42f)) / _chunkMeshSize;
                    _ep.rotation3D = new Vector3(Random.Range(0, 360f), Random.Range(0, 360f), Random.Range(0, 360f));
                    _ep.angularVelocity3D = Random.insideUnitSphere * 400f;
                    _ep.startLifetime = Random.Range(1.4f, 2.0f);
                    _ep.startColor = new Color(1f, 0.9f, 0.74f);
                    _chunkPS.Emit(_ep, 1);
                }
                for (int _i = 0; _i < _gravelPerTile; _i++)
                {
                    _ep.position = _c + new Vector3(Random.Range(-_half, _half), 0.02f, Random.Range(-_half, _half));
                    _ep.velocity = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(1.8f, 4.8f), Random.Range(-2.2f, 2.2f));
                    _ep.startSize3D = Vector3.one * Random.Range(0.06f, 0.13f) / _chunkMeshSize;
                    _ep.rotation3D = new Vector3(Random.Range(0, 360f), Random.Range(0, 360f), Random.Range(0, 360f));
                    _ep.angularVelocity3D = Random.insideUnitSphere * 700f;
                    _ep.startLifetime = Random.Range(1.0f, 1.6f);
                    _ep.startColor = new Color(0.95f, 0.84f, 0.68f);
                    _gravelPS.Emit(_ep, 1);
                }
            }
            if (_dustPS)
            {
                var _dp = new ParticleSystem.EmitParams();
                for (int _i = 0; _i < _dustPerTile; _i++)
                {
                    // 먼지: 바닥 가까이 옆으로 퍼지는 어두운 흙먼지.
                    // 조명을 받지 않는 셰이더라 밝은 색이면 밤 장면에서 흰 막처럼 떠 보이므로, 밤 톤으로 어둡고 옅고 짧게.
                    var _dir = Random.insideUnitCircle.normalized;
                    _dp.position = _c + new Vector3(Random.Range(-_half, _half), 0.08f, Random.Range(-_half, _half));
                    _dp.velocity = new Vector3(_dir.x * Random.Range(0.4f, 0.9f), Random.Range(0.05f, 0.25f), _dir.y * Random.Range(0.4f, 0.9f));
                    _dp.startSize = Random.Range(0.6f, 1.0f);
                    _dp.startLifetime = Random.Range(0.9f, 1.3f);
                    _dp.rotation = Random.Range(0, 360f);
                    _dp.startColor = _dustColor;
                    _dustPS.Emit(_dp, 1);
                }
            }
        }

        private void EmitRestoreDust(HorusTile _t)
        {
            if (!_dustPS) return;
            var _dp = new ParticleSystem.EmitParams
            {
                position = _t.Center + Vector3.up * 0.05f,
                velocity = new Vector3(Random.Range(-0.3f, 0.3f), 0.15f, Random.Range(-0.3f, 0.3f)),
                startSize = Random.Range(0.4f, 0.6f), startLifetime = 0.7f, startColor = new Color(_dustColor.r, _dustColor.g, _dustColor.b, _dustColor.a * 0.6f)
            };
            _dustPS.Emit(_dp, 1);
        }

        private void EmitEmbers(float _dt)
        {
            if (_brokenTiles == null || _brokenTiles.Count == 0 || !_emberPS) return;
            int _n = Mathf.FloorToInt(_abyssEmberRate * _dt + Random.value);
            var _ep = new ParticleSystem.EmitParams();
            for (int _i = 0; _i < _n; _i++)
            {
                HorusTile _t = _brokenTiles[Random.Range(0, _brokenTiles.Count)];
                float _half = _grid.TileSize * 0.4f;
                _ep.position = _t.Center + new Vector3(Random.Range(-_half, _half), -Random.Range(0.6f, 1.6f), Random.Range(-_half, _half));
                _ep.velocity = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(0.6f, 1.4f), Random.Range(-0.1f, 0.1f));
                _ep.startSize = Random.Range(0.05f, 0.14f);
                _ep.startLifetime = Random.Range(1.2f, 2.0f);
                _ep.startColor = _abyssEmberColor;
                _emberPS.Emit(_ep, 1);
            }
        }

        // ---------- 호루스 눈 ----------
        private void CreateEyes()
        {
            _eyeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _eyeMat.SetColor("_BaseColor", _eyeColor);
            for (int _i = 0; _i < _eyePositions.Length; _i++)
            {
                var _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _go.name = "Horus_EyeGlow_" + _i;
                Destroy(_go.GetComponent<Collider>());
                _go.transform.SetParent(transform, false);
                _go.transform.position = _eyePositions[_i];
                _go.transform.localScale = Vector3.one * _eyeSize;
                var _r = _go.GetComponent<Renderer>();
                _r.sharedMaterial = _eyeMat;
                _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _eyeRenderers.Add(_r);
                var _lgo = new GameObject("Light");
                _lgo.transform.SetParent(_go.transform, false);
                var _l = _lgo.AddComponent<Light>();
                _l.type = LightType.Point; _l.color = _eyeColor.maxColorComponent > 0 ? (_eyeColor / _eyeColor.maxColorComponent) : Color.green;
                _l.range = _eyeLightRange; _l.intensity = 0; _l.shadows = LightShadows.None;
                _eyeLights.Add(_l);
            }
        }

        private void SetEyes(float _k)
        {
            foreach (var _r in _eyeRenderers) { _r.enabled = _k > 0.01f; _r.transform.localScale = Vector3.one * _eyeSize * Mathf.Lerp(0.4f, 1f, _k); }
            foreach (var _l in _eyeLights) _l.intensity = _eyeLightIntensity * _k;
        }

        private void SetCrackEmission(List<HorusTile> _tiles, float _k)
        {
            foreach (HorusTile _t in _tiles)
            {
                _t.R.GetPropertyBlock(_mpb);
                if (_k <= 1.001f) _mpb.Clear(); else _mpb.SetColor("emissiveFactor", Color.white * (_k * EnvEmissionBoost.Factor));   // 신전 발광 배율(EnvEmissionBoost)에서 이어지게
                _t.R.SetPropertyBlock(_mpb);
            }
        }

        private void SetWarnGlow(float _a)
        {
            var _c = _warnGlowColor; _c.a = Mathf.Clamp01(_a);
            _glowMat.SetColor("_BaseColor", _c);
        }

        // ---------- 주기 ----------
        private async UniTaskVoid CycleAsync(CancellationToken _token)
        {
            _phaseName = "wait"; _phaseEnd = Time.time + _firstDelay;
            while (Time.time < _phaseEnd) if (await NextFrame(_token)) return;
            while (true)
            {
                float _cycleStart = Time.time;
                char _type;
                do { _type = _types[Random.Range(0, _types.Count)]; } while (_types.Count > 1 && _type == _lastType);
                _lastType = _currentType = _type;
                var _tiles = _byType[_type];
                _doomedCells.Clear();
                foreach (HorusTile _t in _tiles) _doomedCells.Add(_t.Cell);

                // 1) 경고: 눈이 켜지고, 해당 타일 전체가 은은하게(점점 빠르게) 빛남
                _phaseName = "warn"; _phaseEnd = Time.time + _warnTime;
                foreach (HorusTile _t in _tiles) GlowOf(_t).SetActive(true);
                for (float _t = 0; _t < _warnTime; _t += Time.deltaTime)
                {
                    float _u = _t / _warnTime;
                    SetEyes(Mathf.SmoothStep(0, 1, _u * 1.5f));
                    float _pulse = 0.55f + 0.45f * Mathf.Sin(_t * Mathf.Lerp(5f, 18f, _u));
                    SetWarnGlow(_warnGlowMax * Mathf.Lerp(0.35f, 1f, _u) * _pulse);
                    SetCrackEmission(_tiles, 1f + (_warnCrackEmission - 1f) * _pulse * _u);
                    if (await NextFrame(_token)) return;
                }
                SetEyes(1f);

                // 2) 붕괴: 사암 파편·자갈·먼지가 튀고, 타일은 빠르게 꺼지며 심연이 드러남
                foreach (HorusTile _t in _tiles) { _grid.SetHole(_t.Cell, true); SetTileCollider(_t, false); _t.Glow.SetActive(false); PitOf(_t).SetActive(true); EmitBreak(_t); }
                SetWarnGlow(0); SetCrackEmission(_tiles, 1f);
                _brokenTiles = _tiles;
                _phaseName = "broken"; _phaseEnd = Time.time + _brokenTime;
                for (float _t = 0; _t < _breakAnimTime; _t += Time.deltaTime)
                {
                    float _u = _t / _breakAnimTime;
                    foreach (HorusTile _tl in _tiles)
                    {
                        _tl.Mesh.localPosition = _tl.BasePos + Vector3.down * _sinkDepth * _u * _u / Mathf.Max(_tl.Mesh.parent.lossyScale.y, 1e-4f);
                        _tl.Mesh.localScale = _tl.BaseScale * (1f - _u * 0.6f);
                    }
                    if (await NextFrame(_token)) return;
                }
                foreach (HorusTile _tl in _tiles) _tl.R.enabled = false;

                // 3) 사라진 채 유지: 심연에서 불씨가 올라옴
                while (Time.time < _phaseEnd) { SetEyes(0.75f + 0.25f * Mathf.Sin(Time.time * 4f)); EmitEmbers(Time.deltaTime); if (await NextFrame(_token)) return; }

                // 4) 수복: 아래에서 올라오며 커짐, 끝나면 구멍 해제. 눈은 꺼짐
                _phaseName = "restore"; _phaseEnd = Time.time + _restoreAnimTime;
                _brokenTiles = null;
                foreach (HorusTile _tl in _tiles) { _tl.R.enabled = true; EmitRestoreDust(_tl); }
                for (float _t = 0; _t < _restoreAnimTime; _t += Time.deltaTime)
                {
                    float _u = _t / _restoreAnimTime;
                    float _e = 1f - (1f - _u) * (1f - _u);
                    foreach (HorusTile _tl in _tiles)
                    {
                        _tl.Mesh.localPosition = _tl.BasePos + Vector3.down * _sinkDepth * (1f - _e) / Mathf.Max(_tl.Mesh.parent.lossyScale.y, 1e-4f);
                        _tl.Mesh.localScale = _tl.BaseScale * _e;
                    }
                    SetEyes(1f - _u);
                    if (await NextFrame(_token)) return;
                }
                foreach (HorusTile _tl in _tiles) { _tl.Mesh.localPosition = _tl.BasePos; _tl.Mesh.localScale = _tl.BaseScale; _grid.SetHole(_tl.Cell, false); SetTileCollider(_tl, true); if (_tl.Pit) _tl.Pit.SetActive(false); }
                SetEyes(0f);

                _phaseName = "wait"; _currentType = ' ';
                _doomedCells.Clear();
                _phaseEnd = _cycleStart + _period;
                while (Time.time < _phaseEnd) if (await NextFrame(_token)) return;
            }
        }

        private void Update()
        {
            float _left = Mathf.Max(0, _phaseEnd - Time.time);
            switch (_phaseName)
            {
                case "warn": Status = "Horus: tile " + _currentType + " breaks in " + _left.ToString("F1") + "s"; break;
                case "broken": Status = "Horus: tile " + _currentType + " broken, " + _left.ToString("F1") + "s left"; break;
                case "restore": Status = "Horus: tile " + _currentType + " restoring"; break;
                default: Status = "Horus: next in " + _left.ToString("F1") + "s"; break;
            }
        }

        private void OnGUI()
        {
            if (HistoryCrash.UI.PauseSettingsUI.IsOpen) return;   // 설정창이 열리면 OnGUI 글자는 그리지 않음(항상 UI 위에 그려지므로)
            GUI.Box(new Rect(10, HistoryCrash.UI.DebugHudLayout.Top + 198, 360, 26), "");
            GUI.Label(new Rect(20, HistoryCrash.UI.DebugHudLayout.Top + 202, 340, 20), Status);
        }

        /// <summary>테스트용: 다음 붕괴까지 남은 대기 시간을 바로 끝낸다.</summary>
        public void SkipWait() { if (_phaseName == "wait") _phaseEnd = Time.time; }
        public string Phase { get { return _phaseName; } }

        // 이번 주기에 경고 중·사라짐·수복 중인 칸(아누비스 도착 칸 후보에서 제외)
        private readonly HashSet<Vector2Int> _doomedCells = new HashSet<Vector2Int>();
        /// <summary>호루스 붕괴로 곧 사라지거나 사라져 있는 칸이면 true</summary>
        public bool IsDoomed(Vector2Int _c) { return _doomedCells.Contains(_c); }
        /// <summary>다음 경고가 시작되기까지 남은 시간(대기 중이 아니면 0)</summary>
        public float TimeToNextWarn { get { return _phaseName == "wait" ? Mathf.Max(0f, _phaseEnd - Time.time) : 0f; } }
        public char CurrentType { get { return _currentType; } }
    }
}
