using System.Collections.Generic;
using HistoryCrash.Cutscene;
using HistoryCrash.Player;
using HistoryCrash.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoryCrash.Sound
{
    /// <summary>
    /// 게임플레이 효과음. 다른 스크립트가 GameSfx.Play("파일 이름", …)로 부르고, 파일은 Resources/Sfx 아래에서 이름으로 찾는다.
    /// - 음량: 설정창 Master × Effects × 아래 표(TUNE). 파일별 크기·들리는 방식은 표만 고치면 된다.
    /// - 설정창(timeScale 0)에서는 함께 멈췄다가 이어서 재생. 게임 오버·클리어가 시작되면 StopAll로 걷고 그 씬에서는 더 내지 않음.
    /// - 발걸음·피격·호루스·아누비스 강제 이동은 여기서 상태를 읽기만 해서 냄(팀 플레이어 스크립트는 고치지 않음).
    /// - 씬에 따로 놓지 않아도 BossIntroCutscene.SCENES 씬이 열리면 자동으로 생기고, 씬이 바뀌면 함께 사라진다.
    /// </summary>
    public class GameSfx : MonoBehaviour
    {
        /// <summary>false면 소리를 내지 않음(테스트용)</summary>
        public static bool Enabled = true;
        /// <summary>true면 소리를 낼 때마다 시각을 기록(테스트용)</summary>
        public static bool DebugLog;

        private enum Space { Flat, Half, World }   // 2D / 절반만 3D(방향만 느껴짐) / 3D(멀면 작아짐)

        // ---------- 파일별 음량·들리는 방식 ----------
        private static readonly Dictionary<string, (float vol, Space space)> TUNE = new Dictionary<string, (float, Space)>
        {
            // 파라오
            { "Pharaoh_CastA", (0.85f, Space.Half) }, { "Pharaoh_CastB", (0.70f, Space.Half) }, { "Pharaoh_CastC", (0.75f, Space.Half) },
            { "Pharaoh_CastD", (0.85f, Space.Half) }, { "Pharaoh_CastE", (0.75f, Space.Half) },
            { "Weapon_A", (0.50f, Space.Half) }, { "Weapon_B", (0.70f, Space.Half) }, { "Weapon_C", (1.00f, Space.Half) },
            { "Weapon_C_GlassBreak", (0.80f, Space.World) }, { "Weapon_D", (0.85f, Space.Half) }, { "Weapon_E", (0.70f, Space.Half) },
            { "Pharaoh_Attacked", (0.75f, Space.Half) },
            // 미라
            { "Mummy_Walk", (0.45f, Space.World) }, { "Mummy_Death", (1.00f, Space.World) },
            // 플레이어
            { "Player_Cast", (0.65f, Space.Flat) }, { "Player_AttackA", (0.75f, Space.Flat) }, { "Player_AttackB", (0.90f, Space.World) },
            { "Player_AttackC", (0.75f, Space.Flat) }, { "Player_AttackD", (0.70f, Space.Flat) }, { "Player_AttackE", (0.75f, Space.Flat) },
            { "Player_Hit_Leather", (0.75f, Space.Flat) }, { "Player_hit_voice", (0.85f, Space.Flat) }, { "Player_Walk", (0.55f, Space.Flat) },
            // 기믹
            { "Horus_Warning", (0.70f, Space.Flat) }, { "Horus_Collapse", (0.80f, Space.Flat) }, { "Horus_Restore", (0.70f, Space.Flat) },
            { "Anubis_Warning", (0.50f, Space.Half) }, { "Anubis_Pull", (0.40f, Space.Flat) }, { "Anubis_Land", (0.48f, Space.Flat) },
            // 제드 기둥(수복은 6개가 함께라 2D)
            { "Zedtop_Warning", (0.46f, Space.Half) }, { "Zedtop_Collapse", (0.40f, Space.Half) }, { "Zedtop_Evaporate", (0.31f, Space.Half) },
            { "Zedtop_Restore", (0.46f, Space.Flat) }, { "Zedtop_TierLand", (0.57f, Space.Flat) }, { "Zedtop_RestoreDone", (0.35f, Space.Flat) },
        };
        private const float STRIDE = 1.8f;        // 발걸음 간격(m): 걷기 4m/s → 0.45초, 달리기 6.4m/s → 0.28초
        private const int MAX_VOICES = 40;

        /// <summary>재생 중인 소리 하나. 나중에 걷거나(Fade) 크기를 바꿀 때(SetGain) 씀</summary>
        public class Voice
        {
            internal string Name; internal AudioSource Src; internal Transform Follow; internal bool HasFollow, Started, Paused, Exclusive;
            internal float Wait, Vol, Gain = 1f, GainTo = 1f, Fade = 1f, FadeSpeed;   // FadeSpeed > 0이면 걷는 중
            internal bool Dead;
        }

        private static GameSfx s_inst;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly Stack<AudioSource> _pool = new Stack<AudioSource>();
        private bool _muted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook() { s_inst = null; SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene _s, LoadSceneMode _m) { if (_m == LoadSceneMode.Single) AutoCreate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!Enabled || s_inst) return;
            if (System.Array.IndexOf(BossIntroCutscene.SCENES, SceneManager.GetActiveScene().name) < 0) return;
            s_inst = new GameObject("GameSfx").AddComponent<GameSfx>();
        }

        private void Awake()
        {
            foreach (var _c in Resources.LoadAll<AudioClip>("Sfx")) _clips[_c.name] = _c;   // 처음 쓸 때 끊기지 않게 한꺼번에
        }

        private void OnDestroy()
        {
            if (s_inst == this) s_inst = null;
            if (_movable != null) { _movable.OnForceMoveStarted -= OnPull; _movable.OnForceMoveEnded -= OnLand; }
            if (_motion != null) _motion.OnMoveChanged -= OnMove;
        }

        // ---------------- 재생 ----------------
        /// <summary>위치 없이(2D) 재생</summary>
        public static Voice Play(string _name, float _delay = 0f) => Play(_name, Vector3.zero, null, _delay);
        /// <summary>대상을 따라다니며 재생</summary>
        public static Voice Play(string _name, Transform _follow, float _delay = 0f, bool _loop = false, bool _exclusive = false)
            => Play(_name, _follow ? _follow.position : Vector3.zero, _follow, _delay, _loop, _exclusive);
        /// <param name="_delay">기다렸다가 시작(게임 시간, 초)</param>
        /// <param name="_loop">반복(대상이 사라지거나 꺼지면 멈춤). 시작 지점은 무작위</param>
        /// <param name="_exclusive">같은 이름의 앞 소리를 걷고 시작</param>
        /// <param name="_pitchRange">높낮이 무작위 폭(0.05 = ±5%)</param>
        public static Voice Play(string _name, Vector3 _pos, Transform _follow = null, float _delay = 0f, bool _loop = false, bool _exclusive = false, float _pitchRange = 0f)
        {
            var _i = s_inst;
            if (!_i || !Enabled || _i._muted) return null;
            if (!_i._clips.TryGetValue(_name, out var _clip)) { Debug.LogWarning("[GameSfx] 소리를 찾지 못함: " + _name); return null; }
            if (_i._voices.Count >= MAX_VOICES) return null;
            if (!TUNE.TryGetValue(_name, out var _t)) _t = (0.8f, Space.Flat);

            var _src = _i._pool.Count > 0 ? _i._pool.Pop() : _i.NewSource();
            _src.gameObject.SetActive(true);
            _src.transform.position = _pos; _src.clip = _clip; _src.loop = _loop; _src.volume = 0f;
            _src.pitch = 1f + Random.Range(-_pitchRange, _pitchRange);
            _src.time = _loop ? Random.value * _clip.length * 0.95f : 0f;
            _src.spatialBlend = _t.space == Space.Flat ? 0f : _t.space == Space.Half ? 0.5f : 1f;
            _src.minDistance = _t.space == Space.Half ? 12f : 4f; _src.maxDistance = _t.space == Space.Half ? 80f : 30f;
            var _v = new Voice { Name = _name, Src = _src, Follow = _follow, HasFollow = _follow, Wait = Mathf.Max(0f, _delay), Vol = _t.vol, Exclusive = _exclusive };
            _i._voices.Add(_v);
            if (_v.Wait <= 0f) _i.Begin(_v);
            return _v;
        }

        /// <summary>소리를 서서히 걷고 멈춤</summary>
        public static void FadeOut(Voice _v, float _sec = 0.2f) { if (_v != null && !_v.Dead) _v.FadeSpeed = 1f / Mathf.Max(0.01f, _sec); }
        /// <summary>반복음 크기(0~1). 부드럽게 바뀜</summary>
        public static void SetGain(Voice _v, float _gain) { if (_v != null) _v.GainTo = _gain; }
        /// <summary>모두 걷고 이 씬에서는 더 내지 않음(게임 오버·클리어)</summary>
        public static void StopAll(float _sec)
        {
            if (!s_inst) return;
            s_inst._muted = true;
            foreach (var _v in s_inst._voices) FadeOut(_v, _sec);
        }

        private void Begin(Voice _v)
        {
            if (_v.Exclusive) foreach (var _o in _voices) if (_o != _v && _o.Started && _o.Name == _v.Name) FadeOut(_o, 0.2f);
            _v.Started = true; _v.Src.volume = Level(_v); _v.Src.Play();
            if (DebugLog) Debug.Log("[GameSfx] " + Time.time.ToString("F3") + " " + _v.Name);
        }

        private AudioSource NewSource()
        {
            var _go = new GameObject("Sfx"); _go.transform.SetParent(transform, false);
            var _s = _go.AddComponent<AudioSource>(); _s.playOnAwake = false; _s.dopplerLevel = 0f; _s.rolloffMode = AudioRolloffMode.Linear;
            return _s;
        }

        private static float Level(Voice _v) => _v.Vol * _v.Gain * _v.Fade * MasterVolume();
        private static float MasterVolume()
        {
            var _sm = SoundManager.Instance;
            if (_sm) return _sm.MasterVolume * _sm.SfxVolume;
            return PlayerPrefs.GetFloat("HC_Master", 100f) / 100f * PlayerPrefs.GetFloat("HC_Effects", 50f) / 100f;
        }

        private void Update()
        {
            bool _stopped = Time.timeScale <= 0f && !_muted;   // 설정창·컷씬으로 멈춤(걷는 중에는 멈추지 않음)
            for (int _n = _voices.Count - 1; _n >= 0; _n--)
            {
                var _v = _voices[_n];
                if (_v.FadeSpeed > 0f) { _v.Fade -= Time.unscaledDeltaTime * _v.FadeSpeed; if (_v.Fade <= 0f || !_v.Started) { Release(_n); continue; } }
                if (_stopped) { if (_v.Started && !_v.Paused) { _v.Src.Pause(); _v.Paused = true; } continue; }
                if (_v.Paused) { _v.Src.UnPause(); _v.Paused = false; }
                if (!_v.Started) { _v.Wait -= Time.deltaTime; if (_v.Wait > 0f) continue; Begin(_v); }
                if (_v.HasFollow)
                {
                    if (_v.Follow) _v.Src.transform.position = _v.Follow.position;
                    if (_v.Src.loop && (!_v.Follow || !_v.Follow.gameObject.activeInHierarchy) && _v.FadeSpeed <= 0f) _v.FadeSpeed = 6f;
                }
                _v.Gain = Mathf.MoveTowards(_v.Gain, _v.GainTo, Time.unscaledDeltaTime * 6f);
                _v.Src.volume = Level(_v);
                if (!_v.Src.isPlaying && AudioSettings.dspTime > 0) Release(_n);
            }
            if (!_stopped && !_muted) { Footsteps(); Hit(); Horus(); }
        }

        private void Release(int _n)
        {
            var _v = _voices[_n]; _v.Dead = true; _voices.RemoveAt(_n);
            _v.Src.Stop(); _v.Src.clip = null; _v.Src.gameObject.SetActive(false); _pool.Push(_v.Src);
        }

        // ---------------- 플레이어 상태를 읽어 내는 소리 ----------------
        private Transform _player; private IPlayerMotion _motion; private IJumpState _jump; private IForcedMovable _movable; private IHealthReadOnly _hp;
        private PlayerMover _jmcMover; private PlayerHealth _jmcHp;
        private float _moveSpeed, _walked, _nextFind; private Vector3 _lastPos; private int _lastHp = -1;

        private bool FindPlayer()
        {
            if (_player) return true;
            if (Time.unscaledTime < _nextFind) return false;
            _nextFind = Time.unscaledTime + 0.5f;
            var _gm = GameManager.Instance;
            if (_gm && _gm.PlayerHealth is Component _pc && _pc) { _hp = _gm.PlayerHealth; _player = _pc.transform; }
            else { _jmcHp = FindFirstObjectByType<PlayerHealth>(); if (_jmcHp) _player = _jmcHp.transform; }
            if (!_player) return false;
            _motion = _player.GetComponent<IPlayerMotion>(); _jump = _player.GetComponent<IJumpState>(); _movable = _player.GetComponent<IForcedMovable>(); _jmcMover = _player.GetComponent<PlayerMover>();
            if (_motion != null) _motion.OnMoveChanged += OnMove;
            if (_movable != null) { _movable.OnForceMoveStarted += OnPull; _movable.OnForceMoveEnded += OnLand; }
            _lastPos = _player.position; _lastHp = Hp();
            return true;
        }

        private void OnMove(Vector2 _input, float _speed) { _moveSpeed = _speed; }
        private void OnPull(Vector3 _from, Vector3 _to) { Play("Anubis_Pull"); }
        private void OnLand() { Play("Anubis_Land"); }
        private int Hp() => _hp != null ? _hp.CurrentHp.CurrentValue : _jmcHp ? _jmcHp.HP : -1;

        // 실제로 걸어간 거리로 발을 디딤(밀려남·강제 이동·공중은 제외)
        private void Footsteps()
        {
            if (!FindPlayer()) return;
            Vector3 _d = _player.position - _lastPos; _lastPos = _player.position; _d.y = 0f;
            bool _input = _jmcMover ? _jmcMover.HasMoveInput && !_jmcMover.IsFalling : _moveSpeed > 0.05f;
            bool _ground = _jmcMover ? _jmcMover.IsGrounded : _jump == null || _jump.IsGrounded;
            if (!_input) { _walked = STRIDE * 0.6f; return; }                                  // 다시 걸을 때 첫 발은 금방
            if (!_ground || (_movable != null && _movable.IsForceMoving) || _d.magnitude > 1f) return;
            _walked += _d.magnitude;
            if (_walked < STRIDE) return;
            _walked -= STRIDE;
            Play("Player_Walk", Vector3.zero, null, 0f, false, false, 0.06f);
        }

        private void Hit()
        {
            if (!_player) return;
            int _now = Hp();
            if (_lastHp >= 0 && _now < _lastHp && _now > 0) { Play("Player_Hit_Leather"); Play("Player_hit_voice"); }   // 죽는 한 방은 게임 오버 소리가 이어받음
            _lastHp = _now;
        }

        // ---------------- 호루스 ----------------
        private HorusTileCollapse _horus; private string _phase = ""; private float _nextHorus; private Voice _horusVoice;
        private void Horus()
        {
            if (!_horus)
            {
                if (Time.unscaledTime < _nextHorus) return;
                _nextHorus = Time.unscaledTime + 0.5f;
                _horus = FindFirstObjectByType<HorusTileCollapse>();
                if (!_horus) return;
                _phase = _horus.Phase;
            }
            if (_horus.Phase == _phase) return;
            _phase = _horus.Phase;
            string _name = _phase == "warn" ? "Horus_Warning" : _phase == "broken" ? "Horus_Collapse" : _phase == "restore" ? "Horus_Restore" : null;
            if (_name == null) return;
            FadeOut(_horusVoice, 0.15f); _horusVoice = Play(_name);
        }
    }
}
