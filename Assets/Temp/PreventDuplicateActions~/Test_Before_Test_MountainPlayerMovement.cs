using Cheon.Testing.PlayerFan;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cheon.Testing.MountainMap
{
    [RequireComponent(typeof(CharacterController))]
    public class Test_MountainPlayerMovement : MonoBehaviour
    {
        [SerializeField] private Test_PlayerFanAnimationController animationControl;
        [SerializeField] private Transform view;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6f;
        [SerializeField, Min(0.1f)] private float dodgeDistance = 7.5f;
        [Tooltip("1은 등속 이동, 2 이상은 초반에 빠르게 이동한 뒤 끝에서 감속합니다.")]
        [SerializeField, Range(1f, 4f)] private float dodgeDecelerationPower = 2f;
        [Tooltip("구르기 시작 방향에 이동 입력을 섞는 비율입니다. 0은 방향 고정, 1은 완전 조작입니다.")]
        [SerializeField, Range(0f, 1f)] private float dodgeSteering = 0.35f;
        [SerializeField, Min(0.1f)] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -24f;
        [SerializeField, Range(0f, 0.6f)] private float inputBufferSeconds = 0.25f;
        [SerializeField] private float respawnBelow = -30f;

        private CharacterController body;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float verticalSpeed, pendingUntil;
        private int pendingAction;
        private float dodgeProgress;
        private Vector3 dodgeStartDirection;
        private Vector3 dodgeDirection;

        public Test_PlayerFanAnimationController AnimationControl => animationControl;
        public bool IsMoving { get; private set; }
        public string BufferedAction => pendingAction == -2 ? "회피" : pendingAction == -1 ? "발도 / 납도" : pendingAction > 0 ? "공격 " + pendingAction : "없음";

        public void Configure(Test_PlayerFanAnimationController controller, Transform cameraTransform)
        {
            animationControl = controller;
            view = cameraTransform;
        }

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (animationControl == null || view == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool typing = Test_PlayerTestUi.IsTyping;
            Vector2 input = Vector2.zero;
            if (keyboard != null && !typing)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                    (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                    (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);

                if (keyboard.spaceKey.wasPressedThisFrame) QueueAction(-2);
                else if (keyboard.fKey.wasPressedThisFrame) QueueAction(-1);
                else if (keyboard.digit1Key.wasPressedThisFrame) QueueAction(1);
                else if (keyboard.digit2Key.wasPressedThisFrame) QueueAction(2);
                else if (keyboard.digit3Key.wasPressedThisFrame) QueueAction(3);
                else if (keyboard.jKey.wasPressedThisFrame) QueueCombo();
                if (keyboard.digit4Key.wasPressedThisFrame)
                {
                    pendingAction = 0;
                    animationControl.PlayHit();
                }
                if (keyboard.rKey.wasPressedThisFrame) ResetToSpawn();
            }

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!typing && !overUi && mouse != null && mouse.leftButton.wasPressedThisFrame) QueueCombo();
            if (typing || animationControl.IsPaused) pendingAction = 0;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
            Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            TryBufferedAction(direction);
            bool sprint = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            IsMoving = direction.sqrMagnitude > 0.01f && animationControl.CanMove;
            animationControl.SetLocomotion(IsMoving, sprint ? 1f : moveSpeed / sprintSpeed);
            if (IsMoving)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            Vector3 displacement = IsMoving ? direction * (sprint ? sprintSpeed : moveSpeed) * Time.deltaTime : Vector3.zero;
            if (animationControl.IsDodging)
            {
                displacement = GetDodgeDisplacement(direction, Time.deltaTime);
            }
            displacement.y = verticalSpeed * Time.deltaTime;
            body.Move(displacement);
            if (transform.position.y < respawnBelow) ResetToSpawn();
        }

        private Vector3 GetDodgeDisplacement(Vector3 inputDirection, float deltaTime)
        {
            if (animationControl.IsPaused || deltaTime <= 0f) return Vector3.zero;

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                // 매 프레임 방향을 누적해서 꺾지 않고, 시작 방향에 입력의 35%만 반영한다.
                Vector3 targetDirection = Vector3.Lerp(dodgeStartDirection, inputDirection.normalized,
                    Mathf.Clamp01(dodgeSteering) * inputDirection.magnitude).normalized;
                if (targetDirection.sqrMagnitude > 0.01f)
                    dodgeDirection = Vector3.Slerp(dodgeDirection, targetDirection,
                        1f - Mathf.Exp(-rotationSpeed * deltaTime)).normalized;
            }
            transform.rotation = Quaternion.LookRotation(dodgeDirection);

            float nextProgress = Mathf.Clamp01(dodgeProgress + deltaTime / Mathf.Max(0.01f, animationControl.RollDuration));
            float power = Mathf.Max(1f, dodgeDecelerationPower);
            // 감속 곡선의 누적 거리 차이를 사용해 프레임 수나 재생 속도가 달라도 총 거리를 유지한다.
            float distance = dodgeDistance * (Mathf.Pow(1f - dodgeProgress, power) - Mathf.Pow(1f - nextProgress, power));
            dodgeProgress = nextProgress;
            return dodgeDirection * distance;
        }

        private void QueueCombo()
        {
            QueueAction(animationControl.SelectedState == "ATTACK1" ? 2 : animationControl.SelectedState == "ATTACK2" ? 3 : 1);
        }

        private void QueueAction(int action)
        {
            // 납도 중 공격을 예약하지 않아 나중에 발도했을 때 의도치 않게 공격하지 않는다.
            if (action > 0 && !animationControl.IsCombatRequested) return;
            pendingAction = action;
            pendingUntil = Time.time + inputBufferSeconds;
        }

        private void TryBufferedAction(Vector3 direction)
        {
            if (pendingAction == 0) return;
            if (Time.time > pendingUntil) { pendingAction = 0; return; }
            bool accepted;
            if (pendingAction == -2)
            {
                accepted = animationControl.TryPlayDodge();
                if (accepted)
                {
                    dodgeDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
                    dodgeStartDirection = dodgeDirection;
                    dodgeProgress = 0f;
                    transform.rotation = Quaternion.LookRotation(dodgeDirection);
                }
            }
            else accepted = pendingAction == -1 ? animationControl.TryToggleEquipment() : animationControl.TryPlayAttack(pendingAction);
            if (accepted) pendingAction = 0;
        }

        public void ResetToSpawn()
        {
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            body.enabled = true;
            verticalSpeed = 0f;
            pendingAction = 0;
            dodgeProgress = 0f;
            animationControl.PlayIdle();
        }
    }
}
