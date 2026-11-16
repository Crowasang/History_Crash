using R3;

/// <summary>
/// 게임 진행 상태를 읽기만 하는 쪽(스포너, UI, 보스 패턴, 기믹)이 쓰는 인터페이스.
/// GameManager가 구현합니다.
/// </summary>
public interface IGameFlow
{
    /// <summary>현재 상태. 구독하면 상태가 바뀔 때마다 알림을 받습니다.</summary>
    ReadOnlyReactiveProperty<GameStateType> CurrentState { get; }

    /// <summary>경과 시간 (초, 정수). 1초마다 알림 (UI용)</summary>
    ReadOnlyReactiveProperty<int> ElapsedSeconds { get; }

    /// <summary>경과 시간 (초, 소수점)</summary>
    float ElapsedTime { get; }

    bool IsPlaying { get; }
}
