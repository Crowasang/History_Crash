using System.Collections;
using System.Collections.Generic;
using HistoryCrash.Boss;
using HistoryCrash.CameraRig;
using HistoryCrash.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryCrash.Cutscene
{
    /// <summary>
    /// 게임 시작 보스 소개 컷씬(샘플 '보스소개연출_v1' 확정본을 게임에서 재생).
    /// 흐름(24fps 기준 408프레임 = 17초):
    ///   0–35   플레이어 뒤 시점, 위아래 검은 띠가 내려옴(조작 잠김)
    ///   36–131 카메라가 통로를 따라 제단으로, 잠든 파라오(원래 자리보다 0.8m 아래)
    ///   132–215 깨어남: 떠오름(132~200), 150 초록 섬광, 독기, 눈빛, 초록 조명
    ///   216–311 올려다보는 구도, 이름 카드(228/238/254 → 292~310 사라짐)
    ///   312–407 카메라가 원래 게임 카메라 자리로 돌아옴, 360~384 띠 걷힘, 364~384 보스바, 384~ 나머지 HUD
    /// 동안 게임 시간은 멈춤(보스 패턴·미라 정지), 입력 막음. 소리: Resources/Cutscene/BossIntro/BossIntro_Mix(VARCO 레이어 1차 믹스).
    /// 씬에 따로 놓지 않아도 SCENES에 있는 씬이 열리면 자동으로 생김(메인메뉴에서 오면 책이 내려앉은 뒤 시작).
    /// </summary>
    public class BossIntroCutscene : MonoBehaviour
    {
        /// <summary>컷씬을 재생할 씬 이름(팀 Game 씬에도 쓰려면 여기에 추가)</summary>
        public static readonly string[] SCENES = { "TEST_JMC", "Game" };
        /// <summary>false면 재생하지 않음(테스트용)</summary>
        public static bool Enabled = true;
        /// <summary>true면 다음 한 번만 재생하지 않음(게임 오버 → Retry)</summary>
        public static bool SkipNext;

        private const string RES = "Cutscene/BossIntro/";
        private const float FPS = 24f, TOTAL = 408f, LB = 70f;
        // 설정 언어가 영어면 이름 끝 _EN 그림(없으면 한국어 그림)
        private static Texture2D LoadLang(string _path)
        {
            if (!HistoryCrash.UI.GameLanguage.IsKorean) { var _en = Resources.Load<Texture2D>(_path + "_EN"); if (_en) return _en; }
            return Resources.Load<Texture2D>(_path);
        }

        private static readonly Rect CARD_RECT = new Rect(1060f, 120f, 540f, 326f);   // 1920x1080 기준, 왼쪽 위 원점
        private static readonly Color GREEN = new Color(0.35f, 1f, 0.4f);
        private const float SKIP_GUARD = 0.3f;   // 시작 직후 남아 있던 키 입력으로 바로 넘어가지 않게(초)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook() { SkipNext = false; SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene _s, LoadSceneMode _m) { if (_m == LoadSceneMode.Single) AutoCreate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!Enabled) return;
            if (System.Array.IndexOf(SCENES, SceneManager.GetActiveScene().name) < 0) return;
            if (SkipNext) { SkipNext = false; return; }
            if (FindFirstObjectByType<BossIntroCutscene>()) return;
            if (!FindPharaoh()) return;
            new GameObject("BossIntroCutscene").AddComponent<BossIntroCutscene>();
        }

        // 파라오 찾기: JMC PharaohHealth(TEST_JMC) → 없으면 팀 GameManager의 보스 체력이 붙은 오브젝트(Game 씬)
        internal static Transform FindPharaoh()
        {
            var _h = FindFirstObjectByType<PharaohHealth>();
            if (_h) return _h.transform;
            var _gm = GameManager.Instance;
            if (_gm != null && _gm.BossHealth is Component _c && _c) return _c.transform;
            return null;
        }

        // ---- 장면 ----
        private Camera _cam; private ThirdPersonCamera _tpc;
        private static readonly int IDLE = Animator.StringToHash("Idle");
        private Transform _ph; private Animator _phAnim; private AnimatorUpdateMode _phMode;
        private bool _phIdle; private float _phSpeed, _phNorm; private int _phHash;   // 컷씬 전 상태(끝나면 되돌림)
        private Vector3 _phBase, _fwd, _right;
        private Vector3 _s1, _s3, _s4, _s3e, _s4e, _s1L, _s3L, _s4L; private Quaternion _s1Rot; private float _fov0;
        private float _prevScale = 1f; private readonly List<Behaviour> _locked = new List<Behaviour>();
        private Light _green, _eye; private ParticleSystem _smoke, _sparks;
        private float _eyeHeight;
        // ---- 화면 ----
        private RectTransform _top, _bot; private Image _flash; private RawImage[] _cards;
        private readonly List<(CanvasGroup g, float a, bool added, bool boss)> _hud = new List<(CanvasGroup, float, bool, bool)>();
        private AudioSource _audio;

        private IEnumerator Start()
        {
            PauseSettingsUI.InputBlocked = true;                                   // 메인메뉴 전환 중에도 이미 막혀 있음
            yield return null; yield return null;
            while (FindFirstObjectByType<BookToGameTransition>()) yield return null;   // 책이 다 내려앉을 때까지
            // 중간에 오류가 나도 게임이 멈춘 채로 남지 않게 항상 Finish로 되돌림
            bool _ok = false;
            try { _ok = Setup(); } catch (System.Exception _e) { Debug.LogException(_e, this); }
            if (!_ok) { Finish(); yield break; }
            float _t0 = Time.unscaledTime;
            if (_audio && _audio.clip) _audio.Play();
            float _f = 0f;
            while (_f < TOTAL)
            {
                // Esc로 건너뛰기: 남은 연출 없이 바로 게임으로(소리도 끊음).
                // 설정창의 Esc 확인(Update)이 이 코루틴보다 먼저 돌기 때문에, 같은 입력으로 설정창이 열리지 않음
                var _kb = Keyboard.current;
                if (_kb != null && _kb.escapeKey.wasPressedThisFrame && Time.unscaledTime - _t0 > SKIP_GUARD)
                {
                    if (_audio) _audio.Stop();
                    break;
                }
                _f = (Time.unscaledTime - _t0) * FPS;
                if (_audio && _audio.isPlaying) _f = Mathf.Max(_f, _audio.time * FPS);   // 소리와 어긋나지 않게
                try { Apply(Mathf.Min(_f, TOTAL - 1f)); }
                catch (System.Exception _e) { Debug.LogException(_e, this); if (_audio) _audio.Stop(); break; }
                yield return null;
            }
            Finish();
        }

        private bool Setup()
        {
            var _phT = FindPharaoh();
            _cam = Camera.main;
            if (!_phT || !_cam) return false;
            _tpc = _cam.GetComponent<ThirdPersonCamera>(); if (!_tpc) _tpc = FindFirstObjectByType<ThirdPersonCamera>();

            // 멈춤·입력 잠금(BookToGameTransition과 같은 방식)
            _prevScale = Time.timeScale > 0f ? Time.timeScale : 1f; Time.timeScale = 0f;
            foreach (var _b in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None)) if (_b.enabled) { _b.enabled = false; _locked.Add(_b); }
            foreach (var _b in FindObjectsByType<PauseInputHandler>(FindObjectsSortMode.None)) if (_b.enabled) { _b.enabled = false; _locked.Add(_b); }
            if (_tpc && _tpc.enabled) { _tpc.enabled = false; _locked.Add(_tpc); }
            // Game 씬: 카메라는 Cinemachine(Brain이 매 프레임 카메라를 덮어씀), 이동은 팀 PlayerInput → 컷씬 동안 끔(패키지·팀 스크립트는 고치지 않고 켜고 끄기만)
            foreach (var _b in _cam.GetComponents<Behaviour>()) if (_b && _b.enabled && _b.GetType().Name == "CinemachineBrain") { _b.enabled = false; _locked.Add(_b); }
            foreach (var _b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None)) if (_b && _b.enabled && _b.GetType().Name == "PlayerInput") { _b.enabled = false; _locked.Add(_b); }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

            // 파라오 기준 좌표(샘플: 파라오 (0.58,2.4,19.02)가 +x를 봄 → 카메라 위치를 그 기준으로 옮김)
            _ph = _phT;
            _phAnim = _ph.GetComponentInChildren<Animator>();
            // 컷씬 동안 파라오는 대기(Idle) 동작만: 시전 중이었다면 기억해 두고 끝난 뒤 되돌림
            if (_phAnim && _phAnim.HasState(0, IDLE))
            {
                _phIdle = true; _phMode = _phAnim.updateMode; _phSpeed = _phAnim.speed;
                var _st = _phAnim.GetCurrentAnimatorStateInfo(0); _phHash = _st.shortNameHash; _phNorm = _st.normalizedTime;
                _phAnim.updateMode = AnimatorUpdateMode.UnscaledTime; _phAnim.speed = 1f; _phAnim.Play(IDLE, 0, 0f);
            }
            _phBase = _ph.position;
            _fwd = _ph.forward; _fwd.y = 0f; if (_fwd.sqrMagnitude < 0.01f) _fwd = Vector3.right; _fwd.Normalize();
            _right = Vector3.Cross(Vector3.up, _fwd);
            _s1 = _cam.transform.position; _s1Rot = _cam.transform.rotation; _fov0 = _cam.fieldOfView;
            _s1L = _s1 + _s1Rot * Vector3.forward * 12f;
            _s3 = P(6.92f, 1.2f, -1.38f); _s3L = P(-0.03f, 2.6f, 0f);
            _s4 = P(5.62f, -0.7f, 2.42f); _s4L = P(-0.03f, 3.2f, -0.58f);
            _s3e = _s3 + _fwd * -0.8f + Vector3.up * 0.1f + _right * 0.2f;
            _s4e = _s4 + _fwd * -0.5f + Vector3.up * 0.2f + _right * -0.3f;
            var _rs = _ph.GetComponentsInChildren<Renderer>();
            if (_rs.Length > 0) { var _bb = _rs[0].bounds; foreach (var _r in _rs) _bb.Encapsulate(_r.bounds); _eyeHeight = _bb.max.y - _phBase.y - _bb.size.y * 0.1f; }
            else _eyeHeight = 3f;

            // 조명·독기
            _green = NewLight("Cutscene_Green", GREEN, 12f);
            _eye = NewLight("Cutscene_Eye", new Color(0.4f, 1f, 0.8f), 2.2f);
            _smoke = NewSmoke(false); _sparks = NewSmoke(true);

            BuildUI();
            HideHud();

            var _clip = Resources.Load<AudioClip>(RES + "BossIntro_Mix");
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = _clip; _audio.playOnAwake = false; _audio.spatialBlend = 0f; _audio.ignoreListenerPause = true;
            _audio.volume = Volume();
            return true;
        }

        // 파라오 기준 오프셋(앞 f, 위 u, 오른쪽 r) → 월드 좌표
        private Vector3 P(float _f, float _u, float _r) { return _phBase + _fwd * _f + Vector3.up * _u + _right * _r; }

        private void Apply(float f)
        {
            // ---- 카메라 ----
            Vector3 _pos, _look; float _fov; Quaternion? _rotOverride = null;
            if (f < 36f) { _pos = _s1; _look = _s1L; _fov = _fov0; _rotOverride = _s1Rot; }
            else if (f < 132f)
            {
                float e = E(S(f, 36, 131));
                Vector3 _c1 = Vector3.Lerp(_s1, _s3, 0.35f); _c1.y = Mathf.Lerp(_s1.y, _s3.y, 0.2f) - 0.4f;
                Vector3 _c2 = _s3 + _fwd * 2f + _right * -0.6f + Vector3.up * -1f;
                _pos = Bez(_s1, _c1, _c2, _s3, e); _look = Vector3.Lerp(_s1L, _s3L, E(S(f, 36, 120))); _fov = Mathf.Lerp(_fov0, 38f, e);
            }
            else if (f < 216f) { float e = E(S(f, 132, 215)); _pos = Vector3.Lerp(_s3, _s3e, e); _look = _s3L + Vector3.up * (0.4f * E(S(f, 132, 200))); _fov = 38f; }
            else if (f < 312f) { float e = E(S(f, 216, 311)); _pos = Vector3.Lerp(_s4, _s4e, e); _look = _s4L; _fov = 42f; }
            else if (f < 384f)
            {
                float e = E(S(f, 312, 383));
                _pos = Bez(_s4e, _s4e + _fwd * 3f + Vector3.up * 4f + _right * -1f, _s1 + (_s1 - _phBase).normalized * -4f + Vector3.up * 3f, _s1, e);
                _look = Vector3.Lerp(_s4L, _s1L, E(S(f, 312, 372))); _fov = Mathf.Lerp(42f, _fov0, e);
                if (f > 372f) _rotOverride = Quaternion.Slerp(Quaternion.LookRotation(_look - _pos), _s1Rot, E(S(f, 372, 383)));
            }
            else { _pos = _s1; _look = _s1L; _fov = _fov0; _rotOverride = _s1Rot; }
            _cam.transform.position = _pos;
            _cam.transform.rotation = _rotOverride ?? Quaternion.LookRotation(_look - _pos, Vector3.up);
            _cam.fieldOfView = _fov;

            // ---- 파라오: 잠듦 → 떠오름 ----
            float _rise = E(S(f, 132, 200));
            _ph.position = _phBase + Vector3.up * Mathf.Lerp(-0.8f, 0f, _rise);   // 천천히 떠오르기만 함(떠 있는 흔들림은 Idle 동작에 있음)
            if (_phIdle && (_phAnim.IsInTransition(0) || _phAnim.GetCurrentAnimatorStateInfo(0).shortNameHash != IDLE)) _phAnim.Play(IDLE, 0);   // 다른 스크립트가 시전 동작을 켜도 Idle 유지

            // ---- 초록 조명·눈빛 ----
            float _wake = E(S(f, 140, 175));
            _green.transform.position = Vector3.Lerp(_phBase + Vector3.up * 1.6f + _fwd * -0.03f, _phBase + _fwd * 1.9f + Vector3.up * 0.8f, _wake);
            _green.intensity = Mathf.Lerp(2.5f, 9f, _wake) * (1f + 0.08f * Mathf.Sin(f * 0.9f) * _wake) * (f >= 312f ? Mathf.Lerp(1f, 0.6f, E(S(f, 312, 383))) : 1f) * (1f - E(S(f, 384, 407)));
            _green.range = Mathf.Lerp(12f, 9f, _wake);
            _eye.transform.position = _ph.position + Vector3.up * _eyeHeight + _fwd * 0.35f;
            _eye.intensity = 3f * E(S(f, 150, 166)) * (1f + 0.1f * Mathf.Sin(f * 0.7f)) * (1f - E(S(f, 384, 407)));

            // ---- 독기: 깨어나며 터지고 계속 피어오름 ----
            float _ms = E(S(f, 140, 170)) * (f < 312f ? 1f : 1f - 0.5f * E(S(f, 312, 383))) * (1f - E(S(f, 384, 400)));
            var _em = _smoke.emission; _em.rateOverTime = 26f * _ms + (f >= 150f && f < 156f ? 120f : 0f);
            var _es = _sparks.emission; _es.rateOverTime = 14f * _ms;
            _smoke.transform.position = _sparks.transform.position = _ph.position + Vector3.up * (_eyeHeight * 0.55f);

            // ---- 화면: 섬광·띠·카드·HUD ----
            _flash.color = new Color(GREEN.r, GREEN.g, GREEN.b, f >= 150f ? 0.18f * Mathf.Exp(-(f - 150f) / 6f) : 0f);
            float _k = f < 360f ? E(S(f, 0, 24)) : 1f - E(S(f, 360, 384));
            _top.sizeDelta = new Vector2(0f, LB * _k); _bot.sizeDelta = new Vector2(0f, LB * _k);
            if (_skipHint) _skipHint.alpha = 0.75f * _k;   // 아래 띠와 함께 나타나고 사라짐
            int[] _cs = { 228, 238, 254 };
            for (int i = 0; i < 3; i++) _cards[i].color = new Color(1f, 1f, 1f, E(S(f, _cs[i], _cs[i] + 14)) * (1f - E(S(f, 292, 310))));
            foreach (var _h in _hud) if (_h.g) _h.g.alpha = _h.a * (_h.boss ? E(S(f, 364, 384)) : E(S(f, 384, 407)));
        }

        private void Finish()
        {
            if (_cam && _fov0 > 0f) { _cam.transform.SetPositionAndRotation(_s1, _s1Rot); _cam.fieldOfView = _fov0; }   // _fov0가 0이면 준비 전에 실패한 것 → 되돌릴 위치 없음
            if (_ph) _ph.position = _phBase;
            if (_phAnim && _phIdle)
            {
                _phAnim.updateMode = _phMode; _phAnim.speed = _phSpeed;
                if (_phHash != IDLE) _phAnim.Play(_phHash, 0, _phNorm);   // 시전 중이었다면 그 자리부터 이어서
            }
            foreach (var _h in _hud) { if (!_h.g) continue; if (_h.added) Destroy(_h.g); else _h.g.alpha = _h.a; }
            Time.timeScale = _prevScale;
            PauseSettingsUI.InputBlocked = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            foreach (var _b in _locked) if (_b) _b.enabled = true;
            if (_green) Destroy(_green.gameObject);
            if (_eye) Destroy(_eye.gameObject);
            if (_smoke) { _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(_smoke.gameObject, 3f); }
            if (_sparks) { _sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting); Destroy(_sparks.gameObject, 3f); }
            Destroy(gameObject, _audio && _audio.isPlaying ? Mathf.Max(0f, _audio.clip.length - _audio.time) + 0.1f : 0f);   // 소리 꼬리까지 듣고 사라짐
            if (_canvasGo) Destroy(_canvasGo);
            enabled = false;
        }

        // ---------------- 만들기 ----------------
        private GameObject _canvasGo;
        private CanvasGroup _skipHint;
        private void BuildUI()
        {
            _canvasGo = new GameObject("BossIntro_UI", typeof(RectTransform));
            var _c = _canvasGo.AddComponent<Canvas>(); _c.renderMode = RenderMode.ScreenSpaceOverlay; _c.sortingOrder = 60;
            var _sc = _canvasGo.AddComponent<CanvasScaler>(); _sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _sc.referenceResolution = new Vector2(1920f, 1080f); _sc.matchWidthOrHeight = 0.5f;
            var _root = (RectTransform)_canvasGo.transform;

            _flash = NewGraphic<Image>("Flash", _root); Stretch(_flash.rectTransform); _flash.color = Color.clear;
            _top = Bar("Top", _root, 1f); _bot = Bar("Bottom", _root, 0f);

            // 아래 검은 띠 오른쪽: 건너뛰기 안내
            var _hint = new GameObject("SkipHint", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var _hr = _hint.rectTransform; _hr.SetParent(_bot, false);
            _hr.anchorMin = _hr.anchorMax = new Vector2(1f, 0.5f); _hr.pivot = new Vector2(1f, 0.5f);
            _hr.anchoredPosition = new Vector2(-48f, 0f); _hr.sizeDelta = new Vector2(400f, 40f);
            _hint.text = GameLanguage.T("건너뛰기 (Esc)", "Skip (Esc)");
            var _ko = GameLanguage.IsKorean ? PauseSettingsUI.KoreanFont() : null;
            var _font = _ko ? _ko : Resources.Load<TMP_FontAsset>("UI/Settings/Font/EBGaramond_BoldItalic_SDF");
            if (_font) _hint.font = _font;
            _hint.fontSize = _ko ? 22f : 26f; _hint.alignment = TextAlignmentOptions.MidlineRight;
            _hint.color = new Color(0.9f, 0.86f, 0.75f); _hint.raycastTarget = false; _hint.textWrappingMode = TextWrappingModes.NoWrap;
            _skipHint = _hint.gameObject.AddComponent<CanvasGroup>(); _skipHint.alpha = 0f;

            string[] _n = { "Card_Epithet", "Card_Name", "Card_Sub" };
            _cards = new RawImage[3];
            for (int i = 0; i < 3; i++)
            {
                var _r = NewGraphic<RawImage>(_n[i], _root); _r.texture = LoadLang(RES + _n[i]);
                var _rt = _r.rectTransform; _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 1f); _rt.pivot = new Vector2(0f, 1f);
                _rt.anchoredPosition = new Vector2(CARD_RECT.x, -CARD_RECT.y); _rt.sizeDelta = CARD_RECT.size;   // 화면 비율이 16:9가 아니면 왼쪽 위 기준
                _r.color = new Color(1f, 1f, 1f, 0f);
                _cards[i] = _r;
            }
        }

        private RectTransform Bar(string _name, RectTransform _parent, float _y)
        {
            var _img = NewGraphic<Image>(_name, _parent); _img.color = Color.black;
            var _rt = _img.rectTransform; _rt.anchorMin = new Vector2(0f, _y); _rt.anchorMax = new Vector2(1f, _y); _rt.pivot = new Vector2(0.5f, _y);
            _rt.anchoredPosition = Vector2.zero; _rt.sizeDelta = Vector2.zero;
            return _rt;
        }

        private void HideHud()
        {
            var _boss = FindFirstObjectByType<BossPurifyBarUI>();
            foreach (var _c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!_c.isRootCanvas || _c.renderMode == RenderMode.WorldSpace || _c.gameObject == _canvasGo) continue;
                if (_c.GetComponentInParent<BookToGameTransition>() || _c.GetComponent<PauseSettingsUI>()) continue;
                var _g = _c.GetComponent<CanvasGroup>(); bool _added = false;
                if (!_g) { _g = _c.gameObject.AddComponent<CanvasGroup>(); _added = true; }
                _hud.Add((_g, _g.alpha, _added, _boss && _c.gameObject == _boss.gameObject));
                _g.alpha = 0f;
            }
        }

        private Light NewLight(string _name, Color _col, float _range)
        {
            var _l = new GameObject(_name).AddComponent<Light>(); _l.type = LightType.Point; _l.color = _col; _l.range = _range; _l.intensity = 0f;
            _l.shadows = LightShadows.None; return _l;
        }

        private ParticleSystem NewSmoke(bool _sparksMode)
        {
            var _go = new GameObject(_sparksMode ? "Cutscene_Sparks" : "Cutscene_Miasma");
            var _ps = _go.AddComponent<ParticleSystem>(); _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var _m = _ps.main; _m.useUnscaledTime = true; _m.loop = true; _m.playOnAwake = false; _m.simulationSpace = ParticleSystemSimulationSpace.World;
            _m.startLifetime = _sparksMode ? new ParticleSystem.MinMaxCurve(0.8f, 1.6f) : new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            _m.startSpeed = _sparksMode ? new ParticleSystem.MinMaxCurve(0.6f, 1.6f) : new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            _m.startSize = _sparksMode ? new ParticleSystem.MinMaxCurve(0.04f, 0.1f) : new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            _m.startColor = _sparksMode ? new Color(0.5f, 1f, 0.5f, 1f) : new Color(0.05f, 0.22f, 0.08f, 0.55f);
            _m.maxParticles = 400; _m.gravityModifier = 0f;
            var _sh = _ps.shape; _sh.shapeType = ParticleSystemShapeType.Sphere; _sh.radius = _sparksMode ? 0.9f : 1.1f;
            var _v = _ps.velocityOverLifetime; _v.enabled = true; _v.space = ParticleSystemSimulationSpace.World;
            _v.x = new ParticleSystem.MinMaxCurve(0f); _v.z = new ParticleSystem.MinMaxCurve(0f); _v.y = new ParticleSystem.MinMaxCurve(_sparksMode ? 1.2f : 0.6f);
            var _col = _ps.colorOverLifetime; _col.enabled = true;
            var _gr = new Gradient();
            _gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            _col.color = _gr;
            var _sz = _ps.sizeOverLifetime; _sz.enabled = true; _sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var _r = _go.GetComponent<ParticleSystemRenderer>(); _r.material = ParticleMat(_sparksMode); _r.renderMode = ParticleSystemRenderMode.Billboard;
            var _em = _ps.emission; _em.rateOverTime = 0f;
            _ps.Play();
            return _ps;
        }

        private static Texture2D s_soft;
        private static Material ParticleMat(bool _additive)
        {
            if (!s_soft)
            {
                const int N = 64; s_soft = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var _px = new Color[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float _d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f - 0.5f, N / 2f - 0.5f)) / (N / 2f);
                    float _a = Mathf.Clamp01(1f - _d); _a *= _a; _px[y * N + x] = new Color(1f, 1f, 1f, _a);
                }
                s_soft.SetPixels(_px); s_soft.Apply();
            }
            var _sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!_sh) _sh = Shader.Find("Sprites/Default");
            var _m = new Material(_sh);
            _m.SetTexture("_BaseMap", s_soft); _m.SetTexture("_MainTex", s_soft);
            _m.SetColor("_BaseColor", Color.white);
            _m.SetFloat("_Surface", 1f); _m.SetFloat("_Blend", _additive ? 2f : 0f);
            _m.SetOverrideTag("RenderType", "Transparent");
            _m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); _m.SetFloat("_DstBlend", (float)(_additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            _m.SetFloat("_ZWrite", 0f);
            _m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _m.renderQueue = (int)RenderQueue.Transparent;
            return _m;
        }

        private static T NewGraphic<T>(string _name, RectTransform _parent) where T : Graphic
        {
            var _g = new GameObject(_name, typeof(RectTransform)).AddComponent<T>();
            _g.rectTransform.SetParent(_parent, false); _g.raycastTarget = false; return _g;
        }
        private static void Stretch(RectTransform _r) { _r.anchorMin = Vector2.zero; _r.anchorMax = Vector2.one; _r.offsetMin = _r.offsetMax = Vector2.zero; }
        internal static float Volume()
        {
            var _sm = SoundManager.Instance;
            if (_sm) return _sm.MasterVolume * _sm.BgmVolume;
            return PlayerPrefs.GetFloat("HC_Master", 100f) / 100f * PlayerPrefs.GetFloat("HC_Music", 50f) / 100f;
        }
        private static float E(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float S(float f, float a, float b) { return Mathf.Clamp01((f - a) / (b - a)); }
        private static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) { float u = 1f - t; return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d; }
    }
}
