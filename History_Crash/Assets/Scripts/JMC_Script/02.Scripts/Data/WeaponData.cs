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
        /// <summary>
        /// 무기/속성 아이콘 1종 = 에셋 1개.
        /// 패턴 무기 5종(A~E)과 아누비스 지팡이 속성 코어 4종(불·물·전기·풀)을 같은 구조로 관리.
        /// </summary>
        [CreateAssetMenu(menuName = "HistoryCrash/Weapon Data", fileName = "WD_New")]
        public class WeaponData : ScriptableObject
        {
            [Header("분류")]
            [SerializeField, FormerlySerializedAs("group")] private WeaponGroup _group;
            public WeaponGroup Group { get => _group; set => _group = value; }
            [SerializeField, FormerlySerializedAs("pattern"), Tooltip("group = PatternWeapon일 때 사용")] private PatternId _pattern;
            public PatternId Pattern { get => _pattern; set => _pattern = value; }
            [SerializeField, FormerlySerializedAs("element"), Tooltip("group = AnubisStaffCore일 때 사용")] private Element _element;
            public Element Element { get => _element; set => _element = value; }

            [Header("표시")]
            [SerializeField, FormerlySerializedAs("displayName")] private string _displayName;
            public string DisplayName { get => _displayName; set => _displayName = value; }
            [SerializeField, FormerlySerializedAs("weaponIcon")] private Sprite _weaponIcon;
            public Sprite WeaponIcon { get => _weaponIcon; set => _weaponIcon = value; }

            [SerializeField, FormerlySerializedAs("patternColor"), Tooltip("패턴 무기 전용 색. 속성 코어는 SignatureColor가 ElementInfo 색을 그대로 씀")] private Color _patternColor = new Color(0.86f, 0.70f, 0.38f);

            /// <summary>슬롯 테두리·글로우에 쓰는 대표 색. 속성 코어는 오브·공격 이펙트와 같은 ElementInfo 색(한 곳에서만 정의)</summary>
            public Color SignatureColor
            {
                get { return _group == WeaponGroup.AnubisStaffCore ? ElementInfo.Color(_element) : _patternColor; }
            }

    #if UNITY_EDITOR
            private void OnValidate()
            {
                if (string.IsNullOrEmpty(_displayName))
                    _displayName = _group == WeaponGroup.PatternWeapon ? _pattern.ToString() : "Anubis Staff (" + _element + ")";
            }
    #endif
        }
}
