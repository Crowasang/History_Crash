using HistoryCrash.Cutscene;
using HistoryCrash.Data;
using HistoryCrash.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 화면 한가운데 에임선(나침반 바늘). 색은 가진 속성 오브를 따르고, 오브가 없으면 양피지색.
    /// 씬에 따로 놓지 않아도 BossIntroCutscene.SCENES 씬이 열리면 자동으로 생긴다. 컷씬에서는 다른 HUD와 함께 숨겨진다.
    /// </summary>
    public class AimReticleUI : MonoBehaviour
    {
        private const string TEX = "UI/Aim/UI_Aim_";
        private const float SIZE = 91f;   // 1920×1080 기준(px)
        private static readonly Color NO_ORB = new Color(0.725f, 0.651f, 0.486f), NO_ORB_LIGHT = new Color(0.945f, 0.902f, 0.784f);

        // TODO: 새 조준점 UI를 만들 때까지 화면 가운데 에임을 숨김. 다시 보이게 하려면 true (Claude)
        private static readonly bool ENABLED = false;

        private PlayerLoadout _loadout;
        private Canvas _canvas;
        private RawImage _main, _light;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene _s, LoadSceneMode _m) { AutoCreate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!ENABLED) return;
            if (System.Array.IndexOf(BossIntroCutscene.SCENES, SceneManager.GetActiveScene().name) < 0) return;
            if (FindFirstObjectByType<AimReticleUI>() || !FindFirstObjectByType<PlayerLoadout>()) return;
            new GameObject("UI_AimReticle").AddComponent<AimReticleUI>();
        }

        private void Awake()
        {
            _loadout = FindFirstObjectByType<PlayerLoadout>();
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;   // 다른 HUD 아래
            var _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = 0.5f;
            _main = Layer("Main");
            _light = Layer("Light");
        }

        private RawImage Layer(string _name)
        {
            var _img = new GameObject(_name, typeof(RectTransform)).AddComponent<RawImage>();
            _img.transform.SetParent(transform, false);
            _img.rectTransform.sizeDelta = Vector2.one * SIZE;
            _img.texture = Resources.Load<Texture2D>(TEX + _name);
            _img.raycastTarget = false;
            return _img;
        }

        private void LateUpdate()
        {
            _canvas.enabled = _loadout && !PauseSettingsUI.IsOpen;
            if (!_canvas.enabled) return;
            Color _c = _loadout.HasOrb ? ElementInfo.Color(_loadout.HeldOrb) : NO_ORB;
            // 속성색 그대로면 물이 어두워서 흰색을 조금 섞음
            _main.color = _loadout.HasOrb ? Color.Lerp(_c, Color.white, 0.2f) : NO_ORB;
            _light.color = _loadout.HasOrb ? Color.Lerp(_c, Color.white, 0.7f) : NO_ORB_LIGHT;
        }
    }
}
