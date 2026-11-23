using UnityEngine;

/// <summary>
/// 바닥 아래에 까는 보이지 않는 낙하 판정 영역 (트리거).
/// 윗면이 바닥보다 1.6m(기획 v20) 아래에 오도록 자동으로 크기를 맞춥니다.
/// 플레이어가 여기에 닿으면 PlayerFallHandler가 피해 + 복귀를 처리합니다.
/// 걸어서 빠지든, 점프해서 빠지든, 서 있던 타일이 사라지든 같은 방식으로 감지됩니다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FallZone : MonoBehaviour
{
    private const float DEPTH = 2f;

    [Tooltip("바닥 윗면 높이 (월드 y)")]
    [SerializeField] private float _groundY = 0.1f;
    [Tooltip("바닥에서 이만큼(m) 떨어지면 낙하 처리 (기획 v20: 1.6)")]
    [SerializeField] private float _fallDistance = 1.6f;
    [Tooltip("덮을 범위 (x, z, m). 맵보다 넉넉하게")]
    [SerializeField] private Vector2 _areaSize = new Vector2(40f, 32f);

    /// <summary>바닥 윗면 높이. 복귀할 안전한 칸을 찾을 때 기준</summary>
    public float GroundY => _groundY;

    private void Awake()
    {
        Layout();
    }

    private void OnValidate()
    {
        Layout();
    }

    // 회전·크기 없는 오브젝트 기준으로, 윗면이 (바닥 - 낙하 거리)에 오도록 상자를 맞춤
    private void Layout()
    {
        BoxCollider _box = GetComponent<BoxCollider>();
        _box.isTrigger = true;

        float _top = _groundY - _fallDistance;
        _box.size = new Vector3(_areaSize.x, DEPTH, _areaSize.y);
        _box.center = new Vector3(0f, _top - DEPTH * 0.5f - transform.position.y, 0f);
    }
}
