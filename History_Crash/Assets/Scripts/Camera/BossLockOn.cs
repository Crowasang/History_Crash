using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 보스 락온과 시점 스냅 (기획 v20).
/// - Tab: 락온 켜기/끄기 (기본 꺼짐). 켜져 있으면 좌우 시점이 보스를 부드럽게(0.15초) 따라감. 위아래는 마우스로 그대로 조절
/// - R: 보스 쪽으로 한 번 스냅
/// - V: 게임 시작 때의 시점으로 돌아감 (락온도 꺼짐)
/// 매 프레임 확인은 락온이 켜져 있는 동안에만 UniTask로 돌고, 꺼지면 멈춥니다. (Update 없음)
/// 같은 오브젝트(플레이어)에 ILookDirection(PlayerLook)이 있어야 합니다.
/// </summary>
public class BossLockOn : MonoBehaviour
{
    [SerializeField] private InputActionReference _lockOnAction;
    [SerializeField] private InputActionReference _snapToBossAction;
    [SerializeField] private InputActionReference _resetViewAction;
    [Tooltip("락온 대상 (파라오)")]
    [SerializeField] private Transform _target;
    [Tooltip("락온 중 좌우 시점이 대상을 따라가는 시간 (초, 기획 v20: 0.15)")]
    [SerializeField] private float _yawSmoothTime = 0.15f;

    private ILookDirection _look;
    private CancellationTokenSource _trackCts;
    private bool _isLockedOn;

    public bool IsLockedOn => _isLockedOn;

    /// <summary>락온이 켜지거나 꺼질 때 (UI 표시용)</summary>
    public event Action<bool> OnLockOnChanged;

    private void Awake()
    {
        _look = GetComponent<ILookDirection>();
        if (_look == null)
        {
            Debug.LogError($"[BossLockOn] {name}에 ILookDirection(PlayerLook 등)이 없습니다.", this);
        }
    }

    private void OnEnable()
    {
        Bind(_lockOnAction, OnLockOnPressed);
        Bind(_snapToBossAction, OnSnapToBossPressed);
        Bind(_resetViewAction, OnResetViewPressed);
    }

    private void OnDisable()
    {
        Unbind(_lockOnAction, OnLockOnPressed);
        Unbind(_snapToBossAction, OnSnapToBossPressed);
        Unbind(_resetViewAction, OnResetViewPressed);

        SetLockOn(false);
    }

    private static void Bind(InputActionReference _reference, Action<InputAction.CallbackContext> _callback)
    {
        if (_reference == null) return;

        _reference.action.performed += _callback;
        _reference.action.Enable();
    }

    private static void Unbind(InputActionReference _reference, Action<InputAction.CallbackContext> _callback)
    {
        if (_reference == null) return;

        _reference.action.performed -= _callback;
        _reference.action.Disable();
    }

    private void OnLockOnPressed(InputAction.CallbackContext _context)
    {
        SetLockOn(!_isLockedOn);
    }

    private void OnSnapToBossPressed(InputAction.CallbackContext _context)
    {
        SnapToTarget();
    }

    private void OnResetViewPressed(InputAction.CallbackContext _context)
    {
        SetLockOn(false);
        _look?.ResetToHomeView();
    }

    /// <summary>락온을 켜거나 끕니다. 대상이 없으면 켜지지 않습니다.</summary>
    public void SetLockOn(bool _isOn)
    {
        if (_isOn && (_target == null || _look == null)) return;
        if (_isLockedOn == _isOn) return;

        _isLockedOn = _isOn;
        _look.IsYawLocked = _isOn;

        _trackCts?.Cancel();
        _trackCts?.Dispose();
        _trackCts = null;

        if (_isOn)
        {
            _trackCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            TrackTargetAsync(_trackCts.Token).Forget();
        }

        OnLockOnChanged?.Invoke(_isOn);
    }

    /// <summary>보스 쪽으로 시점을 한 번 돌립니다. (R)</summary>
    public void SnapToTarget()
    {
        if (_target == null || _look == null) return;

        if (TryGetYawToTarget(out float _targetYaw))
        {
            _look.SetYaw(_targetYaw);
        }
    }

    // 락온이 켜져 있는 동안만 물리 프레임마다 좌우 시점을 대상 쪽으로 부드럽게 돌림
    private async UniTaskVoid TrackTargetAsync(CancellationToken _token)
    {
        float _yawVelocity = 0f;

        while (true)
        {
            if (_target == null)
            {
                SetLockOn(false);
                return;
            }

            if (TryGetYawToTarget(out float _targetYaw))
            {
                float _yaw = Mathf.SmoothDampAngle(_look.Yaw, _targetYaw, ref _yawVelocity,
                    _yawSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
                _look.SetYaw(_yaw);
            }

            bool _isCanceled = await UniTask.Yield(PlayerLoopTiming.FixedUpdate, _token)
                .SuppressCancellationThrow();
            if (_isCanceled) return;
        }
    }

    private bool TryGetYawToTarget(out float _yaw)
    {
        Vector3 _dir = _target.position - transform.position;
        _dir.y = 0f;

        if (_dir.sqrMagnitude < 0.0001f)
        {
            _yaw = 0f;
            return false;
        }

        _yaw = Quaternion.LookRotation(_dir).eulerAngles.y;
        return true;
    }
}
