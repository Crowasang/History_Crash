using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.UI;

namespace HistoryCrash.Stage
{
    public class HorusTile
    {
        private Transform _mesh;
        public Transform Mesh { get => _mesh; set => _mesh = value; }

        private Renderer _r;
        public Renderer R { get => _r; set => _r = value; }

        private Vector2Int _cell;
        public Vector2Int Cell { get => _cell; set => _cell = value; }

        private Vector3 _basePos;
        private Vector3 _baseScale;
        private Vector3 _center;
        public Vector3 BasePos { get => _basePos; set => _basePos = value; }
        public Vector3 BaseScale { get => _baseScale; set => _baseScale = value; }
        public Vector3 Center { get => _center; set => _center = value; }
        private GameObject _glow;
        private GameObject _pit;
        public GameObject Glow { get => _glow; set => _glow = value; }
        public GameObject Pit { get => _pit; set => _pit = value; }
    }
}
