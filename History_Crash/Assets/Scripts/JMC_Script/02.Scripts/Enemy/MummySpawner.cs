using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Enemy
{
    /// <summary>
    /// 보스 패턴과 패턴 사이에 머미를 소환한다.
    /// - 성별(여/남)과 속성(불·물·전기)은 무작위.
    /// - 파라오 제단 계단 아래 칸(파라오에서 가장 가까운 걷기 가능 칸 주변)에서 속성 불꽃이 피어오른 뒤 바닥에서 솟아오른다.
    /// </summary>
    public class MummySpawner : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("grid")] private TileGrid _grid;
        [SerializeField, FormerlySerializedAs("director")] private BossPatternDirector _director;
        [SerializeField, FormerlySerializedAs("player")] private Transform _player;
        [SerializeField, FormerlySerializedAs("pharaoh")] private Transform _pharaoh;
        [SerializeField, FormerlySerializedAs("femalePrefab")] private GameObject _femalePrefab;
        [SerializeField, FormerlySerializedAs("malePrefab")] private GameObject _malePrefab;

        [SerializeField, FormerlySerializedAs("maxAlive"), Tooltip("동시에 살아 있을 수 있는 최대 수")] private int _maxAlive = 5;
        [SerializeField, Tooltip("미라 1킬당 파라오에게 주는 피해 (기획 v20: 20 → 맞히기 힘들어 25로 조정)")] private int _pharaohDamagePerKill = 25;
        [SerializeField, FormerlySerializedAs("perGap"), Tooltip("패턴 사이마다 소환할 수")] private int _perGap = 1;
        [SerializeField, FormerlySerializedAs("spawnSpread"), Tooltip("소환 칸 후보: 계단 아래 기준 칸에서 이 칸수 안")] private int _spawnSpread = 1;
        [SerializeField, FormerlySerializedAs("spawnCells"), Tooltip("직접 지정한 소환 칸(비우면 자동)")] private List<Vector2Int> _spawnCells = new List<Vector2Int>();
        [SerializeField, Tooltip("소환 기준점(계단 앞 빈 오브젝트). 비우면 파라오 위치 기준. 이 점에서 가장 가까운 바닥 칸 주변 Spawn Spread 칸 안에서 나옴")] private Transform _spawnCenter;
        [SerializeField, FormerlySerializedAs("flameTime")] private float _flameTime = 0.9f;
        [SerializeField, FormerlySerializedAs("riseTime")] private float _riseTime = 0.6f;
        [SerializeField, FormerlySerializedAs("footOffset"), Tooltip("모델 원점에서 발바닥까지(바닥 위로 올리는 양, m)")] private float _footOffset = 0.07f;

        // 플레이어·파라오 연결 (KDH 코어 인터페이스)
        private IJumpState _jump; private ISpeedModifier _speed; private IDamageable _health;
        private IDamageable _pharaohHealth;

        private void Start()
        {
            if (_player) { _jump = _player.GetComponent<IJumpState>(); _speed = _player.GetComponent<ISpeedModifier>(); _health = _player.GetComponent<IDamageable>(); }
            if (_pharaoh) _pharaohHealth = _pharaoh.GetComponent<IDamageable>();
            if (_director) _director.OnPatternGap += SpawnWave;
        }

        private void OnDestroy() { if (_director) _director.OnPatternGap -= SpawnWave; }

        private void EnsureSpawnCells()
        {
            if (_spawnCells.Count > 0 || _grid == null || !_pharaoh) return;
            // 파라오 칸에서 가장 가까운 걷기 가능 칸 = 제단 계단 아래
            var _center = _spawnCenter ? _spawnCenter.position : _pharaoh.position;
            var _baseCell = _grid.NearestSolid(_grid.WorldToCell(_center));
            for (int _dy = -_spawnSpread; _dy <= _spawnSpread; _dy++)
                for (int _dx = -_spawnSpread; _dx <= _spawnSpread; _dx++)
                {
                    var _c = new Vector2Int(_baseCell.x + _dx, _baseCell.y + _dy);
                    if (_grid.IsSolid(_c)) _spawnCells.Add(_c);
                }
            Debug.Log("[Mummy] spawn cells around " + _baseCell + ": " + _spawnCells.Count);
        }

        public void SpawnWave()
        {
            if (_pharaohHealth != null && !_pharaohHealth.IsAlive) return;
            for (int _i = 0; _i < _perGap; _i++)
            {
                // 아직 솟아오르는 중인 머미(pending)까지 세어 최대 수를 넘지 않게
                if (Mummy.Alive.Count + _pending >= _maxAlive) return;
                _pending++;
                StartCoroutine(SpawnOne());
            }
        }

        private int _pending;

        private IEnumerator SpawnOne()
        {
            EnsureSpawnCells();
            if (_spawnCells.Count == 0) { _pending--; yield break; }
            // 다른 머미와 가장 멀리 떨어진 후보 칸(같으면 무작위)
            Vector2Int _cell = _spawnCells[Random.Range(0, _spawnCells.Count)]; float _bestGap = -1f;
            foreach (var _c in _spawnCells)
            {
                Vector3 _w = _grid.CellCenter(_c, 0); float _gap = float.MaxValue;
                foreach (var _m0 in Mummy.Alive) { if (!_m0) continue; Vector3 _d = _m0.transform.position - _w; _d.y = 0; _gap = Mathf.Min(_gap, _d.magnitude); }
                _gap += Random.value * 0.1f;
                if (_gap > _bestGap) { _bestGap = _gap; _cell = _c; }
            }
            MummyGender _gender = Random.value < 0.5f ? MummyGender.Female : MummyGender.Male;
            var _element = ElementInfo.Random();
            Vector3 _floor = _grid.CellCenter(_cell, _grid.FloorTopY);

            ElementFx.Flame(_element, _floor, _flameTime + _riseTime, 0.42f);
            yield return new WaitForSeconds(_flameTime * 0.6f);

            var _prefab = _gender == MummyGender.Female ? _femalePrefab : _malePrefab;
            // 소환기 자식으로 생성: 게임이 끝나 소환기가 꺼지면(ActiveOnlyWhilePlaying) 미라도 함께 멈춤
            var _go = Instantiate(_prefab, _floor - Vector3.up * 1.1f, Quaternion.LookRotation(FlatTo(_player ? _player.position : _floor + Vector3.forward, _floor), Vector3.up), transform);
            _go.name = "Mummy_" + _gender + "_" + ElementInfo.Name(_element);
            _pending--;   // 생성되는 순간부터 Mummy.Alive에 들어감
            var _m = _go.GetComponent<Mummy>();
            _m.Grid = _grid; _m.Player = _player; _m.PlayerJump = _jump; _m.PlayerMoveSpeed = _speed != null ? _speed.BaseMoveSpeed : 4f;
            _m.PlayerHealth = _health; _m.Pharaoh = _pharaoh; _m.PharaohHealth = _pharaohHealth;
            _m.PharaohDamage = _pharaohDamagePerKill;
            _m.Init(_gender, _element);
            if (_m.Animator) _m.Animator.Play("Walk", 0, 0f);

            Vector3 _top = _floor + Vector3.up * _footOffset, _bottom = _go.transform.position;
            for (float _t = 0; _t < _riseTime; _t += Time.deltaTime)
            {
                float _u = _t / _riseTime; _u = 1f - (1f - _u) * (1f - _u);
                _go.transform.position = Vector3.Lerp(_bottom, _top, _u);
                yield return null;
            }
            _go.transform.position = _top;
            _m.Activate();
            Debug.Log("[Mummy] spawned " + _go.name + " at cell " + _cell);
        }

        private static Vector3 FlatTo(Vector3 _target, Vector3 _from)
        {
            Vector3 _d = _target - _from; _d.y = 0;
            return _d.sqrMagnitude > 1e-4f ? _d.normalized : Vector3.forward;
        }
    }
}
