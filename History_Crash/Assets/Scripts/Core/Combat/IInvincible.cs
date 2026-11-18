/// <summary>
/// 잠깐 무적으로 만들 수 있는 대상. (낙하 복귀, 아누비스 강제 이동 등)
/// Health가 구현합니다.
/// </summary>
public interface IInvincible
{
    bool IsInvincible { get; }

    /// <summary>지정한 시간(초) 동안 무적. 이미 더 긴 무적이면 그대로 유지</summary>
    void SetInvincible(float _seconds);
}
