using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
    /// E키로 줍는 바닥 아이템(스태프·오브). Player는 PlayerLoadout에서 1칸 안의 가장 가까운 것을 줍는다.
    /// </summary>
    public class PickupItem : MonoBehaviour
    {
        private static readonly List<PickupItem> _all = new List<PickupItem>();
        public static List<PickupItem> All => _all;

        [SerializeField, FormerlySerializedAs("kind")] private PickupKind _kind;
        public PickupKind Kind { get => _kind; set => _kind = value; }
        [SerializeField, FormerlySerializedAs("value"), Tooltip("스태프: 패턴 번호(0=A~4=E), 오브: Element 값")] private int _value;
        public int Value { get => _value; set => _value = value; }
        [SerializeField, FormerlySerializedAs("cell")] private Vector2Int _cell;
        public Vector2Int Cell { get => _cell; set => _cell = value; }
        [SerializeField, FormerlySerializedAs("ready"), Tooltip("땅에 박히거나 놓인 뒤에만 주울 수 있음")] private bool _ready;
        public bool Ready { get => _ready; set => _ready = value; }

        public Element ElementValue { get { return (Element)_value; } }

        private void OnEnable() { _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }

        public static bool CellTaken(Vector2Int _c)
        {
            foreach (var _p in _all) if (_p && _p._cell == _c) return true;
            return false;
        }

        public void Collect() { Destroy(gameObject); }
    }
}
