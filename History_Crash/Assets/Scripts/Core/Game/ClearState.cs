using UnityEngine;

/// <summary>
/// 클리어 상태 (파라오 HP 0).
/// 정화 연출(피라미드 복원, 소환 몹 소멸, 모래바람 해제)은 CurrentState를 구독한 쪽에서 처리합니다.
/// </summary>
public class ClearState : GameState
{
    public ClearState(GameManager _owner) : base(_owner)
    {
    }

    public override GameStateType Type => GameStateType.Clear;

    public override void Enter()
    {
        Debug.Log($"[GameManager] 클리어! 경과 시간 {_manager.ElapsedTime:F1}초");
    }
}
