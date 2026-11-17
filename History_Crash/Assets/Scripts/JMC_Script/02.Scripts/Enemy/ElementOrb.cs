using System.Collections;
using UnityEngine;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Enemy
{
    /// <summary>머미가 떨어뜨리는 속성 오브. 바닥 위에 떠서 빛나며 E키로 줍는다.</summary>
    public static class ElementOrb
    {
        /// <summary>바닥 오브 지름(m)</summary>
        public const float ORB_SIZE = 0.26f;

        public static GameObject Spawn(Element _e, Vector2Int _cell, TileGrid _grid, float _lifetime)
        {
            Vector3 _c = _grid.CellCenter(_cell, _grid.FloorTopY);
            var _root = new GameObject("Orb_" + ElementInfo.Name(_e));
            _root.transform.position = _c;
            var _item = _root.AddComponent<PickupItem>();
            _item.Kind = PickupKind.Orb; _item.Value = (int)_e; _item.Cell = _cell; _item.Ready = true;
            // Blender에서 만든 속성 오브 모델(Resources/Orbs/Orb_<속성>, 지름 1로 정규화). 없으면 기존 구체
            GameObject _ball;
            var _prefab = Resources.Load<GameObject>("Orbs/Orb_" + ElementInfo.Name(_e));
            if (_prefab) _ball = Object.Instantiate(_prefab);
            else
            {
                _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(_ball.GetComponent<Collider>());
                _ball.GetComponent<Renderer>().sharedMaterial = ElementFx.OrbMat(_e);
            }
            _ball.name = "Ball";
            _ball.transform.SetParent(_root.transform, false);
            _ball.transform.localScale = Vector3.one * ORB_SIZE;
            _ball.transform.localPosition = new Vector3(0, 0.28f, 0);
            var _trail = ElementFx.Trail(_e, _ball.transform, 0.07f, 10f);
            var _tm = _trail.main; _tm.simulationSpace = ParticleSystemSimulationSpace.Local;
            ElementFx.Burst(_e, _ball.transform.position, 0.6f, 12);
            _root.AddComponent<OrbMotion>().Lifetime = _lifetime;
            return _root;
        }
    }
}
