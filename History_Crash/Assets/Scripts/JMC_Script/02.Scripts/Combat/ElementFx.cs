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
    /// 속성 색 파티클·발광 재질을 코드로 만든다(별도 에셋 없이).
    /// - Flame: 소환 시 바닥에서 솟는 불꽃
    /// - Burst: 짧은 섬광·불티(피격, 영혼 도착)
    /// - Glow: 오브·공격체·영혼 몸체용 발광 재질
    /// </summary>
    public static class ElementFx
    {
        private static Texture2D _softTex;
        private static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        private static Texture2D SoftTex()
        {
            if (_softTex) return _softTex;
            const int N = 64;
            _softTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "ElementFx_Soft" };
            float _c = (N - 1) * 0.5f;
            for (int _y = 0; _y < N; _y++)
                for (int _x = 0; _x < N; _x++)
                {
                    float _d = Mathf.Sqrt((_x - _c) * (_x - _c) + (_y - _c) * (_y - _c)) / _c;
                    float _a = Mathf.Clamp01(1f - _d); _a = _a * _a * (3f - 2f * _a);
                    _softTex.SetPixel(_x, _y, new Color(1, 1, 1, _a));
                }
            _softTex.Apply();
            return _softTex;
        }

        /// <summary>가산 합성 파티클 재질(색은 파티클 시작색으로 준다)</summary>
        public static Material ParticleMat()
        {
            const string KEY = "particle_add";
            if (_mats.TryGetValue(KEY, out var _m) && _m) return _m;
            _m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "ElementFx_Additive" };
            _m.SetTexture("_BaseMap", SoftTex());
            _m.SetColor("_BaseColor", Color.white);
            _m.SetFloat("_Surface", 1f);
            _m.SetFloat("_Blend", 2f);
            _m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _m.SetFloat("_ZWrite", 0f);
            _m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _mats[KEY] = _m;
            return _m;
        }

        /// <summary>속성색으로 스스로 빛나는 불투명 재질(오브·공격체 몸체)</summary>
        public static Material GlowMat(Element _e, float _intensity = 4f)
        {
            string _key = "glow_" + _e + "_" + _intensity;
            if (_mats.TryGetValue(_key, out var _m) && _m) return _m;
            _m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ElementFx_Glow_" + _e };
            Color _c = ElementInfo.Color(_e);
            _m.SetColor("_BaseColor", Color.Lerp(_c, Color.white, 0.35f));
            _m.EnableKeyword("_EMISSION");
            _m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _m.SetColor("_EmissionColor", _c * _intensity);
            _m.SetFloat("_Smoothness", 0.8f);
            _mats[_key] = _m;
            return _m;
        }

        /// <summary>속성 오브용 보석 재질: 어두운 속성색 몸체 + 약한 발광(무대 분위기를 해치지 않게)</summary>
        public static Material OrbMat(Element _e)
        {
            string _key = "orb_" + _e;
            if (_mats.TryGetValue(_key, out var _m) && _m) return _m;
            _m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ElementFx_Orb_" + _e };
            Color _c = ElementInfo.Color(_e);
            _m.SetColor("_BaseColor", Color.Lerp(_c, new Color(0.08f, 0.07f, 0.06f), 0.55f));
            _m.EnableKeyword("_EMISSION");
            _m.SetColor("_EmissionColor", _c * 0.45f);
            _m.SetFloat("_Smoothness", 0.85f);
            _m.SetFloat("_Metallic", 0.35f);
            _mats[_key] = _m;
            return _m;
        }

        private static ParticleSystem Make(string _name, Transform _parent, Vector3 _pos, int _max)
        {
            var _go = new GameObject(_name);
            if (_parent) _go.transform.SetParent(_parent, false);
            _go.transform.position = _pos;
            var _ps = _go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var _main = _ps.main;
            _main.maxParticles = _max;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.playOnAwake = false;
            var _r = _go.GetComponent<ParticleSystemRenderer>();
            _r.sharedMaterial = ParticleMat();
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return _ps;
        }

        /// <summary>바닥에서 솟는 속성 불꽃. duration 동안 타오른 뒤 스스로 사라진다.</summary>
        public static ParticleSystem Flame(Element _e, Vector3 _floorPos, float _duration, float _radius = 0.45f)
        {
            Color _c = ElementInfo.Color(_e);
            var _ps = Make("Fx_SummonFlame_" + _e, null, _floorPos, 300);
            var _main = _ps.main;
            _main.duration = _duration;
            _main.loop = false;
            _main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            _main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            _main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            _main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_c, Color.white, 0.4f), _c);
            _main.gravityModifier = -0.15f;
            var _em = _ps.emission; _em.rateOverTime = 160f;
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Circle; _sh.radius = _radius; _sh.rotation = new Vector3(-90, 0, 0);
            var _sz = _ps.sizeOverLifetime; _sz.enabled = true; _sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var _col = _ps.colorOverLifetime; _col.enabled = true;
            var _g = new Gradient();
            _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            _col.color = _g;
            var _noise = _ps.noise; _noise.enabled = true; _noise.strength = 0.6f; _noise.frequency = 1.5f;
            _ps.Play();
            Object.Destroy(_ps.gameObject, _duration + 1.2f);
            return _ps;
        }

        /// <summary>짧은 섬광 + 불티</summary>
        public static void Burst(Element _e, Vector3 _pos, float _scale = 1f, int _count = 24) { Burst(ElementInfo.Color(_e), _pos, _scale, _count, _e.ToString()); }

        /// <summary>바닥에 박힐 때 옆으로 퍼지는 흙먼지 고리 + 위로 튀는 파편</summary>
        public static void Impact(Vector3 _floorPos, Color _spark, Color _dust, float _scale = 1f)
        {
            var _ring = Make("Fx_ImpactDust", null, _floorPos + Vector3.up * 0.03f, 40);
            var _m = _ring.main; _m.duration = 0.1f; _m.loop = false;
            _m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            _m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * _scale, 2.4f * _scale);
            _m.startSize = new ParticleSystem.MinMaxCurve(0.25f * _scale, 0.5f * _scale);
            _m.startColor = _dust; _m.gravityModifier = 0.05f;
            var _em = _ring.emission; _em.rateOverTime = 0; _em.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var _sh = _ring.shape; _sh.shapeType = ParticleSystemShapeType.Circle; _sh.radius = 0.08f; _sh.radiusThickness = 0f; _sh.rotation = new Vector3(-90, 0, 0);
            var _vl = _ring.velocityOverLifetime; _vl.enabled = true; _vl.space = ParticleSystemSimulationSpace.World; _vl.x = new ParticleSystem.MinMaxCurve(0f, 0f); _vl.y = new ParticleSystem.MinMaxCurve(0.2f, 0.5f); _vl.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // 세 축 모두 같은 모드(TwoConstants)여야 함
            var _limit = _ring.limitVelocityOverLifetime; _limit.enabled = true; _limit.limit = 0.3f; _limit.dampen = 0.15f;
            var _sz = _ring.sizeOverLifetime; _sz.enabled = true; _sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.4f));
            var _col = _ring.colorOverLifetime; _col.enabled = true;
            var _g = new Gradient(); _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            _col.color = _g;
            _ring.GetComponent<ParticleSystemRenderer>().sharedMaterial = AlphaMat();
            _ring.Play();
            Object.Destroy(_ring.gameObject, 1.5f);
            Burst(_spark, _floorPos + Vector3.up * 0.05f, _scale, 26, "Impact");
        }

        /// <summary>알파 합성(먼지용)</summary>
        public static Material AlphaMat()
        {
            const string KEY = "particle_alpha";
            if (_mats.TryGetValue(KEY, out var _m) && _m) return _m;
            _m = new Material(ParticleMat()) { name = "ElementFx_Alpha" };
            _m.SetFloat("_Blend", 0f);
            _m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mats[KEY] = _m;
            return _m;
        }

        public static void Burst(Color _c, Vector3 _pos, float _scale, int _count, string _tag)
        {
            var _ps = Make("Fx_Burst_" + _tag, null, _pos, _count + 4);
            var _main = _ps.main;
            _main.duration = 0.2f; _main.loop = false;
            _main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            _main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * _scale, 4.5f * _scale);
            _main.startSize = new ParticleSystem.MinMaxCurve(0.06f * _scale, 0.18f * _scale);
            _main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_c, Color.white, 0.5f), _c);
            _main.gravityModifier = 0.4f;
            var _em = _ps.emission; _em.rateOverTime = 0; _em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)_count) });
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Sphere; _sh.radius = 0.1f * _scale;
            var _sz = _ps.sizeOverLifetime; _sz.enabled = true; _sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            _ps.Play();
            // 섬광 한 번
            var _fl = Make("Fx_Flash_" + _tag, _ps.transform, _pos, 2);
            var _fm = _fl.main; _fm.duration = 0.1f; _fm.loop = false; _fm.startLifetime = 0.18f; _fm.startSpeed = 0; _fm.startSize = 1.1f * _scale;
            _fm.startColor = Color.Lerp(_c, Color.white, 0.3f);
            var _fe = _fl.emission; _fe.rateOverTime = 0; _fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var _fs = _fl.shape; _fs.enabled = false;
            var _fz = _fl.sizeOverLifetime; _fz.enabled = true; _fz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.2f));
            var _fc = _fl.colorOverLifetime; _fc.enabled = true;
            var _g = new Gradient(); _g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            _fc.color = _g;
            _fl.Play();
            Object.Destroy(_ps.gameObject, 1.2f);
        }

        /// <summary>움직이는 물체 뒤로 남는 꼬리 파티클(부모를 따라감). 반환값을 부모에 붙여 쓴다.</summary>
        public static ParticleSystem Trail(Element _e, Transform _parent, float _size = 0.18f, float _rate = 60f)
        {
            Color _c = ElementInfo.Color(_e);
            var _ps = Make("Fx_Trail_" + _e, _parent, _parent.position, 200);
            var _main = _ps.main;
            _main.loop = true;
            _main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            _main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
            _main.startSize = new ParticleSystem.MinMaxCurve(_size * 0.6f, _size);
            _main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_c, Color.white, 0.4f), _c);
            var _em = _ps.emission; _em.rateOverTime = _rate;
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Sphere; _sh.radius = _size * 0.3f;
            var _sz = _ps.sizeOverLifetime; _sz.enabled = true; _sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            _ps.Play();
            return _ps;
        }

        /// <summary>속성색 점광원</summary>
        public static Light Glow(Element _e, Transform _parent, float _intensity = 2f, float _range = 2.5f)
        {
            var _go = new GameObject("Fx_Light_" + _e);
            _go.transform.SetParent(_parent, false);
            var _l = _go.AddComponent<Light>();
            _l.type = LightType.Point; _l.color = ElementInfo.Color(_e); _l.intensity = _intensity; _l.range = _range; _l.shadows = LightShadows.None;
            return _l;
        }
    }
}
