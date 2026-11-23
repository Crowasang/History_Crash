using R3;
using UnityEngine;

/// <summary>
/// 게임이 끝나면(클리어 / 실패) 지정한 컴포넌트를 끕니다.
/// 예: 플레이어의 PlayerLook, PlayerController를 끄면 시점·이동이 멈추고 커서가 풀려서 결과 화면 버튼을 누를 수 있습니다.
/// 물리 몸(Rigidbody)을 넣으면 그 자리에 고정해서, 멈춘 패턴 투사체 등에 계속 밀리지 않게 합니다.
/// </summary>
public class DisableOnGameEnd : MonoBehaviour
{
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager)")]
    [SerializeField] private MonoBehaviour _gameFlowSource;
    [Tooltip("게임이 끝나면 끌 컴포넌트들")]
    [SerializeField] private Behaviour[] _targets;
    [Tooltip("게임이 끝나면 그 자리에 고정할 물리 몸 (플레이어 Rigidbody 등)")]
    [SerializeField] private Rigidbody[] _freezeBodies;

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

        if (_freezeBodies == null) return;

        // 위치·회전을 모두 잠금 (isKinematic과 달리 강제 이동 등이 끝나며 되돌려도 풀리지 않음)
        foreach (Rigidbody _body in _freezeBodies)
        {
            if (_body == null) continue;

            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
}
