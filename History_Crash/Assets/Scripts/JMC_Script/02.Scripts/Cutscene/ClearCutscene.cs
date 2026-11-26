using System.Collections;
using System.Collections.Generic;
using HistoryCrash.Boss;
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
    /// 게임 클리어 연출(샘플 '클리어연출_샘플2_v4'를 게임에서 재생, 24fps 기준 484프레임 ≈ 20초).
    ///   0–53    지금 카메라 → 고정 구도(피라미드 정면), 플레이어가 피라미드를 봄, 위아래 검은 띠
    ///   54–213  피라미드에서 황금 광명이 퍼지며 지나간 자리가 원래 신전(Temple_defalt)으로 복원(실제 조명 + 두 화면 합성)
    ///   214–293 세계가 흐려지고 도감이 떠올라 펼쳐지며 줌인
    ///   294–    왼쪽 파라오 정화 삽화(플립북), 오른쪽 "Ⅴ. 제자리로" 나레이션, 정화까지 걸린 시간
    ///   478–    Main Menu / Retry 선택지(마우스·좌우 방향키·A/D·Enter). Retry는 보스 소개를 건너뜀
    /// 시작: 팀 GameManager가 Clear가 되면(Game), GameManager가 없으면 JMC PharaohHealth가 0이 되면(TEST_JMC).
    /// 신전 복원은 타락 쪽/복원 쪽을 레이어로 나눠 카메라 두 대로 그리고 ClearRestore 셰이더가 합친다. 씬·팀 스크립트는 고치지 않는다.
    /// </summary>
    public class ClearCutscene : MonoBehaviour
    {
        /// <summary>false면 재생하지 않음(테스트용). 그때는 팀 클리어 화면이 그대로 보임</summary>
        public static bool Enabled = true;

        private const string RES = "Cutscene/Clear/";
        private const string MAIN_MENU = "MainMenu", MAIN_MENU_PATH = "Assets/Scenes/MainMenu.unity";
        private const float FPS = 24f, PRE = 54f, LB = 70f, MENU_IN = 424f, MENU_READY = 436f;   // MENU는 광명 시작(54) 기준
        private const float K0 = 1.3f, K1 = 1700f / 739f;                                          // 펼친 책 크기(도감 펼침면과 맞물리는 배율)
        private static readonly Vector3 CAM_POS = new Vector3(30f, 9f, 18.8f), CAM_LOOK = new Vector3(-6f, 6.5f, 18.8f);
        private static readonly Vector3 LIGHT_POS = new Vector3(-0.5f, 7.5f, 18.8f), CORE_POS = new Vector3(-0.5f, 7.95f, 18.8f);
        private static readonly Color TEXT_OFF = new Color(0.55f, 0.46f, 0.33f), TEXT_ON = new Color(1f, 0.85f, 0.52f);
        private static readonly int IDLE = Animator.StringToHash("Idle_Robe");
        private static readonly HashSet<string> LOCK = new HashSet<string>
            { "PlayerLook", "PauseInputHandler", "ThirdPersonCamera", "CinemachineBrain", "PlayerInput", "PlayerMover", "PlayerAnimator", "PlayerController" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene _s, LoadSceneMode _m)
        {
            if (_m != LoadSceneMode.Single) return;
            foreach (var _c in FindObjectsByType<ClearCutscene>(FindObjectsSortMode.None)) _c.Restore();   // 새 씬의 Start보다 먼저 시간·입력을 되돌림
            AutoCreate();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!Enabled) return;
            if (System.Array.IndexOf(BossIntroCutscene.SCENES, SceneManager.GetActiveScene().name) < 0) return;
            foreach (var _c in FindObjectsByType<ClearCutscene>(FindObjectsSortMode.None)) if (!_c._leaving) return;
            new GameObject("ClearCutscene").AddComponent<ClearCutscene>();
        }

        private bool _started, _leaving, _restored; private float _t0, _out = 1f;
        private PharaohHealth _jmcBoss;
        private ResourceRequest _templeReq;
        private Transform _player; private Rigidbody _playerRb; private float _yaw0; private Camera _cam;
        private Vector3 _p0, _c1, _c2, _look0; private float _fov0;
        private readonly List<Behaviour> _locked = new List<Behaviour>();
        private readonly List<(CanvasGroup g, float a, bool ray, bool inter)> _hud = new List<(CanvasGroup, float, bool, bool)>();
        // 신전 복원
        private Camera _ca, _cb; private RenderTexture _ra, _rb; private GameObject _temple; private Light _gold, _sun; private float _sun0; private Color _sunCol0;
        private readonly List<(Light l, float i)> _impeLights = new List<(Light, float)>(); private bool _views, _movedPlayer;
        private Material _mat;
        // 화면
        private GameObject _canvasGo; private RawImage _world, _book, _purify; private RectTransform _top, _bot, _bookRt, _spread; private CanvasGroup _spreadG, _menu;
        private RawImage _full, _wash, _glow, _sub, _title; private Material _mWash, _mGlow, _mPic; private RawImage[] _linesTx; private CanvasGroup _timeG; private Image _black;
        private Texture[] _frames; private Texture2D[] _flip;
        private TextMeshProUGUI[] _items; private Image[] _under; private int _sel; private Vector2 _lastMouse;
        private AudioSource _mix; private bool _bgmBack; private float _elapsed;

        private bool Cleared()
        {
            var _gm = GameManager.Instance;
            if (_gm) return _gm.CurrentState.CurrentValue == GameStateType.Clear;
            if (!_jmcBoss) _jmcBoss = FindFirstObjectByType<PharaohHealth>();
            return _jmcBoss && _jmcBoss.IsDead;
        }

        private void Start() { _templeReq = Resources.LoadAsync<GameObject>(RES + "Clear_Temple"); }   // 클리어 순간 끊기지 않게 미리 읽어 둠

        private void Update()
        {
            if (_started || !Cleared()) return;
            _started = true;
            StartCoroutine(Run());
        }

        private float Frame => (Time.unscaledTime - _t0) * FPS;

        private IEnumerator Run()
        {
            PauseSettingsUI.InputBlocked = true;
            bool _ok = true;
            try { Setup(); } catch (System.Exception _e) { Debug.LogException(_e, this); _ok = false; }
            if (!_ok) { Restore(); if (_canvasGo) Destroy(_canvasGo); yield break; }   // 실패하면 팀 클리어 화면이 그대로 보이게
            yield return null;   // 신전을 만드느라 길어진 프레임은 세지 않음
            _t0 = Time.unscaledTime;
            if (_mix.clip) _mix.Play();
            while (!_leaving)
            {
                try { Apply(Frame); }
                catch (System.Exception _e) { Debug.LogException(_e, this); Restore(); if (_canvasGo) Destroy(_canvasGo); if (_mix) _mix.Stop(); yield break; }
                if (Frame - PRE >= MENU_READY) Menu();
                yield return null;
            }
        }

        // ---------------- 준비 ----------------
        private void Setup()
        {
            _cam = Camera.main;
            var _gm = GameManager.Instance;
            _elapsed = _gm ? _gm.ElapsedTime : Time.timeSinceLevelLoad;
            if (_gm && _gm.PlayerHealth is Component _pc && _pc) _player = _pc.transform;
            else { var _ph = FindFirstObjectByType<PlayerHealth>(); if (_ph) _player = _ph.transform; }

            Time.timeScale = 0f;
            foreach (var _b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                if (_b && _b.enabled && LOCK.Contains(_b.GetType().Name)) { _b.enabled = false; _locked.Add(_b); }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;

            // 멈춘 시간에도 플레이어·파라오·횃불은 움직이게
            if (_player)
            {
                _yaw0 = _player.eulerAngles.y; _playerRb = _player.GetComponent<Rigidbody>();
                var _an = _player.GetComponentInChildren<Animator>();
                if (_an) { _an.updateMode = AnimatorUpdateMode.UnscaledTime; if (_an.HasState(0, IDLE)) _an.CrossFadeInFixedTime(IDLE, 0.25f, 0); }
            }
            var _boss = BossIntroCutscene.FindPharaoh();
            if (_boss) { var _an = _boss.GetComponentInChildren<Animator>(); if (_an) _an.updateMode = AnimatorUpdateMode.UnscaledTime; }
            foreach (var _ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) { var _m = _ps.main; _m.useUnscaledTime = true; }

            HideHud();
            SetupCamera();
            SetupTemple();
            BuildUI();
            StageBgm.FadeOut(1f);
            HistoryCrash.Sound.GameSfx.StopAll(0.3f);
            var _patterns = FindFirstObjectByType<HistoryCrash.Boss.BossPatternDirector>(); if (_patterns) _patterns.HideTelegraphs();   // 공격 예고가 멈춘 채 남지 않게
            foreach (var _a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (_a.GetComponent<BossIntroCutscene>()) _a.Stop();
            _mix = gameObject.AddComponent<AudioSource>(); _mix.clip = Resources.Load<AudioClip>(RES + "Clear_Mix");
            _mix.playOnAwake = false; _mix.spatialBlend = 0f; _mix.ignoreListenerPause = true; _mix.volume = BossIntroCutscene.Volume();
        }

        // 지금 카메라 → 고정 구도 곡선. 기둥·석상을 뚫지 않게 후보 곡선 중 물체와 가장 먼 것을 고름
        private void SetupCamera()
        {
            _p0 = _cam.transform.position; _fov0 = _cam.fieldOfView; _look0 = _p0 + _cam.transform.forward * 8f;
            var _bounds = new List<Bounds>();
            foreach (var _r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!_r.enabled || _r.bounds.size.magnitude > 30f || (_player && _r.transform.IsChildOf(_player))) continue;
                _bounds.Add(_r.bounds);
            }
            float _best = -1f;
            foreach (float _h in new[] { 5f, 8f, 12f, 16f })
                foreach (float _sx in new[] { 2f, 8f })
                {
                    Vector3 _a = _p0 + new Vector3(_sx, _h, 0f), _b = CAM_POS + new Vector3(-6f, Mathf.Max(2f, _h - 4f), 0f); float _min = 99f;
                    for (int i = 2; i <= 30 && _min > _best; i++)
                    {
                        Vector3 _q = Bez(_p0, _a, _b, CAM_POS, i / 30f);
                        foreach (var _bd in _bounds) { float _d = _bd.SqrDistance(_q); if (_d < _min) _min = _d; }
                    }
                    if (_min > _best + 0.02f) { _best = _min; _c1 = _a; _c2 = _b; }
                }
        }

        // 타락 쪽·복원 쪽을 안 쓰는 레이어 두 개로 나눔: 카메라 A = 타락(지금 씬), 카메라 B = 복원(Temple_defalt)
        private void SetupTemple()
        {
            int _li = -1, _ld = -1;
            for (int i = 31; i >= 8; i--) if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) { if (_li < 0) _li = i; else { _ld = i; break; } }
            if (_ld < 0) throw new System.InvalidOperationException("[ClearCutscene] 빈 레이어가 없음");
            foreach (var _r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (!(_player && _r.transform.IsChildOf(_player))) _r.gameObject.layer = _li;
            foreach (var _l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (_l.type == LightType.Directional) { if (!_sun || _l.intensity > _sun.intensity) _sun = _l; }
                else if (_l.enabled && !(_player && _l.transform.IsChildOf(_player))) _impeLights.Add((_l, _l.intensity));
            }
            if (_sun) { _sun0 = _sun.intensity; _sunCol0 = _sun.color; }

            var _prefab = _templeReq != null && _templeReq.asset ? (GameObject)_templeReq.asset : Resources.Load<GameObject>(RES + "Clear_Temple");
            var _impe = GameObject.Find("Temple_impe");
            _temple = Instantiate(_prefab);
            if (_impe) _temple.transform.position += _impe.transform.position;
            foreach (var _t in _temple.GetComponentsInChildren<Transform>(true)) _t.gameObject.layer = _ld;

            _gold = new GameObject("Clear_GoldLight").AddComponent<Light>(); _gold.type = LightType.Point; _gold.shadows = LightShadows.Soft;
            _gold.color = new Color(1f, 0.78f, 0.45f); _gold.transform.position = LIGHT_POS; _gold.intensity = 0f; _gold.range = 12f;

            int _mask = _cam.cullingMask | (1 << _li);
            _cam.cullingMask = _mask & ~(1 << _ld);
            _ra = NewRT(); _rb = NewRT();
            _ca = NewCam("Clear_CamA", _ra, _mask & ~(1 << _ld));
            _cb = NewCam("Clear_CamB", _rb, (_mask | (1 << _ld)) & ~(1 << _li));

            _cb.clearFlags = CameraClearFlags.SolidColor; _cb.backgroundColor = new Color(0.015f, 0.015f, 0.025f);   // 복원 쪽은 타락한 하늘(초록 무늬) 대신 샘플과 같은 밤하늘

            _mat = new Material(Resources.Load<Shader>(RES + "Clear_Restore"));
            _mat.SetTexture("_A", _ra); _mat.SetTexture("_B", _rb);
        }

        private static RenderTexture NewRT()
        {
            var _r = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            _r.Create(); return _r;
        }

        private Camera NewCam(string _n, RenderTexture _rt, int _mask)
        {
            var _c = new GameObject(_n).AddComponent<Camera>(); _c.CopyFrom(_cam); _c.targetTexture = _rt; _c.cullingMask = _mask; _c.enabled = false; _c.depth = _cam.depth - 1;
            var _src = _cam.GetUniversalAdditionalCameraData(); var _d = _c.GetUniversalAdditionalCameraData();
            _d.renderPostProcessing = _src.renderPostProcessing; _d.volumeLayerMask = _src.volumeLayerMask; _d.renderShadows = true;
            _d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; _d.antialiasingQuality = AntialiasingQuality.High;
            return _c;
        }

        // 카메라 두 대로 그리기 시작/끝. 본 카메라는 아무것도 안 그리게만(화면은 UI가 덮음)
        private int _camMask;
        private void SetViews(bool _on)
        {
            if (_views == _on) return; _views = _on;
            _ca.enabled = _cb.enabled = _on;
            if (_on) { _camMask = _cam.cullingMask; _cam.cullingMask = 0; _world.enabled = true; ShowPlayer(); }
        }

        // 고정 구도에서 플레이어가 화면 밖이면(눈에 안 보이는 동안) 가운데 통로로 옮겨 함께 보이게
        private void ShowPlayer()
        {
            if (!_player || _movedPlayer) return; _movedPlayer = true;
            Vector3 _v = _cam.WorldToViewportPoint(_player.position + Vector3.up);
            if (_v.z > 0f && _v.x > 0.02f && _v.x < 0.98f && _v.y > 0.08f && _v.y < 0.95f) return;
            Vector3 _p = new Vector3(10f, _player.position.y, 18.8f);
            _player.position = _p; if (_playerRb) _playerRb.position = _p;
        }

        // ---------------- 매 프레임 ----------------
        private void Apply(float f)
        {
            float o = f - PRE;   // 광명 시작 기준
            // ---- 카메라·플레이어 ----
            float e = E(S(f, 4, 50));
            Vector3 _pos = Bez(_p0, _c1, _c2, CAM_POS, e); Vector3 _look = Vector3.Lerp(_look0, CAM_LOOK, E(S(f, 4, 48)));
            _cam.transform.SetPositionAndRotation(_pos, Quaternion.LookRotation(_look - _pos, Vector3.up));
            _cam.fieldOfView = Mathf.Lerp(_fov0, 50f, e);
            if (_player && f < 60f)
            {
                var _rot = Quaternion.Euler(0f, Mathf.LerpAngle(_yaw0, -90f, E(S(f, 8, 46))), 0f);
                _player.rotation = _rot; if (_playerRb) _playerRb.rotation = _rot;
            }

            // ---- 조명 ----
            float _grow = E(S(o, 8, 125)), _dawn = E(S(o, 95, 160));
            _gold.intensity = Mathf.Lerp(3f + 75f * _grow, 10f, E(S(o, 120, 160))) * E(S(f, 20, 54)); _gold.range = 12f + 80f * _grow;
            float _off = 1f - E(S(f, 20, 50));   // 파라오의 공격 빛·호루스 눈빛이 꺼짐(복원 쪽에 비치지 않게). 다른 스크립트가 세기를 되돌려도 남지 않게 다 꺼지면 조명 자체를 끔
            foreach (var _l in _impeLights) if (_l.l) { _l.l.intensity = _l.i * _off; if (_off <= 0f) _l.l.enabled = false; }
            if (_sun) { _sun.intensity = Mathf.Lerp(_sun0, 1f, _dawn); _sun.color = Color.Lerp(_sunCol0, new Color(1f, 0.9f, 0.75f), _dawn); }

            // ---- 신전 복원 ----
            if (o >= -1f && o < 170f) SetViews(true);
            if (_views)
            {
                foreach (var _c in new[] { _ca, _cb }) { _c.transform.SetPositionAndRotation(_cam.transform.position, _cam.transform.rotation); _c.fieldOfView = _cam.fieldOfView; }
                Vector3 _vp = _cam.WorldToViewportPoint(CORE_POS);
                _mat.SetVector("_Center", _vp); _mat.SetFloat("_Aspect", _cam.aspect);
                _mat.SetFloat("_R", E(S(o, 14, 130)) * 1750f); _mat.SetFloat("_Ck", E(S(o, 0, 30)) * (1f - 0.8f * E(S(o, 118, 159))));
                _mat.SetFloat("_T", f / FPS); _mat.SetFloat("_Dim", E(S(o, 160, 182))); _mat.SetFloat("_Fx", S(o, 0, 12));
                if (o >= 170f && o < 999f) SetViewsFrozen();
            }

            // ---- 띠 ----
            float _k = f < PRE ? E(S(f, 0, 30)) : 1f - E(S(o, 160, 182));
            _top.sizeDelta = _bot.sizeDelta = new Vector2(0f, LB * _k);

            // ---- 책 ----
            _book.enabled = o >= 161f;
            if (o >= 161f)
            {
                int i = o < 184f ? 0 : Mathf.Clamp(Mathf.RoundToInt(S(o, 184, 215) * 23f), 0, 23);
                float _dy = (1f - E(S(o, 162, 184))) * 950f, z = E(S(o, 216, 240)), k = Mathf.Lerp(K0, K1, z);
                _book.texture = _frames[i]; _bookRt.sizeDelta = new Vector2(960f, 560f) * k; _bookRt.anchoredPosition = new Vector2(0.5f * k, 6f * k - _dy);
                _spreadG.alpha = E(S(o, 222, 240)); _spread.localScale = Vector3.one * (739f * k / 1700f);
                Alpha(_full, z);
                float fi = S(o, 240, 252);
                int g = Mathf.Clamp(Mathf.FloorToInt(o - 252f), 0, 119);
                _purify.texture = _flip[g / 48]; int n = g % 48;
                _mPic.SetVector("_UV", new Vector4((n % 8) / 8f, 1f - (n / 8 + 1) / 6f, 1f / 8f, 1f / 6f));
                _mGlow.SetVector("_UV", new Vector4((g % 11) / 11f, 1f - (g / 11 + 1) / 11f, 1f / 11f, 1f / 11f));
                Alpha(_purify, fi); Alpha(_wash, fi); Alpha(_glow, fi);
                Alpha(_sub, S(o, 226, 238)); Alpha(_title, S(o, 242, 254));
                Alpha(_linesTx[0], S(o, 268, 280)); Alpha(_linesTx[1], S(o, 308, 320)); Alpha(_linesTx[2], S(o, 350, 362));
                _timeG.alpha = S(o, 384, 396);
            }

            // ---- 선택지·소리 ----
            _menu.alpha = E(S(o, MENU_IN, MENU_IN + 18f));
            _mix.volume = BossIntroCutscene.Volume() * _out;
            if (!_bgmBack && o >= MENU_IN) { _bgmBack = true; StageBgm.FadeIn(3f); }
        }

        // 책이 덮기 시작하면 두 카메라는 마지막 화면에서 멈춤(흐려진 배경으로만 보이므로)
        private void SetViewsFrozen() { if (_ca.enabled) { _ca.enabled = false; _cb.enabled = false; } }

        private void Menu()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            var _kb = Keyboard.current; var _ms = Mouse.current;
            if (_ms != null)
            {
                Vector2 _p = _ms.position.ReadValue(); int _hit = -1;
                for (int i = 0; i < _items.Length; i++) if (RectTransformUtility.RectangleContainsScreenPoint(_items[i].rectTransform, _p, null)) _hit = i;
                if (_hit >= 0 && (_p - _lastMouse).sqrMagnitude > 1f) _sel = _hit;
                _lastMouse = _p;
                if (_hit >= 0 && _ms.leftButton.wasPressedThisFrame) { Choose(_hit); return; }
            }
            if (_kb != null)
            {
                if (_kb.leftArrowKey.wasPressedThisFrame || _kb.aKey.wasPressedThisFrame) _sel = 0;
                if (_kb.rightArrowKey.wasPressedThisFrame || _kb.dKey.wasPressedThisFrame) _sel = 1;
                if (_kb.enterKey.wasPressedThisFrame || _kb.numpadEnterKey.wasPressedThisFrame) { Choose(_sel); return; }
            }
            float _k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < _items.Length; i++)
            {
                bool _on = i == _sel;
                _items[i].color = Color.Lerp(_items[i].color, _on ? TEXT_ON : TEXT_OFF, _k);
                var _c = _under[i].color; _c.a = Mathf.Lerp(_c.a, _on ? 0.7f : 0f, _k); _under[i].color = _c;
            }
        }

        // 0 = Main Menu, 1 = Retry
        private void Choose(int _i) { if (_leaving) return; _sel = _i; _leaving = true; StartCoroutine(Leave(_i == 1)); }

        private IEnumerator Leave(bool _retry)
        {
            StageBgm.FadeOut(0.6f);
            float _t = Time.unscaledTime;
            while (_out > 0f)
            {
                _out = 1f - Mathf.Clamp01((Time.unscaledTime - _t) / 0.7f);
                Apply(Frame); _black.color = new Color(0f, 0f, 0f, 1f - _out); yield return null;
            }
            _mix.Stop(); Cursor.visible = false;

            DontDestroyOnLoad(gameObject);   // 검은 화면을 새 씬까지 이어 갔다가 걷음
            var _scene = SceneManager.GetActiveScene();
            string _name = _retry ? _scene.name : MAIN_MENU, _path = _retry ? _scene.path : MAIN_MENU_PATH;
            BossIntroCutscene.SkipNext = _retry;
            AsyncOperation _op = null;
            if (Application.CanStreamedLevelBeLoaded(_name)) _op = SceneManager.LoadSceneAsync(_name);
#if UNITY_EDITOR
            else _op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(_path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            if (_op == null) { Debug.LogError("[ClearCutscene] 씬을 열 수 없음: " + _name); BossIntroCutscene.SkipNext = false; Destroy(gameObject); yield break; }
            while (!_op.isDone) yield return null;
            foreach (Transform _c in _canvasGo.transform) if (_c != _black.transform) Destroy(_c.gameObject);
            yield return null;
            _t = Time.unscaledTime; float _a = 1f;
            while (_a > 0f) { _a = 1f - Mathf.Clamp01((Time.unscaledTime - _t) / 0.6f); _black.color = new Color(0f, 0f, 0f, _a); yield return null; }
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
            if (_views && _cam) _cam.cullingMask = _camMask;
            if (_ca) Destroy(_ca.gameObject);
            if (_cb) Destroy(_cb.gameObject);
            if (_temple) Destroy(_temple);
            if (_gold) Destroy(_gold.gameObject);
            if (_sun) { _sun.intensity = _sun0; _sun.color = _sunCol0; }
        }

        private void OnDestroy()
        {
            if (_started && !_restored) { Time.timeScale = 1f; PauseSettingsUI.InputBlocked = false; }   // 플레이 종료 등
            foreach (var _m in new[] { _mat, _mWash, _mGlow, _mPic }) if (_m) Destroy(_m);
            if (_ra) { _ra.Release(); Destroy(_ra); }
            if (_rb) { _rb.Release(); Destroy(_rb); }
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

        private void BuildUI()
        {
            _canvasGo = new GameObject("Clear_UI", typeof(RectTransform));
            _canvasGo.transform.SetParent(transform, false);
            var _c = _canvasGo.AddComponent<Canvas>(); _c.renderMode = RenderMode.ScreenSpaceOverlay; _c.sortingOrder = 70;
            var _sc = _canvasGo.AddComponent<CanvasScaler>(); _sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _sc.referenceResolution = new Vector2(1920f, 1080f); _sc.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var _root = (RectTransform)_canvasGo.transform;

            _world = NewGraphic<RawImage>("World", _root); Stretch(_world.rectTransform); _world.material = _mat; _world.texture = _ra; _world.enabled = false;
            _top = Bar("Top", _root, 1f); _bot = Bar("Bottom", _root, 0f);

            _frames = new Texture[24];
            for (int i = 0; i < 24; i++) _frames[i] = Resources.Load<Texture2D>("UI/Settings/Open/UI_Settings_Open_" + i.ToString("00"));
            _book = NewGraphic<RawImage>("Book", _root); _bookRt = _book.rectTransform; Center(_bookRt); _book.enabled = false;

            // 도감 펼침면(1920x1080 좌표, 줌인과 함께 커짐)
            _spread = (RectTransform)new GameObject("Spread", typeof(RectTransform)).transform; _spread.SetParent(_root, false); Center(_spread); _spread.sizeDelta = new Vector2(1920f, 1080f);
            _spreadG = _spread.gameObject.AddComponent<CanvasGroup>(); _spreadG.alpha = 0f;
            Tex("SpreadMask", _spread, "Clear_SpreadMask", 0, 0, 1920, 1080);
            _full = Tex("SpreadFull", _spread, "Clear_Spread", 0, 0, 1920, 1080);
            // 삽화 칸 480x600(인트로와 같은 자리) + 바깥 효과: 먹 번짐 → 종이로 번지는 빛 → 붓자국 가장자리 그림
            var _ish = Resources.Load<Shader>(RES + "Clear_Illust"); var _mask = Resources.Load<Texture2D>(RES + "Clear_IllMask");
            _flip = new[] { Resources.Load<Texture2D>(RES + "Clear_Purify_0"), Resources.Load<Texture2D>(RES + "Clear_Purify_1"), Resources.Load<Texture2D>(RES + "Clear_Purify_2") };
            _mWash = IllMat(_ish, 1f, BlendMode.DstColor, BlendMode.Zero); _mWash.SetTexture("_Mask", Resources.Load<Texture2D>(RES + "Clear_IllWash"));
            _mGlow = IllMat(_ish, 2f, BlendMode.OneMinusDstColor, BlendMode.One);
            _mPic = IllMat(_ish, 0f, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha); _mPic.SetTexture("_Mask", _mask);
            _wash = Tex("IllWash", _spread, null, 226, 193, 720, 840); _wash.material = _mWash;
            _glow = Tex("IllGlow", _spread, "Clear_PurifyGlow", 226, 193, 720, 840); _glow.material = _mGlow;
            _purify = Tex("Purify", _spread, null, 346, 313, 480, 600); _purify.material = _mPic; _purify.texture = _flip[0];
            Alpha(_wash, 0f); Alpha(_glow, 0f); Alpha(_purify, 0f);
            _sub = Tex("Sub", _spread, "Clear_Sub", 335, 212, 500, 60);
            _title = Tex("Title", _spread, "Clear_Title", 1085, 212, 500, 60);
            _linesTx = new[] { Tex("Line1", _spread, "Clear_Line1", 1105, 345, 480, 101), Tex("Line2", _spread, "Clear_Line2", 1105, 456, 480, 70), Tex("Line3", _spread, "Clear_Line3", 1105, 536, 480, 70) };

            // 정화까지 걸린 시간(선 + 글 + 숫자)
            var _tr = (RectTransform)new GameObject("Time", typeof(RectTransform)).transform; _tr.SetParent(_spread, false); Stretch(_tr);
            _timeG = _tr.gameObject.AddComponent<CanvasGroup>(); _timeG.alpha = 0f;
            Tex("Line", _tr, "Clear_TimeLine", 1200, 798, 270, 4);
            int _s = Mathf.FloorToInt(_elapsed); string _time = (_s / 60).ToString("00") + ":" + (_s % 60).ToString("00");
            bool _en = !HistoryCrash.UI.GameLanguage.IsKorean;   // 글자 폭(숫자·콜론·앞글 "정화까지 걸린 시간  "): 송명 / EB Garamond 실측
            float DW = _en ? 10.05f : 9.37f, CW = _en ? 5.09f : 5.7f, LW = _en ? 128.6f : 150.9f;
            float _w = LW; foreach (char ch in _time) _w += ch == ':' ? CW : DW;
            float _x = 1335f - _w / 2f;
            Tex("Label", _tr, "Clear_TimeLabel", _x - 2f, 812, 200, 40);
            _x += LW;
            foreach (char ch in _time)
            {
                float _adv = ch == ':' ? CW : DW; int _ci = ch == ':' ? 10 : ch - '0';
                var _d = Tex("D" + ch, _tr, "Clear_Digits", _x + _adv / 2f - 10f, 812, 20, 40); _d.uvRect = new Rect(_ci / 11f, 0f, 1f / 11f, 1f);
                _x += _adv;
            }

            // 선택지(책 아래 여백)
            var _mr = (RectTransform)new GameObject("Menu", typeof(RectTransform)).transform; _mr.SetParent(_root, false); Stretch(_mr);
            _menu = _mr.gameObject.AddComponent<CanvasGroup>(); _menu.alpha = 0f;
            var _font = Resources.Load<TMP_FontAsset>("UI/Settings/Font/EBGaramond_BoldItalic_SDF");
            string[] _n = { "Main Menu", "Retry" };
            _items = new TextMeshProUGUI[2]; _under = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var _t = NewGraphic<TextMeshProUGUI>(_n[i], _mr);
                if (_font) _t.font = _font;
                _t.text = _n[i]; _t.fontSize = 34f; _t.alignment = TextAlignmentOptions.Center; _t.color = i == 0 ? TEXT_ON : TEXT_OFF;
                var _r = _t.rectTransform; Center(_r); _r.sizeDelta = new Vector2(220f, 50f); _r.anchoredPosition = new Vector2(i == 0 ? -140f : 140f, -508f);
                _items[i] = _t;
                var _l = NewGraphic<Image>("Line", _r); _l.color = new Color(TEXT_ON.r, TEXT_ON.g, TEXT_ON.b, i == 0 ? 0.7f : 0f);
                var _lr = _l.rectTransform; Center(_lr); _lr.sizeDelta = new Vector2(_t.GetPreferredValues(_n[i]).x * 0.9f, 2f); _lr.anchoredPosition = new Vector2(0f, -20f);
                _under[i] = _l;
            }
            _black = NewGraphic<Image>("Black", _root); Stretch(_black.rectTransform); _black.color = Color.clear;
        }

        // 설정 언어가 영어면 이름 끝 _EN 그림(없으면 한국어 그림)
        private static Texture2D LoadLang(string _path)
        {
            if (!HistoryCrash.UI.GameLanguage.IsKorean) { var _en = Resources.Load<Texture2D>(_path + "_EN"); if (_en) return _en; }
            return Resources.Load<Texture2D>(_path);
        }

        // 도감 펼침면 위 그림: 1920x1080 기준 왼쪽 위 원점
        private static RawImage Tex(string _name, RectTransform _parent, string _tex, float x, float y, float w, float h)
        {
            var _r = NewGraphic<RawImage>(_name, _parent); if (_tex != null) _r.texture = LoadLang(RES + _tex);
            var _rt = _r.rectTransform; Center(_rt); _rt.sizeDelta = new Vector2(w, h); _rt.anchoredPosition = new Vector2(x + w / 2f - 960f, 540f - y - h / 2f);
            return _r;
        }
        private RectTransform Bar(string _name, RectTransform _parent, float _y)
        {
            var _img = NewGraphic<Image>(_name, _parent); _img.color = Color.black;
            var _rt = _img.rectTransform; _rt.anchorMin = new Vector2(0f, _y); _rt.anchorMax = new Vector2(1f, _y); _rt.pivot = new Vector2(0.5f, _y);
            _rt.anchoredPosition = Vector2.zero; _rt.sizeDelta = Vector2.zero; return _rt;
        }
        private static Material IllMat(Shader _s, float _mode, BlendMode _src, BlendMode _dst)
        {
            var _m = new Material(_s); _m.SetFloat("_Mode", _mode); _m.SetFloat("_Src", (float)_src); _m.SetFloat("_Dst", (float)_dst); return _m;
        }
        private static void Alpha(Graphic _g, float _a) { var _c = _g.color; _c.a = _a; _g.color = _c; }
        private static T NewGraphic<T>(string _name, RectTransform _parent) where T : Graphic
        {
            var _g = new GameObject(_name, typeof(RectTransform)).AddComponent<T>();
            _g.rectTransform.SetParent(_parent, false); _g.raycastTarget = false; return _g;
        }
        private static void Center(RectTransform _r) { _r.anchorMin = _r.anchorMax = _r.pivot = new Vector2(0.5f, 0.5f); }
        private static void Stretch(RectTransform _r) { _r.anchorMin = Vector2.zero; _r.anchorMax = Vector2.one; _r.offsetMin = _r.offsetMax = Vector2.zero; }
        private static float E(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float S(float f, float a, float b) { return Mathf.Clamp01((f - a) / (b - a)); }
        private static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) { float u = 1f - t; return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d; }
    }
}
