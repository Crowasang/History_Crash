using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
    /// 씬: Assets/Scenes/MainMenu.unity(메인메뉴 씬만 예외로 Scenes 폴더) / 영상·그림·소리 데이터: Assets/Art/TEST_JMC/MainMenu/
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
        [Header("책갈피(Blender 책 장면에 천 리본을 넣어 같은 카메라·조명으로 렌더한 겹침 그림) — Assets/Art/TEST_JMC/MainMenu/UI/Ribbon")]
        [SerializeField, Tooltip("우측 리본 한국어: 0 건너뛰기, 1 다음 장, 2 모험 시작")] private Texture2D[] _ribbonNextKo = new Texture2D[3];
        [SerializeField, Tooltip("우측 리본 영어: 0 Skip, 1 Next page, 2 Begin")] private Texture2D[] _ribbonNextEn = new Texture2D[3];
        [SerializeField, Tooltip("좌측 리본(이전 장) 한국어")] private Texture2D _ribbonBackKo;
        [SerializeField, Tooltip("좌측 리본(Back) 영어")] private Texture2D _ribbonBackEn;
        [SerializeField, Tooltip("나타날 때 들린 채 → 내려앉는 프레임(우측 건너뛰기, 한국어)")] private Texture2D[] _ribbonSkipKoDrape;
        [SerializeField, Tooltip("나타날 때 내려앉는 프레임(우측 Skip, 영어)")] private Texture2D[] _ribbonSkipEnDrape;
        [SerializeField, Tooltip("나타날 때 내려앉는 프레임(좌측 이전 장, 한국어)")] private Texture2D[] _ribbonBackKoDrape;
        [SerializeField, Tooltip("나타날 때 내려앉는 프레임(좌측 Back, 영어)")] private Texture2D[] _ribbonBackEnDrape;
        [SerializeField, Tooltip("리본이 표지·책상 위로 내려앉는 시간(초)")] private float _drapeTime = 0.45f;
        [Header("사운드(VARCO Sound) — Assets/Art/TEST_JMC/MainMenu/Audio. 각 영상과 길이가 같아 영상과 함께 시작")]
        [SerializeField, Tooltip("메인메뉴 배경(반복 영상과 함께 반복) — History Crash Main Menu")] private AudioClip _menuAudio;
        [SerializeField, Tooltip("Start 후 도감 줌인·책 펼침 — Chronicles of the Magic Book")] private AudioClip _zoomAudio;
        [SerializeField, Tooltip("스토리 시네마틱 — Mage Temple Portal Activation")] private AudioClip _introAudio;
        [SerializeField, Tooltip("영상이 바뀔 때 앞 소리가 줄어드는 시간(초)")] private float _audioFade = 0.6f;
        [SerializeField, Tooltip("스토리 시네마틱이 시작되면 함께 시작해 메인메뉴 씬 안에서 계속 반복 — Whispers of the ancient tale")] private AudioClip _storyLoop;

        [Header("흐름")]
        [SerializeField, Tooltip("인트로가 끝나면 불러올 씬 이름")] private string _gameScene = "TEST_JMC";
        [SerializeField, Tooltip("빌드 목록에 없을 때 에디터에서 불러올 씬 경로")] private string _gameScenePath = "Assets/Art/Scene/TEST_JMC.unity";
        [SerializeField, Tooltip("테스트용: 저장 상태를 강제로 정함")] private StateOverride _state = StateOverride.Auto;
        [SerializeField, Tooltip("영상이 바뀔 때 겹쳐 넘어가는 시간(초)")] private float _crossFade = 0.35f;
        [SerializeField, Tooltip("테스트용: 메뉴·줌인을 건너뛰고 인트로 마지막 장면(책장 넘김 표시)부터 시작")] private bool _testFromIntroEnd;

        [Header("튜토리얼(조작법 다음 장부터 한 장씩: 왼쪽 그림, 오른쪽 설명)")]
        [SerializeField] private MainMenuTutorialPage[] _tutorialPages =
        {
            new MainMenuTutorialPage("UI/Tutorial/01_BossAttack", "파라오의 공격", "The Pharaoh's Attacks",
                "파라오는 제단 위에서 다섯 가지\n패턴을 번갈아 쓰며 공격합니다.\n\n바닥에 문양이나 균열이 나타나면\n곧 공격이 떨어지는 자리입니다.\n달리거나 점프해서 피하세요.",
                "The Pharaoh stands on the altar and attacks with five patterns in turn.\n\nA glowing mark or crack on the floor shows where an attack will land. Run or jump to dodge it."),
            new MainMenuTutorialPage("UI/Tutorial/02_DroppedWeapon", "떨어진 무기", "Dropped Weapons",
                "패턴이 끝날 무렵, 그 패턴의 무기가\n하늘에서 떨어져 바닥에 박힙니다.\n\n무기를 주우면 파라오의 공격을\n약하지만 따라 쓸 수 있습니다.\n무기는 잠시 뒤 사라집니다.",
                "Near the end of each pattern, that pattern's weapon falls from the sky and sticks in the floor.\n\nPick it up to use a weaker version of the Pharaoh's attack. Weapons vanish after a while."),
            new MainMenuTutorialPage("UI/Tutorial/03_ElementOrb", "속성 오브", "Element Orbs",
                "미라는 불 · 물 · 전기 · 풀 중\n한 속성을 지니고, 같은 속성의\n오브를 떨어뜨립니다.\n\n주운 오브는 무기에 끼워지고,\n무기와 오브가 모두 있어야\n공격할 수 있습니다.\n한 번 공격하면 둘 다 사라집니다.",
                "Each mummy has one element (Fire, Water, Electric or Grass) and drops orbs of that element.\n\nAn orb you pick up is set in your weapon. You need both a weapon and an orb to attack, and both are used up after one attack."),
            new MainMenuTutorialPage("UI/Tutorial/04_ElementMummy", "속성 미라", "Element Mummies",
                "미라의 몸과 머리 위 구슬의 색이\n그 미라의 속성입니다.\n\n오브와 같은 속성의 미라만\n쓰러뜨릴 수 있습니다.\n\n쓰러진 미라의 영혼이 파라오에게\n날아가 피해를 주고, 체력을 모두\n깎으면 피라미드가 정화됩니다.",
                "The color of a mummy's body and the orb above its head shows its element.\n\nYou can only defeat mummies of the same element as your orb.\n\nEach defeated mummy's soul flies to the Pharaoh and deals damage. Bring its HP to zero to purify the pyramid."),
        };

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
        private bool _hasSave, _entered, _busy, _skipRequested, _skipIntro;
        private CanvasGroup _bookmarkGroup;
        private RawImage _nextRibbon, _backRibbon;
        private CanvasGroup _backGroup;
        private bool _bookmarkClicked, _choseBack;
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
        private RawImage _newSpread, _oldLeft, _oldRight, _flipFront, _flipBack;
        private CanvasGroup _ctrlGroup, _turnGroup;
        private TextMeshProUGUI _turnLabel;
        private RenderTexture _snapRT;
        private readonly List<CanvasGroup> _pageGroups = new List<CanvasGroup>();   // 0 = 조작법, 1~ = 튜토리얼
        private CanvasGroup _skipGroup;     // 조작법·튜토리얼 장 오른쪽 아래 '튜토리얼 건너뛰기'
        private bool _skipTutorial;

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
            _settings.AllowEscOpen = false;   // 메인메뉴에서는 Esc로 열리지 않음(Settings 메뉴로만, 열린 창은 Esc로 닫힘)
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
                var _p = _vp[_front];
                long _hold = (long)_p.frameCount - HOLD_BEFORE_END;      // 영상 끝의 암전 직전 장면에서 멈춤
                // 건너뛰기는 우측 책갈피(Skip)를 눌렀을 때만 — 아무 데나 클릭해도 넘어가지 않음
                _skipIntro = _testFromIntroEnd;
                ShowBookmark(true);
                while (_p.isPlaying && _p.frame < _hold)
                {
                    if (_skipIntro)                                        // 마지막 장면(나레이션이 다 쌓인 쪽)으로 — 소리도 같은 자리로
                    {
                        _p.frame = _hold;
                        var _src = _as[_front]; if (_src.clip) _src.time = Mathf.Clamp(_hold / (float)_p.frameRate, 0f, _src.clip.length - 0.01f);
                        break;
                    }
                    yield return null;
                }
                _p.Pause();
                for (int i = 0; i < 3; i++) yield return null;           // 건너뛴 장면이 그려질 때까지
                // 시네마틱이 끝나면 같은 책갈피가 '다음 장' 버튼으로 바뀜 → 조작법 → 튜토리얼 → 마지막 장에서 '모험 시작'
                // 장 번호: 0 = 이야기, 1 = 조작법, 2~ = 튜토리얼. 우측 = 다음 장, 좌측 = 이전 장
                int _page = 0, _lastPage = _pageGroups.Count;
                _skipTutorial = false;
                while (true)
                {
                    ShowSkip(_page >= 1 && _page < _lastPage);                   // 마지막 장은 '모험 시작'이 있으므로 건너뛰기 없음
                    yield return WaitBookmark(_page == _lastPage ? 2 : 1, _page > 0);
                    ShowSkip(false);
                    if (_skipTutorial) break;                                    // 남은 장을 건너뛰고 바로 게임으로
                    if (_choseBack)
                    {
                        if (_page == 1) yield return TurnBackToStory();
                        else yield return TurnBetween(_pageGroups[_page - 1], _pageGroups[_page - 2], false);
                        _page--;
                    }
                    else if (_page == _lastPage) break;
                    else
                    {
                        if (_page == 0) yield return TurnToControls();
                        else yield return TurnBetween(_pageGroups[_page - 1], _pageGroups[_page], true);
                        _page++;
                    }
                }
                ShowBookmark(false);
                // 지금 장 글자가 걷힘 → (게임 씬으로) 도감이 줌아웃되며 뒤로 게임 화면, 책이 덮여 아래로 내려감
                yield return FadeGroup(_pageGroups[_page - 1], 1f, 0f, 0.45f);
                foreach (var _src in _as) if (_src && _src.isPlaying) _src.DOFade(0f, 0.5f).SetUpdate(true).SetLink(gameObject);   // 게임으로 넘어가며 소리도 잦아듦
                if (_loopSrc && _loopSrc.isPlaying) _loopSrc.DOFade(0f, 0.5f).SetUpdate(true).SetLink(gameObject);
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
            // 숨 쉬듯 깜빡임(1 ↔ 0.6 왕복)
            var _pulse = _turnGroup.DOFade(0.6f, 0.98f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(_turnGroup.gameObject);
            while (!_skipRequested) yield return null;
            _pulse.Kill();
            _skipRequested = false;
            yield return FadeGroup(_turnGroup, _turnGroup.alpha, 0f, 0.15f);
            _turnGroup.gameObject.SetActive(false);
        }

        /// <summary>오른쪽 페이지가 책등을 축으로 넘어가며 조작법 펼침면이 나타난다</summary>
        private IEnumerator TurnToControls()
        {
            // 지금 화면(인트로 마지막 장면)을 멈춰 둔 사본
            if (_snapRT == null)
            {
                _snapRT = new RenderTexture(_rt[_front].width, _rt[_front].height, 0) { name = "IntroLastSpread" };
                Graphics.Blit(_rt[_front], _snapRT);
            }
            var _snap = _snapRT;
            _newSpread.texture = _bookSpread; _oldLeft.texture = _snap; _flipFront.texture = _snap; _flipBack.texture = _bookSpread;
            _oldLeft.gameObject.SetActive(true); _flipFront.gameObject.SetActive(true); _oldRight.gameObject.SetActive(false);
            _ctrlGroup.alpha = 0f;
            _book.gameObject.SetActive(true);
            SetFlip(_flipFront, 1f, 1f); SetFlip(_flipBack, 0f, 1f);
            _flipBack.gameObject.SetActive(false);
            // 책장 넘김(DOTween): 앞면이 책등 쪽으로 눕고(가속) → 뒷면이 왼쪽으로 내려앉음(감속)
            var _flip = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _flip.Append(DOVirtual.Float(1f, 0f, 0.425f, _k => SetFlip(_flipFront, _k, Mathf.Lerp(0.55f, 1f, _k))).SetEase(Ease.InSine));
            _flip.AppendCallback(() => { _flipFront.gameObject.SetActive(false); _flipBack.gameObject.SetActive(true); });
            _flip.Append(DOVirtual.Float(0f, 1f, 0.425f, _k => SetFlip(_flipBack, _k, Mathf.Lerp(0.55f, 1f, _k))).SetEase(Ease.OutSine));
            yield return _flip.WaitForCompletion();
            _oldLeft.gameObject.SetActive(false); _flipFront.gameObject.SetActive(false); _flipBack.gameObject.SetActive(false);
            // 영상 레이어는 이제 안 씀
            _vp[_front].Stop(); SetAlpha(_vimg[_front], 0f);
            // 조작법 글자가 잉크처럼 번져 나타남
            yield return FadeGroup(_ctrlGroup, 0f, 1f, 0.6f);
        }

        /// <summary>왼쪽 페이지가 책등을 축으로 오른쪽으로 넘어가며 이야기(인트로 마지막) 펼침면으로 되돌아감</summary>
        private IEnumerator TurnBackToStory()
        {
            yield return FadeGroup(_ctrlGroup, 1f, 0f, 0.3f);
            _newSpread.texture = _snapRT;                                   // 밑: 이야기 펼침면
            _oldRight.texture = _bookSpread; _oldRight.gameObject.SetActive(true);   // 오른쪽은 넘어온 장이 덮을 때까지 지금 페이지
            _oldLeft.gameObject.SetActive(false);
            _flipBack.texture = _bookSpread; _flipFront.texture = _snapRT;
            _flipBack.gameObject.SetActive(true); _flipFront.gameObject.SetActive(false);
            SetFlip(_flipBack, 1f, 1f); SetFlip(_flipFront, 0f, 1f);
            var _flip = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _flip.Append(DOVirtual.Float(1f, 0f, 0.425f, _k => SetFlip(_flipBack, _k, Mathf.Lerp(0.55f, 1f, _k))).SetEase(Ease.InSine));
            _flip.AppendCallback(() => { _flipBack.gameObject.SetActive(false); _flipFront.gameObject.SetActive(true); });
            _flip.Append(DOVirtual.Float(0f, 1f, 0.425f, _k => SetFlip(_flipFront, _k, Mathf.Lerp(0.55f, 1f, _k))).SetEase(Ease.OutSine));
            yield return _flip.WaitForCompletion();
            _oldRight.gameObject.SetActive(false); _flipFront.gameObject.SetActive(false); _flipBack.gameObject.SetActive(false);
        }

        /// <summary>빈 도감 펼침면끼리 책장을 넘김(조작법 ↔ 튜토리얼, 튜토리얼끼리). 글자·그림은 넘기기 전에 걷히고 넘긴 뒤 번져 나타남</summary>
        // 튜토리얼은 빠르게 넘겨 볼 수 있게 이야기·조작법 넘김보다 짧게
        private const float QUICK_FADE_OUT = 0.15f, QUICK_FLIP_HALF = 0.28f, QUICK_FADE_IN = 0.3f;

        private IEnumerator TurnBetween(CanvasGroup _from, CanvasGroup _to, bool _forward)
        {
            yield return FadeGroup(_from, 1f, 0f, QUICK_FADE_OUT);
            _newSpread.texture = _bookSpread; _flipFront.texture = _bookSpread; _flipBack.texture = _bookSpread;
            _oldLeft.gameObject.SetActive(false); _oldRight.gameObject.SetActive(false);
            // 앞으로: 오른쪽 장이 책등 쪽으로 눕고 왼쪽에 내려앉음 / 뒤로: 반대
            var _first = _forward ? _flipFront : _flipBack;
            var _second = _forward ? _flipBack : _flipFront;
            _first.gameObject.SetActive(true); _second.gameObject.SetActive(false);
            SetFlip(_first, 1f, 1f); SetFlip(_second, 0f, 1f);
            var _flip = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _flip.Append(DOVirtual.Float(1f, 0f, QUICK_FLIP_HALF, _k => SetFlip(_first, _k, Mathf.Lerp(0.55f, 1f, _k))).SetEase(Ease.InSine));
            _flip.AppendCallback(() => { _first.gameObject.SetActive(false); _second.gameObject.SetActive(true); });
            _flip.Append(DOVirtual.Float(0f, 1f, QUICK_FLIP_HALF, _k => SetFlip(_second, _k, Mathf.Lerp(0.55f, 1f, _k))).SetEase(Ease.OutSine));
            yield return _flip.WaitForCompletion();
            _second.gameObject.SetActive(false);
            yield return FadeGroup(_to, 0f, 1f, QUICK_FADE_IN);
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
            _oldRight = Raw("OldRight", _book, new Rect(0.5f, 0f, 0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));   // 되돌아 넘길 때 오른쪽 자리
            _oldRight.gameObject.SetActive(false);
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
            _pageGroups.Clear(); _pageGroups.Add(_ctrlGroup);
            if (_tutorialPages != null)
                for (int i = 0; i < _tutorialPages.Length; i++) _pageGroups.Add(BuildTutorialPage(_book, _tutorialPages[i], i));

            // 튜토리얼 건너뛰기(오른쪽 페이지 아래 잉크 글씨, 누르거나 Esc)
            _skipGroup = new GameObject("TutorialSkip", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var _sr = (RectTransform)_skipGroup.transform; _sr.SetParent(_book, false); Stretch(_sr);
            var _skip = BookText(_sr, GameLanguage.T("튜토리얼 건너뛰기 (Esc)  »", "Skip tutorial (Esc)  »"), new Vector2(1590f, 955f), 24f, TextAlignmentOptions.MidlineRight, 420f);
            ApplyFont(_skip, 24f); _skip.raycastTarget = true;
            _skip.gameObject.AddComponent<MainMenuBookmark>().Clicked = SkipTutorial;
            ShowSkip(false);

            // 넘김 표시(우측 페이지 하단)
            _turnGroup = new GameObject("PageTurnHint", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var _tr = (RectTransform)_turnGroup.transform; _tr.SetParent(_book, false); Stretch(_tr);
            _turnLabel = BookText(_tr, "", new Vector2(1590f, 955f), 26f, TextAlignmentOptions.MidlineRight, 400f);
            _turnGroup.alpha = 0f; _turnGroup.gameObject.SetActive(false);
            _book.gameObject.SetActive(false);
        }

        // 튜토리얼 한 장: 조작법 페이지와 같은 칸(왼쪽 372~802, 오른쪽 1123~1553, 제목 줄 242)에 맞춤
        // 왼쪽 페이지 그림 칸(왼쪽 위 0,0): 도감에 그려진 안쪽 틀(x 327~843, y 315~908) 안에 여백 20px을 두고 가운데
        private static readonly Rect TUTORIAL_IMAGE_PX = new Rect(347f, 335f, 476f, 553f);
        private const float TUTORIAL_FRAME = 4f;                                               // 그림 둘레 잉크 테두리 두께

        private CanvasGroup BuildTutorialPage(RectTransform _parent, MainMenuTutorialPage _page, int _index)
        {
            var _group = new GameObject("Tutorial_" + (_index + 1), typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var _r = (RectTransform)_group.transform; _r.SetParent(_parent, false); Stretch(_r);
            _group.alpha = 0f; _group.interactable = _group.blocksRaycasts = false;

            // 왼쪽: 그림(비율 유지해 칸 안에 맞춤) + 잉크 테두리
            var _tex = string.IsNullOrEmpty(_page.ImagePath) ? null : Resources.Load<Texture2D>(_page.ImagePath);
            if (_tex)
            {
                var _box = new GameObject("ImageBox", typeof(RectTransform)).GetComponent<RectTransform>();
                _box.SetParent(_r, false);
                _box.anchorMin = new Vector2(TUTORIAL_IMAGE_PX.xMin / 1920f, 1f - TUTORIAL_IMAGE_PX.yMax / 1080f);
                _box.anchorMax = new Vector2(TUTORIAL_IMAGE_PX.xMax / 1920f, 1f - TUTORIAL_IMAGE_PX.yMin / 1080f);
                _box.offsetMin = _box.offsetMax = Vector2.zero;
                var _frame = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
                _frame.transform.SetParent(_box, false);
                var _fit = _frame.GetComponent<AspectRatioFitter>(); _fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; _fit.aspectRatio = (float)_tex.width / _tex.height;
                var _fi = _frame.GetComponent<Image>(); _fi.color = new Color(INK.r, INK.g, INK.b, 0.55f); _fi.raycastTarget = false;
                var _img = Raw("Image", (RectTransform)_frame.transform, new Rect(0f, 0f, 1f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                _img.rectTransform.offsetMin = new Vector2(TUTORIAL_FRAME, TUTORIAL_FRAME); _img.rectTransform.offsetMax = new Vector2(-TUTORIAL_FRAME, -TUTORIAL_FRAME);
                _img.texture = _tex;
            }
            else Debug.LogWarning($"[MainMenu] 튜토리얼 그림을 찾지 못했습니다: Resources/{_page.ImagePath}");

            // 오른쪽: 제목 + 설명(줄바꿈) + 쪽 번호
            PageTitle(_r, _page.Title, 1085f);
            var _body = BookText(_r, _page.Body, new Vector2(1123f, 345f), 28f, TextAlignmentOptions.TopLeft, 430f);
            _body.rectTransform.pivot = new Vector2(0f, 1f); _body.rectTransform.sizeDelta = new Vector2(430f, 560f);
            _body.textWrappingMode = TextWrappingModes.Normal; _body.lineSpacing = 12f;
            ApplyFont(_body, 28f);
            var _num = BookText(_r, $"{_index + 1} / {_tutorialPages.Length}", new Vector2(1338f, 880f), 22f, TextAlignmentOptions.Center, 200f);
            _num.color = new Color(INK.r, INK.g, INK.b, 0.6f);
            return _group;
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
            float _mv = MusicVolume();
            for (int i = 0; i < 2; i++) if (_as[i] && _as[i].isPlaying && !DOTween.IsTweening(_as[i])) _as[i].volume = _mv;
            if (_loopSrc && _loopSrc.isPlaying && !DOTween.IsTweening(_loopSrc)) _loopSrc.volume = _mv;
            // 설정창(게임용)은 닫힐 때 커서를 잠그므로, 메인메뉴에서는 다시 풀어 줌
            if (!PauseSettingsUI.IsOpen && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
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

        /// <summary>우측 책갈피: 시네마틱 중이면 건너뛰기, 끝난 뒤면 다음 장/모험 시작</summary>
        public void SkipIntro() { if (_bookmarkGroup && _bookmarkGroup.interactable) { _skipIntro = true; _bookmarkClicked = true; _choseBack = false; } }

        /// <summary>튜토리얼 건너뛰기: 남은 장을 넘기지 않고 바로 게임 씬으로</summary>
        public void SkipTutorial() { if (_skipGroup && _skipGroup.interactable) { _skipTutorial = true; _bookmarkClicked = true; _choseBack = false; } }

        private void ShowSkip(bool _on)
        {
            if (!_skipGroup) return;
            _skipGroup.alpha = _on ? 1f : 0f;
            _skipGroup.interactable = _skipGroup.blocksRaycasts = _on;
        }

        /// <summary>좌측 책갈피: 앞 장(이야기 페이지)으로 되돌아감</summary>
        public void GoBackPage() { if (_backGroup && _backGroup.interactable) { _bookmarkClicked = true; _choseBack = true; } }

        /// <summary>우측 책갈피 글씨를 바꾸고 누를 때까지 기다림(깜빡임·번짐 같은 연출 없음). 키보드 Enter·Space·→ = 다음, ← = 이전</summary>
        private IEnumerator WaitBookmark(int _which, bool _allowBack)
        {
            if (!_bookmarkGroup) yield break;
            if (!_bookmarkGroup.gameObject.activeSelf) ShowBookmark(true);
            ShowBack(_allowBack);
            SetNextRibbon(_which);
            yield return null;                                             // 누른 클릭이 다음 단계로 바로 이어지지 않게 한 프레임
            _bookmarkClicked = false; _choseBack = false;
            while (!_bookmarkClicked)
            {
                var _kb = Keyboard.current;
                if (_kb != null && (_kb.enterKey.wasPressedThisFrame || _kb.numpadEnterKey.wasPressedThisFrame || _kb.spaceKey.wasPressedThisFrame || _kb.rightArrowKey.wasPressedThisFrame)) { _choseBack = false; break; }
                if (_allowBack && _kb != null && _kb.leftArrowKey.wasPressedThisFrame) { _choseBack = true; break; }
                if (_kb != null && _kb.escapeKey.wasPressedThisFrame && _skipGroup && _skipGroup.interactable) { _skipTutorial = true; _choseBack = false; break; }
                yield return null;
            }
            _bookmarkClicked = false;
            ShowBack(false);
        }

        // ================= 책갈피(우측: 건너뛰기·다음 장·모험 시작 / 좌측: 이전 장) =================
        // 책 장면(Blender)에 비단 리본을 넣어 같은 카메라·조명으로 렌더 → 리본과 그 그림자만 오려 낸 그림을 같은 자리에 겹침.
        // 리본은 페이지 사이에서 나와 표지 위로 늘어진 뒤 책상(지도)으로 떨어짐. 글씨도 천에 수놓인 채 함께 렌더됨
        private static readonly Rect RIGHT_RIBBON_PX = new Rect(1725f, 235f, 195f, 143f);   // 책 그림(1920×1080) 기준 위치·크기(왼쪽 위 0,0)
        private static readonly Rect LEFT_RIBBON_PX = new Rect(0f, 245f, 195f, 119f);

        private void BuildBookmark(RectTransform _root)
        {
            var _box = new GameObject("Bookmarks", typeof(RectTransform), typeof(AspectRatioFitter), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _box.SetParent(_root, false); Stretch(_box);
            var _fit = _box.GetComponent<AspectRatioFitter>(); _fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; _fit.aspectRatio = 16f / 9f;
            _bookmarkGroup = _box.GetComponent<CanvasGroup>();

            CanvasGroup _unused;
            _nextRibbon = MakeRibbon(_box, "NextRibbon", RIGHT_RIBBON_PX, SkipIntro, out _unused);
            _backRibbon = MakeRibbon(_box, "BackRibbon", LEFT_RIBBON_PX, GoBackPage, out _backGroup);
            _backRibbon.texture = GameLanguage.IsKorean ? _ribbonBackKo : _ribbonBackEn;
            SetNextRibbon(0);
            _backGroup.alpha = 0f; _backGroup.interactable = _backGroup.blocksRaycasts = false;

            _bookmarkGroup.alpha = 0f; _bookmarkGroup.interactable = _bookmarkGroup.blocksRaycasts = false;
            _box.gameObject.SetActive(false);
        }

        /// <summary>책 그림 px 영역에 리본 그림 한 장(누를 수 있음)</summary>
        private RawImage MakeRibbon(RectTransform _box, string _name, Rect _px, System.Action _onClick, out CanvasGroup _group)
        {
            var _go = new GameObject(_name, typeof(RectTransform), typeof(RawImage), typeof(CanvasGroup));
            _go.transform.SetParent(_box, false);
            var _r = (RectTransform)_go.transform;
            _r.anchorMin = new Vector2(_px.xMin / 1920f, 1f - _px.yMax / 1080f); _r.anchorMax = new Vector2(_px.xMax / 1920f, 1f - _px.yMin / 1080f);
            _r.offsetMin = _r.offsetMax = Vector2.zero;
            var _img = _go.GetComponent<RawImage>(); _img.raycastTarget = true;
            _group = _go.GetComponent<CanvasGroup>();
            var _bm = _go.AddComponent<MainMenuBookmark>(); _bm.Clicked = _onClick;
            return _img;
        }

        /// <summary>우측 리본 그림을 바꿈: 0 건너뛰기, 1 다음 장, 2 모험 시작(설정 언어에 맞춰)</summary>
        private void SetNextRibbon(int _which)
        {
            DOTween.Kill(_nextRibbon);   // 내려앉는 중에 글씨가 바뀌면 프레임 재생을 멈추고 바로 새 그림
            var _set = GameLanguage.IsKorean ? _ribbonNextKo : _ribbonNextEn;
            if (_set != null && _which >= 0 && _which < _set.Length && _set[_which]) _nextRibbon.texture = _set[_which];
        }

        private void ShowBookmark(bool _on)
        {
            if (!_bookmarkGroup) return;
            _bookmarkGroup.DOKill();
            _bookmarkGroup.interactable = _bookmarkGroup.blocksRaycasts = _on;
            if (_on)
            {
                // 잠시 뒤 리본이 책 사이에서 걸쳐지듯 놓임: 들린 모양으로 나타나 표지·책상 위로 내려앉음
                _bookmarkGroup.gameObject.SetActive(true); _bookmarkGroup.alpha = 0f;
                DOVirtual.DelayedCall(0.6f, () =>
                {
                    _bookmarkGroup.DOFade(1f, 0.12f).SetUpdate(true).SetLink(_bookmarkGroup.gameObject);
                    PlayDrape(_nextRibbon, GameLanguage.IsKorean ? _ribbonSkipKoDrape : _ribbonSkipEnDrape, _nextRibbon.texture);
                }).SetUpdate(true).SetLink(_bookmarkGroup.gameObject);
            }
            else _bookmarkGroup.DOFade(0f, 0.25f).SetUpdate(true).SetLink(_bookmarkGroup.gameObject).OnComplete(() => _bookmarkGroup.gameObject.SetActive(false));
        }

        private void ShowBack(bool _on)
        {
            if (!_backGroup) return;
            bool _was = _backGroup.alpha > 0.5f;
            _backGroup.interactable = _backGroup.blocksRaycasts = _on;
            _backGroup.alpha = _on ? 1f : 0f;
            // 새로 나타날 때만 걸쳐지듯 내려앉음(사라질 때는 그냥 사라짐)
            if (_on && !_was) PlayDrape(_backRibbon, GameLanguage.IsKorean ? _ribbonBackKoDrape : _ribbonBackEnDrape, GameLanguage.IsKorean ? _ribbonBackKo : _ribbonBackEn);
        }

        /// <summary>Blender에서 렌더한 내려앉는 프레임을 차례로 보여 주고 마지막에 정지 그림으로(천천히 가라앉는 곡선은 렌더에 들어 있음)</summary>
        private void PlayDrape(RawImage _img, Texture2D[] _frames, Texture _still)
        {
            if (!_img) return;
            DOTween.Kill(_img);
            if (_frames == null || _frames.Length == 0) { _img.texture = _still; return; }
            _img.texture = _frames[0];
            DOVirtual.Float(0f, _frames.Length, _drapeTime, _v => { int _i = Mathf.Min(_frames.Length - 1, (int)_v); if (_frames[_i]) _img.texture = _frames[_i]; })
                .SetEase(Ease.Linear).SetUpdate(true).SetId(_img).SetLink(_img.gameObject)
                .OnComplete(() => _img.texture = _still);
        }

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
        private readonly AudioSource[] _as = new AudioSource[2];
        private AudioSource _loopSrc;   // 시네마틱부터 반복되는 배경(메인메뉴 씬 안에서만)

        /// <summary>배경음 음량(전체 × 음악). 메인메뉴 영상 소리는 영상과 맞춰 시작해야 해서 SoundManager 배경음 대신 전용 소리로 틀고 음량만 맞춤</summary>
        private static float MusicVolume()
        {
            // 씬에 팀의 SoundManager가 있으면 그 음량(설정창이 바꾸는 값)을 그대로, 없으면 설정창 저장값
            var _sm = SoundManager.Instance;
            if (_sm) return _sm.MasterVolume * _sm.BgmVolume;
            return PlayerPrefs.GetFloat("HC_Master", 100f) / 100f * PlayerPrefs.GetFloat("HC_Music", 70f) / 100f;
        }

        private AudioClip AudioFor(VideoClip _v)
        {
            if (!_v) return null;
            if (_v == _loopNoPin || _v == _loopPin) return _menuAudio;
            if (_v == _zoomNoPin || _v == _zoomPin) return _zoomAudio;
            if (_v == _introKo || _v == _introEn) return _introAudio;
            return null;   // 핀 꽂힘 등은 소리 없음(배경음은 이어짐)
        }

        private void BuildVideoLayers()
        {
            for (int i = 0; i < 2; i++)
            {
                _as[i] = gameObject.AddComponent<AudioSource>();
                _as[i].playOnAwake = false; _as[i].spatialBlend = 0f; _as[i].volume = 0f;
            }
            _loopSrc = gameObject.AddComponent<AudioSource>();
            _loopSrc.playOnAwake = false; _loopSrc.spatialBlend = 0f; _loopSrc.loop = true; _loopSrc.volume = 0f;
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
            if ((_clip == _introKo || _clip == _introEn) && _storyLoop && !_loopSrc.isPlaying)
            {
                _loopSrc.clip = _storyLoop; _loopSrc.time = 0f; _loopSrc.volume = MusicVolume(); _loopSrc.Play();
            }
            var _a = AudioFor(_clip);
            if (_a)
            {
                var _src = _as[_back]; _src.DOKill();
                _src.clip = _a; _src.loop = _loop; _src.time = Mathf.Clamp((float)_p.time, 0f, _a.length - 0.01f);
                _src.volume = MusicVolume(); _src.Play();
                var _prev = _as[1 - _back];
                if (_prev.isPlaying) _prev.DOFade(0f, _audioFade).SetUpdate(true).SetLink(gameObject).OnComplete(_prev.Stop);
            }
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
            BuildBookmark(_root);

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

        // 페이드는 DOTween(게임이 멈춰도 흐르는 시간). 흐름(영상 준비·입력 기다리기)은 코루틴이 트윈 끝을 기다림
        private static IEnumerator Fade(Graphic _g, float _a0, float _a1, float _dur, bool _smooth = true)
        {
            SetAlpha(_g, _a0);
            yield return _g.DOFade(_a1, _dur).SetEase(_smooth ? Ease.InOutSine : Ease.Linear).SetUpdate(true).SetLink(_g.gameObject).WaitForCompletion();
        }

        private static IEnumerator FadeGroup(CanvasGroup _g, float _a0, float _a1, float _dur)
        {
            _g.alpha = _a0;
            yield return _g.DOFade(_a1, _dur).SetEase(Ease.Linear).SetUpdate(true).SetLink(_g.gameObject).WaitForCompletion();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current || FindFirstObjectByType<EventSystem>()) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
