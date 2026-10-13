using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Camera movementCamera;
    [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float dodgeSpeed = 16f;
    [SerializeField, Min(0.01f)] private float dodgeDuration = 0.18f;
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.6f;
    [SerializeField] private Vector2 mapHalfSize = new Vector2(20f, 10f);

    private CharacterController characterController;
    private Vector3 dodgeDirection;
    private float dodgeEndTime;
    private float nextDodgeTime;
    private float verticalSpeed;

    public bool IsDodging => Time.time < dodgeEndTime;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (movementCamera == null)
            movementCamera = Camera.main;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (keyboard != null)
        {
            input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
        }

        Vector3 cameraForward = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
        Vector3 cameraRight = movementCamera != null ? movementCamera.transform.right : Vector3.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        Vector3 moveDirection = Vector3.ClampMagnitude(
            cameraForward.normalized * input.y + cameraRight.normalized * input.x, 1f);

        if (!IsDodging && moveDirection.sqrMagnitude > 0.01f)
            transform.forward = moveDirection.normalized;

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && Time.time >= nextDodgeTime)
        {
            dodgeDirection = moveDirection.sqrMagnitude > 0.01f ? moveDirection.normalized : transform.forward;
            dodgeEndTime = Time.time + dodgeDuration;
            nextDodgeTime = dodgeEndTime + dodgeCooldown;
        }

        Vector3 velocity = IsDodging ? dodgeDirection * dodgeSpeed : moveDirection * moveSpeed;
        verticalSpeed = characterController.isGrounded ? -2f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
        velocity.y = verticalSpeed;

        // 바닥 끝에서 떨어지지 않게 캡슐 반지름만큼 여유를 두고 이동량을 제한한다.
        Vector3 nextPosition = transform.position + velocity * Time.deltaTime;
        float margin = characterController.radius + characterController.skinWidth;
        nextPosition.x = Mathf.Clamp(nextPosition.x, -mapHalfSize.x + margin, mapHalfSize.x - margin);
        nextPosition.z = Mathf.Clamp(nextPosition.z, -mapHalfSize.y + margin, mapHalfSize.y - margin);
        characterController.Move(nextPosition - transform.position);
    }
}
