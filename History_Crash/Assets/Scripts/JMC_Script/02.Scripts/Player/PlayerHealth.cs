using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Player
{
    /// <summary>
    /// Player 체력. 데미지를 받으면 잠깐 무적(깜빡임)이 되고, 0이 되면 시뮬레이션용으로 잠시 뒤 다시 채운다.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("maxHP")] private int _maxHP = 5;
        [SerializeField, FormerlySerializedAs("invulnerableTime")] private float _invulnerableTime = 1.0f;
        [SerializeField, FormerlySerializedAs("refillDelay"), Tooltip("체력이 0이 되면 이 시간 뒤 다시 가득 채움(시뮬레이션용)")] private float _refillDelay = 2f;
        [SerializeField, FormerlySerializedAs("flashRenderer")] private Renderer _flashRenderer;

        public int HP { get; private set; }
        public string LastDamage { get; private set; } = "";
        public bool IsInvulnerable { get { return Time.time < _invulUntil; } }

        private float _invulUntil, _refillAt = -1f;
        private MaterialPropertyBlock _mpb;

        private void Awake()
        {
            HP = _maxHP;
            _mpb = new MaterialPropertyBlock();
            if (!_flashRenderer) _flashRenderer = GetComponentInChildren<Renderer>();
        }

        /// <summary>데미지를 받으면 true. 무적 중이면 false(ignoreInvulnerability가 true면 무적 중에도 받음).</summary>
        public bool TakeDamage(int _amount, string _reason, bool _ignoreInvulnerability = false)
        {
            if (HP <= 0 || (IsInvulnerable && !_ignoreInvulnerability)) return false;
            HP = Mathf.Max(0, HP - _amount);
            LastDamage = _reason + " -" + _amount;
            _invulUntil = Time.time + _invulnerableTime;
            Debug.Log("[BossPattern] Damage: " + _reason + " -" + _amount + " (HP " + HP + "/" + _maxHP + ")");
            if (HP == 0) { _refillAt = Time.time + _refillDelay; Debug.Log("[BossPattern] Player down"); }
            return true;
        }

        /// <summary>데미지 없이 무적 시간만 준다(예: 낙하 후 복귀 직후).</summary>
        public void GrantInvulnerability(float _seconds)
        {
            _invulUntil = Mathf.Max(_invulUntil, Time.time + _seconds);
        }

        private void Update()
        {
            if (_refillAt > 0 && Time.time >= _refillAt) { HP = _maxHP; _refillAt = -1f; }
        }

        private void LateUpdate()
        {
            if (!_flashRenderer) return;
            // 무적 중 깜빡임(붉은색). BossPatternDirector의 피격 깜빡임보다 나중에 덮어쓴다.
            if (!IsInvulnerable) return;
            _flashRenderer.GetPropertyBlock(_mpb);
            bool _on = Mathf.Repeat(Time.time * 10f, 1f) < 0.5f;
            _mpb.SetColor("_BaseColor", _on ? new Color(1f, 0.2f, 0.2f) : Color.white);
            _flashRenderer.SetPropertyBlock(_mpb);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(10, 168, 360, 26), "");
            string _hearts = "";
            for (int _i = 0; _i < _maxHP; _i++) _hearts += _i < HP ? "O " : "- ";
            GUI.Label(new Rect(20, 172, 340, 20), "HP " + HP + "/" + _maxHP + "  " + _hearts + (HP == 0 ? " DOWN" : "") + (LastDamage != "" ? "  (" + LastDamage + ")" : ""));
        }
    }
}
