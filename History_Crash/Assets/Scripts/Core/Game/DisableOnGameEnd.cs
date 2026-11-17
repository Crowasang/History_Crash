using R3;
using UnityEngine;

/// <summary>
/// 게임이 끝나면(클리어 / 실패) 지정한 컴포넌트를 끕니다.
/// 예: 플레이어의 PlayerLook, PlayerController를 끄면 시점·이동이 멈추고 커서가 풀려서 결과 화면 버튼을 누를 수 있습니다.
/// </summary>
public class DisableOnGameEnd : MonoBehaviour
{
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager)")]
    [SerializeField] private MonoBehaviour _gameFlowSource;
    [Tooltip("게임이 끝나면 끌 컴포넌트들")]
    [SerializeField] private Behaviour[] _targets;

    private void Start()
    {
        IGameFlow _gameFlow = InterfaceRef.Resolve<IGameFlow>(_gameFlowSource, this, nameof(_gameFlowSource));
        if (_gameFlow == null) return;

        _gameFlow.CurrentState
            .Where(_state => _state == GameStateType.Clear || _state == GameStateType.Fail)
            .Subscribe(_ => DisableTargets())
            .AddTo(this);
    }

    private void DisableTargets()
    {
        foreach (Behaviour _target in _targets)
        {
            if (_target != null)
            {
                _target.enabled = false;
            }
        }
    }
}
