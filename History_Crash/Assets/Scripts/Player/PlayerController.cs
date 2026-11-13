using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System의 Move 액션(WASD / 방향키 / 게임패드 스틱)으로 플레이어를 이동시킵니다.
/// 이동 방향은 시점(PlayerLook) 기준입니다. W = 보고 있는 방향.
/// 입력이나 시점이 바뀔 때만 Rigidbody 속도를 바꾸고, 이후 이동과 중력은 물리 엔진이 처리합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerLook))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputActionReference _moveAction;
    [Tooltip("이동 속도 (타일/초)")]
    [SerializeField] private float _moveSpeed = 3f;

    [Header("Debug")]
    [SerializeField] private float _forwardRayDistance = 10f;

    private Rigidbody _rb;
    private PlayerLook _look;
    private Vector2 _moveInput;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _look = GetComponent<PlayerLook>();

        // 바닥 마찰 때문에 속도가 줄지 않도록 마찰 0
        PhysicsMaterial _noFriction = new PhysicsMaterial("NoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };
        GetComponent<CapsuleCollider>().material = _noFriction;
    }

    private void OnEnable()
    {
        _moveAction.action.performed += OnMove;
        _moveAction.action.canceled += OnMove;
        _moveAction.action.Enable();
        _look.OnLookChanged += ApplyVelocity;
    }

    private void OnDisable()
    {
        _moveAction.action.performed -= OnMove;
        _moveAction.action.canceled -= OnMove;
        _moveAction.action.Disable();
        _look.OnLookChanged -= ApplyVelocity;
        _moveInput = Vector2.zero;
    }

    // 입력이 바뀔 때만 호출됨 (키를 떼면 canceled로 (0, 0)이 들어옴)
    private void OnMove(InputAction.CallbackContext _context)
    {
        _moveInput = _context.ReadValue<Vector2>();
        ApplyVelocity();
    }

    // 입력이나 시점이 바뀔 때 이동 속도를 다시 계산
    private void ApplyVelocity()
    {
        // 시점의 좌우 각도 기준으로 입력을 돌림 (W = 보고 있는 방향)
        Vector3 _moveDir = Quaternion.Euler(0f, _look.Yaw, 0f) * new Vector3(_moveInput.x, 0f, _moveInput.y);

        // 낙하 속도(y)는 유지하고 수평 속도만 바꿈
        Vector3 _velocity = _moveDir * _moveSpeed;
        _velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = _velocity;
    }

    // 바라보는 방향 표시: 맞으면 빨간색(맞은 지점까지), 안 맞으면 초록색
    private void OnDrawGizmos()
    {
        Vector3 _origin = transform.position;
        Vector3 _direction = transform.forward;

        if (Physics.Raycast(_origin, _direction, out RaycastHit _hit, _forwardRayDistance))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(_origin, _hit.point);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(_origin, _direction * _forwardRayDistance);
        }
    }
}
