using R3;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 하트 UI. 인스펙터에서 플레이어 체력을 연결하고, 하트 Image를 체력 칸 수만큼 등록해 두면
/// 남은 체력만큼 채워진 하트를 보여줍니다.
/// </summary>
public class HeartView : MonoBehaviour
{
    [Tooltip("IHealthReadOnly를 구현한 컴포넌트 (플레이어 Health)")]
    [SerializeField] private MonoBehaviour _healthSource;
    [Tooltip("왼쪽부터 순서대로 하트 Image")]
    [SerializeField] private Image[] _hearts;
    [SerializeField] private Sprite _fullSprite;
    [SerializeField] private Sprite _emptySprite;

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
        _subscription.Disposable = _health.CurrentHp.Subscribe(Refresh);
    }

    private void Refresh(int _hp)
    {
        for (int _i = 0; _i < _hearts.Length; _i++)
        {
            bool _isFull = _i < _hp;

            // 빈 하트 스프라이트가 없으면 숨기기로 처리
            if (_emptySprite == null)
            {
                _hearts[_i].enabled = _isFull;
            }
            else
            {
                _hearts[_i].sprite = _isFull ? _fullSprite : _emptySprite;
            }
        }
    }

    private void OnDestroy()
    {
        _subscription.Dispose();
    }
}
