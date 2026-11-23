using System;
using UnityEngine;

/// <summary>
/// 구덩이 낙하와 복귀 (기획 v20).
/// 바닥 아래의 낙하 영역(FallZone, 바닥보다 1.6m 아래)에 닿으면
/// 1 피해 → 떨어진 곳에서 가장 가까운 안전한 칸 가운데로 복귀 → 1초 무적.
/// 걸어서 빠지든, 점프해서 빠지든, 서 있던 타일이 사라지든 트리거 이벤트 하나로 처리합니다. (Update 없음)
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerFallHandler : MonoBehaviour
{
    // 같은 순간에 트리거가 여러 번 들어와도 한 번만 처리
    private const float RECOVER_COOLDOWN = 0.2f;
    // 안전한 칸으로 인정하는 바닥 높이 차이 (m)
    private const float SAFE_HEIGHT_TOLERANCE = 0.3f;

    [SerializeField] private int _fallDamage = 1;
    [Tooltip("복귀 후 무적 시간 (초, 기획 v20: 1)")]
    [SerializeField] private float _recoverInvincibleTime = 1f;

    [Header("안전한 칸 찾기")]
    [Tooltip("바닥 타일 한 칸 크기 (m)")]
    [SerializeField] private float _tileSize = 0.975f;
    [Tooltip("떨어진 곳에서 몇 칸 떨어진 곳까지 찾을지")]
    [SerializeField] private int _searchRadius = 6;
    [SerializeField] private LayerMask _groundMask = ~0;

    private CapsuleCollider _capsule;
    private IPlayerTeleport _teleport;
    private IDamageable _damageable;
    private IInvincible _invincible;
    private float _lastRecoverTime = float.NegativeInfinity;

    /// <summary>떨어져서 복귀했을 때 (효과음, 연출용). 복귀한 위치</summary>
    public event Action<Vector3> OnFellAndRecovered;

    private void Awake()
    {
        _capsule = GetComponent<CapsuleCollider>();
        _teleport = GetComponent<IPlayerTeleport>();
        _damageable = GetComponent<IDamageable>();
        _invincible = GetComponent<IInvincible>();

        if (_teleport == null)
        {
            Debug.LogError($"[PlayerFallHandler] {name}에 IPlayerTeleport(PlayerController)가 없습니다.", this);
            enabled = false;
        }
    }

    // 낙하 영역에 닿는 순간만 호출됨
    private void OnTriggerEnter(Collider _other)
    {
        if (!enabled || !_other.TryGetComponent(out FallZone _zone)) return;
        if (Time.time < _lastRecoverTime + RECOVER_COOLDOWN) return;

        _lastRecoverTime = Time.time;
        Recover(transform.position, _zone.GroundY);
    }

    private void Recover(Vector3 _from, float _groundY)
    {
        Vector3 _target = FindSafePosition(_from, _groundY);

        _damageable?.TakeDamage(new DamageInfo(_fallDamage, ElementType.None, null));
        _teleport.TeleportTo(_target);
        _invincible?.SetInvincible(_recoverInvincibleTime);

        OnFellAndRecovered?.Invoke(_target);
    }

    /// <summary>
    /// 떨어진 곳(수평 위치)에서 가까운 칸부터 바깥으로 한 바퀴씩 넓혀 가며,
    /// 바닥 높이에 바닥이 있고 플레이어가 들어갈 자리가 있는 칸을 찾습니다.
    /// 못 찾으면 떨어진 곳 바로 위 바닥 높이로 돌려보냅니다.
    /// </summary>
    private Vector3 FindSafePosition(Vector3 _from, float _groundY)
    {
        float _pivotHeight = _teleport.PivotHeightAboveFeet;
        Vector3 _origin = new Vector3(_from.x, _groundY, _from.z);

        for (int _ring = 0; _ring <= _searchRadius; _ring++)
        {
            bool _found = false;
            float _bestDistance = float.MaxValue;
            Vector3 _best = _origin;

            for (int _x = -_ring; _x <= _ring; _x++)
            {
                for (int _z = -_ring; _z <= _ring; _z++)
                {
                    // 이번 바퀴의 테두리 칸만 검사
                    if (Mathf.Max(Mathf.Abs(_x), Mathf.Abs(_z)) != _ring) continue;

                    Vector3 _cell = _origin + new Vector3(_x * _tileSize, 0f, _z * _tileSize);
                    if (!TryGetSafeCell(_cell, _groundY, _pivotHeight, out Vector3 _position)) continue;

                    float _distance = (_position - _origin).sqrMagnitude;
                    if (_distance >= _bestDistance) continue;

                    _bestDistance = _distance;
                    _best = _position;
                    _found = true;
                }
            }

            if (_found) return _best;
        }

        return _origin + Vector3.up * (_pivotHeight + 0.02f);
    }

    private bool TryGetSafeCell(Vector3 _cell, float _groundY, float _pivotHeight, out Vector3 _position)
    {
        _position = default;

        Vector3 _rayOrigin = new Vector3(_cell.x, _groundY + 2f, _cell.z);
        if (!Physics.Raycast(_rayOrigin, Vector3.down, out RaycastHit _hit, 3f, _groundMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // 벽 윗면이나 낮은 곳은 제외
        if (Mathf.Abs(_hit.point.y - _groundY) > SAFE_HEIGHT_TOLERANCE) return false;

        // 타일이면 칸 가운데로, 큰 바닥이면 맞은 지점으로
        Bounds _bounds = _hit.collider.bounds;
        bool _isTile = _bounds.size.x <= _tileSize * 1.5f && _bounds.size.z <= _tileSize * 1.5f;
        Vector3 _feet = _isTile ? new Vector3(_bounds.center.x, _hit.point.y, _bounds.center.z) : _hit.point;

        // 그 자리에 기둥·벽이 없어야 함 (캡슐을 살짝 띄워서 검사, 트리거는 무시)
        float _radius = _capsule.radius * 0.9f;
        Vector3 _bottom = _feet + Vector3.up * (_radius + 0.05f);
        Vector3 _top = _feet + Vector3.up * Mathf.Max(_pivotHeight * 2f - _radius, _radius + 0.05f);
        if (Physics.CheckCapsule(_bottom, _top, _radius, _groundMask, QueryTriggerInteraction.Ignore)) return false;

        _position = _feet + Vector3.up * (_pivotHeight + 0.02f);
        return true;
    }
}
