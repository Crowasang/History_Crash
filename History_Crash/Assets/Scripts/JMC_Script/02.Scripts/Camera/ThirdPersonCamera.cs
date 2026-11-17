using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.CameraRig
{
    /// <summary>
    /// 3인칭 마우스 시점 카메라.
    /// - 마우스를 움직이면 시점이 돌고, Player는 그 방향을 기준으로 이동한다(커서 잠금).
    /// - Esc: 커서 풀기, 왼쪽 클릭: 다시 잠금.
    /// - Tab: 보스 락온 켜기/끄기(기본 꺼짐), Z/C: 45도 스냅(E는 줍기 키), R: 보스 쪽으로 스냅, V: 원래 고정 시점, 휠: 거리.
    /// - 벽 관통 방지: Player에서 카메라 쪽으로 SphereCast해서 막히면 앞으로 당긴다.
    ///   씬에 콜라이더가 없으므로 시작할 때 장애물 렌더러에 BoxCollider를 붙인다(Play 중에만).
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("target")] private Transform _target;
        [SerializeField, FormerlySerializedAs("lockTarget")] private Transform _lockTarget;
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        [SerializeField, FormerlySerializedAs("collisionRoots"), Tooltip("BoxCollider를 붙일 장애물 루트(비우면 grid의 obstacleRoots 사용)")] private Transform[] _collisionRoots;

        [Header("거리·각도")]
        [SerializeField, FormerlySerializedAs("distance")] private float _distance = 5f;
        [SerializeField, FormerlySerializedAs("minDistance")] private float _minDistance = 2f;
        [SerializeField, FormerlySerializedAs("maxDistance")] private float _maxDistance = 12f;
        [SerializeField, FormerlySerializedAs("pivotHeight"), Tooltip("Player 중심에서 바라보는 점까지 높이. 너무 높으면 벽에 막혀 가까워졌을 때 Player가 화면 밖으로 나감")] private float _pivotHeight = 0.35f;
        [SerializeField, FormerlySerializedAs("pitch")] private float _pitch = 18f;
        [SerializeField, FormerlySerializedAs("minPitch")] private float _minPitch = -10f;
        [SerializeField, FormerlySerializedAs("maxPitch")] private float _maxPitch = 70f;

        [Header("입력")]
        [SerializeField, FormerlySerializedAs("mouseSensitivity")] private float _mouseSensitivity = 0.12f;
        [SerializeField, FormerlySerializedAs("snapStep")] private float _snapStep = 45f;
        [SerializeField, FormerlySerializedAs("lockOn")] private bool _lockOn = false;

        [Header("부드럽게")]
        [SerializeField, FormerlySerializedAs("followSmooth")] private float _followSmooth = 0.06f;
        [SerializeField, FormerlySerializedAs("lockYawSmooth")] private float _lockYawSmooth = 0.15f;

        [Header("충돌")]
        [SerializeField, FormerlySerializedAs("collisionRadius")] private float _collisionRadius = 0.25f;
        [SerializeField, FormerlySerializedAs("collisionPadding")] private float _collisionPadding = 0.1f;
        [SerializeField, FormerlySerializedAs("minHeightAboveFloor")] private float _minHeightAboveFloor = 0.25f;

        private float _yaw, _yawVel, _distVel, _targetDistance, _currentDistance;
        private Vector3 _pivotVel, _smoothedPivot;
        private bool _overview;
        private Vector3 _overviewPos; private Quaternion _overviewRot;

        private void Start()
        {
            _overviewPos = transform.position; _overviewRot = transform.rotation;
            _targetDistance = _currentDistance = _distance;
            _yaw = transform.eulerAngles.y;
            if (_target)
            {
                _smoothedPivot = _target.position + Vector3.up * _pivotHeight;
                if (_lockTarget) _yaw = YawToward(_lockTarget.position);
            }
            BuildColliders();
            LockCursor(true);
        }

        private void BuildColliders()
        {
            var _roots = (_collisionRoots != null && _collisionRoots.Length > 0) ? _collisionRoots : (_grid ? _grid.ObstacleRoots : null);
            if (_roots == null) return;
            foreach (var _root in _roots)
            {
                if (!_root) continue;
                foreach (var _mf in _root.GetComponentsInChildren<MeshFilter>())
                {
                    if (_mf.GetComponent<Collider>() || !_mf.sharedMesh) continue;
                    var _bc = _mf.gameObject.AddComponent<BoxCollider>();
                    _bc.center = _mf.sharedMesh.bounds.center;
                    _bc.size = _mf.sharedMesh.bounds.size;
                }
            }
        }

        private static void LockCursor(bool _on)
        {
            Cursor.lockState = _on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_on;
        }

        private float YawToward(Vector3 _worldPos)
        {
            Vector3 _d = _worldPos - _target.position; _d.y = 0;
            if (_d.sqrMagnitude < 0.01f) return _yaw;
            return Mathf.Atan2(_d.x, _d.z) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            var _kb = Keyboard.current; var _mouse = Mouse.current;
            bool _cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            if (_kb != null)
            {
                if (_kb.escapeKey.wasPressedThisFrame) LockCursor(false);
                if (_kb.tabKey.wasPressedThisFrame) _lockOn = !_lockOn;
                if (_kb.vKey.wasPressedThisFrame) _overview = !_overview;
                if (_kb.rKey.wasPressedThisFrame && _target && _lockTarget) { _lockOn = false; _yaw = YawToward(_lockTarget.position); }
                // E는 아이템 줍기 키라 스냅은 Z/C로 옮김
                if (_kb.zKey.wasPressedThisFrame) { _lockOn = false; _yaw = Mathf.Round((_yaw - _snapStep) / _snapStep) * _snapStep; }
                if (_kb.cKey.wasPressedThisFrame) { _lockOn = false; _yaw = Mathf.Round((_yaw + _snapStep) / _snapStep) * _snapStep; }
            }
            if (_mouse != null)
            {
                if (!_cursorLocked && _mouse.leftButton.wasPressedThisFrame) LockCursor(true);
                if (_cursorLocked || _mouse.rightButton.isPressed)
                {
                    Vector2 _d = _mouse.delta.ReadValue();
                    if (_d.sqrMagnitude > 0.0001f)
                    {
                        if (Mathf.Abs(_d.x) > 0.5f) _lockOn = false;   // 마우스로 돌리면 락온 해제
                        _yaw += _d.x * _mouseSensitivity;
                        _pitch = Mathf.Clamp(_pitch - _d.y * _mouseSensitivity, _minPitch, _maxPitch);
                    }
                }
                float _scroll = _mouse.scroll.ReadValue().y;
                if (Mathf.Abs(_scroll) > 0.01f) _targetDistance = Mathf.Clamp(_targetDistance - Mathf.Sign(_scroll) * 0.7f, _minDistance, _maxDistance);
            }
        }

        private void LateUpdate()
        {
            if (_overview) { transform.SetPositionAndRotation(_overviewPos, _overviewRot); return; }
            if (!_target) return;

            if (_lockOn && _lockTarget) _yaw = Mathf.SmoothDampAngle(_yaw, YawToward(_lockTarget.position), ref _yawVel, _lockYawSmooth);
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _distVel, 0.1f);

            Vector3 _pivot = _target.position + Vector3.up * _pivotHeight;
            _smoothedPivot = Vector3.SmoothDamp(_smoothedPivot, _pivot, ref _pivotVel, _followSmooth);
            Quaternion _rot = Quaternion.Euler(_pitch, _yaw, 0);
            Vector3 _back = _rot * Vector3.back;

            // 벽 관통 방지: 막히면 바로 당기고, 풀리면 천천히 원래 거리로
            float _allowed = _distance;
            // 패턴 투사체 콜라이더(Ignore Raycast 레이어)는 제외하고 벽·기둥에만 반응
            if (Physics.SphereCast(_smoothedPivot, _collisionRadius, _back, out RaycastHit _hit, _distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                _allowed = Mathf.Max(0.3f, _hit.distance - _collisionPadding);
            _currentDistance = _allowed < _currentDistance ? _allowed : Mathf.Lerp(_currentDistance, _allowed, 1f - Mathf.Exp(-6f * Time.deltaTime));

            Vector3 _pos = _smoothedPivot + _back * _currentDistance;
            if (_grid) _pos.y = Mathf.Max(_pos.y, _grid.FloorTopY + _minHeightAboveFloor);
            transform.SetPositionAndRotation(_pos, _rot);
        }

        private void OnDisable() { LockCursor(false); }
    }
}
