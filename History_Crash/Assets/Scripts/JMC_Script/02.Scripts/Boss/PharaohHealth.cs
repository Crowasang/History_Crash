using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Boss
{
    /// <summary>파라오 체력. 플레이어는 직접 공격하지 않고, 머미의 영혼이 날아와 깎는다(기획: 1,200 / 1킬 20).</summary>
    public class PharaohHealth : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("maxHP")] private int _maxHP = 1200;
        [SerializeField, Tooltip("화면 위 디버그 체력 글자·막대(정식 UI는 BossPurifyBarUI)")] private bool _debugBar = false;
        public int HP { get; private set; }
        public int MaxHP { get { return _maxHP; } }
        public float LastHitTime { get; private set; } = -10f;
        public bool IsDead { get { return HP <= 0; } }
        private float _flashUntil;

        private void Awake() { HP = _maxHP; }

        public void Damage(int _amount, string _reason)
        {
            if (IsDead) return;
            HP = Mathf.Max(0, HP - _amount);
            _flashUntil = Time.time + 0.4f; LastHitTime = Time.time;
            Debug.Log("[Pharaoh] -" + _amount + " (" + _reason + ") HP " + HP + "/" + _maxHP);
            if (IsDead) Debug.Log("[Pharaoh] Defeated");
        }

        private void OnGUI()
        {
            if (!_debugBar) return;
            float _w = Mathf.Min(520, Screen.width * 0.5f), _h = 18, _x = (Screen.width - _w) / 2, _y = 14;
            var _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(_x, _y, _w, 20), "Pharaoh  " + HP + " / " + _maxHP, _title);
            _y += 22;
            GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(_x, _y, _w, _h), Texture2D.whiteTexture);
            GUI.color = Time.time < _flashUntil ? new Color(1f, 0.9f, 0.6f) : new Color(0.75f, 0.12f, 0.1f);
            GUI.DrawTexture(new Rect(_x + 2, _y + 2, (_w - 4) * HP / (float)_maxHP, _h - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
