using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Player
{
    /// <summary>
    /// 모델은 항상 카메라가 보는 수평 방향을 바라보고,
    /// 이동 방향(모델 기준 앞/뒤/좌/우, 대각은 블렌드)과 접지 상태를 Animator에 넘긴다.
    /// 재생 배속은 이동 속도 / 방향별 클립 보폭 속도로 맞춰 발 미끄러짐을 줄인다.
    /// Animator 파라미터: MoveX(좌-우+), MoveZ(뒤-앞+), Speed(m/s), MoveAnimSpeed(배속), Grounded(bool).
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    [DefaultExecutionOrder(100)]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("animator")] private Animator _animator;
        [SerializeField, FormerlySerializedAs("faceCamera"), Tooltip("모델이 카메라 방향을 바라보게 함(PlayerMover의 이동 방향 회전을 덮어씀)")] private bool _faceCamera = true;

        [Header("클립 1배속일 때의 보폭 속도(m/s)")]
        [SerializeField, FormerlySerializedAs("forwardClipSpeed")] private float _forwardClipSpeed = 2.04f;
        [SerializeField, FormerlySerializedAs("backClipSpeed")] private float _backClipSpeed = 1.69f;
        [SerializeField, FormerlySerializedAs("strafeClipSpeed")] private float _strafeClipSpeed = 0.47f;
        [SerializeField, FormerlySerializedAs("maxAnimSpeed"), Tooltip("재생 배속 상한(너무 빠른 다리 움직임 방지)")] private float _maxAnimSpeed = 3.5f;
        [SerializeField, FormerlySerializedAs("dampTime"), Tooltip("방향 값 변화 부드럽게(초)")] private float _dampTime = 0.08f;

        private PlayerMover _mover;
        private static readonly int _speedId = Animator.StringToHash("Speed");
        private static readonly int _moveXId = Animator.StringToHash("MoveX");
        private static readonly int _moveZId = Animator.StringToHash("MoveZ");
        private static readonly int _moveAnimSpeedId = Animator.StringToHash("MoveAnimSpeed");
        private static readonly int _groundedId = Animator.StringToHash("Grounded");

        private void Awake()
        {
            _mover = GetComponent<PlayerMover>();
            if (!_animator && _mover.Model) _animator = _mover.Model.GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            Transform _model = _mover.Model;
            if (_faceCamera && _model && _mover.CameraTransform)
            {
                Vector3 _fwd = Vector3.ProjectOnPlane(_mover.CameraTransform.forward, Vector3.up);
                if (_fwd.sqrMagnitude > 1e-4f) _model.rotation = Quaternion.LookRotation(_fwd.normalized, Vector3.up);
            }
            if (!_animator) return;

            bool _grounded = _mover.IsGrounded && !_mover.IsFalling;
            Vector3 _v = _mover.Velocity; _v.y = 0f;
            float _speed = _mover.IsFalling ? 0f : _v.magnitude;
            Vector3 _local = _model ? _model.InverseTransformDirection(_v) : _v;
            _local.y = 0f;
            Vector2 _dir = _speed > 0.05f ? new Vector2(_local.x, _local.z).normalized : Vector2.zero;

            _animator.SetFloat(_moveXId, _dir.x, _dampTime, Time.deltaTime);
            _animator.SetFloat(_moveZId, _dir.y, _dampTime, Time.deltaTime);
            _animator.SetFloat(_speedId, _speed);
            _animator.SetBool(_groundedId, _grounded);

            float _animSpeed = 1f;
            if (_speed > 0.05f)
            {
                // 방향 가중치로 클립 보폭 속도를 섞는다
                float _fz = Mathf.Max(_dir.y, 0f), _bz = Mathf.Max(-_dir.y, 0f), _sx = Mathf.Abs(_dir.x);
                float _sum = _fz + _bz + _sx;
                float _clipSpeed = _sum > 0f ? (_fz * _forwardClipSpeed + _bz * _backClipSpeed + _sx * _strafeClipSpeed) / _sum : _forwardClipSpeed;
                _animSpeed = Mathf.Clamp(_speed / Mathf.Max(_clipSpeed, 0.01f), 0.5f, _maxAnimSpeed);
            }
            _animator.SetFloat(_moveAnimSpeedId, _animSpeed);
        }
    }
}
