using HistoryCrash.Cutscene;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoryCrash.Player
{
    /// <summary>
    /// 플레이어가 맞으면 움찔하는 동작(0.5초). 지금 재생 중인 모션(대기·이동·시전) 위에 덧붙이므로 이동과 시전을 끊지 않는다.
    /// 체력이 줄어든 것을 읽기만 하고, 죽는 한 방에는 하지 않음(게임 오버 컷씬이 이어받음).
    /// 씬에 따로 놓지 않아도 BossIntroCutscene.SCENES 씬이 열리면 자동으로 생긴다.
    /// </summary>
    [DefaultExecutionOrder(200)]   // Animator와 다른 LateUpdate 뒤
    public class PlayerHitMotion : MonoBehaviour
    {
        private const float DUR = 0.5f;
        // 시간(초), 값 … 각도는 모델 기준(앞으로 숙임 +), 엉덩이는 m
        private static readonly AnimationCurve SPINE_PITCH = C(0, 0, .07f, -22, .2f, 8, .5f, 0), SPINE_YAW = C(0, 0, .08f, -10, .25f, 3, .5f, 0),
            HEAD_PITCH = C(0, 0, .05f, 14, .13f, -12, .27f, 6, .5f, 0), HIP_Z = C(0, 0, .09f, -.02f, .5f, 0), HIP_Y = C(0, 0, .09f, -.005f, .22f, -.012f, .5f, 0),
            L_ARM_OUT = C(0, 0, .11f, 30, .5f, 0), L_FOREARM = C(0, 0, .12f, -25, .5f, 0), R_ARM_OUT = C(0, 0, .11f, 10, .5f, 0), R_ARM_PITCH = C(0, 0, .1f, -8, .5f, 0);
        private static readonly string[] BONES = { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftArm", "LeftForeArm", "RightArm",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };

        private readonly Transform[] _b = new Transform[BONES.Length];
        private readonly Quaternion[] _base = new Quaternion[BONES.Length], _wrote = new Quaternion[BONES.Length];
        private Vector3 _hipBase, _hipWrote;
        private Transform _model; private IHealthReadOnly _hp; private PlayerHealth _jmcHp;
        private int _lastHp = -1; private float _t = DUR, _nextFind;

        private static AnimationCurve C(params float[] _k)
        {
            var _c = new AnimationCurve();
            for (int _i = 0; _i < _k.Length; _i += 2) _c.AddKey(new Keyframe(_k[_i], _k[_i + 1], 0f, 0f));
            return _c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene _s, LoadSceneMode _m) { AutoCreate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (System.Array.IndexOf(BossIntroCutscene.SCENES, SceneManager.GetActiveScene().name) < 0 || FindFirstObjectByType<PlayerHitMotion>()) return;
            new GameObject("PlayerHitMotion").AddComponent<PlayerHitMotion>();
        }

        private bool FindPlayer()
        {
            if (_model) return true;
            if (Time.unscaledTime < _nextFind) return false;
            _nextFind = Time.unscaledTime + 0.5f;
            var _gm = GameManager.Instance; Transform _p = null;
            if (_gm && _gm.PlayerHealth is Component _pc && _pc) { _hp = _gm.PlayerHealth; _p = _pc.transform; }
            else { _jmcHp = FindFirstObjectByType<PlayerHealth>(); if (_jmcHp) _p = _jmcHp.transform; }
            var _anim = _p ? _p.GetComponentInChildren<Animator>() : null;
            if (!_anim) return false;
            foreach (var _x in _anim.GetComponentsInChildren<Transform>())
            {
                int _i = System.Array.IndexOf(BONES, _x.name);
                if (_i >= 0) _b[_i] = _x;
            }
            foreach (var _x in _b) if (!_x) return false;
            _model = _anim.transform; _lastHp = Hp();
            return true;
        }

        private int Hp() => _hp != null ? _hp.CurrentHp.CurrentValue : _jmcHp ? _jmcHp.HP : -1;

        private void LateUpdate()
        {
            if (!FindPlayer()) return;
            int _now = Hp();
            if (_now < _lastHp) _t = 0f;
            if (_now <= 0) _t = DUR;
            _lastHp = _now;
            if (_t >= DUR) return;
            Apply(_t);
            _t += Time.deltaTime;
        }

        private void Apply(float _time)
        {
            // Animator가 이번 프레임에 뼈를 쓰지 않았으면(멈춤·화면 밖) 앞 프레임에 덧붙인 것을 걷어내고 다시 덧붙인다
            for (int _i = 0; _i < _b.Length; _i++)
            {
                if (_b[_i].localRotation.Equals(_wrote[_i])) _b[_i].localRotation = _base[_i];
                _base[_i] = _b[_i].localRotation;
            }
            if (_b[0].localPosition.Equals(_hipWrote)) _b[0].localPosition = _hipBase;
            _hipBase = _b[0].localPosition;

            Vector3 _right = _model.right, _up = _model.up, _fwd = _model.forward;
            Vector3 _lf = _b[11].position, _rf = _b[14].position; Quaternion _lr = _b[11].rotation, _rr = _b[14].rotation;
            float _yaw = SPINE_YAW.Evaluate(_time) * 0.25f, _pitch = SPINE_PITCH.Evaluate(_time) / 3f, _head = HEAD_PITCH.Evaluate(_time);
            _b[0].position += _up * HIP_Y.Evaluate(_time) + _fwd * HIP_Z.Evaluate(_time);
            Turn(0, _yaw, _up);
            for (int _i = 1; _i <= 3; _i++) { Turn(_i, _pitch, _right); Turn(_i, _yaw, _up); }
            Turn(4, _head * 0.4f, _right); Turn(5, _head * 0.6f, _right);
            Turn(6, -L_ARM_OUT.Evaluate(_time), _fwd); Turn(7, L_FOREARM.Evaluate(_time), _right);
            Turn(8, R_ARM_OUT.Evaluate(_time), _fwd); Turn(8, R_ARM_PITCH.Evaluate(_time), _right);
            Leg(_b[9], _b[10], _b[11], _lf, _lr); Leg(_b[12], _b[13], _b[14], _rf, _rr);

            for (int _i = 0; _i < _b.Length; _i++) _wrote[_i] = _b[_i].localRotation;
            _hipWrote = _b[0].localPosition;
        }

        private void Turn(int _i, float _angle, Vector3 _axis) { _b[_i].rotation = Quaternion.AngleAxis(_angle, _axis) * _b[_i].rotation; }

        // 엉덩이가 움직여도 발은 원래 모션의 자리에 둔다(두 뼈 IK)
        private static void Leg(Transform _upper, Transform _lower, Transform _foot, Vector3 _target, Quaternion _footRot)
        {
            Vector3 _h = _upper.position, _k = _lower.position, _a = _foot.position;
            float _l1 = (_k - _h).magnitude, _l2 = (_a - _k).magnitude;
            float _d = Mathf.Clamp((_target - _h).magnitude, Mathf.Abs(_l1 - _l2) + 0.001f, _l1 + _l2 - 0.0005f);
            Vector3 _n = Vector3.Cross(_h - _k, _a - _k);
            if (_n.sqrMagnitude > 1e-8f)
            {
                float _want = Mathf.Acos(Mathf.Clamp((_l1 * _l1 + _l2 * _l2 - _d * _d) / (2f * _l1 * _l2), -1f, 1f)) * Mathf.Rad2Deg;
                _lower.rotation = Quaternion.AngleAxis(_want - Vector3.Angle(_h - _k, _a - _k), _n.normalized) * _lower.rotation;
            }
            _upper.rotation = Quaternion.FromToRotation(_foot.position - _h, _target - _h) * _upper.rotation;
            _foot.rotation = _footRot;
        }
    }
}
