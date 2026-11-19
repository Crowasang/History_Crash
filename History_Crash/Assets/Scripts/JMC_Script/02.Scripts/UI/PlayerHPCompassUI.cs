using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HistoryCrash.Player;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 플레이어 HP 나침반 UI(화면 좌측 상단).
    /// - Blender에서 렌더한 HP 0~5 상태 이미지 6장(Assets/Art/Resources/UI/PlayerHP/UI_PlayerHP_n.png)을 HP에 맞춰 바꿔 보여 준다.
    /// - HP가 바뀌면 새 이미지가 짧게 겹쳐 나타나고(바늘이 넘어가며 루비가 꺼지는 느낌), 맞았을 때는 살짝 흔들린다.
    /// - 씬에 따로 배치하지 않아도 게임이 시작되면 PlayerHealth가 있는 씬에서 스스로 만들어진다.
    /// </summary>
    public class PlayerHPCompassUI : MonoBehaviour
    {
        private const string TEX_PATH = "UI/PlayerHP/UI_PlayerHP_";
        private const int MAX_STATE = 5;

        [Header("참조")]
        [SerializeField, Tooltip("비우면 씬에서 찾음")] private PlayerHealth _health;

        [Header("배치(기준 해상도 1920×1080)")]
        [SerializeField] private float _size = 330f;
        [SerializeField] private Vector2 _offset = Vector2.zero;

        [Header("연출")]
        [SerializeField, Tooltip("새 상태 이미지가 겹쳐 나타나는 시간")] private float _fadeTime = 0.25f;
        [SerializeField, Tooltip("HP가 줄 때 흔들리는 시간")] private float _shakeTime = 0.3f;
        [SerializeField, Tooltip("흔들림 크기(px)")] private float _shakeAmp = 6f;

        private readonly Texture2D[] _states = new Texture2D[MAX_STATE + 1];
        private RawImage _base, _over;
        private RectTransform _root;
        private int _shown = -1;
        private Coroutine _running;

        // 씬이 열리면 PlayerHealth가 있는 경우에만 HP UI를 하나 만든다(씬 저장 없이 동작)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindFirstObjectByType<PlayerHPCompassUI>()) return;
            if (!FindFirstObjectByType<PlayerHealth>()) return;
            new GameObject("UI_PlayerHPCompass").AddComponent<PlayerHPCompassUI>();
        }

        private void Awake()
        {
            if (!_health) _health = FindFirstObjectByType<PlayerHealth>();
            for (int _i = 0; _i <= MAX_STATE; _i++) _states[_i] = Resources.Load<Texture2D>(TEX_PATH + _i);
            Build();
        }

        private void Build()
        {
            var _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("Compass", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(transform, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = _offset;
            _root.sizeDelta = new Vector2(_size, _size);
            _base = MakeImage("Base");
            _over = MakeImage("Over");
            _over.color = new Color(1f, 1f, 1f, 0f);
        }

        private RawImage MakeImage(string _name)
        {
            var _img = new GameObject(_name, typeof(RectTransform)).AddComponent<RawImage>();
            _img.rectTransform.SetParent(_root, false);
            _img.rectTransform.anchorMin = Vector2.zero; _img.rectTransform.anchorMax = Vector2.one;
            _img.rectTransform.offsetMin = _img.rectTransform.offsetMax = Vector2.zero;
            _img.raycastTarget = false;
            return _img;
        }

        private void Update()
        {
            if (!_health) return;
            int _hp = Mathf.Clamp(_health.HP, 0, MAX_STATE);
            if (_hp == _shown) return;
            if (_shown < 0) { _base.texture = _states[_hp]; _shown = _hp; return; }   // 처음엔 연출 없이
            bool _hurt = _hp < _shown;
            _shown = _hp;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Change(_states[_hp], _hurt));
        }

        // 새 상태를 위에 겹쳐 서서히 드러낸 뒤 아래 이미지와 바꿈. 맞았으면 흔들림
        private IEnumerator Change(Texture2D _next, bool _hurt)
        {
            _over.texture = _next;
            for (float _t = 0f; _t < Mathf.Max(_fadeTime, _shakeTime); _t += Time.unscaledDeltaTime)
            {
                _over.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_t / Mathf.Max(0.01f, _fadeTime)));
                if (_hurt && _t < _shakeTime)
                {
                    float _k = 1f - _t / _shakeTime;
                    _root.anchoredPosition = _offset + new Vector2(Mathf.Sin(_t * 90f), Mathf.Cos(_t * 70f)) * _shakeAmp * _k;
                }
                else _root.anchoredPosition = _offset;
                yield return null;
            }
            _base.texture = _next;
            _over.color = new Color(1f, 1f, 1f, 0f);
            _root.anchoredPosition = _offset;
            _running = null;
        }
    }
}
