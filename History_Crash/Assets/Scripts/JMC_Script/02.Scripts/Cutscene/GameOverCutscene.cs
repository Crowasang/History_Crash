using System.Collections;
using System.Collections.Generic;
using HistoryCrash.Player;
using HistoryCrash.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryCrash.Cutscene
{
    /// <summary>
    /// 게임 오버 연출(샘플 '게임오버연출_v2' 확정본을 게임에서 재생, 24fps 기준 120프레임 = 5초).
    ///   0–50   플레이어 큰 피격 동작, 시간이 느려지다 멈춤, 카메라가 돌며 다가감, 색이 빠지고 가장자리부터 초록 균열이 번져 암전
    ///   50–    검은 화면에 초록 불꽃에 감싸인 GAME OVER (계속 타오름)
    ///   100–118 Retry / Main Menu 선택지(마우스·방향키·W/S·Enter). Retry는 같은 씬을 다시 시작하고 보스 소개는 건너뜀
    /// 시작: 팀 GameManager가 Fail이 되면(Game), GameManager가 없으면 JMC PlayerHealth.HP가 0이 되면(TEST_JMC).
    /// 팀 실패 화면(FailPanel)을 포함한 HUD는 투명하게만 만들고, 씬·팀 스크립트는 고치지 않는다.
    /// 데이터: Resources/Cutscene/GameOver/. 씬에 따로 놓지 않아도 BossIntroCutscene.SCENES 씬이 열리면 자동으로 생긴다.
    /// </summary>
    public class GameOverCutscene : MonoBehaviour
    {
        /// <summary>false면 재생하지 않음(테스트용). 그때는 팀 실패 화면이 그대로 보임</summary>
        public static bool Enabled = true;

        private const string RES = "Cutscene/GameOver/";
        private const string MAIN_MENU = "MainMenu", MAIN_MENU_PATH = "Assets/Scenes/MainMenu.unity";
        private const float FPS = 24f, MENU_IN = 100f, MENU_READY = 112f, LOOP_AT = 4.55f;
        private static readonly Rect TITLE = new Rect(365f, 222f, 1193f, 428f);   // 1920x1080 기준, 왼쪽 위 원점
        private static readonly Color BG = new Color(0.004f, 0.012f, 0.006f);
        private static readonly Color TEXT_OFF = new Color(0.42f, 0.56f, 0.44f), TEXT_ON = new Color(0.62f, 1f, 0.6f);
        private static readonly int HIT = Animator.StringToHash("Hit_Large_Robe");
        private static readonly HashSet<string> LOCK = new HashSet<string>
            { "PlayerLook", "PauseInputHandler", "ThirdPersonCamera", "CinemachineBrain", "PlayerInput", "PlayerMover", "PlayerAnimator", "PlayerController" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene _s, LoadSceneMode _m)
        {
            if (_m != LoadSceneMode.Single) return;
            foreach (var _c in FindObjectsByType<GameOverCutscene>(FindObjectsSortMode.None)) _c.Restore();   // 새 씬의 Start보다 먼저 시간·입력을 되돌림
            AutoCreate();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!Enabled) return;
            if (System.Array.IndexOf(BossIntroCutscene.SCENES, SceneManager.GetActiveScene().name) < 0) return;
            foreach (var _c in FindObjectsByType<GameOverCutscene>(FindObjectsSortMode.None)) if (!_c._leaving) return;
            new GameObject("GameOverCutscene").AddComponent<GameOverCutscene>();
        }

        private bool _started, _leaving, _restored;
        private PlayerHealth _jmcHealth;
        private Transform _player; private Camera _cam;
        private Vector3 _pivot, _off0; private Quaternion _rot0;
        private readonly List<Behaviour> _locked = new List<Behaviour>();
        private readonly List<(CanvasGroup g, float a, bool ray, bool inter)> _hud = new List<(CanvasGroup, float, bool, bool)>();
        private GameObject _canvasGo; private RawImage _infect, _flame; private Image _bg; private Material _mInfect, _mFlame;
        private CanvasGroup _menu; private TextMeshProUGUI[] _items; private Image[] _lines; private int _sel; private Vector2 _lastMouse;
        private Volume _vol; private VolumeProfile _prof; private ColorAdjustments _ca;
        private AudioSource _mix, _loop; private float _out = 1f, _t0;

        private bool PlayerDead()
        {
            var _gm = GameManager.Instance;
            if (_gm) return _gm.CurrentState.CurrentValue == GameStateType.Fail;
            if (!_jmcHealth) _jmcHealth = FindFirstObjectByType<PlayerHealth>();
            return _jmcHealth && _jmcHealth.HP <= 0;
        }

        private void Update()
        {
            if (_started || !PlayerDead()) return;
            _started = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            PauseSettingsUI.InputBlocked = true;
            bool _ok = true;
            try { Setup(); } catch (System.Exception _e) { Debug.LogException(_e, this); _ok = false; }
            if (!_ok) { Restore(); if (_canvasGo) Destroy(_canvasGo); yield break; }   // 실패하면 팀 실패 화면이 그대로 보이게
            _t0 = Time.unscaledTime;
            if (_mix.clip) _mix.Play();
            while (!_leaving)
            {
                Apply(Frame);
                if (Frame >= MENU_READY) Menu();
                yield return null;
            }
        }
        private float Frame => (Time.unscaledTime - _t0) * FPS;

        private void Setup()
        {
            _cam = Camera.main;
            var _gm = GameManager.Instance;
            if (_gm && _gm.PlayerHealth is Component _pc && _pc) _player = _pc.transform;
            else if (_jmcHealth) _player = _jmcHealth.transform;

            foreach (var _b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                if (_b && _b.enabled && LOCK.Contains(_b.GetType().Name)) { _b.enabled = false; _locked.Add(_b); }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

            var _anim = _player ? _player.GetComponentInChildren<Animator>() : null;
            if (_anim && _anim.HasState(0, HIT)) _anim.CrossFadeInFixedTime(HIT, 0.08f, 0);

            if (_cam)
            {
                _pivot = _player ? Center(_player) : _cam.transform.position + _cam.transform.forward * 5f;
                _off0 = _cam.transform.position - _pivot; _rot0 = _cam.transform.rotation;
            }

            HideHud();
            BuildUI();
            BuildVolume();
            StageBgm.FadeOut(0.6f);
            HistoryCrash.Sound.GameSfx.StopAll(0.3f);
            foreach (var _a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (_a.GetComponent<BossIntroCutscene>()) _a.Stop();
            _mix = NewAudio("GameOver_Mix", false); _loop = NewAudio("GameOver_Loop", true);
        }

        private static Vector3 Center(Transform _t)
        {
            var _c = _t.GetComponentInChildren<CapsuleCollider>(); if (_c) return _c.bounds.center;
            var _k = _t.GetComponentInChildren<CharacterController>(); if (_k) return _k.bounds.center;
            return _t.position + Vector3.up * 0.9f;
        }

        private void Apply(float f)
        {
            // ---- 시간: 느려지다 멈춤 ----
            Time.timeScale = Mathf.Lerp(0.35f, 0f, S(f, 34, 44));

            // ---- 카메라: 돌며 다가감(50프레임 뒤엔 검은 화면) ----
            if (_cam && f < 60f)
            {
                float e = E(S(f, 0, 50)), _d0 = _off0.magnitude, _d1 = Mathf.Min(_d0, Mathf.Max(3f, _d0 * 0.55f));
                Vector3 _dir = Quaternion.AngleAxis(36f * e, Vector3.up) * _off0.normalized;
                float _d = Mathf.Lerp(_d0, _d1, e);
                foreach (var _h in Physics.SphereCastAll(_pivot, 0.25f, _dir, _d, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    if (_h.distance > 0.6f && (!_player || !_h.transform.IsChildOf(_player))) _d = Mathf.Min(_d, Mathf.Max(1.2f, _h.distance));   // 벽에 파묻히지 않게
                _cam.transform.position = _pivot + _dir * _d;
                _cam.transform.rotation = Quaternion.Slerp(_rot0, Quaternion.LookRotation(_pivot - _cam.transform.position, Vector3.up), E(S(f, 0, 14)));
            }

            // ---- 1부: 색 빠짐 → 감염 → 암전 ----
            float _ds = E(S(f, 4, 30));
            _ca.saturation.value = -75f * _ds;
            bool _partB = f >= 50f;
            _infect.enabled = !_partB; _bg.enabled = _partB; _flame.enabled = _partB;
            if (!_partB)
            {
                _mInfect.SetFloat("_In", E(S(f, 0, 6))); _mInfect.SetFloat("_Ds", _ds);
                _mInfect.SetFloat("_T", E(S(f, 18, 46))); _mInfect.SetFloat("_Black", E(S(f, 40, 50)));
            }
            else
            {
                // ---- 2부: 불꽃 글자 ----
                _mFlame.SetFloat("_F", f - 50f);
                _mFlame.SetFloat("_K", E(S(f, 50, 66)) * _out); _mFlame.SetFloat("_Kf", E(S(f, 52, 72)));
            }

            // ---- 선택지·소리 ----
            _menu.alpha = E(S(f, MENU_IN, MENU_IN + 18f)) * _out;
            float _v = BossIntroCutscene.Volume() * _out, _sec = f / FPS;
            _mix.volume = _v;
            if (_sec >= LOOP_AT && _loop.clip && !_loop.isPlaying) _loop.Play();
            _loop.volume = _v * Mathf.Clamp01((_sec - LOOP_AT) / 0.45f);
        }

        private void Menu()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            var _kb = Keyboard.current; var _ms = Mouse.current;
            if (_ms != null)
            {
                Vector2 _p = _ms.position.ReadValue(); int _hit = -1;
                for (int i = 0; i < _items.Length; i++) if (RectTransformUtility.RectangleContainsScreenPoint(_items[i].rectTransform, _p, null)) _hit = i;
                if (_hit >= 0 && (_p - _lastMouse).sqrMagnitude > 1f) _sel = _hit;   // 키보드로 고른 뒤 가만히 있는 마우스가 되돌리지 않게
                _lastMouse = _p;
                if (_hit >= 0 && _ms.leftButton.wasPressedThisFrame) { Choose(_hit); return; }
            }
            if (_kb != null)
            {
                if (_kb.upArrowKey.wasPressedThisFrame || _kb.wKey.wasPressedThisFrame) _sel = 0;
                if (_kb.downArrowKey.wasPressedThisFrame || _kb.sKey.wasPressedThisFrame) _sel = 1;
                if (_kb.enterKey.wasPressedThisFrame || _kb.numpadEnterKey.wasPressedThisFrame) { Choose(_sel); return; }
            }
            float _k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < _items.Length; i++)
            {
                bool _on = i == _sel;
                _items[i].color = Color.Lerp(_items[i].color, _on ? TEXT_ON : TEXT_OFF, _k);
                var _c = _lines[i].color; _c.a = Mathf.Lerp(_c.a, _on ? 0.7f : 0f, _k); _lines[i].color = _c;
            }
        }

        // 0 = Retry, 1 = Main Menu
        private void Choose(int _i) { if (_leaving) return; _sel = _i; _leaving = true; StartCoroutine(Leave(_i == 0)); }

        private IEnumerator Leave(bool _retry)
        {
            float _t = Time.unscaledTime;
            while (_out > 0f) { _out = 1f - Mathf.Clamp01((Time.unscaledTime - _t) / 0.5f); Apply(Frame); yield return null; }
            _mix.Stop(); _loop.Stop();
            Cursor.visible = false;

            DontDestroyOnLoad(gameObject);   // 검은 화면을 새 씬까지 이어 갔다가 걷음
            var _scene = SceneManager.GetActiveScene();
            string _name = _retry ? _scene.name : MAIN_MENU, _path = _retry ? _scene.path : MAIN_MENU_PATH;
            BossIntroCutscene.SkipNext = _retry;
            AsyncOperation _op = null;
            if (Application.CanStreamedLevelBeLoaded(_name)) _op = SceneManager.LoadSceneAsync(_name);
#if UNITY_EDITOR
            else _op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(_path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            if (_op == null) { Debug.LogError("[GameOverCutscene] 씬을 열 수 없음: " + _name); BossIntroCutscene.SkipNext = false; Destroy(gameObject); yield break; }
            while (!_op.isDone) yield return null;
            yield return null;
            _t = Time.unscaledTime; float _a = 1f;
            while (_a > 0f) { _a = 1f - Mathf.Clamp01((Time.unscaledTime - _t) / 0.6f); _bg.color = new Color(BG.r, BG.g, BG.b, _a); yield return null; }
            Destroy(gameObject);
        }

        // 시간·입력·HUD를 되돌림(한 번만). 씬이 바뀌어 사라진 것은 건너뜀
        private void Restore()
        {
            if (!_started || _restored) return;
            _restored = true;
            Time.timeScale = 1f;
            PauseSettingsUI.InputBlocked = false;
            foreach (var _b in _locked) if (_b) _b.enabled = true;
            foreach (var _h in _hud) if (_h.g) { _h.g.alpha = _h.a; _h.g.blocksRaycasts = _h.ray; _h.g.interactable = _h.inter; }
            if (_vol) Destroy(_vol.gameObject);
            if (_prof) Destroy(_prof);
        }

        private void OnDestroy()
        {
            if (_started && !_restored) { Time.timeScale = 1f; PauseSettingsUI.InputBlocked = false; }   // 플레이 종료 등
            if (_mInfect) Destroy(_mInfect);
            if (_mFlame) Destroy(_mFlame);
        }

        // ---------------- 만들기 ----------------
        private void HideHud()
        {
            foreach (var _c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!_c.isRootCanvas || _c.renderMode == RenderMode.WorldSpace) continue;
                var _g = _c.GetComponent<CanvasGroup>(); if (!_g) _g = _c.gameObject.AddComponent<CanvasGroup>();
                _hud.Add((_g, _g.alpha, _g.blocksRaycasts, _g.interactable));
                _g.alpha = 0f; _g.blocksRaycasts = false; _g.interactable = false;
            }
        }

        private void BuildVolume()
        {
            _prof = ScriptableObject.CreateInstance<VolumeProfile>();
            _ca = _prof.Add<ColorAdjustments>(false); _ca.active = true;
            _ca.saturation.overrideState = true; _ca.saturation.value = 0f;
            var _go = new GameObject("GameOver_Volume"); _go.transform.SetParent(transform);
            _vol = _go.AddComponent<Volume>(); _vol.isGlobal = true; _vol.priority = 900f; _vol.sharedProfile = _prof;
        }

        private void BuildUI()
        {
            _canvasGo = new GameObject("GameOver_UI", typeof(RectTransform));
            _canvasGo.transform.SetParent(transform, false);
            var _c = _canvasGo.AddComponent<Canvas>(); _c.renderMode = RenderMode.ScreenSpaceOverlay; _c.sortingOrder = 70;
            var _sc = _canvasGo.AddComponent<CanvasScaler>(); _sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _sc.referenceResolution = new Vector2(1920f, 1080f); _sc.matchWidthOrHeight = 0.5f;
            var _root = (RectTransform)_canvasGo.transform;

            _infect = NewGraphic<RawImage>("Infect", _root); Stretch(_infect.rectTransform);
            _infect.texture = Resources.Load<Texture2D>(RES + "GameOver_InfectMap");
            _mInfect = new Material(Resources.Load<Shader>(RES + "GameOver_Infect")); _infect.material = _mInfect;

            _bg = NewGraphic<Image>("Black", _root); Stretch(_bg.rectTransform); _bg.color = BG; _bg.enabled = false;

            _flame = NewGraphic<RawImage>("Flame", _root);
            var _rt = _flame.rectTransform; _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = TITLE.size; _rt.anchoredPosition = new Vector2(TITLE.center.x - 960f, 540f - TITLE.center.y);
            _flame.texture = Resources.Load<Texture2D>(RES + "GameOver_Title"); _flame.enabled = false;
            _mFlame = new Material(Resources.Load<Shader>(RES + "GameOver_Flame")); _mFlame.SetTexture("_Noise", Resources.Load<Texture2D>(RES + "GameOver_Noise"));
            _flame.material = _mFlame;

            var _mr = (RectTransform)new GameObject("Menu", typeof(RectTransform)).transform; _mr.SetParent(_root, false); Stretch(_mr);
            _menu = _mr.gameObject.AddComponent<CanvasGroup>(); _menu.alpha = 0f;
            var _font = Resources.Load<TMP_FontAsset>("UI/Settings/Font/EBGaramond_BoldItalic_SDF");
            string[] _n = { "Retry", "Main Menu" };
            _items = new TextMeshProUGUI[2]; _lines = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var _t = NewGraphic<TextMeshProUGUI>(_n[i], _mr);
                if (_font) _t.font = _font;
                _t.text = _n[i]; _t.fontSize = 50f; _t.alignment = TextAlignmentOptions.Center; _t.color = i == 0 ? TEXT_ON : TEXT_OFF;
                var _r = _t.rectTransform; _r.anchorMin = _r.anchorMax = _r.pivot = new Vector2(0.5f, 0.5f);
                _r.sizeDelta = new Vector2(320f, 70f); _r.anchoredPosition = new Vector2(0f, -220f - i * 84f);
                _items[i] = _t;
                var _l = NewGraphic<Image>("Line", _r); _l.color = new Color(TEXT_ON.r, TEXT_ON.g, TEXT_ON.b, i == 0 ? 0.7f : 0f);
                var _lr = _l.rectTransform; _lr.anchorMin = _lr.anchorMax = _lr.pivot = new Vector2(0.5f, 0.5f);
                _lr.sizeDelta = new Vector2(_t.GetPreferredValues(_n[i]).x * 0.9f, 2f); _lr.anchoredPosition = new Vector2(0f, -32f);
                _lines[i] = _l;
            }
        }

        private AudioSource NewAudio(string _n, bool _isLoop)
        {
            var _a = gameObject.AddComponent<AudioSource>(); _a.clip = Resources.Load<AudioClip>(RES + _n);
            _a.playOnAwake = false; _a.loop = _isLoop; _a.spatialBlend = 0f; _a.ignoreListenerPause = true; _a.volume = 0f;
            return _a;
        }
        private static T NewGraphic<T>(string _name, RectTransform _parent) where T : Graphic
        {
            var _g = new GameObject(_name, typeof(RectTransform)).AddComponent<T>();
            _g.rectTransform.SetParent(_parent, false); _g.raycastTarget = false; return _g;
        }
        private static void Stretch(RectTransform _r) { _r.anchorMin = Vector2.zero; _r.anchorMax = Vector2.one; _r.offsetMin = _r.offsetMax = Vector2.zero; }
        private static float E(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float S(float f, float a, float b) { return Mathf.Clamp01((f - a) / (b - a)); }
    }
}
