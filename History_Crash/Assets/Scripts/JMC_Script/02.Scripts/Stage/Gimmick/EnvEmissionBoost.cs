using System.Collections.Generic;
using UnityEngine;

namespace HistoryCrash.Stage
{
    /// <summary>
    /// 신전(Enviorment)의 glTF 재질 발광(emissiveFactor)을 배율만큼 올린다. 조명은 그대로 두어 어두운 분위기는 유지.
    /// glTF 안의 재질은 직접 고칠 수 없어서, 재생이 시작될 때 재질 사본(종류별 1개)을 만들어 바꿔 끼운다(배칭 유지).
    /// 호루스 경고 발광은 Factor를 곱해 이어지게 한다(HorusTileCollapse).
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class EnvEmissionBoost : MonoBehaviour
    {
        /// <summary>지금 적용된 배율(없으면 1)</summary>
        public static float Factor = 1f;

        [Tooltip("비우면 이름이 Enviorment인 오브젝트")] public Transform root;
        [Tooltip("발광 배율")] public float boost = 1.8f;
        [Tooltip("호루스 경고 발광도 이 배율을 따름(신전용). 파라오 외피 등 다른 대상은 끔")] public bool affectsHorus = true;

        private static readonly int EMISSIVE = Shader.PropertyToID("emissiveFactor");
        private readonly List<Material> _copies = new List<Material>();

        private void Awake()
        {
            if (!root) { var _g = GameObject.Find("Enviorment"); if (_g) root = _g.transform; }
            if (!root || boost <= 0f) return;
            if (affectsHorus) Factor = boost;
            var _map = new Dictionary<Material, Material>();
            foreach (var _r in root.GetComponentsInChildren<Renderer>(true))
            {
                var _mats = _r.sharedMaterials; bool _changed = false;
                for (int i = 0; i < _mats.Length; i++)
                {
                    var _m = _mats[i];
                    if (!_m || !_m.HasProperty(EMISSIVE) || !_m.shader.name.Contains("glTF")) continue;
                    if (!_map.TryGetValue(_m, out var _c))
                    {
                        _c = new Material(_m) { name = _m.name + "_Boost" };
                        _c.SetColor(EMISSIVE, _m.GetColor(EMISSIVE) * boost);
                        _map[_m] = _c; _copies.Add(_c);
                    }
                    _mats[i] = _c; _changed = true;
                }
                if (_changed) _r.sharedMaterials = _mats;
            }
        }

        private void OnDestroy()
        {
            if (affectsHorus) Factor = 1f;
            foreach (var _c in _copies) if (_c) Destroy(_c);
        }
    }
}
