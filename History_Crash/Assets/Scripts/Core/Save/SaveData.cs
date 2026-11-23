using System;
using UnityEngine;

/// <summary>
/// 디스크에 저장되는 데이터. 저장할 항목이 늘면 여기에 [SerializeField] 필드와 프로퍼티를 추가합니다.
/// JsonUtility로 저장하므로 [SerializeField] 필드만 저장됩니다.
/// 예전 저장 파일에 없는 새 필드는 기본값으로 채워집니다.
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>저장 형식 버전 (나중에 형식이 크게 바뀌면 올려서 변환에 사용)</summary>
    public const int CURRENT_VERSION = 1;

    [SerializeField] private int _version = CURRENT_VERSION;
    [SerializeField] private bool _hasSeenIntro;

    public int Version => _version;

    /// <summary>처음 실행 때 보여 주는 조작법·스토리 소개를 끝까지 봤는지</summary>
    public bool HasSeenIntro
    {
        get => _hasSeenIntro;
        set => _hasSeenIntro = value;
    }
}
