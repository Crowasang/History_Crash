/// <summary>
/// 피해를 받을 수 있는 대상 (플레이어, 미라, 파라오 등).
/// 공격하는 쪽은 구체 클래스 대신 이 인터페이스만 알면 됩니다.
/// </summary>
/// <example>
/// if (_other.TryGetComponent(out IDamageable _target))
/// {
///     _target.TakeDamage(new DamageInfo(1, ElementType.None, gameObject));
/// }
/// </example>
public interface IDamageable
{
    bool IsAlive { get; }

    void TakeDamage(DamageInfo _info);
}
