using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 배경음과 효과음을 재생합니다. 씬에 하나만 둡니다.
/// - 배경음: 전용 AudioSource 하나 (페이드 인/아웃)
/// - 효과음: AudioSource를 풀링해서 동시에 여러 개 재생, 끝나면 자동 반납
/// - 볼륨: 전체 × 배경음 / 전체 × 효과음
/// </summary>
/// <example>
/// SoundManager.Instance.PlayBgm(_battleBgm);
/// SoundManager.Instance.PlaySfx(_hitClip);                 // 2D 효과음
/// SoundManager.Instance.PlaySfx(_explodeClip, transform.position); // 위치 있는 3D 효과음
/// </example>
public class SoundManager : MonoBehaviour
{
    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _bgmVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1f;

    [Header("Options")]
    [Tooltip("씬이 바뀌어도 유지")]
    [SerializeField] private bool _dontDestroyOnLoad = true;
    [Tooltip("효과음 AudioSource 최대 보관 수")]
    [SerializeField] private int _maxSfxSources = 32;

    private AudioSource _bgmSource;
    private ObjectPool<AudioSource> _sfxPool;
    private Tween _bgmFade;

    public static SoundManager Instance { get; private set; }

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Mathf.Clamp01(value);
            ApplyBgmVolume();
        }
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = Mathf.Clamp01(value);
            ApplyBgmVolume();
        }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxPool = new ObjectPool<AudioSource>(
            createFunc: CreateSfxSource,
            actionOnGet: _source => _source.gameObject.SetActive(true),
            actionOnRelease: _source =>
            {
                _source.Stop();
                _source.clip = null;
                _source.gameObject.SetActive(false);
            },
            actionOnDestroy: _source => Destroy(_source.gameObject),
            collectionCheck: false,
            defaultCapacity: 8,
            maxSize: _maxSfxSources);
    }

    private void OnDestroy()
    {
        _bgmFade?.Kill();
        _sfxPool?.Dispose();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 배경음을 재생합니다. 같은 곡이 이미 재생 중이면 무시합니다.
    /// </summary>
    public void PlayBgm(AudioClip _clip, float _fadeSeconds = 0.5f)
    {
        if (_clip == null) return;
        if (_bgmSource.clip == _clip && _bgmSource.isPlaying) return;

        _bgmFade?.Kill();
        _bgmSource.clip = _clip;
        _bgmSource.volume = 0f;
        _bgmSource.Play();
        _bgmFade = _bgmSource.DOFade(_masterVolume * _bgmVolume, _fadeSeconds);
    }

    /// <summary>배경음을 서서히 줄이며 정지합니다.</summary>
    public void StopBgm(float _fadeSeconds = 0.5f)
    {
        _bgmFade?.Kill();
        _bgmFade = _bgmSource.DOFade(0f, _fadeSeconds).OnComplete(() => _bgmSource.Stop());
    }

    /// <summary>2D 효과음을 재생합니다. (UI, 플레이어 피격 등)</summary>
    public void PlaySfx(AudioClip _clip, float _volumeScale = 1f)
    {
        PlaySfxInternal(_clip, Vector3.zero, 0f, _volumeScale);
    }

    /// <summary>지정한 위치에서 3D 효과음을 재생합니다. (폭발, 미라 사망 등)</summary>
    public void PlaySfx(AudioClip _clip, Vector3 _position, float _volumeScale = 1f)
    {
        PlaySfxInternal(_clip, _position, 1f, _volumeScale);
    }

    private void PlaySfxInternal(AudioClip _clip, Vector3 _position, float _spatialBlend, float _volumeScale)
    {
        if (_clip == null) return;

        AudioSource _source = _sfxPool.Get();
        _source.transform.position = _position;
        _source.spatialBlend = _spatialBlend;
        _source.volume = _masterVolume * _sfxVolume * _volumeScale;
        _source.clip = _clip;
        _source.Play();

        ReleaseWhenFinishedAsync(_source, _clip.length, destroyCancellationToken).Forget();
    }

    private async UniTaskVoid ReleaseWhenFinishedAsync(AudioSource _source, float _seconds, CancellationToken _token)
    {
        bool _isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_seconds), ignoreTimeScale: true, cancellationToken: _token)
            .SuppressCancellationThrow();

        if (!_isCanceled)
        {
            _sfxPool.Release(_source);
        }
    }

    private AudioSource CreateSfxSource()
    {
        GameObject _go = new GameObject("Sfx");
        _go.transform.SetParent(transform, false);
        AudioSource _source = _go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        return _source;
    }

    private void ApplyBgmVolume()
    {
        if (_bgmSource == null) return;

        _bgmFade?.Kill();
        _bgmSource.volume = _masterVolume * _bgmVolume;
    }
}
