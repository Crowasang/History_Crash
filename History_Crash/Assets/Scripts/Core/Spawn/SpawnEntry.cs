using System;
using UnityEngine;

/// <summary>
/// SpawnManager에 등록하는 생성 규칙 하나. (예: 미라 - 10초마다 파라오 앞)
/// </summary>
[Serializable]
public class SpawnEntry
{
    [Tooltip("생성할 프리팹 (루트에 PooledObject 필요)")]
    [SerializeField] private PooledObject _prefab;

    [Tooltip("생성 주기 (초). 분당 6마리 = 10초")]
    [SerializeField] private float _interval = 10f;

    [Tooltip("진행 시작 후 첫 생성까지 대기 (초)")]
    [SerializeField] private float _startDelay = 0f;

    [Tooltip("생성 위치 후보. 매번 하나를 무작위로 고름")]
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("생성 위치 주변으로 흩뿌리는 반경 (타일)")]
    [SerializeField] private float _scatterRadius = 0f;

    [Tooltip("동시에 살아 있을 수 있는 최대 수 (0이면 제한 없음)")]
    [SerializeField] private int _maxAlive = 0;

    public PooledObject Prefab => _prefab;
    public float Interval => _interval;
    public float StartDelay => _startDelay;
    public int MaxAlive => _maxAlive;

    /// <summary>생성 위치 후보 중 하나를 골라 반경 안 무작위 지점을 돌려줍니다.</summary>
    public Vector3 PickPosition(Vector3 _fallback)
    {
        Vector3 _center = _fallback;
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            Transform _point = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];
            if (_point != null)
            {
                _center = _point.position;
            }
        }

        Vector2 _offset = UnityEngine.Random.insideUnitCircle * _scatterRadius;
        return _center + new Vector3(_offset.x, 0f, _offset.y);
    }
}
