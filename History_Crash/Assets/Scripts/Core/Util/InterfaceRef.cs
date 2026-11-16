using UnityEngine;

/// <summary>
/// 인스펙터에 연결한 컴포넌트를 인터페이스로 꺼내는 도우미.
/// Unity 인스펙터는 인터페이스 타입 필드를 보여주지 못해서, 필드는 MonoBehaviour로 두고 쓸 때 인터페이스로 바꿉니다.
/// </summary>
/// <example>
/// [SerializeField] private MonoBehaviour _bossHealth;   // Health 등 IHealthReadOnly를 구현한 컴포넌트를 연결
///
/// IHealthReadOnly _boss = InterfaceRef.Resolve&lt;IHealthReadOnly&gt;(_bossHealth, this, nameof(_bossHealth));
/// </example>
public static class InterfaceRef
{
    /// <summary>
    /// 연결된 컴포넌트를 T로 바꿔 돌려줍니다.
    /// 비어 있으면 null, T를 구현하지 않은 컴포넌트가 연결되어 있으면 에러 로그 후 null.
    /// </summary>
    public static T Resolve<T>(MonoBehaviour _source, Object _owner, string _fieldName) where T : class
    {
        if (_source == null) return null;

        if (_source is T _result) return _result;

        Debug.LogError($"[{_owner.name}] {_fieldName}에 연결된 {_source.GetType().Name}은(는) {typeof(T).Name}을(를) 구현하지 않습니다.", _owner);
        return null;
    }
}
