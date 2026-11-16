using R3;
using TMPro;
using UnityEngine;

/// <summary>
/// 경과 시간 표시 ("경과 00:05"). 인스펙터에서 게임 흐름(GameManager)을 연결하면 스스로 갱신됩니다.
/// </summary>
public class TimerView : MonoBehaviour
{
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager)")]
    [SerializeField] private MonoBehaviour _gameFlowSource;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private string _prefix = "경과 ";

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
        _subscription.Disposable = _gameFlow.ElapsedSeconds.Subscribe(Refresh);
    }

    private void Refresh(int _seconds)
    {
        _timeText.text = $"{_prefix}{_seconds / 60:00}:{_seconds % 60:00}";
    }

    private void OnDestroy()
    {
        _subscription.Dispose();
    }
}
