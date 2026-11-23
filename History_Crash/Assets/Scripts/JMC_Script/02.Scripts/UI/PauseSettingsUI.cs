using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using HistoryCrash.CameraRig;
using HistoryCrash.Player;

namespace HistoryCrash.UI
{
    /// <summary>
    /// ESC 설정창(일시 정지): 화면이 어두워지고 펼친 도감이 나타난다.
    /// - 왼쪽 페이지 = 메뉴(Resume·Settings·Controls·Main Menu·Quit Game), 오른쪽 페이지 = 고른 메뉴의 내용.
    /// - 열려 있는 동안 Time.timeScale = 0, 커서를 풀고, 카메라·공격·이동 입력은 막는다(IsOpen).
    /// - 설정 값은 PlayerPrefs에 저장하고 씬이 시작될 때 바로 적용한다(마우스 감도·상하 반전·음량·전체 화면·밝기).
    /// - 그림: Assets/Art/Resources/UI/Settings/ (Blender 도감 렌더 + 잉크·루비 조각), 글꼴: EB Garamond Bold Italic(SIL OFL 1.1).
    /// - 플레이어가 있는 씬에서 스스로 만들어지므로 씬 수정은 필요 없다.
    /// </summary>
    public class PauseSettingsUI : MonoBehaviour
    {
        /// <summary>설정창이 열려 있는지(다른 입력 스크립트가 확인)</summary>
        public static bool IsOpen { get; private set; }

        private const string TEX = "UI/Settings/";
        private const float BOOK_W = 960f, BOOK_H = 560f;   // 아래 좌표의 기준(도감 렌더 px). 실제 이미지는 2배 해상도
        private static readonly Color INK = new Color32(44, 26, 10, 228);
        private static readonly Color INK_SOFT = new Color32(70, 45, 20, 200);

        [Header("배치(기준 해상도 1920×1080)")]
        [SerializeField, Tooltip("도감 렌더 1px이 화면 몇 px인지")] private float _scale = 1.6f;
        [SerializeField] private float _yOffset = -12f;
        [SerializeField, Range(0f, 1f)] private float _dim = 0.62f;

        [Header("열기/닫기 연출(초, 게임이 멈춰도 흐르는 시간)")]
        [SerializeField, Tooltip("덮인 도감이 아래에서 올라오는 시간")] private float _riseTime = 0.32f;
        [SerializeField, Tooltip("표지가 넘어가고 낱장이 촤라락 넘어가는 시간(프레임 재생)")] private float _openTime = 0.8f;
        [SerializeField, Tooltip("다 펼친 뒤 글자가 번져 나타나는 시간")] private float _textTime = 0.25f;
        [SerializeField, Tooltip("닫을 때 전체 시간(거꾸로 재생 + 내려감)")] private float _closeTime = 0.45f;
        [SerializeField, Tooltip("덮였을 때 표지가 화면 가운데 오도록 옮기는 거리(도감 렌더 px). 렌더에서 덮인 책은 오른쪽 절반에 있으므로 왼쪽(-)으로")] private float _closedShift = -170f;

        // ---- 설정 값(PlayerPrefs) ----
        private const string K_SENS = "HC_MouseSens", K_INV = "HC_InvertY", K_MASTER = "HC_Master", K_MUSIC = "HC_Music", K_SFX = "HC_Effects", K_FULL = "HC_Fullscreen", K_BRIGHT = "HC_Brightness";
        private const float D_SENS = 5f, D_MASTER = 100f, D_MUSIC = 70f, D_SFX = 100f, D_BRIGHT = 50f;
        private const float SENS_TO_CAMERA = 0.024f;         // 슬라이더 5 = 카메라 0.12(기본값)

