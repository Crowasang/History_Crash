using UnityEngine;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Combat
{
    /// <summary>
    /// Player의 공격체. 오브 속성색으로 빛나며 직선으로 날아간다.
    /// 같은 속성의 머미에만 맞고(맞으면 사라짐), 다른 속성 머미는 통과한다. 벽·기둥(걷기 불가 칸)에 닿으면 깨진다.
    /// </summary>
    public class PlayerBolt : MonoBehaviour
    {
        private Element _element; private Vector3 _dir; private float _speed, _range, _hitRadius, _traveled; private TileGrid _grid;

        public static PlayerBolt Fire(Element _e, int _staffPattern, Vector3 _start, Vector3 _dir, float _speed, float _range, float _hitRadius, TileGrid _grid)
        {
            var _go = new GameObject("PlayerBolt_" + ElementInfo.Name(_e));
            _go.transform.position = _start;
            _go.transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
            var _core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_core.GetComponent<Collider>());
            _core.transform.SetParent(_go.transform, false);
            _core.transform.localScale = new Vector3(0.22f, 0.22f, 0.42f);
            _core.GetComponent<Renderer>().sharedMaterial = ElementFx.GlowMat(_e, 6f);
            ElementFx.Trail(_e, _go.transform, 0.26f, 140f);
            ElementFx.Glow(_e, _go.transform, 3f, 3.5f);
            ElementFx.Burst(_e, _start, 0.7f, 14);
            var _b = _go.AddComponent<PlayerBolt>();
            _b._element = _e; _b._dir = _dir; _b._speed = _speed; _b._range = _range; _b._hitRadius = _hitRadius; _b._grid = _grid;
            return _b;
        }

        private void Update()
        {
            float _step = _speed * Time.deltaTime;
            Vector3 _p = transform.position + _dir * _step;
            _traveled += _step;
            transform.position = _p;

            foreach (var _m in Mummy.Alive.ToArray())
            {
                if (!_m || !_m.IsAlive || !_m.Active) continue;
                Vector3 _d = _m.transform.position - _p; _d.y = 0;
                if (_d.magnitude > _hitRadius) continue;
                if (_m.TryHit(_element, _dir)) { End(true); return; }
            }
            if (_grid != null && _traveled > 0.6f)
            {
                var _c = _grid.WorldToCell(_p);
                if (!_grid.InBounds(_c) || !_grid.IsWalkable(_c)) { End(true); return; }
            }
            if (_traveled >= _range) End(false);
        }

        private void End(bool _burst)
        {
            if (_burst) ElementFx.Burst(_element, transform.position, 1f, 22);
            var _trail = GetComponentInChildren<ParticleSystem>();
            if (_trail) { _trail.transform.SetParent(null, true); var _em = _trail.emission; _em.rateOverTime = 0; Destroy(_trail.gameObject, 0.8f); }
            Destroy(gameObject);
        }
    }
}
