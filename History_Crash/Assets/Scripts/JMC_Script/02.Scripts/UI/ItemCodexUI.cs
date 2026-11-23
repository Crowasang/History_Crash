using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using HistoryCrash.Data;
using HistoryCrash.Player;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 획득 아이템 UI: 화면 좌측 하단의 펼친 고대 도감.
    /// - 왼쪽 페이지 = 주운 스태프(패턴 무기 A~E), 오른쪽 페이지 = 주운 속성 오브.
    /// - 아이템은 도감에 펜으로 그린 삽화(세피아 펜선 + 해칭 + 옅은 담채)와 광채(스태프 금색, 오브 속성색)로,
    ///   이름은 표제 띠에 미리 구운 이미지로 보여 준다(Assets/Art/Resources/UI/Codex/).
    /// - 아이템이 바뀌면 이전 그림이 번지며 지워지고, 새 그림이 왼쪽→오른쪽으로 그려진 뒤 이름이 스며든다.
    /// - 씬에 배치하지 않아도 PlayerLoadout이 있는 씬에서 스스로 만들어지고, 기존 양피지 UI(WeaponUIController 캔버스)는 실행 중에만 숨긴다.
    /// </summary>
    public class ItemCodexUI : MonoBehaviour
    {
        private const string TEX = "UI/Codex/";
        private const float BOOK_W = 800f, BOOK_H = 440f;   // 도감 배경 이미지 크기(px), 아래 좌표의 기준
        private static readonly string[] PATTERN_KEY = { "A", "B", "C", "D", "E" };

        [Header("참조")]
        [SerializeField, Tooltip("비우면 씬에서 찾음")] private PlayerLoadout _loadout;
        [SerializeField, Tooltip("기존 양피지 UI 캔버스를 실행 중에 숨김")] private bool _hideOldHud = true;

        [Header("배치(기준 해상도 1920×1080)")]
        [SerializeField] private float _width = 400f;
        [SerializeField] private Vector2 _offset = new Vector2(10f, -6f);

        [Header("페이지 위치(도감 배경 px)")]
        [SerializeField] private Vector2 _leftArt = new Vector2(236f, 235f);
        [SerializeField] private Vector2 _rightArt = new Vector2(564f, 235f);
        [SerializeField] private Vector2 _leftTitle = new Vector2(238f, 89f);
        [SerializeField] private Vector2 _rightTitle = new Vector2(562f, 89f);

        [Header("연출(초)")]
        [SerializeField] private float _eraseTime = 0.22f;
        [SerializeField] private float _drawTime = 0.6f;
        [SerializeField] private float _titleTime = 0.35f;

        private class Page
        {
            public RectTransform Slot, Mask; public RawImage Art, Title; public string Key; public Sequence Running;
        }
        private Page _left, _right;
        private RectTransform _root;
        private int _lastStaff = int.MinValue; private bool _lastHasOrb; private Element _lastOrb;

        // 메인메뉴 → 게임 씬처럼 씬이 바뀌어도 다시 만들어지게(AfterSceneLoad는 플레이를 시작할 때 한 번만 불림)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoaded() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedAutoCreate; UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedAutoCreate; }
        private static void OnSceneLoadedAutoCreate(UnityEngine.SceneManagement.Scene _s, UnityEngine.SceneManagement.LoadSceneMode _m) { AutoCreate(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindFirstObjectByType<ItemCodexUI>()) return;
            if (!FindFirstObjectByType<PlayerLoadout>()) return;
            new GameObject("UI_ItemCodex").AddComponent<ItemCodexUI>();
        }

        private void Start()
        {
            if (!_loadout) _loadout = FindFirstObjectByType<PlayerLoadout>();
            if (_hideOldHud)
                foreach (var _old in FindObjectsByType<WeaponUIController>(FindObjectsSortMode.None))
                {
                    var _c = _old.GetComponentInParent<Canvas>(); if (_c) _c.enabled = false;
                }
            Build();
        }

        private void Build()
        {
            var _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 9;
            var _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f); _scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("Codex", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(transform, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.zero;
            _root.anchoredPosition = new Vector2(_offset.x, _offset.y);
            _root.sizeDelta = new Vector2(_width, _width * BOOK_H / BOOK_W);

            var _book = NewImage("Book", _root);
            Stretch(_book.rectTransform); _book.texture = Resources.Load<Texture2D>(TEX + "UI_Codex_Book");

            _left = MakePage("Left", _leftArt, _leftTitle);
            _right = MakePage("Right", _rightArt, _rightTitle);
        }

        // 도감 배경 px 좌표 → 책 안의 앵커(0~1)
        private static Vector2 Anchor(Vector2 _px) { return new Vector2(_px.x / BOOK_W, 1f - _px.y / BOOK_H); }
        private float Scale { get { return _width / BOOK_W; } }

        private Page MakePage(string _name, Vector2 _art, Vector2 _title)
        {
            var _p = new Page();
            _p.Slot = new GameObject(_name + "Art", typeof(RectTransform)).GetComponent<RectTransform>();
            _p.Slot.SetParent(_root, false); _p.Slot.anchorMin = _p.Slot.anchorMax = Anchor(_art); _p.Slot.pivot = new Vector2(0.5f, 0.5f);
            // 펜으로 그려지는 연출: 왼쪽에 붙은 가림 틀(RectMask2D)의 폭을 0→전체로
            _p.Mask = new GameObject("Draw", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            _p.Mask.SetParent(_p.Slot, false); _p.Mask.anchorMin = new Vector2(0f, 0f); _p.Mask.anchorMax = new Vector2(0f, 1f); _p.Mask.pivot = new Vector2(0f, 0.5f);
            _p.Art = NewImage("Ink", _p.Mask);
            _p.Art.rectTransform.anchorMin = _p.Art.rectTransform.anchorMax = new Vector2(0f, 0.5f); _p.Art.rectTransform.pivot = new Vector2(0f, 0.5f);
            _p.Title = NewImage(_name + "Title", _root);
            _p.Title.rectTransform.anchorMin = _p.Title.rectTransform.anchorMax = Anchor(_title);
            _p.Title.rectTransform.sizeDelta = new Vector2(300f, 70f) * Scale;
            _p.Slot.gameObject.SetActive(false); _p.Title.gameObject.SetActive(false);
            return _p;
        }

        private static RawImage NewImage(string _name, Transform _parent)
        {
            var _img = new GameObject(_name, typeof(RectTransform)).AddComponent<RawImage>();
            _img.rectTransform.SetParent(_parent, false); _img.raycastTarget = false;
            return _img;
        }
        private static void Stretch(RectTransform _rt) { _rt.anchorMin = Vector2.zero; _rt.anchorMax = Vector2.one; _rt.offsetMin = _rt.offsetMax = Vector2.zero; }

        private void Update()
        {
            if (!_loadout || _left == null) return;
            if (_loadout.HeldStaff != _lastStaff)
            {
                _lastStaff = _loadout.HeldStaff;
                Show(_left, _lastStaff >= 0 ? "Pattern_" + PATTERN_KEY[Mathf.Clamp(_lastStaff, 0, 4)] : null);
            }
            if (_loadout.HasOrb != _lastHasOrb || (_loadout.HasOrb && _loadout.HeldOrb != _lastOrb))
            {
                _lastHasOrb = _loadout.HasOrb; _lastOrb = _loadout.HeldOrb;
                Show(_right, _lastHasOrb ? "Core_" + _lastOrb : null);
            }
        }

        private void Show(Page _p, string _key)
        {
            if (_key == _p.Key) return;
            _p.Running?.Kill();
            _p.Slot.localScale = Vector3.one; _p.Slot.localRotation = Quaternion.identity;
            // 1) 이전 그림·이름이 번지며 지워짐 → 끝나면 2) 새 그림
            var _seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (_p.Slot.gameObject.activeSelf)
            {
                _seq.Insert(0f, _p.Art.DOFade(0f, _eraseTime).SetEase(Ease.Linear));
                _seq.Insert(0f, _p.Title.DOFade(0f, _eraseTime).SetEase(Ease.Linear));
                _seq.Insert(0f, _p.Slot.DOScale(1.06f, _eraseTime).SetEase(Ease.Linear));
            }
            _seq.OnComplete(() => Draw(_p, _key));
            _p.Running = _seq;
        }

        private void Draw(Page _p, string _key)
        {
            _p.Slot.localScale = Vector3.one; _p.Key = _key;
            _p.Slot.gameObject.SetActive(false); _p.Title.gameObject.SetActive(false);
            _p.Running = null;
            if (string.IsNullOrEmpty(_key)) return;
            var _art = Resources.Load<Texture2D>(TEX + "UI_Codex_" + _key);
            var _title = Resources.Load<Texture2D>(TEX + "UI_Codex_Title_" + _key);
            if (!_art) return;

            // 2) 새 삽화가 왼쪽→오른쪽으로 그려짐(펜 속도: 처음 빠르고 끝에서 느림, 살짝 떨림)
            Vector2 _size = new Vector2(_art.width, _art.height) * Scale;
            _p.Slot.sizeDelta = _size; _p.Art.rectTransform.sizeDelta = _size; _p.Art.texture = _art; _p.Art.color = Color.white;
            _p.Mask.sizeDelta = Vector2.zero; _p.Slot.gameObject.SetActive(true);
            var _seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _seq.Insert(0f, DOTween.To(() => _p.Mask.sizeDelta.x, w => _p.Mask.sizeDelta = new Vector2(w, 0f), _size.x, _drawTime).SetEase(Ease.OutQuad));
            _seq.Insert(0f, DOVirtual.Float(0f, 1f, _drawTime, u => _p.Slot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(u * Mathf.PI * 3f) * 1.0f * (1f - u))).SetEase(Ease.Linear));
            _seq.AppendCallback(() => _p.Slot.localRotation = Quaternion.identity);
            // 3) 이름이 스며듦
            if (_title)
            {
                _seq.AppendCallback(() => { _p.Title.texture = _title; _p.Title.color = new Color(1f, 1f, 1f, 0f); _p.Title.gameObject.SetActive(true); });
                _seq.Append(_p.Title.DOFade(1f, _titleTime).SetEase(Ease.InOutSine));
            }
            _seq.OnComplete(() => _p.Running = null);
            _p.Running = _seq;
        }
    }
}
