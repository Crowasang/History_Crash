using UnityEngine;

/// <summary>
/// 시작할 때 지정한 대상(예: 파라오)을 바라보도록 시점을 돌립니다.
/// 같은 오브젝트의 ILookDirection(PlayerLook)을 사용합니다.
/// PlayerLook.Start보다 먼저 실행되어야 이 방향이 V(기본 시점)로 기억됩니다.
/// </summary>
[DefaultExecutionOrder(-10)]
public class FaceTargetOnStart : MonoBehaviour
{
    [Tooltip("시작할 때 바라볼 대상 (파라오 등)")]
    [SerializeField] private Transform _target;

    private void Start()
    {
        if (_target == null) return;

        ILookDirection _look = GetComponent<ILookDirection>();
        if (_look == null)
        {
            Debug.LogWarning($"[FaceTargetOnStart] {name}에 ILookDirection(PlayerLook 등)이 없습니다.", this);
            return;
        }

        _look.LookAt(_target.position);
    }
}
