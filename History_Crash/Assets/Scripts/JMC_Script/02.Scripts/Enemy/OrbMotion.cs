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
    /// <summary>오브가 위아래로 떠다니며 천천히 돌고(호루스의 눈 무늬가 보이게), 수명 끝에 줄어들며 사라짐</summary>
    public class OrbMotion : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("lifetime")] private float _lifetime = 12f;
        public float Lifetime { get => _lifetime; set => _lifetime = value; }
        [SerializeField, FormerlySerializedAs("spinSpeed")] private float _spinSpeed = 70f;
        private float _born; private Transform _ball; private Vector3 _basePos, _baseScale;
        private void Start() { _born = Time.time; _ball = transform.Find("Ball"); if (_ball) { _basePos = _ball.localPosition; _baseScale = _ball.localScale; } }
        private void Update()
        {
            float _age = Time.time - _born;
            if (_ball)
            {
                _ball.localPosition = _basePos + Vector3.up * Mathf.Sin(_age * 2.6f) * 0.05f;
                _ball.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
                float _left = _lifetime - _age;
                if (_left < 1f) _ball.localScale = _baseScale * Mathf.Max(0f, _left);
            }
            if (_age >= _lifetime) Destroy(gameObject);
        }
    }
}