        private Canvas _canvas; private CanvasGroup _group;
        private RectTransform _book, _content;
        private CanvasGroup _contentGroup;
        private Image _bookImg, _dimImg;
        private Sprite _bookStatic; private Sprite[] _frames;   // 다 펼친 고해상도 / 펼침 프레임(Open/UI_Settings_Open_00~)
        private bool _busy;                                      // 열고 닫는 연출 중
        private TMP_FontAsset _font;
        private TextMeshProUGUI _rightTitle;
        private GameObject _settingsPage, _controlsPage;
        private readonly TextMeshProUGUI[] _menu = new TextMeshProUGUI[5];
        private Image _selL, _selR, _selLine;
        private int _page = 1;   // 1 Settings, 2 Controls
        private float _prevTimeScale = 1f;
        private Coroutine _fade;
        private Slider _sens, _master, _music, _sfx, _bright; private Toggle _invert, _full;
        private ColorAdjustments _colorAdj;

        // 메인메뉴 → 게임 씬처럼 씬이 바뀌어도 다시 만들어지게(AfterSceneLoad는 플레이를 시작할 때 한 번만 불림)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoaded() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedAutoCreate; UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedAutoCreate; }
        private static void OnSceneLoadedAutoCreate(UnityEngine.SceneManagement.Scene _s, UnityEngine.SceneManagement.LoadSceneMode _m) { AutoCreate(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            IsOpen = false;
            if (FindFirstObjectByType<PauseSettingsUI>()) return;
            if (!FindFirstObjectByType<PlayerMover>()) return;
            new GameObject("UI_PauseSettings").AddComponent<PauseSettingsUI>();
        }

        private void Start()
        {
            _font = Resources.Load<TMP_FontAsset>(TEX + "Font/EBGaramond_BoldItalic_SDF");
            EnsureEventSystem();
            Build();
            ApplyAll();
            _canvas.enabled = false;
        }

        private void OnDestroy() { if (IsOpen) { IsOpen = false; Time.timeScale = _prevTimeScale; } }

        private void Update()
        {
            var _kb = Keyboard.current;
            if (_kb != null && _kb.escapeKey.wasPressedThisFrame && !_busy) { if (IsOpen) Close(); else Open(); }
        }

        // ================= 열기/닫기 =================
        // 열기: 화면이 어두워지며 덮인 도감이 아래에서 튀어 올라옴 → 표지·책장·낱장이 넘어가며 펼쳐짐(프레임) → 고해상도 펼친 책 + 글자가 번져 나타남
        // 닫기: 글자가 사라짐 → 거꾸로 덮힘 → 아래로 내려가며 화면이 밝아짐. 게임은 다 닫힌 뒤 다시 흐른다
        public void Open()
        {
            if (IsOpen || _busy) return;
            IsOpen = true;
            _prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            ShowPage(1); ShowLanguage(); RefreshControls();
            _canvas.enabled = true;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(OpenAnim());
        }

        public void Close()
        {
            if (!IsOpen || _busy) return;
            PlayerPrefs.Save();
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CloseAnim());
        }

        private IEnumerator OpenAnim()
        {
            _busy = true;
            SetContent(0f);
            bool _has = _frames != null && _frames.Length > 1;
            _bookImg.sprite = _has ? _frames[0] : _bookStatic;
            // 1) 덮인 채로 올라옴(살짝 튀어 오름)
            for (float t = 0f; t < _riseTime; t += Time.unscaledDeltaTime)
            {
                float _u = t / _riseTime;
                SetDim(Smooth(_u));
                SetBook(_has ? _closedShift : 0f, (1f - EaseOutBack(_u)) * -720f);
                yield return null;
            }
            SetDim(1f); SetBook(_has ? _closedShift : 0f, 0f);
            // 2) 펼침 프레임 재생(표지가 넘어가는 동안 책등이 화면 가운데로 옴)
            if (_has)
            {
                for (float t = 0f; t < _openTime; t += Time.unscaledDeltaTime)
                {
                    float _u = t / _openTime;
                    _bookImg.sprite = _frames[Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(_u * _frames.Length))];
                    SetBook(_closedShift * (1f - Smooth(Mathf.Clamp01(_u * 2f))), 0f);
                    yield return null;
                }
            }
            // 3) 고해상도 펼친 책으로 바꾸고 글자가 번져 나타남
            _bookImg.sprite = _bookStatic; SetBook(0f, 0f);
            for (float t = 0f; t < _textTime; t += Time.unscaledDeltaTime) { SetContent(Smooth(t / _textTime)); yield return null; }
            SetContent(1f);
            _busy = false; _fade = null;
        }

