using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pause 액션(Esc)을 누르면 일시정지를 켜고 끕니다. GameManager와 같은 오브젝트에 둡니다.
/// 설정창은 이 입력이 아니라 IGameFlow.IsPaused를 구독해서 열고 닫습니다. (Update 없음)
/// </summary>
public class PauseInputHandler : MonoBehaviour
{
    [SerializeField] private InputActionReference _pauseAction;
    [Tooltip("IPauseControl을 구현한 컴포넌트 (GameManager). 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private MonoBehaviour _pauseControlSource;

    private IPauseControl _pauseControl;

    private void Awake()
    {
        _pauseControl = InterfaceRef.Resolve<IPauseControl>(_pauseControlSource, this, nameof(_pauseControlSource))
            ?? GetComponent<IPauseControl>();
        if (_pauseControl == null)
        {
            Debug.LogError($"[PauseInputHandler] {name}에 IPauseControl(GameManager)이 없습니다.", this);
        }
    }

    private void OnEnable()
    {
        if (_pauseAction == null) return;

        _pauseAction.action.performed += OnPause;
        _pauseAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_pauseAction == null) return;

        _pauseAction.action.performed -= OnPause;
        _pauseAction.action.Disable();
    }

    // Esc를 누를 때만 호출됨
    private void OnPause(InputAction.CallbackContext _context)
    {
        _pauseControl?.TogglePause();
    }
}
