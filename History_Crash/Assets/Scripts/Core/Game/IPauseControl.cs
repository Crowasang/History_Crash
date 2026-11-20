/// <summary>
/// 일시정지를 켜고 끄는 쪽(Esc 입력, 설정창 UI의 "계속하기" 버튼)이 쓰는 인터페이스.
/// 일시정지 여부를 읽기만 할 때는 IGameFlow.IsPaused를 씁니다. GameManager가 구현합니다.
/// </summary>
/// <example>
/// // 설정창 UI: 일시정지 상태를 구독해서 창을 열고 닫음
/// _gameFlow.IsPaused.Subscribe(_paused => _settingsPanel.SetActive(_paused)).AddTo(this);
///
/// // "계속하기" 버튼
/// _pauseControl.Resume();
/// </example>
public interface IPauseControl
{
    /// <summary>지금 일시정지할 수 있는지 (준비·진행 상태에서만 가능, 클리어·실패 후에는 불가)</summary>
    bool CanPause { get; }

    /// <summary>일시정지합니다. (시간 정지, 플레이어 조작 정지, 커서 표시)</summary>
    void Pause();

    /// <summary>일시정지를 풉니다.</summary>
    void Resume();

    /// <summary>일시정지 상태를 뒤집습니다. (Esc)</summary>
    void TogglePause();
}
