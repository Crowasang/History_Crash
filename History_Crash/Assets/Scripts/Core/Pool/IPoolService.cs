using UnityEngine;

/// <summary>
/// 오브젝트를 풀에서 꺼내고 반납하는 기능. PoolManager가 구현합니다.
/// 생성하는 쪽(스포너, 드랍, 탄막)은 PoolManager 대신 이 인터페이스를 씁니다.
/// </summary>
public interface IPoolService
{
    /// <summary>풀에서 꺼내 지정한 위치에 활성화합니다. (프리팹 루트에 PooledObject 필요)</summary>
    T Spawn<T>(T _prefab, Vector3 _position, Quaternion _rotation, Transform _parent = null) where T : Component;

    /// <summary>풀로 반납합니다. 풀링 대상이 아니면 파괴합니다.</summary>
    void Release(GameObject _target);
}
