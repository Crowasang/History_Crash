using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 프리팹별 오브젝트 풀을 관리합니다. 씬에 하나만 둡니다.
/// Instantiate / Destroy 대신 Spawn / Release를 쓰면 됩니다.
/// - 풀링할 프리팹 루트에는 PooledObject 컴포넌트가 있어야 합니다.
/// - 프리팹별 풀은 처음 Spawn할 때 자동으로 만들어집니다.
/// </summary>
/// <example>
/// Mummy _mummy = PoolManager.Instance.Spawn(_mummyPrefab, _spawnPos, Quaternion.identity);
/// PoolManager.Instance.Release(_mummy.gameObject);
/// </example>
public class PoolManager : MonoBehaviour
{
    [Serializable]
    private class PrewarmEntry
    {
        [SerializeField] private PooledObject _prefab;
        [SerializeField] private int _count = 10;

        public PooledObject Prefab => _prefab;
        public int Count => _count;
    }

    [Tooltip("시작할 때 미리 만들어 둘 오브젝트 (첫 생성 렉 방지)")]
    [SerializeField] private List<PrewarmEntry> _prewarmList = new List<PrewarmEntry>();

    [Tooltip("프리팹별 풀이 보관하는 최대 개수. 넘으면 반납 시 파괴")]
    [SerializeField] private int _maxPoolSize = 200;

    private readonly Dictionary<PooledObject, ObjectPool<PooledObject>> _pools =
        new Dictionary<PooledObject, ObjectPool<PooledObject>>();

    public static PoolManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[PoolManager] 씬에 PoolManager가 둘 이상 있습니다. {name}을(를) 제거합니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Prewarm();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 풀에서 꺼내 지정한 위치에 활성화합니다. 풀이 비어 있으면 새로 만듭니다.
    /// </summary>
    public T Spawn<T>(T _prefab, Vector3 _position, Quaternion _rotation, Transform _parent = null)
        where T : Component
    {
        if (!_prefab.TryGetComponent(out PooledObject _pooledPrefab))
        {
            Debug.LogError($"[PoolManager] {_prefab.name} 프리팹 루트에 PooledObject가 없습니다.");
            return null;
        }

        PooledObject _instance = GetPool(_pooledPrefab).Get();

        Transform _transform = _instance.transform;
        _transform.SetParent(_parent != null ? _parent : transform, false);
        _transform.SetPositionAndRotation(_position, _rotation);

        _instance.gameObject.SetActive(true);
        _instance.NotifySpawned();

        return _instance.GetComponent<T>();
    }

    /// <summary>
    /// 풀로 반납합니다. 풀링 대상이 아니면 파괴합니다.
    /// </summary>
    public void Release(GameObject _target)
    {
        if (_target.TryGetComponent(out PooledObject _pooled))
        {
            _pooled.Release();
        }
        else
        {
            Destroy(_target);
        }
    }

    private ObjectPool<PooledObject> GetPool(PooledObject _prefab)
    {
        if (_pools.TryGetValue(_prefab, out ObjectPool<PooledObject> _pool))
        {
            return _pool;
        }

        _pool = null;
        _pool = new ObjectPool<PooledObject>(
            createFunc: () => CreateInstance(_prefab, _pool),
            actionOnGet: null,
            actionOnRelease: OnRelease,
            actionOnDestroy: _instance => Destroy(_instance.gameObject),
            collectionCheck: true,
            defaultCapacity: 10,
            maxSize: _maxPoolSize);

        _pools.Add(_prefab, _pool);
        return _pool;
    }

    private PooledObject CreateInstance(PooledObject _prefab, ObjectPool<PooledObject> _pool)
    {
        PooledObject _instance = Instantiate(_prefab, transform);
        _instance.gameObject.SetActive(false);
        _instance.Init(_pool);
        return _instance;
    }

    private void OnRelease(PooledObject _instance)
    {
        _instance.NotifyDespawned();
        _instance.gameObject.SetActive(false);
        _instance.transform.SetParent(transform, false);
    }

    private void Prewarm()
    {
        foreach (PrewarmEntry _entry in _prewarmList)
        {
            if (_entry.Prefab == null) continue;

            ObjectPool<PooledObject> _pool = GetPool(_entry.Prefab);
            List<PooledObject> _temp = new List<PooledObject>(_entry.Count);
            for (int _i = 0; _i < _entry.Count; _i++)
            {
                _temp.Add(_pool.Get());
            }
            foreach (PooledObject _instance in _temp)
            {
                _pool.Release(_instance);
            }
        }
    }
}
