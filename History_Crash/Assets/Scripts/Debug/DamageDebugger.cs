using UnityEngine;

/// <summary>
/// 테스트용 피해 도구. 공격 기능이 없어도 승패를 확인할 수 있게 합니다.
/// 플레이 중 인스펙터에서 이 컴포넌트의 ⋮ 메뉴(또는 우클릭)를 열어 항목을 누르면 됩니다.
/// 실제 게임 로직에는 쓰지 않습니다.
/// </summary>
public class DamageDebugger : MonoBehaviour
{
    [Tooltip("IDamageable을 구현한 컴포넌트 (Health)")]
    [SerializeField] private MonoBehaviour _playerHealth;
    [Tooltip("IDamageable을 구현한 컴포넌트 (Health)")]
    [SerializeField] private MonoBehaviour _bossHealth;
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager). 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private MonoBehaviour _gameFlowSource;

    [Tooltip("보스 피해량 (미라 1킬 = 20)")]
    [SerializeField] private int _bossDamage = 20;

    [ContextMenu("보스에 피해 (미라 1킬)")]
    private void DamageBoss()
    {
        Damage(_bossHealth, nameof(_bossHealth), _bossDamage);
    }

    [ContextMenu("보스 즉시 처치")]
    private void KillBoss()
    {
        Damage(_bossHealth, nameof(_bossHealth), int.MaxValue);
    }

    [ContextMenu("플레이어에 1 피해")]
    private void DamagePlayer()
    {
        Damage(_playerHealth, nameof(_playerHealth), 1);
    }

    [ContextMenu("플레이어 즉시 처치")]
    private void KillPlayer()
    {
        Damage(_playerHealth, nameof(_playerHealth), int.MaxValue);
    }

    private void Damage(MonoBehaviour _source, string _fieldName, int _amount)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageDebugger] 플레이 중에만 사용할 수 있습니다.");
            return;
        }

        IDamageable _target = InterfaceRef.Resolve<IDamageable>(_source, this, _fieldName);
        if (_target == null)
        {
            Debug.LogWarning($"[DamageDebugger] {_fieldName}이(가) 연결되지 않았습니다.");
            return;
        }

        // 준비 상태(시작 후 대기 중)나 이미 끝난 뒤에는 죽어도 승패로 치지 않음
        IGameFlow _gameFlow = InterfaceRef.Resolve<IGameFlow>(_gameFlowSource, this, nameof(_gameFlowSource))
            ?? GetComponent<IGameFlow>();
        if (_gameFlow != null && !_gameFlow.IsPlaying)
        {
            Debug.LogWarning($"[DamageDebugger] 지금은 진행 상태가 아니라 승패에 반영되지 않습니다. (현재 상태: {_gameFlow.CurrentState.CurrentValue})");
        }

        _target.TakeDamage(new DamageInfo(_amount, ElementType.None, gameObject));

        string _amountText = _amount == int.MaxValue ? "즉사" : _amount.ToString();
        string _hpText = _source is IHealthReadOnly _health ? _health.CurrentHp.CurrentValue.ToString() : "?";
        Debug.Log($"[DamageDebugger] {_source.name}에 {_amountText} 피해 → 남은 체력 {_hpText}");
    }
}
