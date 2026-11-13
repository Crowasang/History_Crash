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
public class PlayerLook : MonoBehaviour
{
    [SerializeField] private InputActionReference _lookAction;
    [Tooltip("Cinemachine 카메라가 따라가는 어깨 높이 기준점 (플레이어 자식)")]
    [SerializeField] private Transform _cameraTarget;
    [Tooltip("조준 계산에 쓸 카메라 (비우면 Main Camera)")]
    [SerializeField] private Camera _camera;

    [Header("Sensitivity")]
    [Tooltip("마우스 1픽셀당 회전 각도 (도)")]
    [SerializeField] private float _sensitivity = 0.1f;
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("Aim")]
    [Tooltip("조준 레이 최대 거리. 아무것도 안 맞으면 이 거리의 지점을 조준")]
    [SerializeField] private float _aimDistance = 100f;

    private Rigidbody _rb;
    private float _yaw;
    private float _pitch;

    /// <summary>좌우 시점 각도 (도). 이동 방향 계산에 사용</summary>
    public float Yaw => _yaw;

    /// <summary>화면 가운데가 가리키는 지점 (공격 방향 계산에 사용)</summary>
    public Vector3 AimPoint { get; private set; }

    /// <summary>시점이 바뀔 때마다 발생 (이동 방향 갱신용)</summary>
    public event Action OnLookChanged;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _yaw = transform.eulerAngles.y;

        if (_camera == null)
        {
            _camera = Camera.main;
        }
    }

    private void OnEnable()
    {
        _lookAction.action.performed += OnLook;
        _lookAction.action.Enable();

        // 게임 중에는 커서를 숨기고 화면에 고정 (에디터에서는 ESC로 해제)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        _lookAction.action.performed -= OnLook;
        _lookAction.action.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 마우스가 움직일 때만 호출됨
    private void OnLook(InputAction.CallbackContext _context)
    {
        Vector2 _delta = _context.ReadValue<Vector2>();

        _yaw += _delta.x * _sensitivity;
        _pitch = Mathf.Clamp(_pitch - _delta.y * _sensitivity, _minPitch, _maxPitch);

        _rb.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
        if (_cameraTarget != null)
        {
            _cameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        UpdateAimPoint();
        OnLookChanged?.Invoke();
    }

    private void UpdateAimPoint()
    {
        if (_camera == null) return;

        Ray _ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        AimPoint = _ray.GetPoint(_aimDistance);

        // 가장 가까운 지점부터 보되, 플레이어 자기 몸(자식 포함)은 건너뜀
        RaycastHit[] _hits = Physics.RaycastAll(_ray, _aimDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float _closest = float.MaxValue;
        foreach (RaycastHit _hit in _hits)
        {
            if (_hit.transform.IsChildOf(transform) || _hit.distance >= _closest) continue;

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
