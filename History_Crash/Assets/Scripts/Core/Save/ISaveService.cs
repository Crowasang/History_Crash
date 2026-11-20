using System;

/// <summary>
/// 저장·불러오기. 게임 시작 때 자동으로 불러오고, 값이 바뀌는 쪽에서 Save()를 불러 자동 저장합니다.
/// (플레이어가 누르는 저장 버튼은 없음) SaveManager가 구현합니다.
/// </summary>
/// <example>
/// // 새 항목을 추가했을 때: SaveData에 필드·프로퍼티를 만들고, 값을 바꾼 뒤 Save()
/// SaveManager.Instance.Data.BestClearTime = 93.5f;
/// SaveManager.Instance.Save();
/// </example>
public interface ISaveService
{
    /// <summary>현재 저장 데이터 (불러온 값). 바꾼 뒤에는 Save()를 불러야 디스크에 기록됩니다.</summary>
    SaveData Data { get; }

    /// <summary>현재 데이터를 디스크에 기록합니다.</summary>
    void Save();

    /// <summary>저장 데이터를 처음 상태로 되돌리고 기록합니다. (테스트, 데이터 초기화 메뉴)</summary>
    void ResetAll();

    /// <summary>저장이 끝났을 때</summary>
    event Action OnSaved;
}
