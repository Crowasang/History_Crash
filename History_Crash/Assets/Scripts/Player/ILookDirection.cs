using System;
using UnityEngine;

/// <summary>
/// 플레이어가 보는 방향. 이동(PlayerController)과 공격이 이것을 기준으로 방향을 정합니다.
/// PlayerLook이 구현합니다.
/// </summary>
public interface ILookDirection
{
    /// <summary>좌우 시점 각도 (도)</summary>
    float Yaw { get; }

    /// <summary>조준 지점 (공격 방향 계산에 사용)</summary>
    Vector3 AimPoint { get; }

    /// <summary>시점이 바뀔 때마다 발생</summary>
    event Action OnLookChanged;

    /// <summary>true면 마우스 좌우 회전과 Z/C 스냅을 무시합니다. (락온 중)</summary>
    bool IsYawLocked { get; set; }

    /// <summary>지정한 위치를 바라보도록 시점을 돌립니다. (시작 방향 지정, 연출 등)</summary>
    void LookAt(Vector3 _worldPoint);

    /// <summary>좌우 시점 각도를 직접 정합니다. (락온 추적용)</summary>
    void SetYaw(float _yaw);

    /// <summary>게임 시작 때의 시점으로 돌아갑니다. (V)</summary>
    void ResetToHomeView();
}
