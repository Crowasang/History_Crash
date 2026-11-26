using System.Collections.Generic;
using UnityEngine;

namespace HistoryCrash.Boss
{
    /// <summary>
    /// 파라오 자체 빛(영혼광·림 라이트·몸 발광)을 한곳에서 다룬다. 플레이 중 인스펙터에서 바로 조정 가능.
    /// 두 라이트는 파라오 전용 라이트 레이어(16)만 비추고, 패턴 라이트는 이 레이어를 비추지 않는다.
    /// 몸 발광은 glTF 재질 사본(종류별 1개)의 emissiveFactor에 배율을 곱한다.
    /// </summary>
    public class PharaohSelfGlow : MonoBehaviour
    {
        [Header("영혼광(옆앞)")]
        public Light spiritLight;
        [ColorUsage(false)] public Color spiritColor = new Color(0.35f, 0.95f, 1f);
        [Min(0)] public float spiritIntensity = 3.2f;
        [Min(0)] public float spiritRange = 4f;

        [Header("림 라이트(뒤위)")]
        public Light rimLight;
        [ColorUsage(false)] public Color rimColor = new Color(0.55f, 1f, 0.8f);
        [Min(0)] public float rimIntensity = 9f;
        [Min(0)] public float rimRange = 4.5f;

        [Header("몸 발광")]
        public Transform bodyRoot;
        [Min(0)] public float bodyEmission = 3f;

        private static readonly int EMISSIVE = Shader.PropertyToID("emissiveFactor");
        private readonly List<(Material copy, Color baseCol)> _copies = new List<(Material, Color)>();
        private float _appliedBoost = -1f;

        private void Start()
        {
            if (!bodyRoot) return;
            var _map = new Dictionary<Material, Material>();
            foreach (var _r in bodyRoot.GetComponentsInChildren<Renderer>(true))
            {
                var _mats = _r.sharedMaterials; bool _changed = false;
                for (int i = 0; i < _mats.Length; i++)
                {
                    var _m = _mats[i];
                    if (!_m || !_m.HasProperty(EMISSIVE)) continue;
                    if (!_map.TryGetValue(_m, out var _c))
                    {
                        _c = new Material(_m) { name = _m.name + "_SelfGlow" };
                        _map[_m] = _c; _copies.Add((_c, _m.GetColor(EMISSIVE)));
                    }
                    _mats[i] = _c; _changed = true;
                }
                if (_changed) _r.sharedMaterials = _mats;
            }
        }

        private void LateUpdate()
        {
            Apply(spiritLight, spiritColor, spiritIntensity, spiritRange);
            Apply(rimLight, rimColor, rimIntensity, rimRange);
            if (!Mathf.Approximately(_appliedBoost, bodyEmission))
            {
                foreach (var (_c, _b) in _copies) _c.SetColor(EMISSIVE, _b * bodyEmission);
                _appliedBoost = bodyEmission;
            }
        }

        private static void Apply(Light _l, Color _col, float _i, float _r)
        {
            if (!_l) return;
            _l.color = _col; _l.intensity = _i; _l.range = _r;
        }

        private void OnDestroy()
        {
            foreach (var (_c, _) in _copies) if (_c) Destroy(_c);
        }
    }
}
