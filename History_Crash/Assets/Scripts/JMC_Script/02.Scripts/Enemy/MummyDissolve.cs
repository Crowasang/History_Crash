using System.Collections.Generic;
using UnityEngine;

namespace HistoryCrash.Enemy
{
    /// <summary>
    /// 쓰러진 미라의 지금 자세를 조각 낸 메시로 바꿔, 손발 끝부터 떨어져 구슬로 빨려 들게 한다(움직임은 MummyDissolve 셰이더).
    /// 조각 나누기는 모델마다 한 번만 계산해 같이 쓴다. 모델 FBX의 Read/Write가 켜져 있어야 한다.
    /// </summary>
    public class MummyDissolve : MonoBehaviour
    {
        private const int PIECES = 300;

        private class Cut { public int[] Src, Piece, Tris; }   // 조각으로 나눈 정점: 원래 정점 번호·조각 번호, 삼각형
        private static readonly Dictionary<UnityEngine.Mesh, Cut> s_cuts = new Dictionary<UnityEngine.Mesh, Cut>();
        private static Shader s_shader;

        private UnityEngine.Mesh _mesh; private Material _mat; private float _t, _time;

        /// <summary>조각 나누기를 미리 계산(소환될 때). 죽는 순간에 끊기지 않게</summary>
        public static void Prepare(SkinnedMeshRenderer _smr) { if (_smr) GetCut(_smr.sharedMesh); }

        private static Cut GetCut(UnityEngine.Mesh _m)
        {
            if (s_cuts.TryGetValue(_m, out var _cut)) return _cut;
            if (!_m.isReadable) { Debug.LogWarning("[MummyDissolve] 모델의 Read/Write를 켜야 합니다: " + _m.name); return null; }
            Vector3[] _v = _m.vertices; int[] _t = _m.GetTriangles(0);
            int _n = _t.Length / 3;
            var _c = new Vector3[_n]; var _best = new float[_n]; var _own = new int[_n];
            for (int _i = 0; _i < _n; _i++) { _c[_i] = (_v[_t[_i * 3]] + _v[_t[_i * 3 + 1]] + _v[_t[_i * 3 + 2]]) / 3f; _best[_i] = float.MaxValue; }
            // 고르게 퍼진 씨앗(가장 먼 곳부터)마다 가장 가까운 면을 묶음
            for (int _s = 0, _seed = 0; _s < PIECES; _s++)
            {
                Vector3 _p = _c[_seed]; int _far = 0;
                for (int _i = 0; _i < _n; _i++)
                {
                    float _d = (_c[_i] - _p).sqrMagnitude;
                    if (_d < _best[_i]) { _best[_i] = _d; _own[_i] = _s; }
                    if (_best[_i] > _best[_far]) _far = _i;
                }
                _seed = _far;
            }
            var _map = new Dictionary<long, int>(); var _src = new List<int>(); var _piece = new List<int>(); var _tris = new int[_t.Length];
            for (int _i = 0; _i < _t.Length; _i++)
            {
                long _key = (long)_own[_i / 3] * _v.Length + _t[_i];
                if (!_map.TryGetValue(_key, out int _id)) { _id = _src.Count; _map[_key] = _id; _src.Add(_t[_i]); _piece.Add(_own[_i / 3]); }
                _tris[_i] = _id;
            }
            return s_cuts[_m] = new Cut { Src = _src.ToArray(), Piece = _piece.ToArray(), Tris = _tris };
        }

