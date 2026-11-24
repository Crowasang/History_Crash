using R3;
using UnityEngine;

/// <summary>
/// 지정한 오브젝트를 게임 진행(Playing) 중에만 켭니다. 준비 중에는 꺼 두고, 클리어·실패하면 끕니다.
/// 예: 보스 패턴·아누비스·호루스가 붙은 오브젝트를 넣으면 진행 중에만 패턴이 돌고, 게임이 끝나면 멈춥니다.
/// - 오브젝트를 끄면 그 안의 코루틴도 함께 멈춥니다.
/// - 처음 켜질 때 Start가 실행되므로, 패턴의 "첫 발동까지 대기 시간"은 진행 시작 시점부터 셉니다.
/// 대상 스크립트를 고치지 않고 게임 흐름에 맞출 때 씁니다. (일시정지는 timeScale로 이미 멈추므로 따로 처리하지 않음)
/// </summary>
public class ActiveOnlyWhilePlaying : MonoBehaviour
{
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager). 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private MonoBehaviour _gameFlowSource;
    [Tooltip("진행 중에만 켤 오브젝트들 (이 컴포넌트가 붙은 오브젝트는 넣지 않음)")]
    [SerializeField] private GameObject[] _targets;

    private void Awake()
    {
        // 대상의 Start(패턴 루프 시작)가 진행 전에 실행되지 않도록 가장 먼저 꺼 둠
        SetTargetsActive(false);
    }

    private void Start()
    {
        IGameFlow _gameFlow = InterfaceRef.Resolve<IGameFlow>(_gameFlowSource, this, nameof(_gameFlowSource))
            ?? GetComponent<IGameFlow>();
        if (_gameFlow == null)
        {
            Debug.LogError($"[ActiveOnlyWhilePlaying] {name}: IGameFlow(GameManager)를 찾지 못해 대상을 계속 켜 둡니다.", this);
            SetTargetsActive(true);
            return;
        }

        _gameFlow.CurrentState
            .Subscribe(_state => SetTargetsActive(_state == GameStateType.Playing))
            .AddTo(this);
    }

    private void SetTargetsActive(bool _isActive)
    {
        foreach (GameObject _target in _targets)
        {
            if (_target != null && _target.activeSelf != _isActive)
            {
                _target.SetActive(_isActive);
            }
        }
    }
}
