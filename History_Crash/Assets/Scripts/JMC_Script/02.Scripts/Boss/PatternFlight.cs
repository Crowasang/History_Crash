using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
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
    /// <summary>발사 이후 직선 비행 상태. 위치만 덮어쓰고 회전(스핀)·크기는 클립 그대로 쓴다.</summary>
    public class PatternFlight
    {
        private bool _armed;
        private bool _active;
        private bool _done;
        private bool _straight;
        private bool _notified;
        public bool Armed { get => _armed; set => _armed = value; }
        public bool Active { get => _active; set => _active = value; }
        public bool Done { get => _done; set => _done = value; }
        public bool Straight { get => _straight; set => _straight = value; }
        public bool Notified { get => _notified; set => _notified = value; }

        private float _zRef;
        public float ZRef { get => _zRef; set => _zRef = value; }
        private Vector3 _start;
        private Vector3 _dir;
        public Vector3 Start { get => _start; set => _start = value; }
        public Vector3 Dir { get => _dir; set => _dir = value; }

        private float _traveled;
        private float _hitDist;
        private float _total;
        private float _doneTime;
        public float Traveled { get => _traveled; set => _traveled = value; }
        public float HitDist { get => _hitDist; set => _hitDist = value; }
        public float Total { get => _total; set => _total = value; }
        public float DoneTime { get => _doneTime; set => _doneTime = value; }
        public void Reset() { _armed = _active = _done = _straight = _notified = false; _traveled = 0; }
    }
}
