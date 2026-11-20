using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 저장·불러오기 매니저. 씬이 바뀌어도 유지됩니다. (첫 씬에 하나만 둠)
/// - 시작할 때(Awake) 저장 파일을 자동으로 불러옴. 파일이 없거나 깨졌으면 처음 상태로 시작
/// - 값이 바뀔 때 Save()로 자동 저장 (플레이어가 누르는 저장 버튼 없음)
/// - 저장 위치: Application.persistentDataPath/save.json (JSON)
/// - 임시 파일에 먼저 쓴 뒤 바꿔치기해서, 저장 도중 꺼져도 기존 파일이 깨지지 않음
/// </summary>
/// <example>
/// // 소개 화면 (팀원 UI)
/// IIntroProgress _intro = SaveManager.Instance;
/// if (!_intro.HasSeenIntro) { /* 조작법·스토리 페이지 보여 주기 */ }
/// _intro.MarkIntroSeen(); // 마지막 페이지를 넘겼을 때
/// </example>
public class SaveManager : MonoBehaviour, ISaveService, IIntroProgress
{
    private const string FILE_NAME = "save.json";
    private const string TEMP_SUFFIX = ".tmp";
    private const string BROKEN_SUFFIX = ".broken";

    public static SaveManager Instance { get; private set; }

    public SaveData Data { get; private set; } = new SaveData();

    public bool HasSeenIntro => Data.HasSeenIntro;

    public event Action OnSaved;

    private string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void MarkIntroSeen()
    {
        if (Data.HasSeenIntro) return;

        Data.HasSeenIntro = true;
        Save();
    }

    public void Save()
    {
        string _path = FilePath;
        string _tempPath = _path + TEMP_SUFFIX;

        try
        {
            File.WriteAllText(_tempPath, JsonUtility.ToJson(Data, true));

            // 임시 파일을 완성한 뒤 바꿔치기 (쓰는 도중 꺼져도 기존 저장 파일은 그대로)
            if (File.Exists(_path))
            {
                File.Replace(_tempPath, _path, null);
            }
            else
            {
                File.Move(_tempPath, _path);
            }

            OnSaved?.Invoke();
        }
        catch (Exception _exception)
        {
            Debug.LogError($"[SaveManager] 저장 실패: {_exception.Message}", this);
        }
    }

    public void ResetAll()
    {
        Data = new SaveData();
        Save();
        Debug.Log($"[SaveManager] 저장 데이터 초기화: {FilePath}");
    }

    private void Load()
    {
        string _path = FilePath;
        if (!File.Exists(_path))
        {
            Data = new SaveData();
            return;
        }

        try
        {
            SaveData _loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(_path));
            Data = _loaded ?? new SaveData();
        }
        catch (Exception _exception)
        {
            // 깨진 파일은 지우지 않고 옆에 보관한 뒤 처음 상태로 시작
            Debug.LogWarning($"[SaveManager] 저장 파일을 읽지 못해 처음 상태로 시작합니다: {_exception.Message}", this);
            TryBackupBrokenFile(_path);
            Data = new SaveData();
        }
    }

    private void TryBackupBrokenFile(string _path)
    {
        try
        {
            File.Copy(_path, _path + BROKEN_SUFFIX, true);
        }
        catch (Exception)
        {
            // 보관에 실패해도 게임 진행에는 문제없음
        }
    }

    // 테스트용: 인스펙터 ⋮ 메뉴에서 처음 실행 상태로 되돌림 (소개를 다시 보고 싶을 때)
    [ContextMenu("저장 데이터 초기화 (처음 실행 상태로)")]
    private void ResetFromMenu()
    {
        if (!Application.isPlaying)
        {
            // 플레이 중이 아니어도 파일만 지우면 다음 실행 때 처음 상태
            if (File.Exists(FilePath)) File.Delete(FilePath);
            Debug.Log($"[SaveManager] 저장 파일 삭제: {FilePath}");
            return;
        }

        ResetAll();
    }
}
