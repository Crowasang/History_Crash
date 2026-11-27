using UnityEngine;

/// <summary>
/// 피격 등으로 짧게 밀려나는 대상. PlayerController가 구현합니다.
/// 물리 충돌로 밀리는 것이 아니라 정해진 거리만큼만 움직이고 멈추므로 미끄러지지 않습니다.
/// </summary>
public interface IKnockback
{
    /// <summary>수평 _direction 쪽으로 _distance(m)를 _duration(초) 동안 밀려납니다. 밀리는 동안 이동 입력은 잠깁니다.</summary>
    void Knockback(Vector3 _direction, float _distance, float _duration);
}
