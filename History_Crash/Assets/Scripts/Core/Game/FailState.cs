using UnityEngine;

/// <summary>
/// 실패 상태 (플레이어 HP 0).
/// 결과 화면 등은 CurrentState를 구독한 쪽에서 처리합니다.
/// </summary>
public class FailState : GameState
{
    public FailState(GameManager _owner) : base(_owner)
    {
    }

    public override GameStateType Type => GameStateType.Fail;

    public override void Enter()
    {
        Debug.Log($"[GameManager] 실패. 경과 시간 {_manager.ElapsedTime:F1}초");
    }
}
