using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System의 Move 액션(WASD / 방향키 / 게임패드 스틱)으로 플레이어를 이동시킵니다.
/// 입력이 바뀔 때만 Rigidbody 속도를 바꾸고, 이후 이동과 중력은 물리 엔진이 처리합니다.
/// 이동 방향은 월드 기준 XZ 평면입니다.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private float _moveSpeed = 5f;

    [Header("Debug")]
    [SerializeField] private float _forwardRayDistance = 10f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;

        // 바닥 마찰 때문에 속도가 줄지 않도록 마찰 0
        PhysicsMaterial noFriction = new PhysicsMaterial("NoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };
        GetComponent<CapsuleCollider>().material = noFriction;
    }

    private void OnEnable()
    {
        _moveAction.action.performed += OnMove;
        _moveAction.action.canceled += OnMove;
        _moveAction.action.Enable();
    }

    private void OnDisable()
    {
        _moveAction.action.performed -= OnMove;
        _moveAction.action.canceled -= OnMove;
        _moveAction.action.Disable();
    }

    // 입력이 바뀔 때만 호출됨 (키를 떼면 canceled로 (0, 0)이 들어옴)
    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);

        // 이동 방향으로 회전
        if (moveDir.sqrMagnitude > 0.01f)
        {
            _rb.MoveRotation(Quaternion.LookRotation(moveDir));
        }

        // 낙하 속도(y)는 유지하고 수평 속도만 바꿈
        Vector3 velocity = moveDir * _moveSpeed;
        velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = velocity;
    }

    // 바라보는 방향 표시: 맞으면 빨간색(맞은 지점까지), 안 맞으면 초록색
    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;
        Vector3 direction = transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, _forwardRayDistance))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, hit.point);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(origin, direction * _forwardRayDistance);
        }
    }
}
