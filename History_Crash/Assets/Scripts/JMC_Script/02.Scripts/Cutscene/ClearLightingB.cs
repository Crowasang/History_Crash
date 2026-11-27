using HistoryCrash.Boss;
using UnityEngine;
using UnityEngine.Rendering;

namespace HistoryCrash.Cutscene
{
    /// <summary>
    /// (조명 시험용) 클리어 연출 중 신전이 복원되는 구간에 조명을 A3(감염된 밤) → B(정화된 밝은 신전)로 바꾼다.
    /// 씬에 직접 놓아 쓴다(Game_LightTest_A3). ClearCutscene은 고치지 않고, 이 씬에서 자동 생성이 안 되므로 여기서 만들어 준다.
    /// 시점은 ClearCutscene과 같은 24fps 기준: 클리어 + (54 + 95)프레임부터 65프레임 동안 바뀜(신전 복원·새벽빛과 같은 구간).
    /// ClearCutscene이 해를 매 프레임 바꾸므로 LateUpdate에서 덮어쓴다.
    /// </summary>
    public class ClearLightingB : MonoBehaviour
    {
        [Header("B 조명(정화)")]
        public Color sunColor = new Color(1f, 0.89f, 0.80f);          // 5,000K
        public float sunIntensity = 1.4f;
        public Vector3 sunDirection = new Vector3(0.45f, -0.85f, 0.25f);
        public Color ambient = new Color(0.36f, 0.40f, 0.48f);        // 8,000K 하늘 필
        public Color fogColor = new Color(0.62f, 0.62f, 0.60f);
        public float fogDensity = 0.006f;
        public Color skyColor = new Color(0.25f, 0.35f, 0.55f);       // 복원 쪽 카메라 배경(하늘 재질이 없을 때)
        public Material skyMaterial;                                   // 클리어용 HDRI 하늘(Skybox/Panoramic). 복원 쪽 카메라에만
        public float skyExposure = 1f;
        [Range(0, 360)] public float skyRotation = 0f;
        public Volume bVolume;                                         // B 후처리(가중치 0 → 1)
        public float torchIntensity = 0.6f;                           // B에서도 횃불은 약하게 남김
        public Light[] torches;

        [Header("시점(24fps 프레임)")]
        public float startFrame = 54f + 95f, lengthFrames = 65f;

        private bool _cleared; private float _t0;
        private Light _sun; private Quaternion _sunRot0; private Color _amb0, _fog0; private float _fogD0;
        private PharaohHealth _jmcBoss;

        private void Start()
        {
            if (FindFirstObjectByType<ClearCutscene>() == null && ClearCutscene.Enabled) new GameObject("ClearCutscene").AddComponent<ClearCutscene>();
            foreach (var _l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (_l.type == LightType.Directional && (!_sun || _l.intensity > _sun.intensity)) _sun = _l;
            if (bVolume) bVolume.weight = 0f;
        }

        private bool Cleared()
        {
            var _gm = GameManager.Instance;
            if (_gm) return _gm.CurrentState.CurrentValue == GameStateType.Clear;
            if (!_jmcBoss) _jmcBoss = FindFirstObjectByType<PharaohHealth>();
            return _jmcBoss && _jmcBoss.IsDead;
        }

        private void LateUpdate()
        {
            if (!_cleared)
            {
                if (!Cleared()) return;
                _cleared = true; _t0 = Time.unscaledTime;
                if (_sun) _sunRot0 = _sun.transform.rotation;
                _amb0 = RenderSettings.ambientLight; _fog0 = RenderSettings.fogColor; _fogD0 = RenderSettings.fogDensity;
            }
            float f = (Time.unscaledTime - _t0) * 24f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - startFrame) / lengthFrames));
            if (k <= 0f) return;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(_amb0, ambient, k);
            RenderSettings.fogColor = Color.Lerp(_fog0, fogColor, k);
            RenderSettings.fogDensity = Mathf.Lerp(_fogD0, fogDensity, k);
            if (_sun)
            {
                _sun.color = Color.Lerp(_sun.color, sunColor, k);
                _sun.intensity = Mathf.Lerp(_sun.intensity, sunIntensity, k);
                _sun.transform.rotation = Quaternion.Slerp(_sunRot0, Quaternion.LookRotation(sunDirection.normalized), k);
            }
            if (bVolume) bVolume.weight = k;
            foreach (var _t in torches) if (_t) { _t.enabled = true; _t.intensity = torchIntensity * k; }
            var _cb = GameObject.Find("Clear_CamB");
            if (_cb && _cb.TryGetComponent(out Camera _c))
            {
                if (skyMaterial)
                {
                    if (!_sky) { _sky = new Material(skyMaterial); var _s = _cb.GetComponent<Skybox>(); if (!_s) _s = _cb.AddComponent<Skybox>(); _s.material = _sky; _c.clearFlags = CameraClearFlags.Skybox; }
                    _sky.SetFloat("_Exposure", skyExposure * k); _sky.SetFloat("_Rotation", skyRotation);   // 노출 0 = 검은 밤하늘 → 낮 하늘
                }
                else _c.backgroundColor = Color.Lerp(new Color(0.015f, 0.015f, 0.025f), skyColor, k);
            }
        }
        private Material _sky;
        private void OnDestroy() { if (_sky) Destroy(_sky); }
    }
}
