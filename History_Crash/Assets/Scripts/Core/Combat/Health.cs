using R3;
using UnityEngine;

/// <summary>
/// 체력 컴포넌트. 플레이어(하트 5칸), 미라(HP 100), 파라오(HP 1200) 모두 이것 하나로 처리합니다.
/// - 체력 변화와 사망은 R3 이벤트로 알립니다. (UI, 연출, 게임 흐름이 구독)
/// - 피격 후 무적 시간을 설정할 수 있습니다. (플레이어 1초)
/// - 오브젝트 풀에서 다시 꺼내지면 체력이 자동으로 초기화됩니다. (IPoolable)
/// </summary>
/// <example>
/// _health.CurrentHp.Subscribe(_hp => UpdateHeartUI(_hp)).AddTo(this);
/// _health.OnDeath.Subscribe(_ => PlayDeathEffect()).AddTo(this);
/// </example>
public class Health : MonoBehaviour, IDamageable, IPoolable
{
    [SerializeField] private int _maxHp = 100;

    [Tooltip("피격 후 무적 시간 (초). 0이면 무적 없음")]
    [SerializeField] private float _invincibleTimeOnHit = 0f;

    private readonly ReactiveProperty<int> _currentHp = new ReactiveProperty<int>();
    private readonly Subject<DamageInfo> _onDamaged = new Subject<DamageInfo>();
    private readonly Subject<Unit> _onDeath = new Subject<Unit>();

    private float _invincibleEndTime;

    public int MaxHp => _maxHp;

    /// <summary>현재 체력. 구독하면 값이 바뀔 때마다 알림을 받습니다.</summary>
    public ReadOnlyReactiveProperty<int> CurrentHp => _currentHp;

    /// <summary>피해를 받았을 때 (무적 중이거나 이미 죽었으면 발생하지 않음)</summary>
    public Observable<DamageInfo> OnDamaged => _onDamaged;

    /// <summary>체력이 0이 되었을 때 한 번 발생</summary>
    public Observable<Unit> OnDeath => _onDeath;

    public bool IsAlive => _currentHp.Value > 0;

    public bool IsInvincible => Time.time < _invincibleEndTime;

    private void Awake()
    {
        ResetHealth();
    }

    private void OnDestroy()
    {
        _currentHp.Dispose();
        _onDamaged.Dispose();
        _onDeath.Dispose();
    }

    public void TakeDamage(DamageInfo _info)
    {
        if (!IsAlive || IsInvincible || _info.Amount <= 0) return;

        _currentHp.Value = Mathf.Max(0, _currentHp.Value - _info.Amount);
        _onDamaged.OnNext(_info);

        if (!IsAlive)
        {
            _onDeath.OnNext(Unit.Default);
            return;
        }

        if (_invincibleTimeOnHit > 0f)
        {
            SetInvincible(_invincibleTimeOnHit);
        }
    }

    /// <summary>
    /// 지정한 시간 동안 무적으로 만듭니다. (아누비스 강제 이동, 타일 낙하 후 복귀 등)
    /// 이미 더 긴 무적이 걸려 있으면 그대로 유지합니다.
    /// </summary>
    public void SetInvincible(float _seconds)
    {
        _invincibleEndTime = Mathf.Max(_invincibleEndTime, Time.time + _seconds);
    }

    /// <summary>
    /// 체력을 최대로 되돌리고 무적을 해제합니다. (풀에서 꺼내질 때는 자동 호출)
    /// </summary>
    public void ResetHealth()
    {
        _currentHp.Value = _maxHp;
        _invincibleEndTime = 0f;
    }

    public void OnSpawned()
    {
        ResetHealth();
    }

    public void OnDespawned()
    {
    }
}
