/// <summary>
/// 풀에서 꺼내질 때 / 반납될 때 알림을 받고 싶은 컴포넌트가 구현합니다.
/// 같은 오브젝트(자식 포함)에 PooledObject가 있으면 자동으로 호출됩니다.
/// </summary>
/// <example>
/// public class Mummy : MonoBehaviour, IPoolable
/// {
///     public void OnSpawned() { /* 속성 랜덤 부여, 오브 드랍 루프 시작 */ }
///     public void OnDespawned() { /* 루프 정지 */ }
/// }
/// </example>
public interface IPoolable
{
    /// <summary>풀에서 꺼내져 활성화된 직후 (위치·회전이 이미 설정된 상태)</summary>
    void OnSpawned();

    /// <summary>풀로 반납되어 비활성화되기 직전</summary>
    void OnDespawned();
}
