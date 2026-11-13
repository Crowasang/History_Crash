/// <summary>
/// 전투 진행 상태. 경과 시간을 잽니다.
/// 리젠·보스 패턴·기믹은 각 시스템이 CurrentState를 구독해서 이 상태일 때만 동작합니다.
/// </summary>
public class PlayingState : GameState
{
    public PlayingState(GameManager _owner) : base(_owner)
    {
    }

    public override GameStateType Type => GameStateType.Playing;

    public override void Enter()
    {
        _manager.StartTimer();
    }

    public override void Exit()
    {
        _manager.StopTimer();
    }
}
