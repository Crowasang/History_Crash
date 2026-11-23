using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

/// <summary>
/// 게임 흐름을 관리합니다. 씬에 하나만 둡니다. (상태 패턴)
/// - 상태: 준비 → 진행 → 클리어 / 실패
/// - 승패: 진행 중에 파라오가 죽으면 클리어, 플레이어가 죽으면 실패
/// - 경과 시간: 진행 상태에서만 흐르고, UI용으로 1초마다 알림
/// - 일시정지: 상태와 별개인 표시(IsPaused). 시간을 멈추고(timeScale 0), 풀면 원래 상태 그대로 이어감
///   준비·진행 상태에서만 가능하고, 게임이 끝나거나 씬이 바뀌면 자동으로 풀림
/// </summary>
/// <example>
/// // 다른 스크립트는 GameManager 대신 IGameFlow로 받습니다. (인스펙터에 GameManager 연결)
/// [SerializeField] private MonoBehaviour _gameFlowSource;
/// IGameFlow _gameFlow = InterfaceRef.Resolve&lt;IGameFlow&gt;(_gameFlowSource, this, nameof(_gameFlowSource));
///
/// // 진행 중일 때만 미라 리젠
/// _gameFlow.CurrentState
///     .Subscribe(_state => SetSpawning(_state == GameStateType.Playing))
///     .AddTo(this);
/// </example>
public class GameManager : MonoBehaviour, IGameFlow, IPauseControl
{
    [Header("Win / Lose")]
    [Tooltip("IHealthReadOnly를 구현한 컴포넌트 (Health)")]
    [SerializeField] private MonoBehaviour _playerHealth;
    [Tooltip("IHealthReadOnly를 구현한 컴포넌트 (Health)")]
    [SerializeField] private MonoBehaviour _bossHealth;

    [Header("Flow")]
    [Tooltip("시작 전 대기 시간 (초)")]
    [SerializeField] private float _readyDuration = 1f;
    [Tooltip("씬 시작 시 자동으로 준비 상태로 들어갈지")]
    [SerializeField] private bool _autoStart = true;

    private readonly ReactiveProperty<GameStateType> _currentState = new ReactiveProperty<GameStateType>(GameStateType.Ready);
    private readonly ReactiveProperty<int> _elapsedSeconds = new ReactiveProperty<int>(0);
    private readonly ReactiveProperty<bool> _isPaused = new ReactiveProperty<bool>(false);

    private Dictionary<GameStateType, GameState> _states;
    private GameState _state;

    private float _accumulatedTime;
    private float _timerStartTime;
    private bool _isTimerRunning;
    private CancellationTokenSource _timerCts;

    public static GameManager Instance { get; private set; }

    /// <summary>현재 상태. 구독하면 상태가 바뀔 때마다 알림을 받습니다.</summary>
    public ReadOnlyReactiveProperty<GameStateType> CurrentState => _currentState;

    /// <summary>경과 시간 (초, 정수). 1초마다 알림 (UI용)</summary>
    public ReadOnlyReactiveProperty<int> ElapsedSeconds => _elapsedSeconds;

    /// <summary>경과 시간 (초, 소수점). 진행 상태에서만 흐름</summary>
    public float ElapsedTime => _isTimerRunning ? _accumulatedTime + (Time.time - _timerStartTime) : _accumulatedTime;

    public bool IsPlaying => _currentState.Value == GameStateType.Playing;

    public float ReadyDuration => _readyDuration;

    /// <summary>일시정지 중인지. 구독하면 바뀔 때마다 알림을 받습니다.</summary>
    public ReadOnlyReactiveProperty<bool> IsPaused => _isPaused;

    /// <summary>플레이어 체력. 다른 스크립트의 Start보다 먼저 쓰일 수 있어서 처음 읽을 때 연결함</summary>
    public IHealthReadOnly PlayerHealth => InterfaceRef.Resolve<IHealthReadOnly>(_playerHealth, this, nameof(_playerHealth));

    /// <summary>보스(파라오) 체력</summary>
    public IHealthReadOnly BossHealth => InterfaceRef.Resolve<IHealthReadOnly>(_bossHealth, this, nameof(_bossHealth));

