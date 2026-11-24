using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 누르면 씬을 전환하는 버튼. (재시작, 타이틀로, 난이도 선택으로 등)
/// - 이동할 씬 이름을 비우면 현재 씬을 다시 불러옵니다. (재시작)
/// - 씬 로더(SceneLoader)를 연결하면 페이드와 함께 전환하고, 비우면 바로 전환합니다.
/// 이동할 씬은 Build Profiles 씬 목록에 등록되어 있어야 합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneChangeButton : MonoBehaviour
{
    [Tooltip("이동할 씬 이름. 비우면 현재 씬을 다시 불러옴 (재시작)")]
    [SerializeField] private string _targetSceneName;
    [Tooltip("ISceneLoader를 구현한 컴포넌트 (SceneLoader). 비우거나 사라졌으면 씬이 바뀌어도 유지되는 SceneLoader를 씀, 그것도 없으면 페이드 없이 바로 전환")]
    [SerializeField] private MonoBehaviour _sceneLoaderSource;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    // 누를 때 찾음: 메인 메뉴에서 넘어오면 이 씬의 SceneLoader는 중복이라 사라지고, 씬이 바뀌어도 유지되는 것(Instance)만 남기 때문
    private ISceneLoader ResolveLoader()
    {
        if (_sceneLoaderSource != null)   // 사라진 컴포넌트는 Unity에서 null로 취급됨
        {
            return InterfaceRef.Resolve<ISceneLoader>(_sceneLoaderSource, this, nameof(_sceneLoaderSource));
        }

        return SceneLoader.Instance;
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        bool _isReload = string.IsNullOrEmpty(_targetSceneName);

        ISceneLoader _sceneLoader = ResolveLoader();
        if (_sceneLoader != null)
        {
            if (_isReload) _sceneLoader.ReloadCurrent();
            else _sceneLoader.LoadScene(_targetSceneName);
            return;
        }

        if (_isReload) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        else SceneManager.LoadScene(_targetSceneName);
    }
}
