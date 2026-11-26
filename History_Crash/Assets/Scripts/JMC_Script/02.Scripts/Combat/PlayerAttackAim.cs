using UnityEngine;
using HistoryCrash.Stage;

namespace HistoryCrash.Combat
{
    /// <summary>
    /// 플레이어 공격 조준(계산만 함, 바닥 범위 표시 없음).
    /// - B: 화면 가운데가 가리키는 바닥 지점(최대 거리 제한) / C·E: 시선 방향
    /// </summary>
    public class PlayerAttackAim : MonoBehaviour
    {
        /// <summary>공격 조준 결과. Point = B 설치 지점(바닥 높이), Dir = 수평 단위 방향</summary>
        public struct AimResult
        {
            public Vector3 Point;
            public Vector3 Dir;
        }

        [SerializeField, Tooltip("B 설치 지점 최대 거리(m, 플레이어 기준)")] private float _maxPlaceDistance = 8f;

        private TileGrid _grid;
        private Camera _camera;

        /// <summary>지금 화면 기준 조준(패턴에 맞춰 B는 지점, C·E는 방향)</summary>
        public AimResult Compute(int _pattern)
        {
            Vector3 _dir = LookDir();
            Vector3 _point = _pattern == 1 ? GroundTarget(_dir) : FloorPoint(transform.position);
            return new AimResult { Point = _point, Dir = _dir };
        }

        // ---------- 조준 계산 ----------
        private Camera Cam { get { if (!_camera) _camera = Camera.main; return _camera; } }
        private TileGrid Grid { get { if (_grid == null) _grid = FindFirstObjectByType<TileGrid>(FindObjectsInactive.Include); return _grid; } }
        private float FloorY { get { return Grid != null && Grid.IsBuilt ? Grid.FloorTopY : transform.position.y; } }
        private Vector3 FloorPoint(Vector3 _p) { _p.y = FloorY; return _p; }

        private Vector3 LookDir()
        {
            Vector3 _d = Cam ? Vector3.ProjectOnPlane(Cam.transform.forward, Vector3.up) : transform.forward;
            _d.y = 0f;
            return _d.sqrMagnitude > 1e-4f ? _d.normalized : Vector3.forward;
        }

        /// <summary>화면 가운데 레이가 바닥과 만나는 지점. 최대 거리 안으로 줄이고, 막힌 칸이면 플레이어 쪽으로 당김</summary>
        private Vector3 GroundTarget(Vector3 _dir)
        {
            Vector3 _player = FloorPoint(transform.position);
            Vector3 _hit = _player + _dir * _maxPlaceDistance;   // 바닥을 안 보면(위를 보면) 최대 거리
            if (Cam)
            {
                Ray _ray = Cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (_ray.direction.y < -1e-3f)
                {
                    float _t = (FloorY - _ray.origin.y) / _ray.direction.y;
                    if (_t > 0f) _hit = _ray.GetPoint(_t);
                }
            }
            Vector3 _off = _hit - _player; _off.y = 0f;
            if (Vector3.Dot(_off, _dir) < 0f) _off = Vector3.zero;   // 카메라와 플레이어 사이 바닥을 보면 발밑
            _off = Vector3.ClampMagnitude(_off, _maxPlaceDistance);

            // 막힌 칸(벽·구멍·바깥)이면 플레이어 쪽으로 조금씩 당겨 서 있을 수 있는 칸을 찾음
            Vector3 _p = _player + _off;
            if (Grid != null && Grid.IsBuilt)
            {
                float _len = _off.magnitude, _step = Grid.TileSize * 0.25f;
                Vector3 _back = _len > 1e-3f ? _off / _len : Vector3.zero;
                while (_len > 0f && !Grid.IsSolid(Grid.WorldToCell(_p))) { _len = Mathf.Max(0f, _len - _step); _p = _player + _back * _len; }
                _p = Grid.CellCenter(Grid.WorldToCell(_p), FloorY);
            }
            return _p;
        }
    }
}
