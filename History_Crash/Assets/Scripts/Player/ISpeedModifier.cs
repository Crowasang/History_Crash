/// <summary>
/// 플레이어 이동 속도를 잠깐 바꾸는 기능. (예: 이동 중 공격 시 60%, 시전 중 정지 0)
/// PlayerController가 구현합니다.
/// </summary>
/// <example>
/// _speedModifier.SpeedMultiplier = 0.6f;  // 이동 중 공격 (기획 v20)
/// _speedModifier.SpeedMultiplier = 1f;    // 공격 끝나면 원래대로
/// </example>
public interface ISpeedModifier
{
    /// <summary>이동 속도 배율 (기본 1, 0이면 정지). 바꾸면 바로 적용됩니다.</summary>
    float SpeedMultiplier { get; set; }

    /// <summary>기본 걷기 속도 (m/초, 달리기·배율 적용 전). 미라처럼 플레이어 속도 기준으로 움직이는 쪽이 씀</summary>
    float BaseMoveSpeed { get; }
}