        /// <summary>지금 자세로 조각 시신을 만들어 바로 시작한다. 만들 수 없으면 null(원래 시신은 부르는 쪽에서 숨긴다)</summary>
        public static MummyDissolve Create(SkinnedMeshRenderer _smr, Vector3 _soul, Color _element, Color _outline, float _outlineWidth, float _time)
        {
            if (!s_shader) s_shader = Resources.Load<Shader>("Mummy/MummyDissolve");
            Cut _cut = _smr && s_shader ? GetCut(_smr.sharedMesh) : null;
            if (_cut == null) return null;

            var _baked = new UnityEngine.Mesh(); _smr.BakeMesh(_baked, true);
            Vector3[] _bv = _baked.vertices, _bn = _baked.normals; Vector4[] _bt = _baked.tangents; Vector2[] _buv = _baked.uv;
            Destroy(_baked);
            Matrix4x4 _mx = _smr.transform.localToWorldMatrix;
            int _n = _cut.Src.Length;
            var _v = new Vector3[_n]; var _nr = new Vector3[_n]; var _tg = new Vector4[_n]; var _uv = new Vector2[_n];
            var _cen = new Vector3[PIECES]; var _cnt = new int[PIECES];
            for (int _i = 0; _i < _n; _i++)
            {
                int _s = _cut.Src[_i];
                _v[_i] = _mx.MultiplyPoint3x4(_bv[_s]); _nr[_i] = _mx.MultiplyVector(_bn[_s]).normalized; _uv[_i] = _buv[_s];
                Vector3 _t3 = _mx.MultiplyVector(_bt[_s]).normalized; _tg[_i] = new Vector4(_t3.x, _t3.y, _t3.z, _bt[_s].w);
                _cen[_cut.Piece[_i]] += _v[_i]; _cnt[_cut.Piece[_i]]++;
            }
            // 손발 끝·머리에서 가슴으로 차례로 떠남
            Vector3 _chest = _smr.bounds.center;
            foreach (var _b in _smr.bones) if (_b && _b.name.Contains("Spine2")) { _chest = _b.position; break; }
            float _far = 0.01f;
            for (int _p = 0; _p < PIECES; _p++) if (_cnt[_p] > 0) { _cen[_p] /= _cnt[_p]; _far = Mathf.Max(_far, (_cen[_p] - _chest).magnitude); }
            var _cd = new Vector4[PIECES]; var _sp = new Vector4[PIECES]; var _lf = new Vector2[PIECES];
            for (int _p = 0; _p < PIECES; _p++)
            {
                float _x = Mathf.Pow((_cen[_p] - _chest).magnitude / _far, 0.8f);
                float _delay = _time * (0.02f + 0.6f * Mathf.Clamp01((1f - _x) * 0.8f + Random.value * 0.2f));
                Vector3 _ax = Random.onUnitSphere;
                _cd[_p] = new Vector4(_cen[_p].x, _cen[_p].y, _cen[_p].z, _delay);
                _sp[_p] = new Vector4(_ax.x, _ax.y, _ax.z, Random.Range(2f, 4.5f) * (Random.value < 0.5f ? -1f : 1f));
                _lf[_p] = new Vector2(_time * Random.Range(0.3f, 0.37f), Random.Range(0.22f, 0.55f));
            }
            var _u1 = new Vector4[_n]; var _u2 = new Vector4[_n]; var _u3 = new Vector2[_n];
            for (int _i = 0; _i < _n; _i++) { int _p = _cut.Piece[_i]; _u1[_i] = _cd[_p]; _u2[_i] = _sp[_p]; _u3[_i] = _lf[_p]; }

            var _go = new GameObject("Mummy_Pieces", typeof(MeshFilter), typeof(MeshRenderer));
            var _d = _go.AddComponent<MummyDissolve>();
            var _mesh = new UnityEngine.Mesh { name = "Mummy_Pieces", vertices = _v, normals = _nr, tangents = _tg, uv = _uv };
            _mesh.SetUVs(1, _u1); _mesh.SetUVs(2, _u2); _mesh.SetUVs(3, _u3);
            _mesh.SetTriangles(_cut.Tris, 0);
            var _bd = _mesh.bounds; _bd.Encapsulate(_soul); _bd.Expand(0.6f); _mesh.bounds = _bd;

            Material _src = _smr.sharedMaterial;
            var _mat = new Material(s_shader);
            _mat.SetTexture("_BaseMap", _src.GetTexture("_BaseMap")); _mat.SetTexture("_BumpMap", _src.GetTexture("_BumpMap"));
            _mat.SetColor("_BaseColor", _src.GetColor("_BaseColor")); _mat.SetFloat("_Smoothness", _src.GetFloat("_Smoothness"));
            _mat.SetColor("_Element", _element); _mat.SetColor("_Pale", Color.Lerp(_element, Color.white, 0.45f));
            _mat.SetColor("_OutlineColor", _outline); _mat.SetFloat("_OutlineWidth", _outlineWidth); _mat.SetVector("_Soul", _soul);
            _go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var _r = _go.GetComponent<MeshRenderer>(); _r.sharedMaterial = _mat; _r.renderingLayerMask = _smr.renderingLayerMask;
            _d._mesh = _mesh; _d._mat = _mat; _d._time = _time;
            return _d;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            _mat.SetFloat("_T", _t);
            if (_t > _time + 0.05f) Destroy(gameObject);
        }

        private void OnDestroy() { if (_mesh) Destroy(_mesh); if (_mat) Destroy(_mat); }
    }
}
