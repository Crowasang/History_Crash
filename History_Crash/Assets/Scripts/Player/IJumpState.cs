/// <summary>
/// 플레이어가 공중에 떠 있는 정도. 보스 패턴의 점프 회피 판정에 씁니다.
/// PlayerController가 구현합니다.
/// </summary>
/// <example>
/// // 패턴 판정: 0.45m 이상 떠 있으면 B(함정)·E(지팡이) 피해 무시
/// if (_player.TryGetComponent(out IJumpState _jump) &amp;&amp; _jump.IsDodgingByJump()) return;
/// </example>
public interface IJumpState
{
    /// <summary>바닥에 서 있는지</summary>
    bool IsGrounded { get; }

    /// <summary>발밑에서 바닥까지의 높이 (m). 아래에 바닥이 없으면 float.PositiveInfinity</summary>
    float GetHeightAboveGround();

    /// <summary>점프 회피 높이(기본 0.45m) 이상 떠 있는지</summary>
    bool IsDodgingByJump();
}
