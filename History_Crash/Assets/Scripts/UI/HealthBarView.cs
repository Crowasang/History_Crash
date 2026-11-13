using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력 바 (파라오 HP 바 등). Bind()로 Health를 연결하면 체력이 바뀔 때마다 갱신됩니다.
/// Fill Image는 Image Type을 Filled로 설정해 두세요.
/// </summary>
public class HealthBarView : MonoBehaviour
{
    [SerializeField] private Image _fillImage;
    [Tooltip("\"1200 / 1200\" 형식 표시 (없어도 됨)")]
    [SerializeField] private TMP_Text _valueText;

    private readonly SerialDisposable _subscription = new SerialDisposable();

    public void Bind(Health _health)
    {
        _subscription.Disposable = _health.CurrentHp
            .Subscribe(_hp => Refresh(_hp, _health.MaxHp));
    }

    private void Refresh(int _hp, int _maxHp)
    {
        if (_fillImage != null)
        {
            _fillImage.fillAmount = _maxHp > 0 ? (float)_hp / _maxHp : 0f;
        }

        if (_valueText != null)
        {
            _valueText.text = $"{_hp:N0} / {_maxHp:N0}";
        }
    }

    private void OnDestroy()
    {
        _subscription.Dispose();
    }
}
