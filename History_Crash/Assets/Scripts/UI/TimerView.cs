using R3;
using TMPro;
using UnityEngine;

/// <summary>
/// 경과 시간 표시 ("경과 00:05"). GameManager의 ElapsedSeconds에 연결합니다.
/// </summary>
public class TimerView : MonoBehaviour
{
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private string _prefix = "경과 ";

    private readonly SerialDisposable _subscription = new SerialDisposable();

    public void Bind(GameManager _gameManager)
    {
        _subscription.Disposable = _gameManager.ElapsedSeconds.Subscribe(Refresh);
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
