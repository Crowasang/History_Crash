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
                FaceCamera(_ball);
            }
            if (_age >= _lifetime) Destroy(gameObject);
        }

        /// <summary>에너지 코어의 빛 판(Bb_ 이름)이 항상 화면을 보게 한다(바닥 오브·스태프 소켓 오브 공용)</summary>
        public static void FaceCamera(Transform _ball)
        {
            var _cam = Camera.main;
            if (!_cam || !_ball) return;
            for (int _i = 0; _i < _ball.childCount; _i++)
            {
                var _c = _ball.GetChild(_i);
                if (_c.name.StartsWith(ElementOrb.BILLBOARD_PREFIX)) _c.rotation = Quaternion.LookRotation(_c.position - _cam.transform.position, _cam.transform.up);
            }
        }
    }
}
