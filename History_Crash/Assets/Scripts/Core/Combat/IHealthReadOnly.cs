using R3;

/// <summary>
/// 체력을 읽기만 하는 쪽(UI, 게임 흐름, 연출)이 쓰는 인터페이스.
/// 피해를 주는 쪽은 IDamageable을, 보기만 하는 쪽은 이것을 씁니다.
/// </summary>
public interface IHealthReadOnly
{
    int MaxHp { get; }

    /// <summary>현재 체력. 구독하면 값이 바뀔 때마다 알림을 받습니다.</summary>
    ReadOnlyReactiveProperty<int> CurrentHp { get; }

    /// <summary>피해를 받았을 때</summary>
    Observable<DamageInfo> OnDamaged { get; }

    /// <summary>체력이 0이 되었을 때</summary>
    Observable<Unit> OnDeath { get; }

    bool IsAlive { get; }
}
