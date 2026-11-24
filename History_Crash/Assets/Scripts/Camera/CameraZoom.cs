using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스 휠로 3인칭 카메라 거리를 조절합니다. (기획 v19: 2~12m)
/// Cinemachine Third Person Follow가 붙은 카메라에 둡니다.
/// 휠을 굴릴 때만 목표 거리를 바꾸고, 거리 변화는 DOTween이 부드럽게 처리합니다. (Update 없음)
/// </summary>
[RequireComponent(typeof(CinemachineThirdPersonFollow))]
public class CameraZoom : MonoBehaviour
{
    [SerializeField] private InputActionReference _zoomAction;
    [SerializeField] private float _minDistance = 2f;
    [SerializeField] private float _maxDistance = 12f;
    [Tooltip("휠 한 칸에 바뀌는 거리 (m)")]
    [SerializeField] private float _stepPerNotch = 1f;
    [Tooltip("거리가 바뀌는 시간 (초)")]
    [SerializeField] private float _smoothTime = 0.15f;

    private CinemachineThirdPersonFollow _follow;
    private float _targetDistance;
    private Tween _zoomTween;

    private void Awake()
    {
        _follow = GetComponent<CinemachineThirdPersonFollow>();
        _targetDistance = Mathf.Clamp(_follow.CameraDistance, _minDistance, _maxDistance);
    }

    private void OnEnable()
    {
        if (_zoomAction == null) return;

        _zoomAction.action.performed += OnZoom;
        _zoomAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_zoomAction != null)
        {
            _zoomAction.action.performed -= OnZoom;
            _zoomAction.action.Disable();
        }

        _zoomTween?.Kill();
    }

    // 휠을 굴릴 때만 호출됨 (위로 굴리면 가까이, 아래로 굴리면 멀리)
    private void OnZoom(InputAction.CallbackContext _context)
    {
        float _scroll = _context.ReadValue<float>();
        if (Mathf.Approximately(_scroll, 0f)) return;

        SetDistance(_targetDistance - Mathf.Sign(_scroll) * _stepPerNotch);
    }

    /// <summary>카메라 거리를 바꿉니다. (최소~최대 범위로 제한)</summary>
    public void SetDistance(float _distance)
    {
        _targetDistance = Mathf.Clamp(_distance, _minDistance, _maxDistance);

        _zoomTween?.Kill();
        _zoomTween = DOTween.To(() => _follow.CameraDistance, _value => _follow.CameraDistance = _value,
            _targetDistance, _smoothTime);
    }
}
