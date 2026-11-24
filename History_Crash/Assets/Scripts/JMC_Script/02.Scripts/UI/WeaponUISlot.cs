using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;

namespace HistoryCrash.UI
{
    [System.Serializable]
    public class WeaponUISlot
    {
        [SerializeField, FormerlySerializedAs("root")] private RectTransform _root;
        public RectTransform Root { get => _root; set => _root = value; }
        [SerializeField, FormerlySerializedAs("icon")] private Image _icon;
        public Image Icon { get => _icon; set => _icon = value; }
        [SerializeField, FormerlySerializedAs("ink"), Tooltip("잉크 윤곽(같은 스프라이트를 어두운 갈색으로, Filled Horizontal)")] private Image _ink;
        public Image Ink { get => _ink; set => _ink = value; }
        [SerializeField, FormerlySerializedAs("caption"), Tooltip("이름 글씨(선택)")] private Text _caption;
        public Text Caption { get => _caption; set => _caption = value; }
        [SerializeField, FormerlySerializedAs("glow")] private Image _glow;
        public Image Glow { get => _glow; set => _glow = value; }
        [SerializeField, FormerlySerializedAs("current"), HideInInspector] private WeaponData _current;
        public WeaponData Current { get => _current; set => _current = value; }
        [System.NonSerialized] private DG.Tweening.Tween _running;   // 지금 진행 중인 연출(DOTween)
        public DG.Tweening.Tween Running { get => _running; set => _running = value; }
    }
}
