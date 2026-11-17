using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.UI;

namespace HistoryCrash.Stage
{
    /// <summary>
    /// 바닥 타일(Floor 자식 렌더러)로 격자를 만들고, 장애물(기둥·벽·제단 등) 아래 칸을 막는다.
    /// 한 칸 = 0.975m (Temple_impe 기준 38 x 30).
    /// </summary>
    [DefaultExecutionOrder(-100)]   // 다른 스크립트의 Awake보다 먼저 격자를 만든다(순서가 어긋나면 칸 좌표가 밀림)
    public class TileGrid : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("floorRoot"), Tooltip("Temple_impe/Floor")] private Transform _floorRoot;
        [SerializeField, FormerlySerializedAs("obstacleRoots"), Tooltip("장애물 그룹(Pillars, Walls, Altar, Statues, Monuments, Entrance)")] private Transform[] _obstacleRoots;
        public Transform[] ObstacleRoots { get => _obstacleRoots; set => _obstacleRoots = value; }
        [SerializeField, FormerlySerializedAs("tileSize")] private float _tileSize = 0.975f;
        public float TileSize { get => _tileSize; set => _tileSize = value; }
        [SerializeField, FormerlySerializedAs("obstacleShrink"), Tooltip("장애물 경계를 이만큼 줄여서 판정(가장자리 칸이 너무 많이 막히지 않게)")] private float _obstacleShrink = 0.15f;
        [SerializeField, FormerlySerializedAs("obstacleMaxBaseHeight"), Tooltip("이 높이보다 아래에서 시작하는 장애물만 막음")] private float _obstacleMaxBaseHeight = 1.5f;

        public Vector2 Origin { get; private set; }   // (0,0) 칸 중심의 월드 x,z
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public float FloorTopY { get; private set; }
        private bool[] _walkable;

        public bool IsBuilt { get; private set; }

        private void Awake() { if (!IsBuilt) Build(); }

        /// <summary>아직 안 만들어졌으면 만든다(다른 스크립트가 Awake에서 격자를 쓸 때 안전장치).</summary>
        public void EnsureBuilt() { if (!IsBuilt) Build(); }

        public void Build()
        {
            IsBuilt = true;
            var _tiles = _floorRoot.GetComponentsInChildren<Renderer>();
            float _minX = float.MaxValue, _minZ = float.MaxValue, _maxX = float.MinValue, _maxZ = float.MinValue, _top = float.MinValue;
            foreach (var _r in _tiles)
            {
                var _c = _r.bounds.center;
                _minX = Mathf.Min(_minX, _c.x); _minZ = Mathf.Min(_minZ, _c.z);
                _maxX = Mathf.Max(_maxX, _c.x); _maxZ = Mathf.Max(_maxZ, _c.z);
                _top = Mathf.Max(_top, _r.bounds.max.y);
            }
            Origin = new Vector2(_minX, _minZ);
            Cols = Mathf.RoundToInt((_maxX - _minX) / _tileSize) + 1;
            Rows = Mathf.RoundToInt((_maxZ - _minZ) / _tileSize) + 1;
            FloorTopY = _top;
            _walkable = new bool[Cols * Rows];

            // 타일이 실제로 있는 칸만 걷기 가능
            foreach (var _r in _tiles)
            {
                var _cell = WorldToCell(_r.bounds.center);
                if (InBounds(_cell)) _walkable[_cell.y * Cols + _cell.x] = true;
            }

            // 장애물 아래 칸 막기
            if (_obstacleRoots != null)
                foreach (var _root in _obstacleRoots)
                {
                    if (_root == null || !_root.gameObject.activeInHierarchy) continue;
                    foreach (var _r in _root.GetComponentsInChildren<Renderer>())
                    {
                        var _b = _r.bounds;
                        if (_b.min.y > FloorTopY + _obstacleMaxBaseHeight) continue;
                        var _a = WorldToCell(new Vector3(_b.min.x + _obstacleShrink, 0, _b.min.z + _obstacleShrink));
                        var _z = WorldToCell(new Vector3(_b.max.x - _obstacleShrink, 0, _b.max.z - _obstacleShrink));
                        for (int _y = _a.y; _y <= _z.y; _y++)
                            for (int _x = _a.x; _x <= _z.x; _x++)
                                if (InBounds(new Vector2Int(_x, _y))) _walkable[_y * Cols + _x] = false;
                    }
                }
        }

        public bool InBounds(Vector2Int _c) { return _c.x >= 0 && _c.y >= 0 && _c.x < Cols && _c.y < Rows; }

        public Vector2Int WorldToCell(Vector3 _p)
        {
            return new Vector2Int(Mathf.RoundToInt((_p.x - Origin.x) / _tileSize), Mathf.RoundToInt((_p.z - Origin.y) / _tileSize));
        }

        public Vector3 CellCenter(Vector2Int _c, float _y)
        {
            return new Vector3(Origin.x + _c.x * _tileSize, _y, Origin.y + _c.y * _tileSize);
        }

        public bool IsWalkable(Vector2Int _c) { return InBounds(_c) && _walkable[_c.y * Cols + _c.x]; }
        public bool IsWalkableWorld(Vector3 _p) { return IsWalkable(WorldToCell(_p)); }

        // ---- 구멍(깨진 타일): 지나갈 수는 있지만(점프) 서 있으면 떨어진다 ----
        private bool[] _holes;
        public void SetHole(Vector2Int _c, bool _hole)
        {
            if (!InBounds(_c)) return;
            if (_holes == null || _holes.Length != Cols * Rows) _holes = new bool[Cols * Rows];
            _holes[_c.y * Cols + _c.x] = _hole;
        }
        public bool IsHole(Vector2Int _c) { return _holes != null && InBounds(_c) && _holes[_c.y * Cols + _c.x]; }
        public bool IsHoleWorld(Vector3 _p) { return IsHole(WorldToCell(_p)); }
        /// <summary>걷기 가능하고 구멍이 아닌 칸(서 있을 수 있는 칸)</summary>
        public bool IsSolid(Vector2Int _c) { return IsWalkable(_c) && !IsHole(_c); }

        /// <summary>주변 8칸까지 모두 서 있을 수 있는 칸인지(구멍 가장자리가 아닌 안전한 칸)</summary>
        public bool IsSafe(Vector2Int _c)
        {
            for (int _dy = -1; _dy <= 1; _dy++)
                for (int _dx = -1; _dx <= 1; _dx++)
                    if (!IsSolid(new Vector2Int(_c.x + _dx, _c.y + _dy))) return false;
            return true;
        }

        /// <summary>from에서 가장 가까운 안전한 칸(없으면 가장 가까운 서 있을 수 있는 칸)</summary>
        public Vector2Int NearestSafe(Vector2Int _from)
        {
            if (IsSafe(_from)) return _from;
            for (int _r = 1; _r < Mathf.Max(Cols, Rows); _r++)
            {
                Vector2Int _best = _from; float _bestD = float.MaxValue;
                for (int _dy = -_r; _dy <= _r; _dy++)
                    for (int _dx = -_r; _dx <= _r; _dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(_dx), Mathf.Abs(_dy)) != _r) continue;
                        var _c = new Vector2Int(_from.x + _dx, _from.y + _dy);
                        if (!IsSafe(_c)) continue;
                        float _d = _dx * _dx + _dy * _dy;
                        if (_d < _bestD) { _bestD = _d; _best = _c; }
                    }
                if (_bestD < float.MaxValue) return _best;
            }
            return NearestSolid(_from);
        }

        /// <summary>from에서 가장 가까운 서 있을 수 있는 칸</summary>
        public Vector2Int NearestSolid(Vector2Int _from)
        {
            if (IsSolid(_from)) return _from;
            for (int _r = 1; _r < Mathf.Max(Cols, Rows); _r++)
            {
                Vector2Int _best = _from; float _bestD = float.MaxValue;
                for (int _dy = -_r; _dy <= _r; _dy++)
                    for (int _dx = -_r; _dx <= _r; _dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(_dx), Mathf.Abs(_dy)) != _r) continue;
                        var _c = new Vector2Int(_from.x + _dx, _from.y + _dy);
                        if (!IsSolid(_c)) continue;
                        float _d = _dx * _dx + _dy * _dy;
                        if (_d < _bestD) { _bestD = _d; _best = _c; }
                    }
                if (_bestD < float.MaxValue) return _best;
            }
            return _from;
        }

        /// <summary>center에서 radius(칸, 체비셰프 거리) 안의 걷기 가능한 칸 중 하나를 무작위로 고른다.</summary>
        public bool TryRandomWalkableNear(Vector2Int _center, int _radius, out Vector2Int _result)
        {
            var _list = new List<Vector2Int>();
            for (int _dy = -_radius; _dy <= _radius; _dy++)
                for (int _dx = -_radius; _dx <= _radius; _dx++)
                {
                    var _c = new Vector2Int(_center.x + _dx, _center.y + _dy);
                    if (IsSolid(_c)) _list.Add(_c);
                }
            if (_list.Count == 0) { _result = _center; return false; }
            _result = _list[Random.Range(0, _list.Count)];
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (_walkable == null) return;
            for (int _y = 0; _y < Rows; _y++)
                for (int _x = 0; _x < Cols; _x++)
                {
                    Gizmos.color = _walkable[_y * Cols + _x] ? new Color(0, 1, 0, 0.15f) : new Color(1, 0, 0, 0.5f);
                    Gizmos.DrawCube(CellCenter(new Vector2Int(_x, _y), FloorTopY + 0.02f), new Vector3(_tileSize * 0.9f, 0.02f, _tileSize * 0.9f));
                }
        }
    }
}
