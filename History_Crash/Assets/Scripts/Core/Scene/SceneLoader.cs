using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬을 비동기로 전환합니다. 씬이 바뀌어도 유지됩니다. (첫 씬에 하나만 둠)
/// - 페이드 아웃 → 로딩 → 페이드 인 (페이드 패널이 없으면 바로 전환)
/// - 로딩 진행률을 Progress로 알림 (로딩 바 UI용)
/// - 로딩 중에 들어온 요청은 무시
/// 불러올 씬은 Build Profiles(Build Settings)의 씬 목록에 등록되어 있어야 합니다.
/// </summary>
/// <example>
/// SceneLoader.Instance.LoadScene("Pharaoh");   // 씬 이름으로 전환
/// SceneLoader.Instance.ReloadCurrent();        // 재시작 버튼
/// </example>
public class SceneLoader : MonoBehaviour
{
    [Tooltip("화면 전체를 덮는 페이드용 CanvasGroup (없으면 페이드 생략)")]
    [SerializeField] private CanvasGroup _fadeGroup;
    [SerializeField] private float _fadeSeconds = 0.4f;

    private readonly ReactiveProperty<float> _progress = new ReactiveProperty<float>(1f);
    private readonly ReactiveProperty<bool> _isLoading = new ReactiveProperty<bool>(false);

    public static SceneLoader Instance { get; private set; }

    /// <summary>로딩 진행률 (0 ~ 1)</summary>
    public ReadOnlyReactiveProperty<float> Progress => _progress;

    /// <summary>로딩 중인지</summary>
    public ReadOnlyReactiveProperty<bool> IsLoading => _isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_fadeGroup != null)
        {
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        _progress.Dispose();
        _isLoading.Dispose();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>씬 이름으로 전환합니다.</summary>
    public void LoadScene(string _sceneName)
    {
        if (_isLoading.Value) return;

        LoadSceneAsync(_sceneName).Forget();
    }

    /// <summary>현재 씬을 처음부터 다시 불러옵니다.</summary>
    public void ReloadCurrent()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    private async UniTaskVoid LoadSceneAsync(string _sceneName)
    {
        _isLoading.Value = true;
        _progress.Value = 0f;

        await FadeAsync(1f);

        AsyncOperation _operation = SceneManager.LoadSceneAsync(_sceneName);
        if (_operation == null)
        {
            Debug.LogError($"[SceneLoader] '{_sceneName}' 씬을 찾을 수 없습니다. Build Settings 씬 목록을 확인하세요.");
            await FadeAsync(0f);
            _isLoading.Value = false;
            return;
        }

        await _operation.ToUniTask(Cysharp.Threading.Tasks.Progress.Create<float>(_value => _progress.Value = _value),
            cancellationToken: destroyCancellationToken);
        _progress.Value = 1f;

        await FadeAsync(0f);
        _isLoading.Value = false;
    }

    private async UniTask FadeAsync(float _targetAlpha)
    {
        if (_fadeGroup == null) return;

        _fadeGroup.blocksRaycasts = true;

        // 트윈이 끝나면(자동 Kill) IsActive가 false가 됨
        Tween _fade = _fadeGroup.DOFade(_targetAlpha, _fadeSeconds).SetUpdate(true);
        await UniTask.WaitWhile(() => _fade.IsActive(), cancellationToken: destroyCancellationToken);

        _fadeGroup.blocksRaycasts = _targetAlpha > 0f;
    }
}
