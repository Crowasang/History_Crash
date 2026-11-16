using R3;
using UnityEngine;

/// <summary>
/// 클리어 / 실패 결과 패널. 인스펙터에서 게임 흐름(GameManager)을 연결하면 해당 상태가 될 때 패널을 켭니다.
/// </summary>
public class ResultView : MonoBehaviour
{
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager)")]
    [SerializeField] private MonoBehaviour _gameFlowSource;
    [SerializeField] private GameObject _clearPanel;
    [SerializeField] private GameObject _failPanel;

    private readonly SerialDisposable _subscription = new SerialDisposable();

    private void Start()
    {
        IGameFlow _gameFlow = InterfaceRef.Resolve<IGameFlow>(_gameFlowSource, this, nameof(_gameFlowSource));
        if (_gameFlow != null)
        {
            Bind(_gameFlow);
        }
    }

    public void Bind(IGameFlow _gameFlow)
    {
        _subscription.Disposable = _gameFlow.CurrentState.Subscribe(Refresh);
    }

    private void Refresh(GameStateType _state)
    {
        if (_clearPanel != null)
        {
            _clearPanel.SetActive(_state == GameStateType.Clear);
        }

        if (_failPanel != null)
        {
            _failPanel.SetActive(_state == GameStateType.Fail);
        }
    }

    private void OnDestroy()
    {
        _subscription.Dispose();
    }
}
