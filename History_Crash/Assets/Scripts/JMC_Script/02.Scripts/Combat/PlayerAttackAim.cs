using R3;
using UnityEngine;
using HistoryCrash.Data;
using HistoryCrash.Player;
using HistoryCrash.Stage;

namespace HistoryCrash.Combat
{
    /// <summary>
    /// 플레이어 공격 조준과 바닥 범위 표시(속성색 선).
    /// - 스태프·오브를 모두 들고 있으면 들고 있는 패턴의 범위를 바닥에 미리 보여 준다.
    /// - 클릭하면 Lock()으로 그 순간의 지점·방향이 고정되고, 공격이 나갈 때 Release()로 사라진다.
    /// - A: 플레이어 주위 회전 범위 원 / B·D: 화면 가운데가 가리키는 바닥 지점(최대 거리 제한)의 범위 원
    /// - C·E: 시선 방향 사거리 띠(벽에 닿으면 거기서 끊김)
    /// 조준점 UI는 따로 만들 예정이라 표시는 선만 쓰는 간단한 형태.
    /// </summary>
    public class PlayerAttackAim : MonoBehaviour
    {
        /// <summary>공격 조준 결과. Point = 설치·낙하 지점(바닥 높이), Dir = 수평 단위 방향</summary>
        public struct AimResult
        {
            public Vector3 Point;
            public Vector3 Dir;
        }

        [SerializeField, Tooltip("B·D 설치 지점 최대 거리(m, 플레이어 기준)")] private float _maxPlaceDistance = 8f;
        [SerializeField, Tooltip("표시 선 굵기(m)")] private float _lineWidth = 0.07f;
        [SerializeField, Tooltip("조준 중 표시 밝기(0~1). 클릭해 고정되면 1")] private float _previewAlpha = 0.6f;
        [SerializeField, Tooltip("바닥 위로 띄우는 높이(m, 바닥과 겹쳐 깜빡임 방지)")] private float _floorOffset = 0.04f;

        private const int CIRCLE_SEG = 48;
        private const float CENTER_DOT = 0.18f;   // B·D 설치 지점 가운데 작은 원 반지름
        private const float STRIP_START = 0.5f;   // C·E 띠가 시작하는 거리(플레이어 발밑과 겹치지 않게)

        private PlayerLoadout _loadout;
        private TileGrid _grid;
        private Camera _camera;
        private Transform _root;
        private LineRenderer _outline, _dot;

        private bool _isLocked;
        private int _lockedPattern;
        private Element _lockedElement;
        private AimResult _lockedAim;

        private void Awake()
        {
            _loadout = GetComponent<PlayerLoadout>();
            _root = new GameObject("AttackAimIndicator").transform;   // 플레이어 회전을 따라가지 않게 부모 없이 둠
            _outline = MakeLine("Outline");
            _dot = MakeLine("Center");
            SetVisible(false);
        }

        private void Start()
        {
            // 매 프레임 표시 갱신 (Update 대신 R3). 카메라(Cinemachine, LateUpdate)가 움직인 뒤에 계산해야 화면 가운데와 맞음
            Observable.EveryUpdate(UnityFrameProvider.PostLateUpdate).Subscribe(_ => Refresh()).AddTo(this);
        }

        private void OnDisable() { if (_root) SetVisible(false); }
        private void OnDestroy() { if (_root) Destroy(_root.gameObject); }

        /// <summary>지금 화면 기준 조준(패턴에 맞춰 B·D는 지점, C·E는 방향)</summary>
        public AimResult Compute(int _pattern)
        {
            Vector3 _dir = LookDir();
            Vector3 _point = _pattern == 1 || _pattern == 3 ? GroundTarget(_dir, _pattern == 1) : FloorPoint(transform.position);
            return new AimResult { Point = _point, Dir = _dir };
        }

        /// <summary>클릭한 순간의 조준을 고정하고 돌려준다. 공격이 나갈 때까지 표시가 그 자리에 남는다.</summary>
        public AimResult Lock(int _pattern, Element _element)
        {
            _lockedAim = Compute(_pattern);
            _lockedPattern = _pattern; _lockedElement = _element; _isLocked = true; _releaseAt = float.MaxValue;
            return _lockedAim;
        }

