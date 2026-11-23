using R3;
using UnityEngine;
using UnityEngine.UI;
using HistoryCrash.Boss;

namespace HistoryCrash.UI
{
    /// <summary>
    /// 보스 체력바: 화면 위 가운데의 도감 두루마리. 체력을 "깎는" 대신 오염(검은 얼룩 + 초록 핏줄)이 "정화"되며 물러난다.
    /// - 바닥 = 정화된 띠, 위 = 오염된 띠(남은 체력만큼 왼쪽부터 보임), 경계 = 금빛으로 타들어 가는 조각.
    /// - 이미지는 Blender 렌더를 미리 구운 것(Assets/Art/Resources/UI/BossBar/).
    /// - 씬에 배치하지 않아도 GameManager에 보스 체력(Health)이 연결된 씬에서 스스로 만들어진다.
    /// </summary>
    public class BossPurifyBarUI : MonoBehaviour
    {
        private const string TEX = "UI/BossBar/";
        private const float IMG_W = 1600f, IMG_H = 300f;      // 띠 이미지 크기(px), 아래 좌표의 기준
        private const float GROOVE_L = 163f, GROOVE_R = 1437f; // 체력 홈 왼쪽·오른쪽 끝(px)
        private const float EDGE_W = 140f;                     // 경계 조각 폭(px)

        // 보스 체력 (KDH 코어: GameManager.BossHealth = 파라오의 Health)
        private IHealthReadOnly _boss;
        // GameManager가 없는 씬(TEST_JMC 등)은 예전 JMC 파라오 체력으로
        private PharaohHealth _legacy;
        private float _lastHitTime = -10f;

        [Header("배치(기준 해상도 1920×1080)")]
        [SerializeField] private float _width = 640f;
        [SerializeField] private float _top = 6f;

        [Header("연출")]
        [SerializeField, Tooltip("표시가 실제 체력을 따라가는 속도(초당 비율)")] private float _follow = 0.6f;
        [SerializeField, Tooltip("맞았을 때 경계가 밝아지는 시간(초)")] private float _flashTime = 0.35f;
        [SerializeField, Tooltip("완전히 정화된 뒤 사라지기까지(초)")] private float _hideDelay = 2.5f;

        private RectTransform _mask, _edge;
        private RawImage _edgeImg;
        private CanvasGroup _group;
        private float _shown = 1f, _deadAt = -1f;

        // 메인메뉴 → 게임 씬처럼 씬이 바뀌어도 다시 만들어지게(AfterSceneLoad는 플레이를 시작할 때 한 번만 불림)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookSceneLoaded() { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedAutoCreate; UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedAutoCreate; }
        private static void OnSceneLoadedAutoCreate(UnityEngine.SceneManagement.Scene _s, UnityEngine.SceneManagement.LoadSceneMode _m) { AutoCreate(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindFirstObjectByType<BossPurifyBarUI>()) return;
            bool _core = GameManager.Instance != null && GameManager.Instance.BossHealth != null;
            if (!_core && !FindFirstObjectByType<PharaohHealth>()) return;
            new GameObject("UI_BossPurifyBar").AddComponent<BossPurifyBarUI>();
        }

        private void Start()
        {
            _boss = GameManager.Instance != null ? GameManager.Instance.BossHealth : null;
            if (_boss != null) _boss.OnDamaged.Subscribe(_ => _lastHitTime = Time.time).AddTo(this);   // 맞은 순간 경계 반짝임
            else _legacy = FindFirstObjectByType<PharaohHealth>();
            Build();
            if (HasBoss) _shown = Fraction();
            Apply(_shown, 0f);
        }

        private void Build()
        {
            var _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 8;
            var _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f); _scaler.matchWidthOrHeight = 0.5f;

