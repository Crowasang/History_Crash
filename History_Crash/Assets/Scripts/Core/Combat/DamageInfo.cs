using UnityEngine;

/// <summary>
/// 피해 한 번에 대한 정보. 피해를 주는 쪽이 만들어서 IDamageable.TakeDamage()로 넘깁니다.
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>피해량 (플레이어는 하트 칸 수, 몬스터·보스는 HP)</summary>
    public int Amount { get; }

    /// <summary>공격 속성 (속성 없는 피해는 None)</summary>
    public ElementType Element { get; }

    /// <summary>피해를 준 오브젝트 (없으면 null)</summary>
    public GameObject Source { get; }

    public DamageInfo(int _amount, ElementType _element = ElementType.None, GameObject _source = null)
    {
        Amount = _amount;
        Element = _element;
        Source = _source;
    }
}
