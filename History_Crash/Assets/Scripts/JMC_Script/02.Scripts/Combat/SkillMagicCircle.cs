using System.Collections;
using System.Collections.Generic;
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
    public class SkillMagicCircle
    {
        private Transform _root;
        private Transform _spin;
        public Transform Root { get => _root; set => _root = value; }
        public Transform Spin { get => _spin; set => _spin = value; }

        private LineRenderer _outer;
        private LineRenderer _inner;
        private LineRenderer _star;
        private LineRenderer _ticks;
        public LineRenderer Outer { get => _outer; set => _outer = value; }
        public LineRenderer Inner { get => _inner; set => _inner = value; }
        public LineRenderer Star { get => _star; set => _star = value; }
        public LineRenderer Ticks { get => _ticks; set => _ticks = value; }

        private Vector3[] _outerPts;
        public Vector3[] OuterPts { get => _outerPts; set => _outerPts = value; }

        private Color _color;
        public Color Color { get => _color; set => _color = value; }
        public void Set(float _draw, float _bright, float _time, float _scale = 1f)
        {
            int _n = Mathf.Max(2, Mathf.CeilToInt(_draw * (_outerPts.Length - 1)) + 1);
            _outer.positionCount = _n;
            for (int _i = 0; _i < _n; _i++) _outer.SetPosition(_i, _outerPts[_i]);
            float _inside = Mathf.Clamp01((_draw - 0.35f) / 0.65f);          // 바깥 고리 이후 안쪽 문양이 드러남
            Paint(_outer, _bright); Paint(_inner, _bright * _inside); Paint(_star, _bright * _inside * 0.9f); Paint(_ticks, _bright * _inside * 0.8f);
            _spin.localRotation = Quaternion.Euler(0, _time * 45f, 0);
            _root.localScale = Vector3.one * _scale;
        }
        private void Paint(LineRenderer _lr, float _a) { var _k = _color; _k.a = Mathf.Clamp01(_a); _lr.startColor = _lr.endColor = _k; }
    }
}
