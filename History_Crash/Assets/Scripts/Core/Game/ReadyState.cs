using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 시작 전 대기 상태. 정해진 시간이 지나면 진행 상태로 넘어갑니다.
/// </summary>
public class ReadyState : GameState
{
    private CancellationTokenSource _cts;

    public ReadyState(GameManager _owner) : base(_owner)
    {
    }

    public override GameStateType Type => GameStateType.Ready;

    public override void Enter()
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(_manager.destroyCancellationToken);
        WaitAndStartAsync(_cts.Token).Forget();
    }

    public override void Exit()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async UniTaskVoid WaitAndStartAsync(CancellationToken _token)
    {
        bool _isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_manager.ReadyDuration), cancellationToken: _token)
            .SuppressCancellationThrow();

        if (!_isCanceled)
        {
            _manager.ChangeState(GameStateType.Playing);
        }
    }
}
