using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System의 Move 액션(WASD / 방향키 / 게임패드 스틱)으로 플레이어를 이동시키고, Jump 액션(Space)으로 점프합니다.
/// 이동 방향은 시점(ILookDirection, 보통 PlayerLook) 기준입니다. W = 보고 있는 방향.
/// 입력이나 시점이 바뀔 때만 Rigidbody 속도를 바꾸고, 이후 이동과 중력은 물리 엔진이 처리합니다.
/// - 중력: 프로젝트 물리 설정 대신 ConstantForce로 이 플레이어에게만 적용 (기본 25, 기획 v19)
/// - 점프: 바닥에 있을 때만, 높이 1.1m
/// - 착지: 매 프레임 확인하지 않고 바닥에 닿는 충돌 이벤트로 감지
/// 같은 오브젝트에 ILookDirection을 구현한 컴포넌트가 있어야 합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour, IJumpState, IPlayerMotion, ISpeedModifier, IPlayerTeleport, IMovementLock
{
    /// <summary>이 높이(m) 이상 떠 있으면 점프 회피로 판정 (기획 v19)</summary>
    public const float JUMP_DODGE_HEIGHT = 0.45f;

    private const float GROUND_CHECK_DISTANCE = 0.1f;
    private const float HEIGHT_CHECK_MAX = 20f;

    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _jumpAction;
    [SerializeField] private InputActionReference _sprintAction;
    [Tooltip("이동 속도 (m/초, 기획 v19·v20: 4)")]
    [SerializeField] private float _moveSpeed = 4f;
    [Tooltip("달리기(Shift) 속도 배율 (기획 v19: 1.6)")]
    [SerializeField] private float _runMultiplier = 1.6f;

    [Header("Jump")]
    [Tooltip("점프 높이 (m)")]
    [SerializeField] private float _jumpHeight = 1.1f;
    [Tooltip("플레이어에게 적용할 중력 (m/초²)")]
    [SerializeField] private float _gravity = 25f;
    [Tooltip("공중에서 이동 속도 배율")]
    [Range(0f, 1f)] [SerializeField] private float _airControl = 0.85f;
    [Tooltip("바닥으로 인식할 레이어")]
    [SerializeField] private LayerMask _groundMask = ~0;

    [Header("Debug")]
    [SerializeField] private float _forwardRayDistance = 10f;

    private Rigidbody _rb;
    private CapsuleCollider _capsule;
    private ILookDirection _look;
    private Vector2 _moveInput;
    private bool _isAirborne;
    private bool _isRunning;
    private float _speedMultiplier = 1f;
    private int _lockCount;

    public bool IsGrounded => CheckGrounded();

    /// <summary>조작이 잠겨 있는지 (강제 이동 등)</summary>
    public bool IsLocked => _lockCount > 0;

    /// <summary>이동·점프 입력을 막습니다. 반드시 같은 횟수만큼 Unlock()을 불러야 합니다.</summary>
    public void Lock()
    {
        _lockCount++;
    }

    /// <summary>잠금을 하나 풉니다. 모두 풀리면 누르고 있던 이동 입력이 바로 다시 적용됩니다.</summary>
    public void Unlock()
    {
        if (_lockCount == 0) return;

        _lockCount--;
        if (_lockCount == 0) ApplyVelocity();
    }

    public float SpeedMultiplier
    {
        get => _speedMultiplier;
        set
        {
            _speedMultiplier = Mathf.Max(0f, value);
            if (_rb == null) return;

            ApplyVelocity();
            NotifyMoveChanged();
        }
    }

    /// <summary>달리기 여부와 속도 배율로 계산한 지상 이동 속도 (m/초)</summary>
    private float CurrentGroundSpeed => _moveSpeed * (_isRunning ? _runMultiplier : 1f) * _speedMultiplier;

    public event Action<Vector2, float> OnMoveChanged;
    public event Action OnJumped;
    public event Action OnLanded;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _capsule = GetComponent<CapsuleCollider>();

        _look = GetComponent<ILookDirection>();
        if (_look == null)
        {
            Debug.LogError($"[PlayerController] {name}에 ILookDirection(PlayerLook 등) 컴포넌트가 없습니다.", this);
        }

        // 바닥 마찰 때문에 속도가 줄지 않도록 마찰 0
        PhysicsMaterial _noFriction = new PhysicsMaterial("NoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };
        _capsule.material = _noFriction;

        ApplyGravity();
    }

    private void OnEnable()
    {
        _moveAction.action.performed += OnMove;
        _moveAction.action.canceled += OnMove;
        _moveAction.action.Enable();

        if (_jumpAction != null)
        {
            _jumpAction.action.performed += OnJump;
            _jumpAction.action.Enable();
        }

        if (_sprintAction != null)
        {
            _sprintAction.action.performed += OnSprint;
            _sprintAction.action.canceled += OnSprint;
            _sprintAction.action.Enable();
        }

        if (_look != null) _look.OnLookChanged += ApplyVelocity;
    }

    private void OnDisable()
    {
        _moveAction.action.performed -= OnMove;
        _moveAction.action.canceled -= OnMove;
        _moveAction.action.Disable();

        if (_jumpAction != null)
        {
            _jumpAction.action.performed -= OnJump;
            _jumpAction.action.Disable();
        }

        if (_sprintAction != null)
        {
            _sprintAction.action.performed -= OnSprint;
            _sprintAction.action.canceled -= OnSprint;
            _sprintAction.action.Disable();
        }

        if (_look != null) _look.OnLookChanged -= ApplyVelocity;
        _moveInput = Vector2.zero;
        _isRunning = false;
        OnMoveChanged?.Invoke(Vector2.zero, 0f);

        // 조작이 꺼지면(게임 종료 등) 미끄러지지 않도록 수평 속도를 멈춤
        if (_rb != null)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
    }

    // 입력이 바뀔 때만 호출됨 (키를 떼면 canceled로 (0, 0)이 들어옴)
    private void OnMove(InputAction.CallbackContext _context)
    {
        _moveInput = _context.ReadValue<Vector2>();
        ApplyVelocity();
        NotifyMoveChanged();
    }

    // Shift를 누르거나 뗄 때만 호출됨
    private void OnSprint(InputAction.CallbackContext _context)
    {
        _isRunning = _context.ReadValueAsButton();
        ApplyVelocity();
        NotifyMoveChanged();
    }

    private void NotifyMoveChanged()
    {
        // 몸이 항상 보는 방향을 향하므로 입력값이 곧 몸 기준 이동 방향
        OnMoveChanged?.Invoke(_moveInput, CurrentGroundSpeed * Mathf.Clamp01(_moveInput.magnitude));
    }

    // Space를 누를 때만 호출됨
    private void OnJump(InputAction.CallbackContext _context)
    {
        Jump();
    }

    /// <summary>바닥에 있으면 점프합니다. (입력 외에 기믹·연출에서도 호출 가능)</summary>
    public bool Jump()
    {
        if (IsLocked || !CheckGrounded()) return false;

        // 원하는 높이 h에 도달하는 초기 속도 = √(2·g·h)
        Vector3 _velocity = _rb.linearVelocity;
        _velocity.y = Mathf.Sqrt(2f * _gravity * _jumpHeight);
        _rb.linearVelocity = _velocity;

        _isAirborne = true;
        OnJumped?.Invoke();
        return true;
    }

    // 바닥에 닿는 순간 착지 처리 (매 프레임 확인하지 않음)
    private void OnCollisionEnter(Collision _collision)
    {
        TryLand();
    }

    // 벽에 먼저 닿은 채로 내려앉는 경우 대비 (공중일 때만 확인)
    private void OnCollisionStay(Collision _collision)
    {
        if (_isAirborne) TryLand();
    }

    private void TryLand()
    {
        if (!_isAirborne || _rb.linearVelocity.y > 0.01f || !CheckGrounded()) return;

        _isAirborne = false;
        ApplyVelocity(); // 공중 이동 감속 해제
        OnLanded?.Invoke();
    }

    // 입력이나 시점이 바뀔 때 이동 속도를 다시 계산
    private void ApplyVelocity()
    {
        // 잠겨 있으면(강제 이동 중) 속도를 건드리지 않음
        if (IsLocked) return;

        // 시점의 좌우 각도 기준으로 입력을 돌림 (W = 보고 있는 방향)
        float _yaw = _look != null ? _look.Yaw : transform.eulerAngles.y;
        Vector3 _moveDir = Quaternion.Euler(0f, _yaw, 0f) * new Vector3(_moveInput.x, 0f, _moveInput.y);

        // 공중에서는 이동 효과를 줄임
        bool _isInAir = _isAirborne || !CheckGrounded();
        float _speed = _isInAir ? CurrentGroundSpeed * _airControl : CurrentGroundSpeed;

        // 낙하 속도(y)는 유지하고 수평 속도만 바꿈
        Vector3 _velocity = _moveDir * _speed;
        _velocity.y = _rb.linearVelocity.y;
        _rb.linearVelocity = _velocity;
    }

    // 프로젝트 물리 설정(9.81)은 그대로 두고 이 플레이어에게만 중력 적용
    private void ApplyGravity()
    {
        _rb.useGravity = false;

        if (!TryGetComponent(out ConstantForce _constantForce))
        {
            _constantForce = gameObject.AddComponent<ConstantForce>();
        }
        _constantForce.force = Vector3.down * _gravity * _rb.mass;
    }

    public float PivotHeightAboveFeet
    {
        get
        {
            GetCapsuleWorld(out Vector3 _center, out float _halfHeight, out float _);
            return transform.position.y - (_center.y - _halfHeight);
        }
    }

    /// <summary>
    /// 지정한 위치로 즉시 옮기고 바닥에 선 상태로 만듭니다. (낙하 복귀, 아누비스 강제 이동 도착)
    /// 떨어지던 속도와 점프 상태는 끊고, 누르고 있던 이동 입력은 그대로 이어집니다.
    /// </summary>
    public void TeleportTo(Vector3 _position)
    {
        _rb.position = _position;
        transform.position = _position;
        _rb.linearVelocity = Vector3.zero;

        bool _wasAirborne = _isAirborne;
        _isAirborne = false;
        ApplyVelocity();

        if (_wasAirborne) OnLanded?.Invoke();
    }

    public float GetHeightAboveGround()
    {
        GetCapsuleWorld(out Vector3 _center, out float _halfHeight, out float _);
        Vector3 _feet = _center + Vector3.down * _halfHeight;

        // 발보다 살짝 위에서 쏴서, 바닥에 딱 붙어 있어도 0으로 잡히게 함
        const float START_OFFSET = 0.05f;
        if (Physics.Raycast(_feet + Vector3.up * START_OFFSET, Vector3.down, out RaycastHit _hit,
                HEIGHT_CHECK_MAX, _groundMask, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(0f, _hit.distance - START_OFFSET);
        }

        return float.PositiveInfinity;
    }

    public bool IsDodgingByJump()
    {
        return GetHeightAboveGround() >= JUMP_DODGE_HEIGHT;
    }

    private bool CheckGrounded()
    {
        GetCapsuleWorld(out Vector3 _center, out float _halfHeight, out float _radius);

        // 캡슐 중심에서 아래로 구를 쏴서 발밑에 바닥이 있는지 확인 (자기 콜라이더 안에서 시작하므로 자기 자신은 안 맞음)
        float _castDistance = _halfHeight - _radius + GROUND_CHECK_DISTANCE;
        return Physics.SphereCast(_center, _radius * 0.95f, Vector3.down, out RaycastHit _,
            _castDistance, _groundMask, QueryTriggerInteraction.Ignore);
    }

    private void GetCapsuleWorld(out Vector3 _center, out float _halfHeight, out float _radius)
    {
        Vector3 _scale = transform.lossyScale;
        _center = transform.TransformPoint(_capsule.center);
        _halfHeight = _capsule.height * 0.5f * Mathf.Abs(_scale.y);
        _radius = _capsule.radius * Mathf.Max(Mathf.Abs(_scale.x), Mathf.Abs(_scale.z));
        _halfHeight = Mathf.Max(_halfHeight, _radius);
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
