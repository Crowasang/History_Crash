using System.Collections.Generic;
using UnityEngine;

namespace HistoryCrash.Combat
{
    /// <summary>
    /// 돌 끌 선으로 새긴 이집트 마법진 텍스처를 바닥에 눕힌 사각 판으로 그린다(가산 발광).
    /// 텍스처는 Assets/Art/Resources/MagicCircle/ 에서 이름으로 읽는다.
    /// 텍스처가 없으면 Load가 null을 돌려주고, 부르는 쪽은 기존 선(LineRenderer) 마법진을 그대로 쓴다.
    /// </summary>
    public class MagicCircleDecal
    {
        public const string FOLDER = "MagicCircle/";
        public const string ANUBIS = "MagicCircle_Anubis";        // 아누비스 강제이동 도착
        public const string PHARAOH_D = "MagicCircle_Pharaoh_D";  // 보스 파라오 D 패턴 예고
        public const string PLAYER_D = "MagicCircle_PlayerD";     // 플레이어 D 스태프 스킬
        /// <summary>텍스처 반지름 대비 바깥 고리 반지름(1024px 중 바깥 고리 500px)</summary>
        public const float OUTER_RATIO = 500f / 512f;

        private static Mesh _quad;
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        private readonly Transform _root;
        private readonly Transform _spin;
        private readonly Material _mat;
        private readonly float _diameter;

        public Transform Root => _root;

        /// <summary>마법진 텍스처를 읽는다(한 번 읽으면 기억). 없으면 null.</summary>
        public static Texture2D Load(string _name)
        {
            if (_cache.TryGetValue(_name, out var _t)) return _t;
            _t = Resources.Load<Texture2D>(FOLDER + _name);
            _cache[_name] = _t;
            return _t;
        }

        /// <param name="_diameter">텍스처 한 변의 길이(m)</param>
        public MagicCircleDecal(string _name, Texture2D _tex, Transform _parent, float _diameter)
        {
            EnsureQuad();
            this._diameter = _diameter;
            _root = new GameObject(_name).transform;
            _root.SetParent(_parent, false);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_root, false);
            _spin.localPosition = Vector3.up * 0.03f;

            _mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = _name + "_Mat" };
            _mat.SetTexture("_BaseMap", _tex);
            _mat.SetColor("_BaseColor", Color.clear);
            _mat.SetFloat("_Surface", 1f); _mat.SetFloat("_Blend", 2f);
            _mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _mat.SetFloat("_ZWrite", 0f);
            _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            _spin.gameObject.AddComponent<MeshFilter>().sharedMesh = _quad;
            var _mr = _spin.gameObject.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = _mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            Set(Color.white, 0f, 0f);
        }

        /// <param name="_color">발광색(rgb, HDR 가능)</param>
        /// <param name="_alpha">0~1 보이는 정도</param>
        /// <param name="_angle">바닥 위 회전(도)</param>
        /// <param name="_scale">크기 배율(1 = 처음 지름)</param>
        public void Set(Color _color, float _alpha, float _angle, float _scale = 1f)
        {
            var _c = _color; _c.a = Mathf.Clamp01(_alpha);
            _mat.SetColor("_BaseColor", _c);
            _spin.localRotation = Quaternion.Euler(0f, _angle, 0f);
            _spin.localScale = new Vector3(_diameter * _scale, 1f, _diameter * _scale);
        }

        private static void EnsureQuad()
        {
            if (_quad != null) return;
            _quad = new Mesh { name = "MagicCircleQuad" };
            _quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            _quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.RecalculateNormals(); _quad.RecalculateBounds();
        }
    }
}
