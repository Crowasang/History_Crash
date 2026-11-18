using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 배틀그라운드식 3인칭 시점. Look 액션(마우스 움직임)으로 시점을 돌립니다.
/// - 좌우: 플레이어 몸을 돌림
/// - 상하: 카메라 기준점(CameraTarget)만 위아래로 기울임 (Cinemachine 3인칭 카메라가 이 기준점을 따라감)
/// - 조준: 화면 가운데에서 쏜 레이가 맞는 지점 (AimPoint)
/// 마우스가 움직일 때만 처리합니다. (Update 없음)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerLook : MonoBehaviour, ILookDirection
{
    [SerializeField] private InputActionReference _lookAction;
    [Tooltip("시점 왼쪽 스냅 (Z)")]
    [SerializeField] private InputActionReference _snapLeftAction;
    [Tooltip("시점 오른쪽 스냅 (C)")]
    [SerializeField] private InputActionReference _snapRightAction;
    [Tooltip("커서 풀기 (Esc)")]
    [SerializeField] private InputActionReference _cursorUnlockAction;
    [Tooltip("커서 다시 잠그기 (좌클릭)")]
    [SerializeField] private InputActionReference _cursorLockAction;
    [Tooltip("Cinemachine 카메라가 따라가는 어깨 높이 기준점 (플레이어 자식)")]
    [SerializeField] private Transform _cameraTarget;
    [Tooltip("조준 계산에 쓸 카메라 (비우면 Main Camera)")]
    [SerializeField] private Camera _camera;

    [Header("Sensitivity")]
    [Tooltip("마우스 1픽셀당 회전 각도 (도, 기획 v20: 0.12)")]
    [SerializeField] private float _sensitivity = 0.12f;
    [Tooltip("위아래 각도 범위 (도, +가 내려다봄, 기획 v20: -10~70)")]
    [SerializeField] private float _minPitch = -10f;
    [SerializeField] private float _maxPitch = 70f;
    [Tooltip("시작할 때 위아래 각도 (도, 기획 v20: 18)")]
    [SerializeField] private float _defaultPitch = 18f;
    [Tooltip("Z/C 스냅 한 번에 도는 각도 (기획 v20: 45)")]
    [SerializeField] private float _snapAngle = 45f;

    [Header("Aim")]
    [Tooltip("조준 레이 최대 거리. 아무것도 안 맞으면 이 거리의 지점을 조준")]
    [SerializeField] private float _aimDistance = 100f;

    // 조준 레이 결과를 담는 배열. 마우스가 움직일 때마다 새로 만들지 않고 재사용 (GC 방지)
    private const int AIM_HIT_BUFFER_SIZE = 16;
    private readonly RaycastHit[] _aimHits = new RaycastHit[AIM_HIT_BUFFER_SIZE];

    private Rigidbody _rb;
    private float _yaw;
    private float _pitch;
    private float _homeYaw;
    private float _homePitch;

    /// <summary>좌우 시점 각도 (도). 이동 방향 계산에 사용</summary>
    public float Yaw => _yaw;

    /// <summary>true면 마우스 좌우 회전과 Z/C 스냅을 무시 (락온 중)</summary>
    public bool IsYawLocked { get; set; }

    /// <summary>화면 가운데가 가리키는 지점 (공격 방향 계산에 사용)</summary>
    public Vector3 AimPoint { get; private set; }

    /// <summary>시점이 바뀔 때마다 발생 (이동 방향 갱신용)</summary>
    public event Action OnLookChanged;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _yaw = transform.eulerAngles.y;
        _pitch = Mathf.Clamp(_defaultPitch, _minPitch, _maxPitch);
        ApplyPitch();

        if (_camera == null)
        {
            _camera = Camera.main;
        }
    }

    // 시작 방향(FaceTargetOnStart 등)이 정해진 뒤의 시점을 V로 돌아갈 기본 시점으로 기억
    private void Start()
    {
        _homeYaw = _yaw;
        _homePitch = _pitch;
    }

    private void OnEnable()
    {
        _lookAction.action.performed += OnLook;
        _lookAction.action.Enable();

        if (_snapLeftAction != null)
        {
            _snapLeftAction.action.performed += OnSnapLeft;
            _snapLeftAction.action.Enable();
        }

        if (_snapRightAction != null)
        {
            _snapRightAction.action.performed += OnSnapRight;
            _snapRightAction.action.Enable();
        }

        if (_cursorUnlockAction != null)
        {
            _cursorUnlockAction.action.performed += OnCursorUnlock;
            _cursorUnlockAction.action.Enable();
        }

        if (_cursorLockAction != null)
        {
            _cursorLockAction.action.performed += OnCursorLock;
            _cursorLockAction.action.Enable();
        }

        // 게임 중에는 커서를 숨기고 화면에 고정 (Esc로 풀고, 좌클릭으로 다시 잠금)
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        _lookAction.action.performed -= OnLook;
        _lookAction.action.Disable();

        if (_snapLeftAction != null)
        {
            _snapLeftAction.action.performed -= OnSnapLeft;
            _snapLeftAction.action.Disable();
        }

        if (_snapRightAction != null)
        {
            _snapRightAction.action.performed -= OnSnapRight;
            _snapRightAction.action.Disable();
        }

        if (_cursorUnlockAction != null)
        {
            _cursorUnlockAction.action.performed -= OnCursorUnlock;
            _cursorUnlockAction.action.Disable();
        }

        if (_cursorLockAction != null)
        {
            _cursorLockAction.action.performed -= OnCursorLock;
            _cursorLockAction.action.Disable();
        }

        SetCursorLocked(false);
    }

    // Esc를 누를 때만 호출됨
    private void OnCursorUnlock(InputAction.CallbackContext _context)
    {
        SetCursorLocked(false);
    }

    // 좌클릭할 때만 호출됨
    private void OnCursorLock(InputAction.CallbackContext _context)
    {
        SetCursorLocked(true);
    }

    private static void SetCursorLocked(bool _isLocked)
    {
        Cursor.lockState = _isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !_isLocked;
    }

    // 마우스가 움직일 때만 호출됨
    private void OnLook(InputAction.CallbackContext _context)
    {
        // 커서가 풀려 있으면(Esc) 시점을 돌리지 않음
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 _delta = _context.ReadValue<Vector2>();

        // 락온 중에는 좌우는 락온이 정하고, 위아래만 마우스로 움직임
        if (!IsYawLocked)
        {
            _yaw += _delta.x * _sensitivity;
        }
        _pitch = Mathf.Clamp(_pitch - _delta.y * _sensitivity, _minPitch, _maxPitch);

        _rb.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
        ApplyPitch();

        UpdateAimPoint();
        OnLookChanged?.Invoke();
    }

    // Z / C를 누를 때만 호출됨
    private void OnSnapLeft(InputAction.CallbackContext _context)
    {
        SnapYaw(-1);
    }

    private void OnSnapRight(InputAction.CallbackContext _context)
    {
        SnapYaw(1);
    }

    /// <summary>
    /// 좌우 시점을 스냅 각도 단위로 돌립니다. 지금 각도를 가장 가까운 45° 단위에 맞춘 뒤 한 칸 돌림.
    /// </summary>
    public void SnapYaw(int _direction)
    {
        if (IsYawLocked) return;

        SetYaw(Mathf.Round(_yaw / _snapAngle) * _snapAngle + _direction * _snapAngle);
    }

    /// <summary>좌우 시점 각도를 직접 정합니다. (락온 추적, 스냅)</summary>
    public void SetYaw(float _newYaw)
    {
        _yaw = _newYaw;

        _rb.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
        UpdateAimPoint();
        OnLookChanged?.Invoke();
    }

    /// <summary>게임 시작 때의 시점으로 돌아갑니다. (V)</summary>
    public void ResetToHomeView()
    {
        _pitch = _homePitch;
        ApplyPitch();
        SetYaw(_homeYaw);
    }

    /// <summary>지정한 위치를 수평으로 바라보도록 시점을 돌립니다. (위아래 각도는 기본 각도로 초기화)</summary>
    public void LookAt(Vector3 _worldPoint)
    {
        Vector3 _dir = _worldPoint - transform.position;
        _dir.y = 0f;
        if (_dir.sqrMagnitude < 0.0001f) return;

        _yaw = Quaternion.LookRotation(_dir).eulerAngles.y;
        _pitch = Mathf.Clamp(_defaultPitch, _minPitch, _maxPitch);

        // 즉시 돌림 (시작 시점에는 보간 없이 바로 그 방향이어야 함)
        Quaternion _rotation = Quaternion.Euler(0f, _yaw, 0f);
        transform.rotation = _rotation;
        _rb.rotation = _rotation;
        ApplyPitch();

        UpdateAimPoint();
        OnLookChanged?.Invoke();
    }

    private void ApplyPitch()
    {
        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    private void UpdateAimPoint()
    {
        if (_camera == null) return;

        Ray _ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        AimPoint = _ray.GetPoint(_aimDistance);

        // 가장 가까운 지점을 고르되, 플레이어 자기 몸(자식 포함)은 건너뜀
        int _hitCount = Physics.RaycastNonAlloc(_ray, _aimHits, _aimDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float _closest = float.MaxValue;
        for (int _i = 0; _i < _hitCount; _i++)
        {
            RaycastHit _hit = _aimHits[_i];
            if (_hit.distance >= _closest || _hit.transform.IsChildOf(transform)) continue;

            _closest = _hit.distance;
            AimPoint = _hit.point;
        }
    }

    // 조준 지점 표시 (노란 구)
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(AimPoint, 0.2f);
    }
}