        /// <summary>공격이 나간 뒤 고정 표시를 지운다. delay초 뒤에 지움(C·E는 소환 후 실제 발사까지 남겨 둠)</summary>
        public void Release(float _delay = 0f)
        {
            if (_delay <= 0f) { _isLocked = false; return; }
            _releaseAt = Time.time + _delay;
        }
        private float _releaseAt = float.MaxValue;

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
        private Vector3 GroundTarget(Vector3 _dir, bool _snapCell)
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
                if (_snapCell) _p = Grid.CellCenter(Grid.WorldToCell(_p), FloorY);
            }
            return _p;
        }

        /// <summary>from에서 dir로 range까지 가다 벽에 닿는 거리</summary>
        private float ClearLength(Vector3 _from, Vector3 _dir, float _range)
        {
            if (Grid == null || !Grid.IsBuilt) return _range;
            const float STEP = 0.25f;
            for (float _d = STRIP_START; _d < _range; _d += STEP)
            {
                var _c = Grid.WorldToCell(_from + _dir * _d);
                if (!Grid.InBounds(_c) || !Grid.IsWalkable(_c)) return Mathf.Max(STRIP_START + STEP, _d);
            }
            return _range;
        }

        // ---------- 표시 ----------
        private void Refresh()
        {
            if (_isLocked && Time.time >= _releaseAt) { _isLocked = false; _releaseAt = float.MaxValue; }
            if (!isActiveAndEnabled || HistoryCrash.UI.PauseSettingsUI.IsOpen) { SetVisible(false); return; }
            int _pattern; Element _e; AimResult _aim; float _alpha;
            if (_isLocked) { _pattern = _lockedPattern; _e = _lockedElement; _aim = _lockedAim; _alpha = 1f; }
            else if (_loadout && _loadout.CanAttack && !_loadout.IsCasting) { _pattern = _loadout.HeldStaff; _e = _loadout.HeldOrb; _aim = Compute(_pattern); _alpha = _previewAlpha; }
            else { SetVisible(false); return; }

            SetVisible(true);
            Color _c = ElementInfo.Color(_e);
            Vector3 _player = FloorPoint(transform.position);
            switch (_pattern)
            {
                case 0: Circle(_outline, _player, PlayerStaffSkill.A_RADIUS + PlayerStaffSkill.A_HIT); _dot.enabled = false; break;
                case 1: Circle(_outline, _aim.Point, PlayerStaffSkill.B_TRIGGER); Circle(_dot, _aim.Point, CENTER_DOT); break;
                case 3: Circle(_outline, _aim.Point, PlayerStaffSkill.D_RADIUS); Circle(_dot, _aim.Point, CENTER_DOT); break;
                case 2: Strip(_player, _aim.Dir, PlayerStaffSkill.C_RANGE, PlayerStaffSkill.C_HIT); _dot.enabled = false; break;
                default: Strip(_player, _aim.Dir, PlayerStaffSkill.E_RANGE, PlayerStaffSkill.E_HIT); _dot.enabled = false; break;
            }
            Paint(_outline, _c, _alpha);
            Paint(_dot, _c, _alpha);
        }

        private void Circle(LineRenderer _lr, Vector3 _center, float _radius)
        {
            _lr.enabled = true; _lr.loop = true;
            _lr.positionCount = CIRCLE_SEG;
            for (int _i = 0; _i < CIRCLE_SEG; _i++)
            {
                float _a = _i * Mathf.PI * 2f / CIRCLE_SEG;
                _lr.SetPosition(_i, _center + new Vector3(Mathf.Sin(_a) * _radius, _floorOffset, Mathf.Cos(_a) * _radius));
            }
        }

        // 사거리 띠(끝이 뾰족한 화살표 모양 외곽선). 폭 = 판정 지름
        private void Strip(Vector3 _player, Vector3 _dir, float _range, float _hitRadius)
        {
            float _len = ClearLength(_player, _dir, _range);
            Vector3 _side = Vector3.Cross(Vector3.up, _dir) * _hitRadius;
            Vector3 _up = Vector3.up * _floorOffset;
            Vector3 _a = _player + _dir * STRIP_START + _up;
            Vector3 _b = _player + _dir * Mathf.Max(STRIP_START, _len - _hitRadius) + _up;
            Vector3 _tip = _player + _dir * _len + _up;
            _outline.enabled = true; _outline.loop = true;
            _outline.positionCount = 5;
            _outline.SetPosition(0, _a - _side);
            _outline.SetPosition(1, _b - _side);
            _outline.SetPosition(2, _tip);
            _outline.SetPosition(3, _b + _side);
            _outline.SetPosition(4, _a + _side);
        }

        private static void Paint(LineRenderer _lr, Color _c, float _alpha) { _c.a = Mathf.Clamp01(_alpha); _lr.startColor = _lr.endColor = _c; }

        private void SetVisible(bool _on) { if (_root.gameObject.activeSelf != _on) _root.gameObject.SetActive(_on); }

        private LineRenderer MakeLine(string _name)
        {
            var _go = new GameObject(_name);
            _go.transform.SetParent(_root, false);
            _go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // TransformZ 정렬 → 선이 바닥(XZ)에 눕게
            var _lr = _go.AddComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.alignment = LineAlignment.TransformZ;
            _lr.widthMultiplier = _lineWidth;
            _lr.sharedMaterial = PlayerStaffSkill.LineMat();
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; _lr.receiveShadows = false;
            _lr.numCornerVertices = 2; _lr.numCapVertices = 2;
            return _lr;
        }
    }
}
