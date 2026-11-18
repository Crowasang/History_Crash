/// <summary>
/// 플레이어 조작(이동·점프)을 잠깐 막는 기능. (아누비스 강제 이동, 연출 등)
/// 잠금 횟수를 세는 방식이라 여러 곳에서 동시에 잠가도, 모두 풀어야 다시 움직입니다.
/// 컴포넌트를 껐다 켜는 방식과 달리 겹쳐도 꺼진 채 굳지 않습니다. PlayerController가 구현합니다.
/// </summary>
/// <example>
/// _movementLock.Lock();
/// try { /* 연출 */ }
/// finally { _movementLock.Unlock(); }  // 중간에 취소돼도 반드시 풀기
/// </example>
public interface IMovementLock
{
    bool IsLocked { get; }

    void Lock();

    void Unlock();
}
