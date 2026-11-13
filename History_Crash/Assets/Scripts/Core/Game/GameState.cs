/// <summary>
/// 게임 상태의 기반 클래스 (상태 패턴).
/// 새 상태를 추가하려면 이 클래스를 상속하고, GameStateType에 값을 추가한 뒤 GameManager에 등록합니다.
/// </summary>
public abstract class GameState
{
    protected readonly GameManager _manager;

    protected GameState(GameManager _owner)
    {
        _manager = _owner;
    }

    public abstract GameStateType Type { get; }

    /// <summary>이 상태로 들어올 때 한 번 호출</summary>
    public virtual void Enter()
    {
    }

    /// <summary>다른 상태로 나갈 때 한 번 호출</summary>
    public virtual void Exit()
    {
    }
}
