using System.Collections;
using HistoryCrash.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoryCrash.Cutscene
{
    /// <summary>
    /// 게임 중 배경음: 보스 소개 컷씬이 끝나면(또는 Retry로 컷씬을 건너뛰면 바로) 시작해 계속 반복한다.
    /// - 곡: Resources/BGM/GameBgm_WhispersOfTheAncientTale (사용자 제공 'Whispers of the ancient tale_2_edit_edit_edit')
    /// - 음량: 설정창 Master × Music. 게임 시간이 멈춰도(설정창) 계속 나온다. 게임 오버 연출이 시작되면 FadeOut으로 걷힌다.
    /// - 씬에 따로 놓지 않아도 BossIntroCutscene.SCENES 씬이 열리면 자동으로 생기고, 씬이 바뀌면 함께 사라진다.
    /// </summary>
    public class StageBgm : MonoBehaviour
    {
        private const string CLIP = "BGM/GameBgm_WhispersOfTheAncientTale";
        private const float FADE_IN = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene _s, LoadSceneMode _m) { if (_m == LoadSceneMode.Single) AutoCreate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (System.Array.IndexOf(BossIntroCutscene.SCENES, SceneManager.GetActiveScene().name) < 0) return;
            if (FindFirstObjectByType<StageBgm>()) return;
            new GameObject("StageBgm").AddComponent<StageBgm>();
        }

        private AudioSource _src;
        private float _gain, _target = 1f, _speed = 1f / FADE_IN;

        /// <summary>배경음을 서서히 걷고 멈춘다(게임 오버)</summary>
        public static void FadeOut(float _sec)
        {
            foreach (var _b in FindObjectsByType<StageBgm>(FindObjectsSortMode.None)) { _b._target = 0f; _b._speed = 1f / Mathf.Max(0.01f, _sec); }
        }

        private IEnumerator Start()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = Resources.Load<AudioClip>(CLIP);
            _src.loop = true; _src.playOnAwake = false; _src.spatialBlend = 0f; _src.ignoreListenerPause = true; _src.volume = 0f;
            if (!_src.clip) { Debug.LogWarning("[StageBgm] 배경음을 찾지 못함: Resources/" + CLIP); yield break; }
            yield return null;
            while (IntroRunning()) yield return null;
            if (_target > 0f) _src.Play();
        }

        // 컷씬은 끝나면 스스로 enabled = false가 되고 소리 꼬리만 남김(책이 내려앉는 전환도 기다림)
        private static bool IntroRunning()
        {
            if (FindFirstObjectByType<BookToGameTransition>()) return true;
            var _c = FindFirstObjectByType<BossIntroCutscene>();
            return _c && _c.enabled;
        }

        private void Update()
        {
            if (!_src || !_src.isPlaying) return;
            _gain = Mathf.MoveTowards(_gain, _target, Time.unscaledDeltaTime * _speed);
            _src.volume = _gain * BossIntroCutscene.Volume();
            if (_gain <= 0f && _target <= 0f) _src.Stop();
        }
    }
}
