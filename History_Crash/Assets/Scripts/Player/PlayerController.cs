using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New Input System의 Move 액션(WASD / 방향키 / 게임패드 스틱)으로 플레이어를 이동시킵니다.
/// 이동 방향은 월드 기준 XZ 평면입니다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private const float GROUNDED_GRAVITY = -2f;

    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
    }

    private void Update()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);

        // 이동 방향으로 회전
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // 바닥에 붙어 있으면 낙하 속도를 초기화
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = GROUNDED_GRAVITY;
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDir * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
}
