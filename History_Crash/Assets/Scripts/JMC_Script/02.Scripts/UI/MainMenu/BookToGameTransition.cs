using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 메인메뉴 → 게임 씬 전환: 화면을 꽉 채운 펼친 도감이 줌아웃되며 뒤로 게임 화면이 비치고,
    /// 책이 덮인 뒤 아래로 내려가며 사라진다(ESC 설정창이 닫히는 연출과 같은 도감 그림·넘김 프레임 사용).
    /// 씬을 넘어가며 유지되는 오버레이. MainMenuController가 만들어 쓰므로 직접 붙일 필요는 없다.
    /// </summary>
    public class BookToGameTransition : MonoBehaviour
    {
        private const string TEX = "UI/Settings/";
        // ESC 설정창(PauseSettingsUI)과 같은 값: 도감 칸 960×560, 화면 배율 1.6, 위치 -12, 덮였을 때 왼쪽으로 170
        private const float BOOK_W = 960f, BOOK_H = 560f, SCALE = 1.6f, Y_OFFSET = -12f, CLOSED_SHIFT = -170f, DIM = 0.62f;
        // 메인메뉴 펼침면(1920×1080 화면 꽉 참)의 책 너비 1700px ↔ 설정창 도감 그림 속 책 너비 740px
        private const float START_SCALE = 1700f / 740f;

        private Image _dim, _bookImg;
        private RawImage _spread;
        private Sprite _bookStatic; private Sprite[] _frames;
        private bool _started;

        public static BookToGameTransition Create(Texture _spreadTex)
        {
            var _go = new GameObject("BookToGameTransition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(_go);
            var _cv = _go.GetComponent<Canvas>(); _cv.renderMode = RenderMode.ScreenSpaceOverlay; _cv.sortingOrder = 1000;
            var _sc = _go.GetComponent<CanvasScaler>(); _sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; _sc.referenceResolution = new Vector2(1920f, 1080f); _sc.matchWidthOrHeight = 0.5f;
            var _t = _go.AddComponent<BookToGameTransition>();
            _t.Build(_spreadTex);
            PauseSettingsUI.InputBlocked = true;     // 책이 다 내려갈 때까지 이동·시점·공격·ESC 막음
            return _t;
        }

        private void OnDestroy() { PauseSettingsUI.InputBlocked = false; }

        private void Build(Texture _spreadTex)
        {
            // 뒤 배경: 처음엔 검정(씬을 불러오는 동안), 게임이 뜨면 설정창과 같은 어두운 막으로
            _dim = NewGraphic<Image>("Dim"); Stretch(_dim.rectTransform); _dim.color = new Color(0f, 0f, 0f, 1f);

            // 설정창과 같은 도감(배경이 투명한 그림) — 처음엔 숨겨 둠
            _bookStatic = Resources.Load<Sprite>(TEX + "UI_Settings_Book");
            _frames = Resources.LoadAll<Sprite>(TEX + "Open");
            System.Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            _bookImg = NewGraphic<Image>("Book"); _bookImg.sprite = _bookStatic;
            var _r = _bookImg.rectTransform; _r.anchorMin = _r.anchorMax = _r.pivot = new Vector2(0.5f, 0.5f); _r.sizeDelta = new Vector2(BOOK_W, BOOK_H);
            SetBook(START_SCALE, 0f, 0f); SetAlpha(_bookImg, 0f);

            // 지금 화면과 똑같은 펼침면(조작법 글자가 걷힌 빈 도감)
            _spread = NewGraphic<RawImage>("Spread"); _spread.texture = _spreadTex;
            Stretch(_spread.rectTransform);
            var _fit = _spread.gameObject.AddComponent<AspectRatioFitter>(); _fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; _fit.aspectRatio = 16f / 9f;
        }

        private void OnEnable() { SceneManager.sceneLoaded += OnLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }
        private void OnLoaded(Scene _s, LoadSceneMode _m) { if (_started) return; _started = true; StartCoroutine(Play()); }

        private IEnumerator Play()
        {
            for (int i = 0; i < 3; i++) yield return null;               // 게임 씬 첫 장면이 자리 잡도록
            float _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;                                          // 책이 내려갈 때까지 게임은 멈춤(설정창 닫힘과 같음)

            // 연출은 DOTween 시퀀스(게임 시간이 멈춰 있으므로 SetUpdate(true))
            var _dimC = new Color(0.03f, 0.035f, 0.02f, DIM);
            bool _has = _frames != null && _frames.Length > 1;
            var _seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            // 1) 펼침면 → 설정창 도감으로 겹쳐 바꾸며 뒤 배경이 게임 화면(어둡게)으로
            _seq.Append(DOVirtual.Float(0f, 1f, 0.35f, _u =>
            {
                SetAlpha(_spread, 1f - _u); SetAlpha(_bookImg, _u);
                _dim.color = Color.Lerp(Color.black, _dimC, _u);
            }).SetEase(Ease.InOutSine));
            _seq.AppendCallback(() => { _spread.gameObject.SetActive(false); SetAlpha(_bookImg, 1f); _dim.color = _dimC; });
            // 2) 줌아웃(화면을 채우던 책이 설정창 크기로)
            _seq.Append(DOVirtual.Float(0f, 1f, 0.65f, _u => SetBook(Mathf.Lerp(START_SCALE, SCALE, _u), 0f, Mathf.Lerp(0f, Y_OFFSET, _u))).SetEase(Ease.InOutSine));
            // 3) 책이 덮임(설정창 펼침 프레임을 거꾸로)
            if (_has)
            {
                _seq.Append(DOVirtual.Float(1f, 0f, 0.5f, _u =>
                {
                    _bookImg.sprite = _frames[Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(_u * _frames.Length))];
                    SetBook(SCALE, CLOSED_SHIFT * (1f - Smooth(Mathf.Clamp01(_u * 2f))), Y_OFFSET);
                }).SetEase(Ease.Linear));
                _seq.AppendCallback(() => _bookImg.sprite = _frames[0]);
            }
            // 4) 아래로 떨어지며 막이 걷힘
            _seq.Append(DOVirtual.Float(0f, 1f, 0.4f, _u =>
            {
                _dim.color = new Color(_dimC.r, _dimC.g, _dimC.b, DIM * (1f - Smooth(_u)));
                SetBook(SCALE, _has ? CLOSED_SHIFT : 0f, Y_OFFSET - 720f * _u * _u);
            }).SetEase(Ease.Linear));
            yield return _seq.WaitForCompletion();
            Time.timeScale = _prevScale;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            PauseSettingsUI.InputBlocked = false;    // 책이 다 내려간 이 순간부터 플레이어 조작 가능
            Destroy(gameObject);
        }

        private void SetBook(float _scale, float _shiftX, float _y)
        {
            var _r = _bookImg.rectTransform;
            _r.localScale = Vector3.one * _scale;
            _r.anchoredPosition = new Vector2(_shiftX * _scale, _y);
        }

        private T NewGraphic<T>(string _n) where T : Graphic
        {
            var _go = new GameObject(_n, typeof(RectTransform), typeof(T));
            _go.transform.SetParent(transform, false);
            var _g = _go.GetComponent<T>(); _g.raycastTarget = false; return _g;
        }
        private static void Stretch(RectTransform _r) { _r.anchorMin = Vector2.zero; _r.anchorMax = Vector2.one; _r.offsetMin = _r.offsetMax = Vector2.zero; }
        private static void SetAlpha(Graphic _g, float _a) { var _c = _g.color; _c.a = _a; _g.color = _c; }
        private static float Smooth(float _t) { _t = Mathf.Clamp01(_t); return _t * _t * (3f - 2f * _t); }
    }
}
