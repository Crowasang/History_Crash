/// <summary>
/// 게임 진행 상태. 다른 시스템은 GameManager.CurrentState를 구독해서 상태에 맞게 동작합니다.
/// </summary>
public enum GameStateType
{
    /// <summary>시작 전 대기 (카운트다운 등)</summary>
    Ready,

    /// <summary>전투 진행 중 (리젠, 보스 패턴, 기믹이 동작)</summary>
    Playing,

    /// <summary>파라오 HP 0 → 클리어 (정화 연출)</summary>
    Clear,

    /// <summary>플레이어 HP 0 → 실패</summary>
    Fail
}
