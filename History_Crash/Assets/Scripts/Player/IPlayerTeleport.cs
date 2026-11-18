using UnityEngine;

/// <summary>
/// 플레이어를 다른 위치로 즉시 옮기고 바닥에 선 상태로 만드는 기능.
/// 낙하 복귀, 아누비스 강제 이동 도착 등에 씁니다. PlayerController가 구현합니다.
/// </summary>
public interface IPlayerTeleport
{
    /// <summary>
    /// 지정한 위치(플레이어 오브젝트 위치 기준)로 옮기고, 떨어지던 속도와 점프 상태를 끊습니다.
    /// </summary>
    void TeleportTo(Vector3 _position);

    /// <summary>발바닥에서 오브젝트 위치(피벗)까지의 높이. 바닥 위 좌표를 피벗 좌표로 바꿀 때 사용</summary>
    float PivotHeightAboveFeet { get; }
}
