using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 아누비스 석상 강제 이동을 받는 쪽 (기획 v20 8-2).
/// 1. 구덩이에 떨어지는 중이면 끝날 때까지 기다림
/// 2. 조작을 잠그고 0.8초 동안 포물선(최고 0.6m)으로 도착 위치까지 끌려감
/// 3. 도착하면 바닥에 착지 (점프 중이었어도), 조작 잠금 해제
/// 무적: 끌려가는 동안 + 도착 후 0.5초. 이미 끌려가는 중이면 새 요청은 무시합니다.
/// 조작 잠금은 IMovementLock(횟수 방식)을 쓰고 finally에서 반드시 풀어서, 겹치거나 취소돼도 꺼진 채 굳지 않습니다.
/// 끌려가는 동안만 물리 프레임마다 위치를 옮깁니다. (Update 없음)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerForcedMover : MonoBehaviour, IForcedMovable
{
    [Tooltip("끌려가는 시간 (초, 기획 v20: 0.8)")]
    [SerializeField] private float _moveDuration = 0.8f;
    [Tooltip("포물선 최고 높이 (m, 기획 v20: 0.6)")]
    [SerializeField] private float _arcHeight = 0.6f;
    [Tooltip("도착 후 무적 시간 (초, 기획 v20: 0.5)")]
    [SerializeField] private float _invincibleAfterArrival = 0.5f;
    [Tooltip("구덩이 낙하가 끝나기를 기다리는 최대 시간 (초). 넘으면 그냥 출발")]
    [SerializeField] private float _maxFallWait = 3f;

    private Rigidbody _rb;
    private IMovementLock _movementLock;
    private IPlayerTeleport _teleport;
    private IJumpState _jumpState;
    private IInvincible _invincible;

    public bool IsForceMoving { get; private set; }

    public event Action<Vector3, Vector3> OnForceMoveStarted;
    public event Action OnForceMoveEnded;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _movementLock = GetComponent<IMovementLock>();
        _teleport = GetComponent<IPlayerTeleport>();
        _jumpState = GetComponent<IJumpState>();
        _invincible = GetComponent<IInvincible>();

        if (_movementLock == null || _teleport == null || _jumpState == null)
        {
            Debug.LogError($"[PlayerForcedMover] {name}에 PlayerController(IMovementLock·IPlayerTeleport·IJumpState)가 없습니다.", this);
            enabled = false;
        }
    }

    public bool TryForceMove(Vector3 _destinationFeet)
    {
        if (!enabled || IsForceMoving) return false;

        ForceMoveAsync(_destinationFeet, destroyCancellationToken).Forget();
        return true;
    }

    private async UniTaskVoid ForceMoveAsync(Vector3 _destinationFeet, CancellationToken _token)
    {
        IsForceMoving = true;

        // 1. 구덩이 위로 떨어지는 중이면(발밑에 바닥이 없음) 낙하·복귀가 끝날 때까지 기다림
        float _waited = 0f;
        while (float.IsPositiveInfinity(_jumpState.GetHeightAboveGround()) && _waited < _maxFallWait)
        {
            if (await UniTask.Yield(PlayerLoopTiming.FixedUpdate, _token).SuppressCancellationThrow())
            {
                IsForceMoving = false;
                return;
            }
            _waited += Time.fixedDeltaTime;
        }

        Vector3 _start = _rb.position;
        Vector3 _end = _destinationFeet + Vector3.up * _teleport.PivotHeightAboveFeet;

        _movementLock.Lock();
        _rb.isKinematic = true;
        _invincible?.SetInvincible(_moveDuration + _invincibleAfterArrival);
        OnForceMoveStarted?.Invoke(_start, _end);

        bool _isCanceled = false;
        try
        {
            // 2. 포물선으로 끌고 감: 수평·수직은 직선 보간 + 위로 볼록한 곡선 (가운데에서 최고 높이)
            float _elapsed = 0f;
            while (_elapsed < _moveDuration)
            {
                _isCanceled = await UniTask.Yield(PlayerLoopTiming.FixedUpdate, _token).SuppressCancellationThrow();
                if (_isCanceled) break;

                _elapsed += Time.fixedDeltaTime;
                float _t = Mathf.Clamp01(_elapsed / _moveDuration);
                Vector3 _position = Vector3.Lerp(_start, _end, _t) + Vector3.up * (_arcHeight * 4f * _t * (1f - _t));
                _rb.MovePosition(_position);
            }
        }
        finally
        {
            // 취소되거나 중간에 문제가 생겨도 항상 원래대로 되돌림
            if (_rb != null) _rb.isKinematic = false;
            _movementLock.Unlock();
            IsForceMoving = false;
        }

        if (_isCanceled) return;

        // 3. 도착: 점프·낙하 상태를 끊고 바닥에 착지
        _teleport.TeleportTo(_end);
        OnForceMoveEnded?.Invoke();
    }
}
