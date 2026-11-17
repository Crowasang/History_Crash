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
    /// 패턴 투사체가 바닥·벽·목표에 닿아 깨지는 이펙트(A 코페쉬, C 보석, E 지팡이).
    /// - 파편: 투사체 자신의 재질로 그린 조각 메시. 튀었다가 바닥에 부딪혀 구르고, 끝에서 작아지며 사라진다.
    /// - 불꽃·섬광: 패턴 발광색(가산 합성). 점처럼 찍히지 않게 짧게 늘어진 불꽃 + 부드러운 섬광 한 번.
    /// - 먼지: 밤 장면에서 흰 막이 되지 않게 어둡고 옅게, 바닥 가까이.
    /// </summary>
    public class PatternImpactFx
    {
        private static Texture2D _dotTex;
        private readonly Transform _root, _floorPlane;
        private readonly ParticleSystem _frag, _spark, _flash, _dust;

        /// <param name="_fragMat">파편 재질(투사체 재질). null이면 파편 없음</param>
        /// <param name="_fragMeshes">파편 메시(La_Sun 파편 메시 재사용)</param>
        /// <param name="_glow">불꽃·섬광 색(HDR)</param>
        public PatternImpactFx(string _name, Transform _parent, Material _fragMat, Mesh[] _fragMeshes, Color _glow, float _floorY)
        {
            EnsureTex();
            _root = new GameObject(_name).transform;
            _root.SetParent(_parent, false);
            _floorPlane = new GameObject("FloorPlane").transform;
            _floorPlane.SetParent(_root, false);
            _floorPlane.position = new Vector3(0, _floorY, 0);

            if (_fragMat && _fragMeshes != null && _fragMeshes.Length > 0)
            {
                _frag = Make("Fragments", 48);
                var _m = _frag.main;
                _m.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 1.9f);
                _m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
                _m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
                _m.startRotation3D = true;
                _m.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); _m.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); _m.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                _m.gravityModifier = 1.6f;
                var _rot = _frag.rotationOverLifetime; _rot.enabled = true; _rot.separateAxes = true;
                _rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f); _rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f); _rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                var _sol = _frag.sizeOverLifetime; _sol.enabled = true;
                _sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.7f, 1), new Keyframe(1, 0)));
                var _col = _frag.collision; _col.enabled = true; _col.type = ParticleSystemCollisionType.Planes;
                _col.SetPlane(0, _floorPlane); _col.bounce = 0.3f; _col.dampen = 0.45f; _col.lifetimeLoss = 0f; _col.radiusScale = 0.5f;
                var _r = _frag.GetComponent<ParticleSystemRenderer>();
                _r.renderMode = ParticleSystemRenderMode.Mesh;
                _r.SetMeshes(_fragMeshes);
                _r.sharedMaterial = _fragMat;
                _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                _r.alignment = ParticleSystemRenderSpace.Local;
            }

            _spark = Make("Sparks", 64);
            {
                var _m = _spark.main;
                _m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                _m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7.5f);
                _m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                _m.startColor = Color.white;   // 파티클 색은 8비트라 HDR 밝기는 재질 색(glow)으로 준다
                _m.gravityModifier = 0.7f;
                var _sol = _spark.sizeOverLifetime; _sol.enabled = true;
                _sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
                var _col = _spark.collision; _col.enabled = true; _col.type = ParticleSystemCollisionType.Planes;
                _col.SetPlane(0, _floorPlane); _col.bounce = 0.4f; _col.dampen = 0.3f;
                var _r = _spark.GetComponent<ParticleSystemRenderer>();
                _r.renderMode = ParticleSystemRenderMode.Stretch; _r.velocityScale = 0.06f; _r.lengthScale = 1.5f;
                _r.sharedMaterial = MakeMat(true, _glow);
            }

            _flash = Make("Flash", 4);
            {
                var _m = _flash.main;
                _m.startLifetime = 0.18f; _m.startSpeed = 0f; _m.startSize = 1.6f; _m.startColor = Color.white;
                var _sol = _flash.sizeOverLifetime; _sol.enabled = true;
                _sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.2f));
                var _c = _flash.colorOverLifetime; _c.enabled = true;
                var _g = new Gradient(); _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                _c.color = _g;
                _flash.GetComponent<ParticleSystemRenderer>().sharedMaterial = MakeMat(true, _glow * 0.6f);
            }

            _dust = Make("Dust", 16);
            {
                var _m = _dust.main;
                _m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                _m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
                _m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                _m.startColor = new Color(0.20f, 0.18f, 0.15f, 0.30f);
                _m.gravityModifier = -0.05f;
                var _sol = _dust.sizeOverLifetime; _sol.enabled = true;
                _sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.4f));
                var _c = _dust.colorOverLifetime; _c.enabled = true;
                var _g = new Gradient(); _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
                _c.color = _g;
                var _r = _dust.GetComponent<ParticleSystemRenderer>();
                _r.sharedMaterial = MakeMat(false, Color.white); _r.maxParticleSize = 0.15f;
            }
        }

        /// <summary>pos에서 normal 방향(바닥이면 위, 벽이면 벽에서 튀어나오는 쪽)으로 깨진다.</summary>
        public void Burst(Vector3 _pos, Vector3 _normal, float _scale = 1f, int _fragments = 10)
        {
            _root.position = _pos;
            _root.rotation = Quaternion.FromToRotation(Vector3.up, _normal.sqrMagnitude > 1e-4f ? _normal.normalized : Vector3.up);
            // 바닥 충돌 평면은 월드 바닥 높이 그대로 유지
            var _fp = _floorPlane.position; _floorPlane.position = new Vector3(_pos.x, _fp.y, _pos.z); _floorPlane.rotation = Quaternion.identity;
            if (_frag) _frag.Emit(_fragments);
            _spark.Emit(Mathf.RoundToInt(22 * _scale));
            _flash.Emit(1);
            _dust.Emit(Mathf.RoundToInt(4 * _scale));
        }

        private ParticleSystem Make(string _n, int _max)
        {
            var _go = new GameObject(_n);
            _go.transform.SetParent(_root, false);
            var _ps = _go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var _m = _ps.main; _m.playOnAwake = false; _m.loop = false; _m.maxParticles = _max;
            _m.simulationSpace = ParticleSystemSimulationSpace.World; _m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var _em = _ps.emission; _em.enabled = false;
            var _sh = _ps.shape; _sh.enabled = true; _sh.shapeType = ParticleSystemShapeType.Hemisphere; _sh.radius = 0.12f;
            _sh.rotation = new Vector3(-90f, 0f, 0f);   // 반구가 로컬 +Y(normal) 쪽을 향하게
            return _ps;
        }

        private static Material MakeMat(bool _additive, Color _color)
        {
            var _m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            _m.SetTexture("_BaseMap", _dotTex);
            _m.SetColor("_BaseColor", _color);
            _m.SetFloat("_Surface", 1f);
            _m.SetFloat("_Blend", _additive ? 2f : 0f);
            _m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _m.SetFloat("_DstBlend", (float)(_additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            _m.SetFloat("_ZWrite", 0f);
            _m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _m;
        }

        private static void EnsureTex()
        {
            if (_dotTex) return;
            const int N = 64;
            _dotTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float _c = (N - 1) * 0.5f;
            for (int _y = 0; _y < N; _y++)
                for (int _x = 0; _x < N; _x++)
                {
                    float _d = Vector2.Distance(new Vector2(_x, _y), new Vector2(_c, _c)) / _c;
                    _dotTex.SetPixel(_x, _y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - _d), 2.2f)));
                }
            _dotTex.Apply();
        }
    }
}
