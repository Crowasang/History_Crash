using R3;
using TMPro;
using UnityEngine;

/// <summary>
/// 클리어 / 실패 결과 패널. 인스펙터에서 게임 흐름(GameManager)을 연결하면 해당 상태가 될 때 패널을 켭니다.
/// 시간 텍스트를 연결하면 결과가 나온 시점의 경과 시간을 보여줍니다.
/// </summary>
public class ResultView : MonoBehaviour
{
    [Tooltip("IGameFlow를 구현한 컴포넌트 (GameManager)")]
    [SerializeField] private MonoBehaviour _gameFlowSource;
    [SerializeField] private GameObject _clearPanel;
    [SerializeField] private GameObject _failPanel;

    [Header("Time (없어도 됨)")]
    [SerializeField] private TMP_Text _clearTimeText;
    [SerializeField] private TMP_Text _failTimeText;
    [Tooltip("{0} = 분, {1} = 초")]
    [SerializeField] private string _timeFormat = "TIME {0:00}:{1:00}";

    private readonly SerialDisposable _subscription = new SerialDisposable();
    private IGameFlow _gameFlow;

    private void Start()
    {
        IGameFlow _source = InterfaceRef.Resolve<IGameFlow>(_gameFlowSource, this, nameof(_gameFlowSource));
        if (_source != null)
        {
            Bind(_source);
        }
    }

    public void Bind(IGameFlow _source)
    {
        _gameFlow = _source;
        _subscription.Disposable = _gameFlow.CurrentState.Subscribe(Refresh);
    }

    private void Refresh(GameStateType _state)
    {
        bool _isClear = _state == GameStateType.Clear;
        bool _isFail = _state == GameStateType.Fail;

        if (_clearPanel != null) _clearPanel.SetActive(_isClear);
        if (_failPanel != null) _failPanel.SetActive(_isFail);

        if (_isClear) SetTime(_clearTimeText);
        if (_isFail) SetTime(_failTimeText);
    }

    private void SetTime(TMP_Text _text)
    {
        if (_text == null) return;

        int _seconds = Mathf.FloorToInt(_gameFlow.ElapsedTime);
        _text.text = string.Format(_timeFormat, _seconds / 60, _seconds % 60);
    }

    private void OnDestroy()
    {
        _subscription.Dispose();
    }
}
