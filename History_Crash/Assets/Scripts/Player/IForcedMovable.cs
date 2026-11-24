using System;
using UnityEngine;

/// <summary>
/// 외부에서 플레이어를 강제로 끌고 가는 기능 (아누비스 석상 등).
/// PlayerForcedMover가 구현합니다.
/// </summary>
/// <example>
/// // 아누비스 패턴: 예고 3초가 끝나면 도착 칸(바닥 윗면 가운데)으로 끌고 감
/// if (_player.TryGetComponent(out IForcedMovable _movable))
/// {
///     _movable.TryForceMove(_arrivalCellCenter);
/// }
/// </example>
public interface IForcedMovable
{
    /// <summary>끌려가는 중인지 (낙하가 끝나기를 기다리는 시간 포함)</summary>
    bool IsForceMoving { get; }

    /// <summary>실제로 끌려가기 시작할 때 (불꽃 꼬리 등 연출용). 출발 위치, 도착 위치</summary>
    event Action<Vector3, Vector3> OnForceMoveStarted;

    /// <summary>도착해서 바닥에 내려섰을 때</summary>
    event Action OnForceMoveEnded;

    /// <summary>
    /// 지정한 바닥 위치(발이 닿을 지점)로 끌고 갑니다.
    /// 이미 끌려가는 중이면 무시하고 false를 돌려줍니다.
    /// </summary>
    bool TryForceMove(Vector3 _destinationFeet);
}