        private IEnumerator CloseAnim()
        {
            _busy = true;
            bool _has = _frames != null && _frames.Length > 1;
            float _tText = _closeTime * 0.15f, _tFlip = _has ? _closeTime * 0.55f : 0f, _tDrop = _closeTime - _tText - _tFlip;
            for (float t = 0f; t < _tText; t += Time.unscaledDeltaTime) { SetContent(1f - t / _tText); yield return null; }
            SetContent(0f);
            for (float t = 0f; t < _tFlip; t += Time.unscaledDeltaTime)
            {
                float _u = 1f - t / _tFlip;   // 거꾸로
                _bookImg.sprite = _frames[Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(_u * _frames.Length))];
                SetBook(_closedShift * (1f - Smooth(Mathf.Clamp01(_u * 2f))), 0f);
                yield return null;
            }
            if (_has) _bookImg.sprite = _frames[0];
            for (float t = 0f; t < _tDrop; t += Time.unscaledDeltaTime)
            {
                float _u = t / _tDrop;
                SetDim(1f - Smooth(_u));
                SetBook(_has ? _closedShift : 0f, -720f * _u * _u);
                yield return null;
            }
            _canvas.enabled = false;
            IsOpen = false;
            Time.timeScale = _prevTimeScale;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            _busy = false; _fade = null;
        }

        private void SetDim(float _k) { var _c = _dimImg.color; _c.a = _dim * _k; _dimImg.color = _c; }
        private void SetBook(float _shiftX, float _y) { _book.anchoredPosition = new Vector2(_shiftX * _scale, _yOffset + _y); }
        private void SetContent(float _a) { _contentGroup.alpha = _a; _contentGroup.interactable = _contentGroup.blocksRaycasts = _a > 0.99f; }
        private static float Smooth(float _t) { _t = Mathf.Clamp01(_t); return _t * _t * (3f - 2f * _t); }
        private static float EaseOutBack(float _t)
        {
            _t = Mathf.Clamp01(_t); const float C1 = 1.3f, C3 = C1 + 1f;
            return 1f + C3 * Mathf.Pow(_t - 1f, 3f) + C1 * Mathf.Pow(_t - 1f, 2f);
        }

        // ================= 메뉴 =================
        private void OnMenu(int _i)
        {
            switch (_i)
            {
                case 0: Close(); break;
                case 1: ShowPage(1); break;
                case 2: ShowPage(2); break;
                case 3: break;   // Main Menu: 타이틀 씬이 생기면 연결
                case 4: Quit(); break;
            }
        }

        private void ShowPage(int _p)
        {
            _page = _p;
            _settingsPage.SetActive(_p == 1); _controlsPage.SetActive(_p == 2);
            _rightTitle.text = _p == 1 ? "Settings" : "Controls";
            // 고른 메뉴: 양옆 마름모 + 밑줄
            var _t = _menu[_p];
            float _w = _t.GetPreferredValues(_t.text).x;
            Vector2 _c = _t.rectTransform.anchoredPosition;
            _selL.rectTransform.anchoredPosition = _c + new Vector2(-_w / 2f - 9f, 0f);
            _selR.rectTransform.anchoredPosition = _c + new Vector2(_w / 2f + 9f, 0f);
            _selLine.rectTransform.anchoredPosition = _c + new Vector2(0f, -8f);
            _selLine.rectTransform.sizeDelta = new Vector2(_w, 2.2f);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================= 설정 적용 =================
        private void ApplyAll()
        {
            var _cam = FindFirstObjectByType<ThirdPersonCamera>();
            if (_cam) { _cam.MouseSensitivity = PlayerPrefs.GetFloat(K_SENS, D_SENS) * SENS_TO_CAMERA; _cam.InvertY = PlayerPrefs.GetInt(K_INV, 0) == 1; }
            var _sm = SoundManager.Instance;
            if (_sm)
            {
                _sm.MasterVolume = PlayerPrefs.GetFloat(K_MASTER, D_MASTER) / 100f;
                _sm.BgmVolume = PlayerPrefs.GetFloat(K_MUSIC, D_MUSIC) / 100f;
                _sm.SfxVolume = PlayerPrefs.GetFloat(K_SFX, D_SFX) / 100f;
            }
            if (PlayerPrefs.HasKey(K_FULL)) Screen.fullScreen = PlayerPrefs.GetInt(K_FULL) == 1;
            ApplyBrightness(PlayerPrefs.GetFloat(K_BRIGHT, D_BRIGHT));
        }

        /// <summary>씬의 전체 후처리 볼륨에 실행 중에만 밝기 보정을 덧붙인다(에셋은 바꾸지 않음). 50 = 변화 없음</summary>
        private void ApplyBrightness(float _v)
        {
            if (_colorAdj == null)
            {
                Volume _vol = null;
                foreach (var _v2 in FindObjectsByType<Volume>(FindObjectsSortMode.None)) if (_v2.isGlobal) { _vol = _v2; break; }
                if (_vol == null) return;
                var _profile = _vol.profile;   // 실행 중 복사본
                if (!_profile.TryGet(out _colorAdj)) _colorAdj = _profile.Add<ColorAdjustments>(false);
            }
            _colorAdj.postExposure.overrideState = true;
            _colorAdj.postExposure.value = (_v - 50f) / 50f;   // -1 ~ +1 EV
        }

        private void RestoreDefaults()
        {
            _sens.value = D_SENS; _invert.isOn = false;
            _master.value = D_MASTER; _music.value = D_MUSIC; _sfx.value = D_SFX;
            _full.isOn = true; _bright.value = D_BRIGHT;
            SetLanguage(GameLanguage.Lang.Korean);
        }

        // ================= 만들기 =================
        private static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var _es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(_es);
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 50;
            var _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f); _scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();

            _dimImg = NewImage("Dim", transform, null);
            Stretch(_dimImg.rectTransform);
            _dimImg.color = new Color(0.03f, 0.035f, 0.02f, _dim);

            _bookStatic = Sprite("UI_Settings_Book");
            _frames = Resources.LoadAll<Sprite>(TEX + "Open");
            System.Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            _bookImg = NewImage("Book", transform, _bookStatic);
            _bookImg.raycastTarget = true;
            _book = _bookImg.rectTransform;
            _book.anchorMin = _book.anchorMax = _book.pivot = new Vector2(0.5f, 0.5f);
            _book.sizeDelta = new Vector2(BOOK_W, BOOK_H);   // 펼침 프레임(960×560)과 고해상도(1920×1120)가 같은 칸에 꼭 맞음
            _book.anchoredPosition = new Vector2(0f, _yOffset);
            _book.localScale = Vector3.one * _scale;

            // 글자·버튼은 모두 _content 아래(펼친 뒤에만 보이고 눌림)
            _content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _content.SetParent(_book, false); Stretch(_content);
            _contentGroup = _content.GetComponent<CanvasGroup>();

            // ---- 왼쪽: 메뉴 ----
            Label("Pause", 323, 157, 27f, TextAlignmentOptions.Center, 200, INK);
            string[] _names = { "Resume", "Settings", "Controls", "Main Menu", "Quit Game" };
            for (int i = 0; i < _names.Length; i++)
            {
                float _y = 222 + i * 38;
                var _t = Label(_names[i], 322, _y, 21f, TextAlignmentOptions.Center, 180, INK);
                int _idx = i;
                if (i == 3) { _t.color = new Color(INK.r, INK.g, INK.b, 0.35f); }   // 타이틀 씬이 없어 아직 비활성
                else MakeButton(_t, () => OnMenu(_idx));
                _menu[i] = _t;
                if (i < _names.Length - 1) Line(302, _y + 19, 342, 1f, INK_SOFT);
            }
            _selL = Picture("UI_Settings_Diamond", 0, 0, 6f, 6f); _selR = Picture("UI_Settings_Diamond", 0, 0, 6f, 6f);
            _selLine = Picture("UI_Settings_Line", 0, 0, 10f, 2.2f);
            var _esc = Label("Esc  ·  Close", 322, 408, 13f, TextAlignmentOptions.Center, 160, INK);
            MakeButton(_esc, Close);

            // ---- 오른쪽 ----
            _rightTitle = Label("Settings", 638, 157, 27f, TextAlignmentOptions.Center, 220, INK);
            _settingsPage = new GameObject("SettingsPage", typeof(RectTransform)); Fit(_settingsPage);
            _controlsPage = new GameObject("ControlsPage", typeof(RectTransform)); Fit(_controlsPage);
            BuildSettings(_settingsPage.transform);
            BuildControls(_controlsPage.transform);
        }

        private void BuildSettings(Transform _p)
        {
            const float LX = 543, SX = 660, EX = 704, VX = 737;   // 숫자(최대 3자리)가 손잡이에 가리지 않게 홈 끝을 704로
            Section(_p, "Controls", LX, 198, VX);
            _sens = SliderRow(_p, "Mouse Sensitivity", LX, SX, EX, VX, 216, 1f, 10f, PlayerPrefs.GetFloat(K_SENS, D_SENS), false,
                v => { PlayerPrefs.SetFloat(K_SENS, v); var c = FindFirstObjectByType<ThirdPersonCamera>(); if (c) c.MouseSensitivity = v * SENS_TO_CAMERA; });
            _invert = ToggleRow(_p, "Invert Y-Axis", LX, VX, 233, PlayerPrefs.GetInt(K_INV, 0) == 1,
                v => { PlayerPrefs.SetInt(K_INV, v ? 1 : 0); var c = FindFirstObjectByType<ThirdPersonCamera>(); if (c) c.InvertY = v; });
            Section(_p, "Audio", LX, 256, VX);
            _master = SliderRow(_p, "Master Volume", LX, SX, EX, VX, 274, 0f, 100f, PlayerPrefs.GetFloat(K_MASTER, D_MASTER), true,
                v => { PlayerPrefs.SetFloat(K_MASTER, v); if (SoundManager.Instance) SoundManager.Instance.MasterVolume = v / 100f; });
            _music = SliderRow(_p, "Music", LX, SX, EX, VX, 291, 0f, 100f, PlayerPrefs.GetFloat(K_MUSIC, D_MUSIC), true,
                v => { PlayerPrefs.SetFloat(K_MUSIC, v); if (SoundManager.Instance) SoundManager.Instance.BgmVolume = v / 100f; });
            _sfx = SliderRow(_p, "Effects", LX, SX, EX, VX, 308, 0f, 100f, PlayerPrefs.GetFloat(K_SFX, D_SFX), true,
                v => { PlayerPrefs.SetFloat(K_SFX, v); if (SoundManager.Instance) SoundManager.Instance.SfxVolume = v / 100f; });
            Section(_p, "Display", LX, 331, VX);
            _full = ToggleRow(_p, "Fullscreen", LX, VX, 349, PlayerPrefs.GetInt(K_FULL, Screen.fullScreen ? 1 : 0) == 1,
                v => { PlayerPrefs.SetInt(K_FULL, v ? 1 : 0); Screen.fullScreen = v; });
            _bright = SliderRow(_p, "Brightness", LX, SX, EX, VX, 366, 0f, 100f, PlayerPrefs.GetFloat(K_BRIGHT, D_BRIGHT), true,
                v => { PlayerPrefs.SetFloat(K_BRIGHT, v); ApplyBrightness(v); });
            LanguageRow(_p, LX, VX, 383);
            Line(604, 398, 672, 1f, INK_SOFT, _p);
            var _rd = Label("Restore Defaults", 638, 410, 13f, TextAlignmentOptions.Center, 180, INK, _p);
            MakeButton(_rd, RestoreDefaults);
        }

        // 조작법: { 키(영어), 설명(영어), 키(한국어), 설명(한국어) } — 설정창 Language에 따라 바뀜
        // 메인메뉴 도감의 조작법 페이지(MainMenuController)도 같은 목록을 씀
        public static readonly string[,] CONTROLS =
        {
            { "W A S D", "Move", "W A S D", "이동" }, { "Shift", "Run", "Shift", "달리기" }, { "Space", "Jump", "Space", "점프" },
            { "Left Click", "Attack", "좌클릭", "공격" }, { "E", "Pick Up", "E", "줍기" }, { "Mouse", "Look", "마우스", "시점" },
            { "Wheel", "Zoom", "휠", "줌" }, { "Z / C", "Turn 45°", "Z / C", "시점 45° 회전" }, { "R", "Face Boss", "R", "보스 바라보기" },
            { "Tab", "Lock On", "Tab", "보스 락온" }, { "V", "Overview", "V", "전체 보기" }, { "Esc", "Pause", "Esc", "일시 정지" },
        };
        private TextMeshProUGUI[] _ctrlKey, _ctrlDesc;

        private void BuildControls(Transform _p)
        {
            int _n = CONTROLS.GetLength(0);
            _ctrlKey = new TextMeshProUGUI[_n]; _ctrlDesc = new TextMeshProUGUI[_n];
            for (int i = 0; i < _n; i++)
            {
                float _y = 200 + i * 16.5f;
                _ctrlKey[i] = Label(CONTROLS[i, 0], 543, _y, 13f, TextAlignmentOptions.Left, 100, INK, _p, true);
                _ctrlDesc[i] = Label(CONTROLS[i, 1], 737, _y, 13f, TextAlignmentOptions.Right, 120, INK, _p, false, true);
                if (i < _n - 1) Line(543, _y + 8.2f, 737, 0.5f, new Color(INK_SOFT.r, INK_SOFT.g, INK_SOFT.b, 0.35f), _p);
            }
            RefreshControls();
        }

        /// <summary>조작법 글자를 지금 언어로. 한글이 들어간 칸만 한글 글꼴(크기를 조금 줄여 영문과 높이를 맞춤)</summary>
        private void RefreshControls()
        {
            if (_ctrlKey == null) return;
            bool _k = GameLanguage.IsKorean; var _ko = _k ? KoreanFont() : null;
            for (int i = 0; i < _ctrlKey.Length; i++)
            {
                SetText(_ctrlKey[i], CONTROLS[i, _k ? 2 : 0], _ko);
                SetText(_ctrlDesc[i], CONTROLS[i, _k ? 3 : 1], _ko);
            }
        }

        private void SetText(TextMeshProUGUI _t, string _s, TMP_FontAsset _ko)
        {
            bool _hangul = false;
            foreach (char _c in _s) if (_c >= 0xAC00 && _c <= 0xD7A3) { _hangul = true; break; }
            _t.font = _hangul && _ko ? _ko : _font;
            _t.fontSize = _hangul && _ko ? 11.5f : 13f;
            _t.text = _s;
        }

        // ---- 행 ----
        private void Section(Transform _p, string _s, float _x0, float _y, float _x1)
        {
            var _t = Label(_s, _x0, _y, 12.8f, TextAlignmentOptions.Left, 120, new Color(INK.r, INK.g, INK.b, 0.72f), _p, true);
            float _w = _t.GetPreferredValues(_s).x;
            Line(_x0 + _w + 5f, _y, _x1, 0.8f, INK_SOFT, _p);
        }

        private Slider SliderRow(Transform _p, string _name, float _lx, float _sx, float _ex, float _vx, float _y, float _min, float _max, float _val, bool _whole, System.Action<float> _onChange)
        {
            Label(_name, _lx, _y, 14.2f, TextAlignmentOptions.Left, 130, INK, _p, true);
            var _valT = Label("", _vx, _y, 12.8f, TextAlignmentOptions.Right, 28, INK, _p, false, true);

            var _root = new GameObject(_name + "_Slider", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(_p, false); Place(_root, (_sx + _ex) / 2f, _y, _ex - _sx + 4f, 12f);
            var _bg = NewImage("Track", _root, Sprite("UI_Settings_Track")); Stretch(_bg.rectTransform);
            var _fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>(); _fillArea.SetParent(_root, false); Stretch(_fillArea);
            var _fill = NewImage("Fill", _fillArea, Sprite("UI_Settings_Fill")); Stretch(_fill.rectTransform);
            var _handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>(); _handleArea.SetParent(_root, false); Stretch(_handleArea);
            var _handle = NewImage("Knob", _handleArea, Sprite("UI_Settings_Knob"));
            _handle.raycastTarget = true; _handle.rectTransform.sizeDelta = new Vector2(13f, 13f);
            var _s = _root.gameObject.AddComponent<Slider>();
            _s.fillRect = _fill.rectTransform; _s.handleRect = _handle.rectTransform; _s.targetGraphic = _handle;
            _s.transition = Selectable.Transition.None; _s.navigation = new Navigation { mode = Navigation.Mode.None };
            _s.minValue = _min; _s.maxValue = _max; _s.wholeNumbers = _whole;
            _s.SetValueWithoutNotify(_val);
            System.Action<float> _show = v => _valT.text = _whole ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
            _show(_val);
            _s.onValueChanged.AddListener(v => { _show(v); _onChange(v); });
            _bg.raycastTarget = true;   // 홈을 눌러도 움직이게
            return _s;
        }

        // ---- 언어: 한국어 / English (고른 쪽은 진하게 + 밑줄) ----
        private TextMeshProUGUI _langKo, _langEn; private Image _langLine;
        private void LanguageRow(Transform _p, float _lx, float _vx, float _y)
        {
            Label("Language", _lx, _y, 14.2f, TextAlignmentOptions.Left, 130, INK, _p, true);
            _langEn = Label("English", _vx, _y, 13f, TextAlignmentOptions.Right, 52, INK, _p, false, true);
            _langKo = Label("한국어", _vx - 58f, _y, 11.5f, TextAlignmentOptions.Right, 40, INK, _p, false, true);
            var _ko = KoreanFont(); if (_ko) _langKo.font = _ko;   // EB Garamond에는 한글이 없어 한글만 컴퓨터의 한글 글꼴로
            _langLine = NewImage("LangLine", _p, Sprite("UI_Settings_Line"));
            MakeButton(_langKo, () => SetLanguage(GameLanguage.Lang.Korean));
            MakeButton(_langEn, () => SetLanguage(GameLanguage.Lang.English));
            ShowLanguage();
        }

        private void SetLanguage(GameLanguage.Lang _l) { GameLanguage.Current = _l; ShowLanguage(); RefreshControls(); }

        private void ShowLanguage()
        {
            bool _k = GameLanguage.IsKorean;
            _langKo.alpha = _k ? 1f : 0.38f; _langEn.alpha = _k ? 0.38f : 1f;
            var _t = _k ? _langKo : _langEn;
            float _w = _t.GetPreferredValues(_t.text).x;
            var _rt = _t.rectTransform;
            float _right = _rt.anchoredPosition.x + _rt.sizeDelta.x / 2f;   // 오른쪽 정렬 글자의 오른쪽 끝
            Place(_langLine.rectTransform, _right - _w / 2f, -_rt.anchoredPosition.y + 7.5f, _w, 1.8f);
        }

        /// <summary>
        /// 한글용 글꼴: 게임에 글꼴 파일을 넣지 않고, 실행하는 컴퓨터에 설치된 한글 글꼴을 그때 불러 쓴다(배포 라이선스 문제 없음).
        /// 찾지 못하면 null(글자가 네모로 보일 수 있음).
        /// </summary>
        private static TMP_FontAsset _koFont; private static bool _koTried;
        internal static TMP_FontAsset KoreanFont()
        {
            if (_koTried) return _koFont;
            _koTried = true;
            string[,] _cands = { { "Malgun Gothic", "Bold" }, { "Malgun Gothic", "Regular" }, { "Apple SD Gothic Neo", "Bold" }, { "Noto Sans CJK KR", "Bold" } };
            for (int i = 0; i < _cands.GetLength(0) && _koFont == null; i++)
            {
                try { _koFont = TMP_FontAsset.CreateFontAsset(_cands[i, 0], _cands[i, 1]); } catch { _koFont = null; }
            }
            return _koFont;
        }

        private Toggle ToggleRow(Transform _p, string _name, float _lx, float _vx, float _y, bool _on, System.Action<bool> _onChange)
        {
            Label(_name, _lx, _y, 14.2f, TextAlignmentOptions.Left, 130, INK, _p, true);
            var _box = NewImage(_name + "_Toggle", _p, Sprite("UI_Settings_Check"));
            _box.raycastTarget = true; Place(_box.rectTransform, _vx - 6f, _y, 13f, 13f);
            var _mark = NewImage("On", _box.rectTransform, Sprite("UI_Settings_CheckOn")); Stretch(_mark.rectTransform);
            var _t = _box.gameObject.AddComponent<Toggle>();
            _t.graphic = _mark; _t.targetGraphic = _box; _t.transition = Selectable.Transition.None;
            _t.navigation = new Navigation { mode = Navigation.Mode.None };
            _t.SetIsOnWithoutNotify(_on);
            _t.onValueChanged.AddListener(v => _onChange(v));
            return _t;
        }

        // ---- 기본 조각 ----
        private TextMeshProUGUI Label(string _s, float _x, float _y, float _size, TextAlignmentOptions _align, float _w, Color _c, Transform _p = null, bool _leftAnchor = false, bool _rightAnchor = false)
        {
            var _go = new GameObject("T_" + _s, typeof(RectTransform));
            var _t = _go.AddComponent<TextMeshProUGUI>();
            _go.transform.SetParent(_p ? _p : _content, false);
            if (_font) _t.font = _font;
            _t.text = _s; _t.fontSize = _size; _t.color = _c; _t.alignment = _align;
            _t.textWrappingMode = TextWrappingModes.NoWrap; _t.overflowMode = TextOverflowModes.Overflow; _t.raycastTarget = false;
            var _rt = _t.rectTransform;
            float _cx = _leftAnchor ? _x + _w / 2f : _rightAnchor ? _x - _w / 2f : _x;
            Place(_rt, _cx, _y, _w, _size * 1.4f);
            return _t;
        }

        private void MakeButton(TextMeshProUGUI _t, UnityEngine.Events.UnityAction _onClick)
        {
            _t.raycastTarget = true;
            var _b = _t.gameObject.AddComponent<Button>();
            _b.targetGraphic = _t; _b.navigation = new Navigation { mode = Navigation.Mode.None };
            var _cb = _b.colors;
            _cb.normalColor = Color.white; _cb.highlightedColor = new Color(0.62f, 0.5f, 0.42f, 1f);
            _cb.pressedColor = new Color(0.45f, 0.35f, 0.28f, 1f); _cb.selectedColor = Color.white; _cb.fadeDuration = 0.08f;
            _b.colors = _cb;
            _b.onClick.AddListener(_onClick);
        }

        private Image Line(float _x0, float _y, float _x1, float _h, Color _c, Transform _p = null)
        {
            var _i = NewImage("Line", _p ? _p : _content, Sprite("UI_Settings_Line"));
            _i.color = new Color(1f, 1f, 1f, _c.a);
            Place(_i.rectTransform, (_x0 + _x1) / 2f, _y, Mathf.Max(1f, _x1 - _x0), _h);
            return _i;
        }

        private Image Picture(string _sprite, float _x, float _y, float _w, float _h)
        {
            var _i = NewImage(_sprite, _content, Sprite(_sprite));
            Place(_i.rectTransform, _x, _y, _w, _h);
            return _i;
        }

        private void Fit(GameObject _go)
        {
            var _rt = _go.GetComponent<RectTransform>(); _rt.SetParent(_content, false); Stretch(_rt);
        }

        private static Image NewImage(string _name, Transform _parent, Sprite _s)
        {
            var _go = new GameObject(_name, typeof(RectTransform));
            _go.transform.SetParent(_parent, false);
            var _i = _go.AddComponent<Image>(); _i.sprite = _s; _i.raycastTarget = false;
            return _i;
        }

        private static Sprite Sprite(string _n) { return Resources.Load<Sprite>(TEX + _n); }

        /// <summary>도감 렌더 좌표(왼쪽 위 0,0)의 (x, y)를 가운데로 하는 사각형</summary>
        private static void Place(RectTransform _rt, float _x, float _y, float _w, float _h)
        {
            _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 1f); _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = new Vector2(_x, -_y); _rt.sizeDelta = new Vector2(_w, _h);
        }

        private static void Stretch(RectTransform _rt)
        {
            _rt.anchorMin = Vector2.zero; _rt.anchorMax = Vector2.one; _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }
}
