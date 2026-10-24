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
        [SerializeField, Range(0f, 1f)] private float dodgeSteering = 0.45f;
        [Tooltip("다 구르고 일어나는 구간에서 이동 입력을 반영하는 비율입니다.")]
        [SerializeField, Range(0f, 1f)] private float dodgeRecoverySteering = 0.75f;
        [Tooltip("일어나는 구간이 시작되는 원본 구르기 클립의 프레임입니다.")]
        [SerializeField, Min(0)] private int dodgeRecoveryStartFrame = 22;
        [SerializeField, Min(0.1f)] private float rotationSpeed = 14f;
        [SerializeField] private float gravity = -24f;
        [Tooltip("각 동작의 캔슬 시점에 도달하기 전 예약 입력을 받는 시간(초)입니다.")]
        [SerializeField, Range(0f, 0.6f)] private float inputBufferSeconds = 0.15f;
        [SerializeField] private float respawnBelow = -30f;

        private CharacterController body;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private const int HitAction = -3, DodgeAction = -2, EquipmentAction = -1;
        private float verticalSpeed;
        private int pendingAction, frameAction, actionExecutedFrame = -1;
        private uint pendingSequence;
        private Vector3 inputDirection;
        private float dodgeProgress;
        private Vector3 dodgeStartDirection;
        private Vector3 dodgeDirection;

        public Test_PlayerFanAnimationController AnimationControl => animationControl;
        public bool IsMoving { get; private set; }
        public string BufferedAction => pendingAction == DodgeAction ? "회피" : pendingAction == EquipmentAction ? "발도 / 납도" : pendingAction > 0 ? "공격 " + pendingAction : "없음";

        public void Configure(Test_PlayerFanAnimationController controller, Transform cameraTransform)
        {
            if (animationControl != null) animationControl.BeforeAutomaticReturn -= ProcessBufferedAction;
            animationControl = controller;
            view = cameraTransform;
            if (isActiveAndEnabled && animationControl != null)
                animationControl.BeforeAutomaticReturn += ProcessBufferedAction;
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
            }

            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
            Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            inputDirection = direction;
            ValidatePendingAction();
            if (typing || animationControl.IsPaused) ClearPendingAction();
            else if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetToSpawn();
            else
            {
                CollectActionInput(keyboard, mouse);
                TryBufferedAction(direction);
            }
            bool sprint = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            IsMoving = direction.sqrMagnitude > 0.01f && animationControl.CanMove &&
                (actionExecutedFrame != Time.frameCount || animationControl.IsEquipmentTransition);
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

        private void ProcessBufferedAction()
        {
            if (animationControl == null) return;
            if (Test_PlayerTestUi.IsTyping || animationControl.IsPaused) ClearPendingAction();
            else TryBufferedAction(inputDirection);
        }

        private Vector3 GetDodgeDisplacement(Vector3 inputDirection, float deltaTime)
        {
            if (animationControl.IsPaused || deltaTime <= 0f) return Vector3.zero;

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                // 배속이나 재생 구간을 바꿔도 원본 프레임을 기준으로 일어나는 구간을 구분한다.
                float frame = Mathf.Max(animationControl.GetPlaybackStartFrame(4),
                    animationControl.NormalizedTime * animationControl.GetPlaybackMaximumFrame(4));
                float steering = frame >= dodgeRecoveryStartFrame ? dodgeRecoverySteering : dodgeSteering;
                Vector3 targetDirection = Vector3.Lerp(dodgeStartDirection, inputDirection.normalized,
                    Mathf.Clamp01(steering) * inputDirection.magnitude).normalized;
                if (targetDirection.sqrMagnitude > 0.01f)
                {
                    // 회복 중 반대 방향을 입력해도 위아래로 기울지 않고 수평으로 방향을 바꾼다.
                    dodgeDirection = Quaternion.Slerp(Quaternion.LookRotation(dodgeDirection),
                        Quaternion.LookRotation(targetDirection), 1f - Mathf.Exp(-rotationSpeed * deltaTime)) * Vector3.forward;
                }
            }
            transform.rotation = Quaternion.LookRotation(dodgeDirection);

            float nextProgress = Mathf.Clamp01(dodgeProgress + deltaTime / Mathf.Max(0.01f, animationControl.RollDuration));
            float power = Mathf.Max(1f, dodgeDecelerationPower);
            // 감속 곡선의 누적 거리 차이를 사용해 프레임 수나 재생 속도가 달라도 총 거리를 유지한다.
            float distance = dodgeDistance * (Mathf.Pow(1f - dodgeProgress, power) - Mathf.Pow(1f - nextProgress, power));
            dodgeProgress = nextProgress;
            return dodgeDirection * distance;
        }

        private int NextComboAction => animationControl.SelectedState == "ATTACK1" ? 2 : animationControl.SelectedState == "ATTACK2" ? 3 : 1;

        private void CollectActionInput(Keyboard keyboard, Mouse mouse)
        {
            frameAction = 0;
            if (keyboard != null)
            {
                if (keyboard.digit4Key.wasPressedThisFrame) ConsiderAction(HitAction);
                if (keyboard.spaceKey.wasPressedThisFrame) ConsiderAction(DodgeAction);
                if (keyboard.digit1Key.wasPressedThisFrame) ConsiderAction(1);
                if (keyboard.digit2Key.wasPressedThisFrame) ConsiderAction(2);
                if (keyboard.digit3Key.wasPressedThisFrame) ConsiderAction(3);
                if (keyboard.jKey.wasPressedThisFrame) ConsiderAction(NextComboAction);
                if (keyboard.fKey.wasPressedThisFrame) ConsiderAction(EquipmentAction);
            }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUi && mouse != null && mouse.leftButton.wasPressedThisFrame) ConsiderAction(NextComboAction);
            if (frameAction != 0) QueueAction(frameAction);
        }

        private void ConsiderAction(int action)
        {
            // 키보드와 마우스를 모두 모은 뒤 유효한 요청 하나만 선택한다. 같은 급은 먼저 받은 요청을 유지한다.
            if (SecondsUntilAction(action) <= inputBufferSeconds && ActionPriority(action) > ActionPriority(frameAction))
                frameAction = action;
        }

        private static int ActionPriority(int action)
        {
            return action == HitAction ? 4 : action == DodgeAction ? 3 : action > 0 ? 2 : action == EquipmentAction ? 1 : 0;
        }

        private bool IsDuplicateAction(int action)
        {
            return action == DodgeAction ? animationControl.IsStatePlaying("ROLL") :
                action > 0 && animationControl.IsStatePlaying("ATTACK" + action);
        }

        private float SecondsUntilAction(int action)
        {
            if (action == HitAction) return animationControl.IsStatePlaying("HIT") ? float.PositiveInfinity : 0f;
            if (action == DodgeAction) return animationControl.SecondsUntilDodgeCancel();
            if (action == EquipmentAction) return animationControl.SecondsUntilEquipmentCancel();
            return action > 0 && action <= 3 ? animationControl.SecondsUntilAttackCancel(action) : float.PositiveInfinity;
        }

        private void QueueAction(int action)
        {
            if (SecondsUntilAction(action) > inputBufferSeconds) return;
            ValidatePendingAction();
            if (pendingAction != 0 && ActionPriority(action) <= ActionPriority(pendingAction)) return;
            pendingAction = action;
            pendingSequence = animationControl.PlaybackSequence;
        }

        private void TryBufferedAction(Vector3 direction)
        {
            ValidatePendingAction();
            if (pendingAction == 0 || actionExecutedFrame == Time.frameCount) return;
            bool accepted;
            if (pendingAction == HitAction)
            {
                animationControl.PlayHit();
                accepted = true;
            }
            else if (pendingAction == DodgeAction)
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
            else accepted = pendingAction == EquipmentAction ? animationControl.TryToggleEquipment() : animationControl.TryPlayAttack(pendingAction);
            if (accepted)
            {
                actionExecutedFrame = Time.frameCount;
                ClearPendingAction();
            }
        }

        private void ValidatePendingAction()
        {
            if (pendingAction != 0 && (pendingSequence != animationControl.PlaybackSequence ||
                animationControl.IsPaused || IsDuplicateAction(pendingAction))) ClearPendingAction();
        }

        private void ClearPendingAction()
        {
            pendingAction = 0;
        }

        private void OnDisable()
        {
            if (animationControl != null) animationControl.BeforeAutomaticReturn -= ProcessBufferedAction;
            ClearPendingAction();
        }

        private void OnEnable()
        {
            if (animationControl != null) animationControl.BeforeAutomaticReturn += ProcessBufferedAction;
        }

        public void ResetToSpawn()
        {
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            body.enabled = true;
            verticalSpeed = 0f;
            ClearPendingAction();
            dodgeProgress = 0f;
            animationControl.PlayIdle();
        }
    }
}
