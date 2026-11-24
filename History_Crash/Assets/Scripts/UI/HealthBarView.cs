using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력 바 (파라오 HP 바 등). 인스펙터에서 보여줄 체력을 연결하면 스스로 갱신됩니다.
/// Fill Image는 Image Type을 Filled로 설정해 두세요.
/// 실행 중에 대상을 바꾸려면 Bind()를 호출합니다. (예: 나중에 생성되는 몬스터)
/// </summary>
public class HealthBarView : MonoBehaviour
{
    [Tooltip("IHealthReadOnly를 구현한 컴포넌트 (Health)")]
    [SerializeField] private MonoBehaviour _healthSource;
    [SerializeField] private Image _fillImage;
    [Tooltip("\"1200 / 1200\" 형식 표시 (없어도 됨)")]
    [SerializeField] private TMP_Text _valueText;

    private readonly SerialDisposable _subscription = new SerialDisposable();

    private void Start()
    {
        IHealthReadOnly _health = InterfaceRef.Resolve<IHealthReadOnly>(_healthSource, this, nameof(_healthSource));
        if (_health != null)
        {
            Bind(_health);
        }
    }

    public void Bind(IHealthReadOnly _health)
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
