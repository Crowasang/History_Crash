/// <summary>
/// 처음 실행 때 보여 주는 조작법·스토리 소개를 봤는지. 타이틀·소개 UI가 이것만 알면 됩니다.
/// SaveManager가 구현하며, 기록하면 자동으로 저장됩니다.
/// </summary>
/// <example>
/// // 시작 버튼
/// if (_introProgress.HasSeenIntro) StartGame();
/// else ShowIntroPages();
///
/// // 소개 마지막 페이지를 넘겼을 때
/// _introProgress.MarkIntroSeen();
/// StartGame();
/// </example>
public interface IIntroProgress
{
    /// <summary>소개를 끝까지 본 적이 있는지</summary>
    bool HasSeenIntro { get; }

    /// <summary>소개를 끝까지 봤다고 기록하고 바로 저장합니다. (중간에 끄면 기록되지 않음)</summary>
    void MarkIntroSeen();
}
