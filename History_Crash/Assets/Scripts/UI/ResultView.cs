using R3;
using UnityEngine;

/// <summary>
/// 클리어 / 실패 결과 패널. 해당 상태가 되면 패널을 켭니다.
/// </summary>
public class ResultView : MonoBehaviour
{
    [SerializeField] private GameObject _clearPanel;
    [SerializeField] private GameObject _failPanel;

    private readonly SerialDisposable _subscription = new SerialDisposable();

    public void Bind(GameManager _gameManager)
    {
        _subscription.Disposable = _gameManager.CurrentState.Subscribe(Refresh);
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
