using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

/// <summary>
/// 몬스터를 주기적으로 생성합니다. 씬에 하나만 둡니다.
/// - GameManager가 진행 상태일 때만 생성하고, 클리어하면 소환된 몹을 모두 풀로 돌려보냅니다.
/// - 생성은 PoolManager를 통해 합니다.
/// - 생성 규칙(프리팹, 주기, 위치)은 인스펙터의 Spawn Entries에 등록합니다.
/// </summary>
/// <example>
/// // 생성될 때마다 알림 받기 (예: 미라에 속성 부여)
/// SpawnManager.Instance.OnSpawned.Subscribe(_go => ...).AddTo(this);
/// </example>
public class SpawnManager : MonoBehaviour
{
    [SerializeField] private List<SpawnEntry> _spawnEntries = new List<SpawnEntry>();

    private readonly Subject<GameObject> _onSpawned = new Subject<GameObject>();
    private readonly Dictionary<SpawnEntry, List<PooledObject>> _spawnedByEntry =
        new Dictionary<SpawnEntry, List<PooledObject>>();
    private CancellationTokenSource _spawnCts;

    public static SpawnManager Instance { get; private set; }

    /// <summary>오브젝트가 생성될 때마다 발생</summary>
    public Observable<GameObject> OnSpawned => _onSpawned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[SpawnManager] 씬에 SpawnManager가 둘 이상 있습니다. {name}을(를) 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[SpawnManager] GameManager가 없어 바로 생성을 시작합니다.");
            StartSpawning();
            return;
        }

        GameManager.Instance.CurrentState
            .Subscribe(OnGameStateChanged)
            .AddTo(this);
    }

    private void OnDestroy()
    {
        StopSpawning();
        _onSpawned.Dispose();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnGameStateChanged(GameStateType _state)
    {
        if (_state == GameStateType.Playing)
        {
            StartSpawning();
            return;
        }

        StopSpawning();

        // 클리어 시 정화: 소환된 몹 소멸
        if (_state == GameStateType.Clear)
        {
            DespawnAll();
        }
    }

    /// <summary>등록된 모든 규칙의 생성 루프를 시작합니다.</summary>
    public void StartSpawning()
    {
        StopSpawning();
        _spawnCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        foreach (SpawnEntry _entry in _spawnEntries)
        {
            if (_entry.Prefab == null) continue;

            SpawnLoopAsync(_entry, _spawnCts.Token).Forget();
        }
    }

    /// <summary>모든 생성 루프를 멈춥니다. (이미 생성된 오브젝트는 그대로)</summary>
    public void StopSpawning()
    {
        if (_spawnCts == null) return;

        _spawnCts.Cancel();
        _spawnCts.Dispose();
        _spawnCts = null;
    }

    /// <summary>이 매니저가 생성한 오브젝트 중 살아 있는 것을 모두 풀로 돌려보냅니다.</summary>
    public void DespawnAll()
    {
        foreach (List<PooledObject> _list in _spawnedByEntry.Values)
        {
            foreach (PooledObject _pooled in _list.ToArray())
            {
                if (_pooled != null && _pooled.IsSpawned)
                {
                    _pooled.Release();
                }
            }
            _list.Clear();
        }
    }

    private async UniTaskVoid SpawnLoopAsync(SpawnEntry _entry, CancellationToken _token)
    {
        if (_entry.StartDelay > 0f)
        {
            bool _isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_entry.StartDelay), cancellationToken: _token)
                .SuppressCancellationThrow();
            if (_isCanceled) return;
        }

        while (!_token.IsCancellationRequested)
        {
            if (_entry.MaxAlive <= 0 || CountAlive(_entry) < _entry.MaxAlive)
            {
                Spawn(_entry);
            }

            bool _isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_entry.Interval), cancellationToken: _token)
                .SuppressCancellationThrow();
            if (_isCanceled) return;
        }
    }

    private void Spawn(SpawnEntry _entry)
    {
        if (PoolManager.Instance == null)
        {
            Debug.LogError("[SpawnManager] 씬에 PoolManager가 없습니다.");
            return;
        }

        Vector3 _position = _entry.PickPosition(transform.position);
        PooledObject _instance = PoolManager.Instance.Spawn(_entry.Prefab, _position, Quaternion.identity);
        if (_instance == null) return;

        CountAlive(_entry); // 반납된 항목 정리 (목록이 계속 커지지 않도록)
        GetSpawnedList(_entry).Add(_instance);
        _onSpawned.OnNext(_instance.gameObject);
    }

    // 반납된 오브젝트는 목록에서 정리하고 살아 있는 수를 돌려줌
    private int CountAlive(SpawnEntry _entry)
    {
        List<PooledObject> _list = GetSpawnedList(_entry);
        _list.RemoveAll(_pooled => _pooled == null || !_pooled.IsSpawned);
        return _list.Count;
    }

    private List<PooledObject> GetSpawnedList(SpawnEntry _entry)
    {
        if (!_spawnedByEntry.TryGetValue(_entry, out List<PooledObject> _list))
        {
            _list = new List<PooledObject>();
            _spawnedByEntry.Add(_entry, _list);
        }
        return _list;
    }
}
