using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 게임 시작 메인 화면: 책상 위 세계지도(Blender 렌더 반복 영상) + 제목·메뉴(코드로 만든 UI).
    /// - 첫 실행(저장 없음): Start / Settings / Quit, 지도에 핀 없음(이집트 자리에 잉크 점선 표시)
    /// - 저장 후: Continue(기본 선택) / Start / Settings / Quit
    /// - 핀: 그 스테이지에 한 번이라도 입장하면 꽂힘. 처음 꽂힐 때 한 번만 핀 꽂힘 영상 재생
    /// - Start·Continue → 메뉴가 사라지며 도감으로 줌인·책 펼침 → 펼친 도감에서 인트로(설정 언어에 따라 한국어/영어, 클릭·키로 건너뛰기) → 게임 씬
    /// - 상태는 PlayerPrefs 키를 읽기만 한다(HC_HasSave, HC_Entered_Egypt). 세이브 시스템이 생기면 이 키를 쓰면 바로 연동된다.
    /// 영상·씬 데이터: Assets/Art/TEST_JMC/MainMenu/
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public enum StateOverride { Auto, FirstRun, Saved, SavedWithPinAnim }

        public const string K_HAS_SAVE = "HC_HasSave";
        public const string K_ENTERED_EGYPT = "HC_Entered_Egypt";
        public const string K_PIN_SHOWN_EGYPT = "HC_PinShown_Egypt";

        [Header("영상 (Assets/Art/TEST_JMC/MainMenu/Video)")]
        [SerializeField, Tooltip("반복 배경 — 핀 없음(첫 실행)")] private VideoClip _loopNoPin;
        [SerializeField, Tooltip("반복 배경 — 이집트 핀 꽂힘")] private VideoClip _loopPin;
        [SerializeField, Tooltip("핀이 처음 꽂히는 연출(한 번만)")] private VideoClip _pinPlant;
        [SerializeField, Tooltip("도감 줌인·책 펼침 — 핀 없음")] private VideoClip _zoomNoPin;
        [SerializeField, Tooltip("도감 줌인·책 펼침 — 핀 꽂힘")] private VideoClip _zoomPin;
        [SerializeField, Tooltip("도감 삽화 인트로 — 한국어")] private VideoClip _introKo;
        [SerializeField, Tooltip("도감 삽화 인트로 — 영어")] private VideoClip _introEn;
        [SerializeField, Tooltip("빈 펼친 도감(책장을 넘긴 뒤 조작법 페이지 바탕) — Assets/Art/TEST_JMC/MainMenu/UI")] private Texture2D _bookSpread;
        [Header("흐름")]
        [SerializeField, Tooltip("인트로가 끝나면 불러올 씬 이름")] private string _gameScene = "TEST_JMC";
        [SerializeField, Tooltip("빌드 목록에 없을 때 에디터에서 불러올 씬 경로")] private string _gameScenePath = "Assets/Art/Scene/TEST_JMC.unity";
        [SerializeField, Tooltip("테스트용: 저장 상태를 강제로 정함")] private StateOverride _state = StateOverride.Auto;
        [SerializeField, Tooltip("영상이 바뀔 때 겹쳐 넘어가는 시간(초)")] private float _crossFade = 0.35f;
        [SerializeField, Tooltip("테스트용: 메뉴·줌인을 건너뛰고 인트로 마지막 장면(책장 넘김 표시)부터 시작")] private bool _testFromIntroEnd;

        [Header("배치(기준 해상도 1920×1080, 왼쪽 위가 0,0)")]
        [SerializeField] private Vector2 _titlePos = new Vector2(90f, 120f);
        [SerializeField] private float _titleSize = 120f;
        [SerializeField] private Vector2 _menuPos = new Vector2(1560f, 470f);
        [SerializeField] private float _menuSize = 46f;

        private static readonly Color FACE = new Color32(226, 206, 166, 255);
        private static readonly Color EDGE = new Color32(60, 36, 18, 200);
        private static readonly Color RUBY = new Color32(150, 26, 22, 230);

        private readonly List<string> _items = new List<string>();
        private readonly List<TextMeshProUGUI> _itemTexts = new List<TextMeshProUGUI>();
        private int _sel;
        private bool _hasSave, _entered, _busy, _skipRequested;
        private CanvasGroup _menuGroup, _titleGroup;
        private RectTransform _selMark;
        private Image _black;
        private TMP_FontAsset _font;
        private PauseSettingsUI _settings;

        // 영상 두 겹(겹쳐 넘어가기용)
        private readonly VideoPlayer[] _vp = new VideoPlayer[2];
        private readonly RawImage[] _vimg = new RawImage[2];
        private readonly RenderTexture[] _rt = new RenderTexture[2];
        private int _front;

        // 책장 넘김·조작법 페이지
        private RectTransform _book;
        private RawImage _newSpread, _oldLeft, _flipFront, _flipBack;
        private CanvasGroup _ctrlGroup, _turnGroup;
        private TextMeshProUGUI _turnLabel;
        private RenderTexture _snapRT;

        private void Awake()
        {
            _hasSave = PlayerPrefs.GetInt(K_HAS_SAVE, 0) == 1;
            _entered = PlayerPrefs.GetInt(K_ENTERED_EGYPT, 0) == 1;
            bool _pinShown = PlayerPrefs.GetInt(K_PIN_SHOWN_EGYPT, 0) == 1;
            switch (_state)
            {
                case StateOverride.FirstRun: _hasSave = false; _entered = false; break;
                case StateOverride.Saved: _hasSave = true; _entered = true; _pinShown = true; break;
                case StateOverride.SavedWithPinAnim: _hasSave = true; _entered = true; _pinShown = false; break;
            }
            _playPinAnim = _entered && !_pinShown && _pinPlant;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private bool _playPinAnim;

        private void Start()
        {
            _font = Resources.Load<TMP_FontAsset>("UI/Settings/Font/EBGaramond_BoldItalic_SDF");
            EnsureEventSystem();
            BuildVideoLayers();
            BuildUI();
            // Settings 메뉴 = 게임 안 ESC 설정창과 같은 도감(자기 Canvas를 만들므로 따로 된 오브젝트에)
            _settings = new GameObject("UI_PauseSettings").AddComponent<PauseSettingsUI>();
            StartCoroutine(Intro());
        }

        private void OnDestroy()
        {
            for (int i = 0; i < 2; i++) if (_rt[i]) { _rt[i].Release(); Destroy(_rt[i]); }
            if (_snapRT) { _snapRT.Release(); Destroy(_snapRT); }
        }

        // ================= 흐름 =================
        private IEnumerator Intro()
        {
            _menuGroup.alpha = 0f; _menuGroup.interactable = false;
            if (_testFromIntroEnd) { SetAlpha(_black, 0f); _titleGroup.alpha = 0f; StartCoroutine(StartGame()); yield break; }
            if (_playPinAnim)
            {
                // 핀이 처음 꽂히는 연출 → 핀 꽂힌 반복 배경
                yield return PlayClip(_pinPlant, false, 0f);
                yield return WaitClipEnd();
                PlayerPrefs.SetInt(K_PIN_SHOWN_EGYPT, 1); PlayerPrefs.Save();
                yield return PlayClip(_loopPin, true, _crossFade);
            }
            else yield return PlayClip(_entered ? _loopPin : _loopNoPin, true, 0f);
            yield return Fade(_black, 1f, 0f, 0.6f);
            yield return FadeGroup(_menuGroup, 0f, 1f, 0.5f);
            _menuGroup.interactable = true;
        }

        private IEnumerator StartGame()
        {
            _busy = true; _menuGroup.interactable = false;
            StartCoroutine(FadeGroup(_menuGroup, 1f, 0f, 0.35f));
            StartCoroutine(FadeGroup(_titleGroup, 1f, 0f, 0.5f));      // 제목 로고도 함께 사라짐(도감 안으로 들어가면 없어야 함)
            // 도감으로 줌인 → 책이 펼쳐지며 화면을 채움
            if (!_testFromIntroEnd)
            {
                yield return PlayClip(_entered ? _zoomPin : _zoomNoPin, false, _crossFade);
                yield return WaitClipEnd();
            }
            // 펼친 도감에서 인트로(줌인 마지막 장면과 인트로 첫 장면이 같은 책이라 바로 이어짐)
            var _intro = GameLanguage.IsKorean ? _introKo : _introEn;
            if (_intro)
            {
                yield return PlayClip(_intro, false, 0.2f);
                _skipRequested = false;
                var _p = _vp[_front];
                long _hold = (long)_p.frameCount - HOLD_BEFORE_END;      // 영상 끝의 암전 직전 장면에서 멈춤
                float _t0 = Time.unscaledTime;
                if (_testFromIntroEnd) { _skipRequested = true; _t0 -= 1f; }
                while (_p.isPlaying && _p.frame < _hold)
                {
                    if (_skipRequested && Time.unscaledTime - _t0 > 0.8f) { _p.frame = _hold; break; }   // 건너뛰면 마지막 장면(나레이션이 다 쌓인 쪽)으로
                    yield return null;
                }
                _p.Pause();
                for (int i = 0; i < 3; i++) yield return null;           // 건너뛴 장면이 그려질 때까지
                // 시네마틱이 끝나면 우측 페이지 하단에 '다음 장' 표시 → 넘기면 조작법 페이지
                yield return WaitPageTurn(GameLanguage.T("다음 장", "Next page"));
                yield return TurnToControls();
                yield return WaitPageTurn(GameLanguage.T("모험 시작", "Begin"));
                // 조작법 글자가 걷힘 → (게임 씬으로) 도감이 줌아웃되며 뒤로 게임 화면, 책이 덮여 아래로 내려감
                yield return FadeGroup(_ctrlGroup, 1f, 0f, 0.45f);
                BookToGameTransition.Create(_bookSpread);
                LoadGameScene();
                yield break;
            }
            // 인트로 영상이 없을 때: 그냥 어두워졌다가 게임 씬
            yield return Fade(_black, 0f, 1f, 0.6f);
            LoadGameScene();
        }

        // ================= 책장 넘김 · 조작법 페이지 =================
        private const int HOLD_BEFORE_END = 16;                 // 인트로 영상은 끝 12프레임이 암전 → 그 직전에 멈춤
        private static readonly Color INK = new Color32(44, 26, 10, 235);
        private const float SPINE_X = 960f;                     // 펼친 책의 가운데(책등) — 영상·책 그림 기준 px

        /// <summary>우측 페이지 하단에 넘김 표시(살짝 숨 쉬듯 깜빡임)를 띄우고 클릭·키를 기다린다</summary>
        private IEnumerator WaitPageTurn(string _label)
        {
            _turnLabel.text = _label + "  ›";
            ApplyFont(_turnLabel, 26f);
            _turnGroup.gameObject.SetActive(true);
            yield return FadeGroup(_turnGroup, 0f, 1f, 0.4f);
            _skipRequested = false;
            while (!_skipRequested)
            {
                _turnGroup.alpha = 0.6f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
                yield return null;
            }
            _skipRequested = false;
            yield return FadeGroup(_turnGroup, _turnGroup.alpha, 0f, 0.15f);
            _turnGroup.gameObject.SetActive(false);
        }

        /// <summary>오른쪽 페이지가 책등을 축으로 넘어가며 조작법 펼침면이 나타난다</summary>
        private IEnumerator TurnToControls()
        {
            // 지금 화면(인트로 마지막 장면)을 멈춰 둔 사본
            var _snap = new RenderTexture(_rt[_front].width, _rt[_front].height, 0) { name = "IntroLastSpread" };
            Graphics.Blit(_rt[_front], _snap);
            _snapRT = _snap;
            _newSpread.texture = _bookSpread; _oldLeft.texture = _snap; _flipFront.texture = _snap; _flipBack.texture = _bookSpread;
            _ctrlGroup.alpha = 0f;
            _book.gameObject.SetActive(true);
            SetFlip(_flipFront, 1f, 1f); SetFlip(_flipBack, 0f, 1f);
            _flipBack.gameObject.SetActive(false);
            const float T = 0.85f;
            for (float t = 0f; t < T; t += Time.unscaledDeltaTime)
            {
                float _u = t / T; _u = _u * _u * (3f - 2f * _u);
                if (_u < 0.5f)
                {
                    float _k = 1f - _u * 2f;                       // 오른쪽 페이지가 들려 책등 쪽으로 눕는 중
                    SetFlip(_flipFront, _k, Mathf.Lerp(0.55f, 1f, _k));
                }
                else
                {
                    _flipFront.gameObject.SetActive(false); _flipBack.gameObject.SetActive(true);
                    float _k = (_u - 0.5f) * 2f;                    // 뒷면(새 왼쪽 페이지)이 왼쪽으로 내려앉는 중
                    SetFlip(_flipBack, _k, Mathf.Lerp(0.55f, 1f, _k));
                }
                yield return null;
            }
            _oldLeft.gameObject.SetActive(false); _flipFront.gameObject.SetActive(false); _flipBack.gameObject.SetActive(false);
            // 영상 레이어는 이제 안 씀
            _vp[_front].Stop(); SetAlpha(_vimg[_front], 0f);
            // 조작법 글자가 잉크처럼 번져 나타남
            yield return FadeGroup(_ctrlGroup, 0f, 1f, 0.6f);
        }

        private static void SetFlip(RawImage _img, float _scaleX, float _shade)
        {
            _img.rectTransform.localScale = new Vector3(Mathf.Max(0.0001f, _scaleX), 1f, 1f);
            _img.color = new Color(_shade, _shade, _shade, 1f);
        }

        private void BuildBookPages(RectTransform _root)
        {
            // 영상과 같은 16:9 틀(화면 비율이 달라도 영상과 정확히 겹치게)
            _book = new GameObject("BookPages", typeof(RectTransform), typeof(AspectRatioFitter)).GetComponent<RectTransform>();
            _book.SetParent(_root, false); Stretch(_book);
            var _fit = _book.GetComponent<AspectRatioFitter>(); _fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; _fit.aspectRatio = 16f / 9f;

            _newSpread = Raw("NewSpread", _book, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            _oldLeft = Raw("OldLeft", _book, new Rect(0f, 0f, 0.5f, 1f), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
            _flipFront = Raw("FlipFront", _book, new Rect(0.5f, 0f, 0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));   // 책등(왼쪽 끝)을 축으로
            _flipBack = Raw("FlipBack", _book, new Rect(0f, 0f, 0.5f, 1f), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0.5f));      // 책등(오른쪽 끝)을 축으로

            // 조작법: 왼쪽 = 이동·시점, 오른쪽 = 전투·기타 (목록은 ESC 설정창 Controls와 같은 PauseSettingsUI.CONTROLS)
            _ctrlGroup = new GameObject("Controls", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var _cr = (RectTransform)_ctrlGroup.transform; _cr.SetParent(_book, false); Stretch(_cr);
            bool _ko = GameLanguage.IsKorean;
            PageTitle(_cr, GameLanguage.T("이동 · 시점", "Movement · Camera"), 335f);
            PageTitle(_cr, GameLanguage.T("전투 · 기타", "Combat · Other"), 1085f);
            int[] _left = { 0, 1, 2, 5, 6, 7 }, _right = { 3, 4, 8, 9, 10, 11 };
            ControlRows(_cr, _left, 372f, 802f, _ko);
            ControlRows(_cr, _right, 1123f, 1553f, _ko);

            // 넘김 표시(우측 페이지 하단)
            _turnGroup = new GameObject("PageTurnHint", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var _tr = (RectTransform)_turnGroup.transform; _tr.SetParent(_book, false); Stretch(_tr);
            _turnLabel = BookText(_tr, "", new Vector2(1590f, 955f), 26f, TextAlignmentOptions.MidlineRight, 400f);
            _turnGroup.alpha = 0f; _turnGroup.gameObject.SetActive(false);
            _book.gameObject.SetActive(false);
        }

        private void PageTitle(RectTransform _p, string _s, float _x)
        {
            var _t = BookText(_p, _s, new Vector2(_x + 250f, 242f), 30f, TextAlignmentOptions.Center, 500f);
            ApplyFont(_t, 30f);
        }

        private void ControlRows(RectTransform _p, int[] _idx, float _x0, float _x1, bool _ko)
        {
            var C = PauseSettingsUI.CONTROLS;
            for (int i = 0; i < _idx.Length; i++)
            {
                float _y = 380f + i * 84f;
                var _k = BookText(_p, C[_idx[i], _ko ? 2 : 0], new Vector2(_x0, _y), 28f, TextAlignmentOptions.MidlineLeft, 220f);
                var _d = BookText(_p, C[_idx[i], _ko ? 3 : 1], new Vector2(_x1, _y), 26f, TextAlignmentOptions.MidlineRight, 260f);
                ApplyFont(_k, 28f); ApplyFont(_d, 26f);
                if (i < _idx.Length - 1)
                {
                    var _ln = MakeRect(_p, "Line", new Vector2(_x0, _y + 42f), new Vector2(_x1 - _x0, 1.2f), new Color(INK.r, INK.g, INK.b, 0.28f), 0f, new Vector2(0f, 0.5f));
                    _ln.rectTransform.anchorMin = _ln.rectTransform.anchorMax = new Vector2(0f, 1f);
                }
            }
        }

        /// <summary>책 그림 좌표(1920×1080, 왼쪽 위 0,0)에 잉크색 글자. 피벗은 정렬에 맞춤</summary>
        private TextMeshProUGUI BookText(RectTransform _p, string _s, Vector2 _pos, float _size, TextAlignmentOptions _align, float _w)
        {
            var _go = new GameObject(_s.Length > 0 ? _s : "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            _go.transform.SetParent(_p, false);
            var _r = (RectTransform)_go.transform;
            float _px = _align == TextAlignmentOptions.MidlineRight ? 1f : (_align == TextAlignmentOptions.Center ? 0.5f : 0f);
            _r.anchorMin = _r.anchorMax = new Vector2(_pos.x / 1920f, 1f - _pos.y / 1080f);
            _r.pivot = new Vector2(_px, 0.5f); _r.anchoredPosition = Vector2.zero; _r.sizeDelta = new Vector2(_w, _size * 1.6f);
            var _t = _go.GetComponent<TextMeshProUGUI>();
            if (_font) _t.font = _font;
            _t.text = _s; _t.fontSize = _size; _t.alignment = _align; _t.color = INK;
            _t.textWrappingMode = TextWrappingModes.NoWrap; _t.raycastTarget = false;
            return _t;
        }

        /// <summary>한글이 있으면 한글 글꼴(영문보다 조금 작게), 없으면 EB Garamond</summary>
        private void ApplyFont(TextMeshProUGUI _t, float _size)
        {
            bool _hangul = false;
            foreach (char ch in _t.text) if (ch >= 0xAC00 && ch <= 0xD7A3) { _hangul = true; break; }
            var _ko = _hangul ? PauseSettingsUI.KoreanFont() : null;
            if (_ko) { _t.font = _ko; _t.fontSize = _size * 0.82f; _t.fontStyle = FontStyles.Normal; }
            else { if (_font) _t.font = _font; _t.fontSize = _size; }
        }

        private static RawImage Raw(string _n, RectTransform _p, Rect _uv, Vector2 _amin, Vector2 _amax, Vector2 _pivot)
        {
            var _go = new GameObject(_n, typeof(RectTransform), typeof(RawImage));
            _go.transform.SetParent(_p, false);
            var _r = (RectTransform)_go.transform;
            // 피벗(넘김 축)을 먼저 정하고 나서 영역을 앵커에 딱 맞춤(순서가 바뀌면 자리가 밀림)
            _r.pivot = _pivot; _r.anchorMin = _amin; _r.anchorMax = _amax; _r.offsetMin = _r.offsetMax = Vector2.zero;
            var _img = _go.GetComponent<RawImage>(); _img.uvRect = _uv; _img.raycastTarget = false;
            return _img;
        }

        private void LoadGameScene()
        {
            if (Application.CanStreamedLevelBeLoaded(_gameScene)) { SceneManager.LoadSceneAsync(_gameScene); return; }
#if UNITY_EDITOR
            // 빌드 목록(ProjectSettings)은 건드리지 않음 — 에디터에서는 경로로 바로 불러옴
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(_gameScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogWarning($"[MainMenu] '{_gameScene}' 씬이 빌드 목록에 없습니다. File > Build Profiles에 추가해 주세요.");
#endif
        }

        // ================= 입력 =================
        private void Update()
        {
            var _kb = Keyboard.current; var _ms = Mouse.current;
            if (_busy)
            {
                if ((_kb != null && (_kb.anyKey.wasPressedThisFrame)) || (_ms != null && _ms.leftButton.wasPressedThisFrame)) _skipRequested = true;
                return;
            }
            if (!_menuGroup.interactable || PauseSettingsUI.IsOpen || _kb == null) return;
            if (_kb.upArrowKey.wasPressedThisFrame || _kb.wKey.wasPressedThisFrame) Select((_sel + _items.Count - 1) % _items.Count);
            if (_kb.downArrowKey.wasPressedThisFrame || _kb.sKey.wasPressedThisFrame) Select((_sel + 1) % _items.Count);
            if (_kb.enterKey.wasPressedThisFrame || _kb.numpadEnterKey.wasPressedThisFrame || _kb.spaceKey.wasPressedThisFrame) Submit(_sel);
        }

        /// <summary>클릭·키와 같은 효과(인트로 건너뛰기·책장 넘기기). 테스트나 다른 입력 연결용</summary>
        public void RequestSkip() { _skipRequested = true; }

        public void Select(int _i)
        {
            if (_busy || PauseSettingsUI.IsOpen || _i < 0 || _i >= _itemTexts.Count) return;
            _sel = _i;
            for (int k = 0; k < _itemTexts.Count; k++)
            {
                bool _on = k == _sel;
                _itemTexts[k].color = _on ? FACE : new Color(FACE.r, FACE.g, FACE.b, 185f / 255f);
                _itemTexts[k].outlineColor = new Color(EDGE.r, EDGE.g, EDGE.b, _on ? 200f / 255f : 140f / 255f);
            }
            var _r = _itemTexts[_sel].rectTransform;
            _selMark.anchoredPosition = new Vector2(_r.anchoredPosition.x - _menuSize * 0.55f, _r.anchoredPosition.y);
        }

        public void Submit(int _i)
        {
            if (_busy || PauseSettingsUI.IsOpen || !_menuGroup.interactable) return;
            Select(_i);
            switch (_items[_i])
            {
                case "Continue": StartCoroutine(StartGame()); break;
                case "Start": StartCoroutine(StartGame()); break;   // 새 게임: 저장 초기화는 세이브 시스템이 생기면 여기서
                case "Settings": if (_settings) _settings.Open(); break;
                case "Quit":
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        // ================= 영상 =================
        private void BuildVideoLayers()
        {
            var _cam = Camera.main;
            if (_cam) { _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = Color.black; }
        }

        private IEnumerator PlayClip(VideoClip _clip, bool _loop, float _fade)
        {
            if (!_clip) yield break;
            int _back = 1 - _front;
            var _p = _vp[_back];
            _p.Stop(); _p.clip = _clip; _p.isLooping = _loop;
            _p.Prepare();
            while (!_p.isPrepared) yield return null;
            _p.Play();
            while (_p.frame < 1) yield return null;            // 첫 장면이 실제로 그려진 뒤 보여 줌(검은 깜빡임 방지)
            _vimg[_back].transform.SetAsLastSibling();
            int _old = _front; _front = _back;
            if (_fade > 0f) yield return Fade(_vimg[_back], 0f, 1f, _fade, true);
            else SetAlpha(_vimg[_back], 1f);
            _vp[_old].Stop(); SetAlpha(_vimg[_old], 0f);
        }

        private IEnumerator WaitClipEnd()
        {
            var _p = _vp[_front];
            bool _done = false;
            VideoPlayer.EventHandler _h = _ => _done = true;
            _p.loopPointReached += _h;
            while (!_done && (_p.isPlaying || _p.frame < 2)) yield return null;
            _p.loopPointReached -= _h;
        }

        // ================= UI 만들기 =================
        private void BuildUI()
        {
            var _cv = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _cv.transform.SetParent(transform, false);
            var _canvas = _cv.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 10;
            var _sc = _cv.GetComponent<CanvasScaler>(); _sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; _sc.referenceResolution = new Vector2(1920, 1080); _sc.matchWidthOrHeight = 0.5f;
            var _root = (RectTransform)_cv.transform;

            // 영상 두 겹(화면 꽉 채움, 비율 유지) — 제목·메뉴 뒤에 있는 묶음 안에서만 앞뒤가 바뀜
            var _videos = new GameObject("Videos", typeof(RectTransform)).GetComponent<RectTransform>();
            _videos.SetParent(_root, false); Stretch(_videos);
            for (int i = 0; i < 2; i++)
            {
                _rt[i] = new RenderTexture(1920, 1080, 0) { name = "MainMenuVideo" + i };
                var _go = new GameObject("Video" + i, typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                _go.transform.SetParent(_videos, false);
                Stretch((RectTransform)_go.transform);
                _vimg[i] = _go.GetComponent<RawImage>(); _vimg[i].texture = _rt[i]; _vimg[i].raycastTarget = false; SetAlpha(_vimg[i], 0f);
                var _fit = _go.GetComponent<AspectRatioFitter>(); _fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; _fit.aspectRatio = 16f / 9f;
                var _p = _go.AddComponent<VideoPlayer>();
                _p.playOnAwake = false; _p.renderMode = VideoRenderMode.RenderTexture; _p.targetTexture = _rt[i];
                _p.audioOutputMode = VideoAudioOutputMode.None; _p.skipOnDrop = true; _p.waitForFirstFrame = true;
                _p.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;   // 설정창이 열려 게임 시간이 멈춰도 배경은 계속 흐름
                _vp[i] = _p;
            }

            // 제목 "History / Crash" + 가는 선과 마름모 (샘플 1 배치 그대로)
            var _title = new GameObject("Title", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _title.SetParent(_root, false); Stretch(_title);
            _titleGroup = _title.GetComponent<CanvasGroup>();
            MakeText(_title, "History", _titlePos, _titleSize, true);
            MakeText(_title, "Crash", _titlePos + new Vector2(_titleSize * 0.9f, _titleSize * 0.95f), _titleSize, true);
            float _lw = _titleSize * 3.4f, _ly = _titlePos.y + _titleSize * 1.75f;
            var _lineC = new Color(FACE.r, FACE.g, FACE.b, 160f / 255f);
            MakeRect(_title, "LineL", new Vector2(_titlePos.x, _ly), new Vector2(_lw * 0.45f, 1.6f), _lineC, 0f, new Vector2(0f, 0.5f));
            MakeRect(_title, "LineR", new Vector2(_titlePos.x + _lw * 0.55f, _ly), new Vector2(_lw * 0.45f, 1.6f), _lineC, 0f, new Vector2(0f, 0.5f));
            MakeRect(_title, "Diamond", new Vector2(_titlePos.x + _lw / 2f, _ly), new Vector2(8.5f, 8.5f), new Color(FACE.r, FACE.g, FACE.b, 200f / 255f), 45f, new Vector2(0.5f, 0.5f));

            // 메뉴
            _items.Clear();
            if (_hasSave) _items.Add("Continue");
            _items.Add("Start"); _items.Add("Settings"); _items.Add("Quit");
            var _menu = new GameObject("Menu", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _menu.SetParent(_root, false); Stretch(_menu);
            _menuGroup = _menu.GetComponent<CanvasGroup>();
            for (int i = 0; i < _items.Count; i++)
            {
                var _t = MakeText(_menu, _items[i], _menuPos + new Vector2(0f, i * _menuSize * 1.55f), _menuSize, false);
                _t.raycastTarget = true;
                var _it = _t.gameObject.AddComponent<MainMenuItem>(); _it.Owner = this; _it.Index = i;
                _itemTexts.Add(_t);
            }
            _selMark = MakeRect(_menu, "Select", Vector2.zero, new Vector2(12.7f, 12.7f), RUBY, 45f, new Vector2(0.5f, 0.5f)).rectTransform;
            Select(0);

            // 인트로 뒤 책장 넘김·조작법 페이지(영상 위, 검은 막 아래)
            BuildBookPages(_root);

            // 검은 막(시작·씬 전환 페이드)
            var _b = new GameObject("Black", typeof(RectTransform), typeof(Image));
            _b.transform.SetParent(_root, false); Stretch((RectTransform)_b.transform);
            _black = _b.GetComponent<Image>(); _black.color = Color.black; _black.raycastTarget = false;
        }

        private TextMeshProUGUI MakeText(RectTransform _parent, string _s, Vector2 _pos, float _size, bool _gradient)
        {
            var _go = new GameObject(_s, typeof(RectTransform), typeof(TextMeshProUGUI));
            _go.transform.SetParent(_parent, false);
            var _r = (RectTransform)_go.transform;
            _r.anchorMin = _r.anchorMax = new Vector2(0f, 1f); _r.pivot = new Vector2(0f, 0.5f);
            _r.anchoredPosition = new Vector2(_pos.x, -_pos.y); _r.sizeDelta = new Vector2(_size * 6f, _size * 1.4f);
            var _t = _go.GetComponent<TextMeshProUGUI>();
            if (_font) _t.font = _font;
            _t.text = _s; _t.fontSize = _size; _t.alignment = TextAlignmentOptions.MidlineLeft;
            _t.textWrappingMode = TextWrappingModes.NoWrap; _t.raycastTarget = false; _t.color = FACE;
            // 진갈색 테두리 + 아래로 떨어지는 그림자
            _t.fontMaterial = new Material(_t.fontSharedMaterial);
            _t.outlineWidth = 0.12f; _t.outlineColor = EDGE;
            var _m = _t.fontMaterial;
            _m.EnableKeyword("UNDERLAY_ON");
            _m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
            _m.SetFloat("_UnderlayOffsetX", 0.35f); _m.SetFloat("_UnderlayOffsetY", -0.5f); _m.SetFloat("_UnderlaySoftness", 0.35f);
            if (_gradient)
            {
                var _top = new Color(Mathf.Min(1f, FACE.r + 0.1f), Mathf.Min(1f, FACE.g + 0.1f), Mathf.Min(1f, FACE.b + 0.1f), 1f);
                var _bot = new Color(FACE.r * 0.6f, FACE.g * 0.6f, FACE.b * 0.6f, 1f);
                _t.enableVertexGradient = true; _t.colorGradient = new VertexGradient(_top, _top, _bot, _bot); _t.color = Color.white;
            }
            return _t;
        }

        private static Image MakeRect(RectTransform _parent, string _n, Vector2 _pos, Vector2 _size, Color _c, float _rot, Vector2 _pivot)
        {
            var _go = new GameObject(_n, typeof(RectTransform), typeof(Image));
            _go.transform.SetParent(_parent, false);
            var _r = (RectTransform)_go.transform;
            _r.anchorMin = _r.anchorMax = new Vector2(0f, 1f); _r.pivot = _pivot;
            _r.anchoredPosition = new Vector2(_pos.x, -_pos.y); _r.sizeDelta = _size; _r.localEulerAngles = new Vector3(0f, 0f, _rot);
            var _img = _go.GetComponent<Image>(); _img.color = _c; _img.raycastTarget = false;
            return _img;
        }

        private static void Stretch(RectTransform _r) { _r.anchorMin = Vector2.zero; _r.anchorMax = Vector2.one; _r.offsetMin = _r.offsetMax = Vector2.zero; }
        private static void SetAlpha(Graphic _g, float _a) { var _c = _g.color; _c.a = _a; _g.color = _c; }

        private static IEnumerator Fade(Graphic _g, float _a0, float _a1, float _dur, bool _smooth = true)
        {
            for (float t = 0f; t < _dur; t += Time.unscaledDeltaTime)
            {
                float _u = t / _dur; if (_smooth) _u = _u * _u * (3f - 2f * _u);
                SetAlpha(_g, Mathf.Lerp(_a0, _a1, _u)); yield return null;
            }
            SetAlpha(_g, _a1);
        }

        private static IEnumerator FadeGroup(CanvasGroup _g, float _a0, float _a1, float _dur)
        {
            for (float t = 0f; t < _dur; t += Time.unscaledDeltaTime) { _g.alpha = Mathf.Lerp(_a0, _a1, t / _dur); yield return null; }
            _g.alpha = _a1;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current || FindFirstObjectByType<EventSystem>()) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
