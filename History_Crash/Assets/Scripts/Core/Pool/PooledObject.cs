using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 풀링할 프리팹의 루트에 붙이는 컴포넌트.
/// 자기가 속한 풀을 기억해서 Release()만 부르면 알아서 풀로 돌아갑니다.
/// </summary>
/// <example>
/// // 미라가 죽으면
/// GetComponent&lt;PooledObject&gt;().Release();
///
/// // 오브가 12초 뒤 사라지게
/// GetComponent&lt;PooledObject&gt;().ReleaseAfter(12f);
/// </example>
[DisallowMultipleComponent]
public class PooledObject : MonoBehaviour
{
    private IObjectPool<PooledObject> _pool;
    private IPoolable[] _poolables;
    private CancellationTokenSource _releaseCts;

    /// <summary>풀에서 꺼내져 사용 중인지</summary>
    public bool IsSpawned { get; private set; }

    /// <summary>PoolManager가 생성 직후 한 번 호출합니다.</summary>
    internal void Init(IObjectPool<PooledObject> _ownerPool)
    {
        _pool = _ownerPool;
        _poolables = GetComponentsInChildren<IPoolable>(true);
    }

    internal void NotifySpawned()
    {
        IsSpawned = true;
        foreach (IPoolable _poolable in _poolables)
        {
            _poolable.OnSpawned();
        }
    }

    internal void NotifyDespawned()
    {
        // 미리 만들어 두기(Prewarm)처럼 꺼낸 적 없이 반납되는 경우는 알리지 않음
        if (!IsSpawned) return;

        IsSpawned = false;
        CancelReleaseTimer();
        foreach (IPoolable _poolable in _poolables)
        {
            _poolable.OnDespawned();
        }
    }

    /// <summary>
    /// 풀로 반납합니다. 풀 없이 씬에 직접 놓인 오브젝트면 파괴합니다.
    /// 이미 반납된 상태에서 다시 불러도 안전합니다.
    /// </summary>
    public void Release()
    {
        if (_pool == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!IsSpawned) return;

        _pool.Release(this);
    }

    /// <summary>
    /// 지정한 시간 뒤에 반납합니다. (드랍 오브 12초, 탄막 수명 등)
    /// 다시 호출하면 타이머가 새로 시작되고, 그 전에 반납되면 타이머는 취소됩니다.
    /// </summary>
    public void ReleaseAfter(float _seconds)
    {
        CancelReleaseTimer();
        _releaseCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        ReleaseAfterAsync(_seconds, _releaseCts.Token).Forget();
    }

    private async UniTaskVoid ReleaseAfterAsync(float _seconds, CancellationToken _token)
    {
        bool _isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_seconds), cancellationToken: _token)
            .SuppressCancellationThrow();

        if (!_isCanceled)
        {
            Release();
        }
    }

    private void CancelReleaseTimer()
    {
        if (_releaseCts == null) return;

        _releaseCts.Cancel();
        _releaseCts.Dispose();
        _releaseCts = null;
    }

    private void OnDestroy()
    {
        CancelReleaseTimer();
    }
}