    /// <summary>준비·진행 상태에서만 일시정지할 수 있음</summary>
    public bool CanPause => _currentState.Value == GameStateType.Ready || _currentState.Value == GameStateType.Playing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[GameManager] 씬에 GameManager가 둘 이상 있습니다. {name}을(를) 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 새 상태를 추가하면 여기에 등록
        _states = new Dictionary<GameStateType, GameState>
        {
            { GameStateType.Ready, new ReadyState(this) },
            { GameStateType.Playing, new PlayingState(this) },
            { GameStateType.Clear, new ClearState(this) },
            { GameStateType.Fail, new FailState(this) }
        };
    }

    private void Start()
    {
        IHealthReadOnly _boss = InterfaceRef.Resolve<IHealthReadOnly>(_bossHealth, this, nameof(_bossHealth));
        if (_boss != null)
        {
            _boss.OnDeath
                .Where(_ => IsPlaying)
                .Subscribe(_ => ChangeState(GameStateType.Clear))
                .AddTo(this);
        }

        IHealthReadOnly _player = InterfaceRef.Resolve<IHealthReadOnly>(_playerHealth, this, nameof(_playerHealth));
        if (_player != null)
        {
            _player.OnDeath
                .Where(_ => IsPlaying)
                .Subscribe(_ => ChangeState(GameStateType.Fail))
                .AddTo(this);
        }

        if (_autoStart)
        {
            StartGame();
        }
    }

    private void OnDestroy()
    {
        // 일시정지 중에 씬이 바뀌어도(재시작 버튼 등) 다음 씬이 멈춘 채 시작하지 않도록 시간을 되돌림
        if (_isPaused.Value)
        {
            Time.timeScale = 1f;
        }

        _state?.Exit();
        CancelTimerLoop();
        _currentState.Dispose();
        _elapsedSeconds.Dispose();
        _isPaused.Dispose();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 처음부터 시작합니다. (경과 시간 초기화 후 준비 상태로)
    /// </summary>
    public void StartGame()
    {
        _accumulatedTime = 0f;
        _elapsedSeconds.Value = 0;

        // 이미 준비 상태여도 대기를 다시 시작하도록 강제로 진입
        EnterState(GameStateType.Ready);
    }

    /// <summary>
    /// 상태를 바꿉니다. 같은 상태로는 바뀌지 않습니다.
    /// </summary>
    public void ChangeState(GameStateType _next)
    {
        if (_state != null && _state.Type == _next) return;

        EnterState(_next);
    }

    public void Pause()
    {
        if (_isPaused.Value || !CanPause) return;

        Time.timeScale = 0f;
        _isPaused.Value = true;
    }

    public void Resume()
    {
        if (!_isPaused.Value) return;

        Time.timeScale = 1f;
        _isPaused.Value = false;
    }

    public void TogglePause()
    {
        if (_isPaused.Value) Resume();
        else Pause();
    }

    private void EnterState(GameStateType _next)
    {
        // 게임이 끝나는 등 상태가 바뀌면 일시정지는 풀고 들어감
        Resume();

        _state?.Exit();
        _state = _states[_next];
        _currentState.Value = _next;
        _state.Enter();
    }

    /// <summary>진행 상태가 호출합니다.</summary>
    internal void StartTimer()
    {
        _timerStartTime = Time.time;
        _isTimerRunning = true;

        CancelTimerLoop();
        _timerCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        TickSecondsAsync(_timerCts.Token).Forget();
    }

    /// <summary>진행 상태가 호출합니다.</summary>
    internal void StopTimer()
    {
        if (!_isTimerRunning) return;

        _accumulatedTime += Time.time - _timerStartTime;
        _isTimerRunning = false;
        _elapsedSeconds.Value = Mathf.FloorToInt(_accumulatedTime);
        CancelTimerLoop();
    }

    private async UniTaskVoid TickSecondsAsync(CancellationToken _token)
    {
        while (!_token.IsCancellationRequested)
        {
            bool _isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: _token)
                .SuppressCancellationThrow();
            if (_isCanceled) return;

            _elapsedSeconds.Value = Mathf.FloorToInt(ElapsedTime);
        }
    }

    private void CancelTimerLoop()
    {
        if (_timerCts == null) return;

        _timerCts.Cancel();
        _timerCts.Dispose();
        _timerCts = null;
    }
}
