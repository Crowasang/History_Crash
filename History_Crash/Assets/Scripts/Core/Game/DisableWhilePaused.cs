using R3;
using UnityEngine;

/// <summary>
/// 일시정지 중에 지정한 컴포넌트를 끄고, 풀리면 다시 켭니다.
/// 예: 플레이어의 PlayerLook, PlayerController를 넣으면 일시정지 중에는 시점·이동이 멈추고 커서가 보이며,
///     풀리면 다시 커서가 잠기고 조작됩니다. (PlayerLook이 꺼질 때 커서를 풀고, 켜질 때 잠금)
/// 일시정지할 때 켜져 있던 것만 다시 켜서, 다른 이유로 꺼진 컴포넌트를 실수로 켜지 않습니다.
/// </summary>
public class DisableWhilePaused : MonoBehaviour
{
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager)")]
    [SerializeField] private MonoBehaviour _gameFlowSource;
    [Tooltip("일시정지 중에 끌 컴포넌트들")]
    [SerializeField] private Behaviour[] _targets;

    private bool[] _disabledByPause;

    private void Start()
    {
        IGameFlow _gameFlow = InterfaceRef.Resolve<IGameFlow>(_gameFlowSource, this, nameof(_gameFlowSource));
        if (_gameFlow == null) return;

        _disabledByPause = new bool[_targets.Length];
        _gameFlow.IsPaused
            .Subscribe(_paused =>
            {
                if (_paused) DisableTargets();
                else RestoreTargets();
            })
            .AddTo(this);
    }

    private void DisableTargets()
    {
        for (int _i = 0; _i < _targets.Length; _i++)
        {
            Behaviour _target = _targets[_i];
            _disabledByPause[_i] = _target != null && _target.enabled;
            if (_disabledByPause[_i]) _target.enabled = false;
        }
    }

    private void RestoreTargets()
    {
        for (int _i = 0; _i < _targets.Length; _i++)
        {
            if (_disabledByPause[_i] && _targets[_i] != null)
            {
                _targets[_i].enabled = true;
            }
            _disabledByPause[_i] = false;
        }
    }
}
