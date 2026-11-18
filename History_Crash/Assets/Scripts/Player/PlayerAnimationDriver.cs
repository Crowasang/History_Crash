using DG.Tweening;
using UnityEngine;

/// <summary>
/// 플레이어 움직임 알림(IPlayerMotion)을 받아 모델 Animator 값을 넣습니다. (Update 없음)
/// Animator 파라미터 (Player_Robe.controller 기준):
/// - MoveX (좌-/우+), MoveZ (뒤-/앞+): 이동 방향. Locomotion 블렌드 트리가 대기·앞·뒤·좌·우를 섞음
/// - Speed (m/초), MoveAnimSpeed (재생 배속): 발 미끄러짐을 줄이도록 보폭에 맞춘 배속
/// - Grounded (bool): 꺼지면 Jump_Air, 켜지면 Locomotion
/// </summary>
public class PlayerAnimationDriver : MonoBehaviour
{
    private static readonly int SPEED_ID = Animator.StringToHash("Speed");
    private static readonly int MOVE_X_ID = Animator.StringToHash("MoveX");
    private static readonly int MOVE_Z_ID = Animator.StringToHash("MoveZ");
    private static readonly int MOVE_ANIM_SPEED_ID = Animator.StringToHash("MoveAnimSpeed");
    private static readonly int GROUNDED_ID = Animator.StringToHash("Grounded");

    [SerializeField] private Animator _animator;
    [Tooltip("IPlayerMotion을 구현한 컴포넌트 (PlayerController). 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private MonoBehaviour _motionSource;

    [Header("클립 1배속일 때의 보폭 속도 (m/초, TEST_JMC에서 조정한 값)")]
    [SerializeField] private float _forwardClipSpeed = 5f;
    [SerializeField] private float _backClipSpeed = 4f;
    [SerializeField] private float _strafeClipSpeed = 4f;
    [Tooltip("재생 배속 상한 (너무 빠른 다리 움직임 방지)")]
    [SerializeField] private float _maxAnimSpeed = 3.5f;
    [Tooltip("방향 값이 바뀌는 시간 (초)")]
    [SerializeField] private float _dampTime = 0.08f;

    private IPlayerMotion _motion;
    private Vector2 _currentDir;
    private Tween _dirTween;

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        _motion = InterfaceRef.Resolve<IPlayerMotion>(_motionSource, this, nameof(_motionSource))
            ?? GetComponent<IPlayerMotion>();
        if (_motion == null)
        {
            Debug.LogError($"[PlayerAnimationDriver] {name}에 IPlayerMotion(PlayerController 등)이 없습니다.", this);
        }
    }

    private void OnEnable()
    {
        if (_motion == null) return;

        _motion.OnMoveChanged += HandleMoveChanged;
        _motion.OnJumped += HandleJumped;
        _motion.OnLanded += HandleLanded;
    }

    private void OnDisable()
    {
        if (_motion != null)
        {
            _motion.OnMoveChanged -= HandleMoveChanged;
            _motion.OnJumped -= HandleJumped;
            _motion.OnLanded -= HandleLanded;
        }

        _dirTween?.Kill();
    }

    private void Start()
    {
        if (_animator == null) return;

        _animator.SetBool(GROUNDED_ID, true);
        _animator.SetFloat(MOVE_ANIM_SPEED_ID, 1f);
    }

    private void HandleMoveChanged(Vector2 _input, float _speed)
    {
        if (_animator == null) return;

        Vector2 _dir = _speed > 0.05f ? _input.normalized : Vector2.zero;

        // 방향 값을 짧게 부드럽게 바꿈 (매 프레임 갱신은 DOTween이 처리)
        _dirTween?.Kill();
        _dirTween = DOTween.To(() => _currentDir, SetDirection, _dir, _dampTime);

        _animator.SetFloat(SPEED_ID, _speed);
        _animator.SetFloat(MOVE_ANIM_SPEED_ID, CalculateAnimSpeed(_dir, _speed));
    }

    private void SetDirection(Vector2 _dir)
    {
        _currentDir = _dir;
        _animator.SetFloat(MOVE_X_ID, _dir.x);
        _animator.SetFloat(MOVE_Z_ID, _dir.y);
    }

    // 방향별 클립 보폭 속도를 섞어서, 실제 이동 속도에 맞는 재생 배속을 계산
    private float CalculateAnimSpeed(Vector2 _dir, float _speed)
    {
        if (_speed <= 0.05f) return 1f;

        float _forward = Mathf.Max(_dir.y, 0f);
        float _back = Mathf.Max(-_dir.y, 0f);
        float _side = Mathf.Abs(_dir.x);
        float _sum = _forward + _back + _side;
        float _clipSpeed = _sum > 0f
            ? (_forward * _forwardClipSpeed + _back * _backClipSpeed + _side * _strafeClipSpeed) / _sum
            : _forwardClipSpeed;

        return Mathf.Clamp(_speed / Mathf.Max(_clipSpeed, 0.01f), 0.5f, _maxAnimSpeed);
    }

    private void HandleJumped()
    {
        if (_animator != null) _animator.SetBool(GROUNDED_ID, false);
    }

    private void HandleLanded()
    {
        if (_animator != null) _animator.SetBool(GROUNDED_ID, true);
    }
}
