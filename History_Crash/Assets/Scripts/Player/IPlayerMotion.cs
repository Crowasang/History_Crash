using System;
using UnityEngine;

/// <summary>
/// 플레이어 움직임 알림. 애니메이션, 효과음, 이펙트가 구독합니다.
/// PlayerController가 구현합니다.
/// </summary>
public interface IPlayerMotion
{
    /// <summary>
    /// 이동 입력이 바뀔 때. (몸 기준 방향: x = 좌-/우+, y = 뒤-/앞+, 크기 0~1), (이동 속도 m/초)
    /// </summary>
    event Action<Vector2, float> OnMoveChanged;

    /// <summary>점프했을 때</summary>
    event Action OnJumped;

    /// <summary>공중에서 바닥에 내려앉았을 때</summary>
    event Action OnLanded;
}
