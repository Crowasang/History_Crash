using System.Collections;
using UnityEngine;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Enemy
{
    /// <summary>머미가 떨어뜨리는 속성 오브. 바닥 위에 떠서 빛나며 E키로 줍는다.</summary>
    public static class ElementOrb
    {
        /// <summary>바닥 오브 지름(m)</summary>
        public const float ORB_SIZE = 0.26f;

        /// <summary>항상 카메라를 보는 빛 판 이름(OrbMotion이 돌려 준다)</summary>
        public const string BILLBOARD_PREFIX = "Bb_";
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

        public static GameObject Spawn(Element _e, Vector2Int _cell, TileGrid _grid, float _lifetime)
        {
            Vector3 _c = _grid.CellCenter(_cell, _grid.FloorTopY);
            var _root = new GameObject("Orb_" + ElementInfo.Name(_e));
            _root.transform.position = _c;
            var _item = _root.AddComponent<PickupItem>();
            _item.Kind = PickupKind.Orb; _item.Value = (int)_e; _item.Cell = _cell; _item.Ready = true;
            // 단단한 몸체 없이 빛으로만 이루어진 에너지 코어(광택 있는 구는 탱탱볼처럼 보여 쓰지 않음)
            GameObject _ball = MakeEnergyCore(_e);
            _ball.name = "Ball";
            _ball.transform.SetParent(_root.transform, false);
            _ball.transform.localScale = Vector3.one * ORB_SIZE;
            _ball.transform.localPosition = new Vector3(0, 0.28f, 0);
            ElementFx.Burst(_e, _ball.transform.position, 0.6f, 12);
            _root.AddComponent<OrbMotion>().Lifetime = _lifetime;
            return _root;
        }

        // ---------- 에너지 코어: 하얗게 달아오른 핵 + 속성색 몸통 빛 + 바깥 빛무리 + 도는 빛 줄기. 크기 단위는 Ball 기준(1 = ORB_SIZE) ----------
        /// <summary>에너지 코어 오브 몸체를 만든다(바닥 오브와 스태프 소켓 오브가 함께 씀). 빛 판은 OrbMotion.FaceCamera로 화면을 보게 한다.</summary>
        public static GameObject MakeEnergyCore(Element _e)
        {
            Color _c = ElementInfo.Color(_e);
            var _ball = new GameObject("Ball");
            MakeBillboard(_ball.transform, "Halo", 2.6f, new Color(_c.r, _c.g, _c.b, 0.32f));   // 바깥 빛무리
            MakeBillboard(_ball.transform, "Inner", 1.3f, new Color(_c.r, _c.g, _c.b, 0.75f));  // 속성색 몸통 빛
            var _hot = Color.Lerp(_c, Color.white, 0.7f); _hot.a = 1f;
            MakeBillboard(_ball.transform, "Core", 0.6f, _hot);                                  // 하얗게 달아오른 핵
            MakeWisps(_ball.transform, _c);
            return _ball;
        }

        /// <summary>항상 화면을 보는 빛 판(가산 합성 + 부드러운 원)</summary>
        private static void MakeBillboard(Transform _parent, string _name, float _size, Color _color)
        {
            var _pivot = new GameObject(BILLBOARD_PREFIX + _name).transform;
            _pivot.SetParent(_parent, false);
            var _go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _go.name = "Quad";
            Object.Destroy(_go.GetComponent<Collider>());
            _go.transform.SetParent(_pivot, false);
            _go.transform.localScale = Vector3.one * _size;
            var _r = _go.GetComponent<MeshRenderer>();
            _r.sharedMaterial = ElementFx.ParticleMat();
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _r.receiveShadows = false;
            var _block = new MaterialPropertyBlock();
            _block.SetColor(_baseColorId, _color);
            _r.SetPropertyBlock(_block);
        }

        /// <summary>핵 주위를 도는 빛 줄기(꼬리 달린 작은 입자)</summary>
        private static void MakeWisps(Transform _parent, Color _c)
        {
            var _go = new GameObject("Wisps");
            _go.transform.SetParent(_parent, false);
            var _ps = _go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var _m = _ps.main;
            _m.loop = true; _m.playOnAwake = true;
            _m.simulationSpace = ParticleSystemSimulationSpace.Local;
            _m.scalingMode = ParticleSystemScalingMode.Hierarchy;   // Ball 크기를 따라감
            _m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            _m.startSpeed = 0f;
            _m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            _m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_c, Color.white, 0.5f), _c);
            _m.maxParticles = 16;
            var _em = _ps.emission; _em.rateOverTime = 3.5f;
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Sphere; _sh.radius = 0.6f; _sh.radiusThickness = 0f;
            var _vel = _ps.velocityOverLifetime; _vel.enabled = true; _vel.space = ParticleSystemSimulationSpace.Local;
            _vel.orbitalX = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            _vel.orbitalY = new ParticleSystem.MinMaxCurve(4f, 6f);
            _vel.orbitalZ = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            var _col = _ps.colorOverLifetime; _col.enabled = true;
            var _g = new Gradient();
            _g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            _col.color = _g;
            var _tr = _ps.trails; _tr.enabled = true; _tr.ratio = 1f;
            _tr.lifetime = new ParticleSystem.MinMaxCurve(0.35f);
            _tr.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            _tr.dieWithParticles = true; _tr.inheritParticleColor = true;
            var _r = _go.GetComponent<ParticleSystemRenderer>();
            _r.sharedMaterial = ElementFx.ParticleMat();
            _r.trailMaterial = ElementFx.ParticleMat();
            _r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ps.Play();
        }
    }
}
