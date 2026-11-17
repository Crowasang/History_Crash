/// <summary>
/// 씬 전환 기능. SceneLoader가 구현합니다.
/// 버튼이나 결과 화면은 SceneLoader 대신 이 인터페이스를 씁니다.
/// </summary>
public interface ISceneLoader
{
    /// <summary>씬 이름으로 전환합니다. (Build Profiles 씬 목록에 등록된 씬)</summary>
    void LoadScene(string _sceneName);

    /// <summary>현재 씬을 처음부터 다시 불러옵니다.</summary>
    void ReloadCurrent();
}