            var _root = new GameObject("BossBar", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _root.SetParent(transform, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, _top);
            Vector2 _size = new Vector2(_width, _width * IMG_H / IMG_W);
            _root.sizeDelta = _size;
            _group = _root.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;

            var _pure = NewImage("Pure", _root); Stretch(_pure.rectTransform);
            _pure.texture = Resources.Load<Texture2D>(TEX + "UI_BossBar_Pure");

            // 오염: 왼쪽에 붙은 가림 틀(RectMask2D) 폭 = 남은 체력
            _mask = new GameObject("Corruption", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            _mask.SetParent(_root, false);
            _mask.anchorMin = new Vector2(0f, 0f); _mask.anchorMax = new Vector2(0f, 1f); _mask.pivot = new Vector2(0f, 0.5f);
            _mask.anchoredPosition = Vector2.zero;
            var _cor = NewImage("Corrupt", _mask);
            _cor.rectTransform.anchorMin = _cor.rectTransform.anchorMax = _cor.rectTransform.pivot = new Vector2(0f, 0.5f);
            _cor.rectTransform.sizeDelta = _size; _cor.rectTransform.anchoredPosition = Vector2.zero;
            _cor.texture = Resources.Load<Texture2D>(TEX + "UI_BossBar_Corrupt");

            _edgeImg = NewImage("PurifyEdge", _root);
            _edge = _edgeImg.rectTransform;
            _edge.anchorMin = new Vector2(0f, 0f); _edge.anchorMax = new Vector2(0f, 1f); _edge.pivot = new Vector2(0.5f, 0.5f);
            _edge.sizeDelta = new Vector2(EDGE_W * Scale, 0f);
            _edgeImg.texture = Resources.Load<Texture2D>(TEX + "UI_BossBar_Edge");
        }

        private float Scale { get { return _width / IMG_W; } }
        private bool HasBoss { get { return _boss != null || _legacy; } }
        private float Fraction()
        {
            if (_boss != null) return _boss.MaxHp > 0 ? Mathf.Clamp01(_boss.CurrentHp.CurrentValue / (float)_boss.MaxHp) : 0f;
            return _legacy.MaxHP > 0 ? Mathf.Clamp01(_legacy.HP / (float)_legacy.MaxHP) : 0f;
        }
        private float LastHit { get { return _boss != null ? _lastHitTime : _legacy.LastHitTime; } }
        private bool BossAlive { get { return _boss != null ? _boss.IsAlive : !_legacy.IsDead; } }

        private static RawImage NewImage(string _name, Transform _parent)
        {
            var _img = new GameObject(_name, typeof(RectTransform)).AddComponent<RawImage>();
            _img.rectTransform.SetParent(_parent, false); _img.raycastTarget = false;
            return _img;
        }
        private static void Stretch(RectTransform _rt) { _rt.anchorMin = Vector2.zero; _rt.anchorMax = Vector2.one; _rt.offsetMin = _rt.offsetMax = Vector2.zero; }

        private void Update()
        {
            if (!HasBoss || _mask == null) return;
            float _target = Fraction();
            // 정화는 처음 빠르게 붙고 끝에서 느려짐(값 차이에 비례 + 최소 속도)
            float _gap = _shown - _target;
            float _step = (Mathf.Abs(_gap) * 4f + _follow * 0.15f) * Time.unscaledDeltaTime;
            _shown = Mathf.Abs(_gap) <= _step ? _target : _shown - Mathf.Sign(_gap) * _step;

            float _flash = Mathf.Clamp01(1f - (Time.time - LastHit) / _flashTime);
            Apply(_shown, _flash);

            // 완전히 정화되면 잠시 뒤 사라짐
            if (!BossAlive && _shown <= 0f)
            {
                if (_deadAt < 0f) _deadAt = Time.unscaledTime;
                _group.alpha = Mathf.Clamp01(1f - (Time.unscaledTime - _deadAt - _hideDelay) / 0.8f);
            }
            else { _deadAt = -1f; _group.alpha = 1f; }
        }

        // _f(0~1) → 오염 가림 틀 폭과 경계 위치
        private void Apply(float _f, float _flash)
        {
            float _px; // 띠 이미지 기준 경계 x
            if (_f <= 0f) _px = 0f;
            else if (_f >= 0.999f) _px = IMG_W;
            else _px = Mathf.Lerp(GROOVE_L, GROOVE_R, _f);
            _mask.sizeDelta = new Vector2(_px * Scale, 0f);

            bool _onGroove = _f > 0.001f && _f < 0.999f;
            _edge.gameObject.SetActive(_onGroove);
            if (_onGroove)
            {
                _edge.anchoredPosition = new Vector2(Mathf.Lerp(GROOVE_L, GROOVE_R, _f) * Scale, 0f);
                // 타들어 가는 경계: 잔잔한 깜빡임 + 맞았을 때 밝아지고 살짝 커짐
                float _flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.unscaledTime * 6f, 0.3f);
                _edgeImg.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_flicker + _flash));
                _edge.localScale = new Vector3(1f + 0.25f * _flash, 1f, 1f);
            }
        }
    }
}
