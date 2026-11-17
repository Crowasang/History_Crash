using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using HistoryCrash.CameraRig;
using HistoryCrash.Combat;
using HistoryCrash.Data;
using HistoryCrash.Enemy;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using HistoryCrash.UI;

namespace HistoryCrash.Boss
{
            public class StaffDropEntry { 
            private GameObject _root;
            public GameObject Root { get => _root; set => _root = value; }

            private int _pattern;
            public int Pattern { get => _pattern; set => _pattern = value; }

            private Vector2Int _cell;
            public Vector2Int Cell { get => _cell; set => _cell = value; }

            private bool _landed;
            public bool Landed { get => _landed; set => _landed = value; }

            private float _landTime;
            public float LandTime { get => _landTime; set => _landTime = value; }

            private Material _mat;
            public Material Mat { get => _mat; set => _mat = value; }

            private Renderer[] _iconRenderers;
            public Renderer[] IconRenderers { get => _iconRenderers; set => _iconRenderers = value; }
    }
}
