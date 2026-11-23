using UnityEngine;
using UnityEngine.InputSystem;
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
    /// WASD/방향키로 카메라(마우스 시점) 기준 이동, Shift 달리기, Space 점프.
    /// 걷기 가능한 타일 밖으로는 못 나간다(축별로 미끄러짐).
    /// </summary>
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        public TileGrid Grid { get => _grid; set => _grid = value; }
        [SerializeField, FormerlySerializedAs("cameraTransform")] private Transform _cameraTransform;
        public Transform CameraTransform { get => _cameraTransform; set => _cameraTransform = value; }
        [SerializeField, FormerlySerializedAs("model"), Tooltip("이동 방향으로 돌릴 모델(없으면 회전 안 함)")] private Transform _model;
        public Transform Model { get => _model; set => _model = value; }
        [SerializeField, FormerlySerializedAs("moveSpeed")] private float _moveSpeed = 4f;
        public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }
        [SerializeField, FormerlySerializedAs("runMultiplier")] private float _runMultiplier = 1.6f;
        [SerializeField, FormerlySerializedAs("turnSpeed")] private float _turnSpeed = 720f;
        [SerializeField, FormerlySerializedAs("speedScale"), Tooltip("외부(시전 등)에서 거는 이동 속도 배율")] private float _speedScale = 1f;
        public float SpeedScale { get => _speedScale; set => _speedScale = value; }
        /// <summary>이번 프레임에 이동 입력이 있는지</summary>
        public bool HasMoveInput { get; private set; }
        [SerializeField, FormerlySerializedAs("radius"), Tooltip("벽 판정용 반경(m)")] private float _radius = 0.25f;

        [Header("점프")]
        [SerializeField, FormerlySerializedAs("jumpHeight")] private float _jumpHeight = 1.1f;
        [SerializeField, FormerlySerializedAs("gravity")] private float _gravity = 25f;
        [SerializeField, FormerlySerializedAs("airControl"), Tooltip("공중에서 이동 속도 배율")] private float _airControl = 0.85f;

        [Header("낙하(깨진 타일)")]
        [SerializeField, FormerlySerializedAs("fallDepth"), Tooltip("구멍으로 이만큼 떨어지면 데미지 후 복귀")] private float _fallDepth = 1.6f;
        [SerializeField, FormerlySerializedAs("fallDamage")] private int _fallDamage = 1;
        [SerializeField, FormerlySerializedAs("respawnGrace"), Tooltip("복귀 직후 무적·낙하 면제 시간(초)")] private float _respawnGrace = 1f;
        [SerializeField, FormerlySerializedAs("health")] private PlayerHealth _health;

        private float _graceUntil;
        /// <summary>복귀 직후 보호 중이면 구멍 위에서도 떨어지지 않는다</summary>
        public bool IsProtected { get { return Time.time < _graceUntil; } }

        public Vector3 Velocity { get; private set; }
        public bool IsGrounded { get; private set; } = true;
        public bool IsFalling { get; private set; }
        public float GroundY { get; private set; }
        public float HeightAboveGround { get { return transform.position.y - GroundY; } }

        private float _verticalVel;
        private Vector3 _lastSafe;
        private Vector3 _fallDrift;

        private void Start()
        {
            // 모델 바닥이 타일 윗면에 닿는 높이를 기준 높이로
            float _halfHeight = 0.3f;
            var _r = _model ? _model.GetComponent<Renderer>() : GetComponentInChildren<Renderer>();
            if (_r) _halfHeight = _r.bounds.extents.y;
            float _floorTop = _grid ? _grid.FloorTopY : transform.position.y - _halfHeight;
            GroundY = _floorTop + _halfHeight;
            var _p = transform.position; _p.y = GroundY; transform.position = _p;
            _lastSafe = _p;
            if (!_health) _health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            if (HistoryCrash.UI.PauseSettingsUI.IsOpen) return;   // 멈춘 동안 점프 입력이 쌓이지 않게
            if (IsFalling) { UpdateFall(); return; }
            var _kb = Keyboard.current;
            if (_kb == null) return;
            Vector2 _input = Vector2.zero;
            if (_kb.wKey.isPressed || _kb.upArrowKey.isPressed) _input.y += 1;
            if (_kb.sKey.isPressed || _kb.downArrowKey.isPressed) _input.y -= 1;
            if (_kb.dKey.isPressed || _kb.rightArrowKey.isPressed) _input.x += 1;
            if (_kb.aKey.isPressed || _kb.leftArrowKey.isPressed) _input.x -= 1;
            if (_input.sqrMagnitude > 1) _input.Normalize();
            HasMoveInput = _input.sqrMagnitude > 0.01f;

            // 진행 방향 = 카메라(마우스로 돌린 시점)의 수평 방향
            Vector3 _fwd = Vector3.forward, _right = Vector3.right;
            if (_cameraTransform != null)
            {
                _fwd = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
                _right = Vector3.Cross(Vector3.up, _fwd);
            }
            Vector3 _dir = _fwd * _input.y + _right * _input.x;
            float _speed = _moveSpeed * (_kb.leftShiftKey.isPressed ? _runMultiplier : 1f) * (IsGrounded ? 1f : _airControl) * _speedScale;
            Vector3 _delta = _dir * _speed * Time.deltaTime;

            Vector3 _p = transform.position;
            if (_grid != null)
            {
                Vector3 _px = _p + new Vector3(_delta.x, 0, 0);
                if (Blocked(_px, new Vector3(Mathf.Sign(_delta.x), 0, 0))) _delta.x = 0;
                Vector3 _pz = _p + new Vector3(_delta.x, 0, _delta.z);
                if (Blocked(_pz, new Vector3(0, 0, Mathf.Sign(_delta.z)))) _delta.z = 0;
            }

            // 점프
            if (IsGrounded && _kb.spaceKey.wasPressedThisFrame)
            {
                _verticalVel = Mathf.Sqrt(2f * _gravity * _jumpHeight);
                IsGrounded = false;
            }
            if (!IsGrounded)
            {
                _verticalVel -= _gravity * Time.deltaTime;
                _delta.y = _verticalVel * Time.deltaTime;
                if (_p.y + _delta.y <= GroundY)
                {
                    // 깨진 타일 위로 내려오면 착지하지 못하고 떨어진다(점프로 건너는 중에는 괜찮음)
                    if (_grid != null && !IsProtected && _grid.IsHoleWorld(_p + _delta)) { transform.position = _p + _delta; StartFall(_delta / Mathf.Max(Time.deltaTime, 1e-4f)); return; }
                    _delta.y = GroundY - _p.y; _verticalVel = 0; IsGrounded = true;
                }
            }

            transform.position = _p + _delta;
            Velocity = Time.deltaTime > 0 ? _delta / Time.deltaTime : Vector3.zero;

            if (IsGrounded && _grid != null)
            {
                // 서 있는 타일이 깨지면 떨어진다
                if (!IsProtected && _grid.IsHoleWorld(transform.position)) { StartFall(Velocity * 0.3f); return; }
                if (!_grid.IsHoleWorld(transform.position)) _lastSafe = transform.position;
            }

            if (_model != null && _dir.sqrMagnitude > 0.001f)
            {
                var _target = Quaternion.LookRotation(_dir, Vector3.up);
                _model.rotation = Quaternion.RotateTowards(_model.rotation, _target, _turnSpeed * Time.deltaTime);
            }
        }

        /// <summary>외부 강제 이동(아누비스 등) 직후: 점프·낙하 상태를 끊고 지금 위치의 바닥에 착지시킴</summary>
        public void SnapToGround()
        {
            var _p = transform.position; _p.y = GroundY; transform.position = _p;
            _verticalVel = 0; IsFalling = false; IsGrounded = true; Velocity = Vector3.zero;
            if (_grid == null || !_grid.IsHoleWorld(_p)) _lastSafe = _p;
        }

        private void StartFall(Vector3 _velocity)
        {
            IsFalling = true; IsGrounded = false;
            _verticalVel = Mathf.Min(_verticalVel, 0f);
            if (_grid) Debug.Log("[BossPattern] Fall start at " + transform.position.ToString("F2") + " cell " + _grid.WorldToCell(transform.position) + " t=" + Time.time.ToString("F1"));
            _fallDrift = new Vector3(_velocity.x, 0, _velocity.z) * 0.5f;
        }

        private void UpdateFall()
        {
            _verticalVel -= _gravity * Time.deltaTime;
            transform.position += (_fallDrift + Vector3.up * _verticalVel) * Time.deltaTime;
            if (transform.position.y > GroundY - _fallDepth) return;

            // 데미지 후, 마지막으로 서 있던 곳에서 가장 가까운 "안전한 칸"(주변 8칸도 멀쩡한 칸)의 중심으로 복귀.
            // 구멍 가장자리 좌표로 돌아가면 이동 키를 누른 채 바로 다시 빠지는 반복이 생기므로 칸 중심을 쓴다.
            if (_health) _health.TakeDamage(_fallDamage, "Fall");
            else Debug.Log("[BossPattern] Fall (no PlayerHealth)");
            Vector3 _back = _lastSafe;
            if (_grid != null) _back = _grid.CellCenter(_grid.NearestSafe(_grid.WorldToCell(_lastSafe)), GroundY);
            _back.y = GroundY;
            transform.position = _back;
            _lastSafe = _back;
            _graceUntil = Time.time + _respawnGrace;
            if (_health) _health.GrantInvulnerability(_respawnGrace);
            if (_grid) Debug.Log("[BossPattern] Respawn at " + _back.ToString("F2") + " cell " + _grid.WorldToCell(_back) + " safe=" + _grid.IsSafe(_grid.WorldToCell(_back)));
            _verticalVel = 0; IsFalling = false; IsGrounded = true;
        }

        private bool Blocked(Vector3 _pos, Vector3 _moveDir)
        {
            if (_moveDir == Vector3.zero) return false;
            Vector3 _side = Vector3.Cross(Vector3.up, _moveDir) * _radius * 0.8f;
            Vector3 _front = _pos + _moveDir * _radius;
            return !_grid.IsWalkableWorld(_front) || !_grid.IsWalkableWorld(_front + _side) || !_grid.IsWalkableWorld(_front - _side);
        }
    }
}
