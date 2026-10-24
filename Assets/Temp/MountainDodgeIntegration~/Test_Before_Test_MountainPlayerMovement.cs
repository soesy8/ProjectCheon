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
        [SerializeField, Min(0.1f)] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -24f;
        [SerializeField, Range(0f, 0.6f)] private float inputBufferSeconds = 0.25f;
        [SerializeField] private float respawnBelow = -30f;

        private CharacterController body;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float verticalSpeed, pendingUntil;
        private int pendingAction;

        public Test_PlayerFanAnimationController AnimationControl => animationControl;
        public bool IsMoving { get; private set; }
        public string BufferedAction => pendingAction == -1 ? "발도 / 납도" : pendingAction > 0 ? "공격 " + pendingAction : "없음";

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

                if (keyboard.fKey.wasPressedThisFrame) QueueAction(-1);
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
            TryBufferedAction();

            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
            Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            IsMoving = direction.sqrMagnitude > 0.01f && animationControl.CanMove;
            animationControl.SetLocomotion(IsMoving);
            if (IsMoving)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            bool sprint = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            Vector3 velocity = IsMoving ? direction * (sprint ? sprintSpeed : moveSpeed) : Vector3.zero;
            velocity.y = verticalSpeed;
            body.Move(velocity * Time.deltaTime);
            if (transform.position.y < respawnBelow) ResetToSpawn();
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

        private void TryBufferedAction()
        {
            if (pendingAction == 0) return;
            if (Time.time > pendingUntil) { pendingAction = 0; return; }
            bool accepted = pendingAction == -1 ? animationControl.TryToggleEquipment() : animationControl.TryPlayAttack(pendingAction);
            if (accepted) pendingAction = 0;
        }

        public void ResetToSpawn()
        {
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            body.enabled = true;
            verticalSpeed = 0f;
            pendingAction = 0;
            animationControl.PlayIdle();
        }
    }
}
