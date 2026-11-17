using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Data
{
    /// <summary>무기/속성 아이콘 9종을 한 곳에서 조회</summary>
    [CreateAssetMenu(menuName = "HistoryCrash/Weapon Database", fileName = "WeaponDatabase")]
    public class WeaponDatabase : ScriptableObject
    {
        [SerializeField, FormerlySerializedAs("entries")] private List<WeaponData> _entries = new List<WeaponData>();

        private Dictionary<PatternId, WeaponData> _byPattern;
        private Dictionary<Element, WeaponData> _byElement;

        private void BuildCache()
        {
            _byPattern = new Dictionary<PatternId, WeaponData>();
            _byElement = new Dictionary<Element, WeaponData>();
            foreach (var _d in _entries)
            {
                if (!_d) continue;
                if (_d.Group == WeaponGroup.PatternWeapon) _byPattern[_d.Pattern] = _d;
                else _byElement[_d.Element] = _d;
            }
        }

        public WeaponData GetPattern(PatternId _id) { if (_byPattern == null) BuildCache(); return _byPattern.TryGetValue(_id, out var _d) ? _d : null; }
        public WeaponData GetCore(Element _e) { if (_byElement == null) BuildCache(); return _byElement.TryGetValue(_e, out var _d) ? _d : null; }
        /// <summary>PlayerLoadout.HeldStaff(0~4) 그대로 넣어 조회</summary>
        public WeaponData GetPattern(int _staffIndex) { return _staffIndex < 0 ? null : GetPattern((PatternId)Mathf.Clamp(_staffIndex, 0, 4)); }

        private void OnEnable() { _byPattern = null; _byElement = null; }
    }
}
