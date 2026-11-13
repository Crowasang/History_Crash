using UnityEngine;

/// <summary>
/// 게임 화면 UI를 데이터에 연결합니다. 씬에 하나만 둡니다.
/// 각 화면 요소(View)는 표시만 담당하고, 어떤 데이터를 보여줄지는 여기서 정합니다.
/// - 새 UI 요소 추가: View 스크립트를 만들고, 여기에 필드 하나와 Bind 한 줄을 추가
/// - 연결하지 않은(비어 있는) View는 건너뜁니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private Health _playerHealth;
    [SerializeField] private Health _bossHealth;

    [Header("Views")]
    [SerializeField] private HealthBarView _bossHpBar;
    [SerializeField] private HeartView _playerHearts;
    [SerializeField] private TimerView _timer;
    [SerializeField] private ResultView _result;

    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[UIManager] 씬에 UIManager가 둘 이상 있습니다. {name}을(를) 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (_bossHpBar != null && _bossHealth != null)
        {
            _bossHpBar.Bind(_bossHealth);
        }

        if (_playerHearts != null && _playerHealth != null)
        {
            _playerHearts.Bind(_playerHealth);
        }

        GameManager _gameManager = GameManager.Instance;
        if (_gameManager != null)
        {
            if (_timer != null) _timer.Bind(_gameManager);
            if (_result != null) _result.Bind(_gameManager);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
