using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cheon.Prototype.MountainPass
{
    [RequireComponent(typeof(CharacterController))]
    public class PassPlayerMovement : MonoBehaviour
    {
        [InspectorName("플레이어 동작 제어기")]
        [SerializeField] private PassAnimationController animationControl;
        [InspectorName("게임 카메라 제어기")]
        [SerializeField] private Transform view;
        [InspectorName("기본 이동 속도 (m/초)")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [InspectorName("달리기 이동 속도 (m/초)")]
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6f;
        [InspectorName("구르기 이동 거리 (m)")]
        [SerializeField, Min(0.1f)] private float dodgeDistance = 7.5f;
        [Tooltip("1은 등속 이동, 2 이상은 초반에 빠르게 이동한 뒤 끝에서 감속합니다.")]
        [InspectorName("구르기 감속 강도")]
        [SerializeField, Range(1f, 4f)] private float dodgeDecelerationPower = 2f;
        [Tooltip("구르기 시작 방향에 이동 입력을 섞는 비율입니다. 0은 방향 고정, 1은 완전 조작입니다.")]
        [InspectorName("구르기 중 방향 조작 비율 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float dodgeSteering = 0.45f;
        [Tooltip("다 구르고 일어나는 구간에서 이동 입력을 반영하는 비율입니다.")]
        [InspectorName("구르기 기상 중 방향 조작 비율 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float dodgeRecoverySteering = 0.75f;
        [Tooltip("일어나는 구간이 시작되는 원본 구르기 클립의 프레임입니다.")]
        [InspectorName("구르기 기상 시작 프레임")]
        [SerializeField, Min(0)] private int dodgeRecoveryStartFrame = 22;
        [InspectorName("플레이어 회전 반응 속도")]
        [SerializeField, Min(0.1f)] private float rotationSpeed = 14f;
        [InspectorName("중력 가속도 (m/초²)")]
        [SerializeField] private float gravity = -24f;
        [Tooltip("각 동작의 캔슬 시점에 도달하기 전 예약 입력을 받는 시간(초)입니다.")]
        [InspectorName("예약 입력 허용 시간 (초)")]
        [SerializeField, Range(0f, 0.6f)] private float inputBufferSeconds = 0.15f;
        [InspectorName("낙하 복귀 높이 (m)")]
        [SerializeField] private float respawnBelow = -30f;

        private CharacterController body;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private const int HitAction = -3, DodgeAction = -2;
        private float verticalSpeed;
        private int pendingAction, frameAction, actionExecutedFrame = -1;
        private uint pendingSequence;
        private Vector3 inputDirection;
        private float dodgeProgress;
        private Vector3 dodgeStartDirection;
        private Vector3 dodgeDirection;
        private PassPlayerEnergy energy;
        private bool sprintExhausted;
        private bool touchControlsEnabled, touchSprint, touchAttackRequested, touchDodgeRequested;
        private Vector2 touchMovement;

        public void SetTouchControlsEnabled(bool enabled) { touchControlsEnabled = enabled; ClearTouchInput(); }
        public void SetTouchMovement(Vector2 input, bool sprint) { touchMovement = Vector2.ClampMagnitude(input, 1f); touchSprint = sprint; }
        public void RequestTouchAttack() { if (touchControlsEnabled && InputEnabled) touchAttackRequested = true; }
        public void RequestTouchDodge() { if (touchControlsEnabled && InputEnabled) touchDodgeRequested = true; }
        public void ClearTouchInput() { touchMovement = Vector2.zero; touchSprint = touchAttackRequested = touchDodgeRequested = false; }

        public PassAnimationController AnimationControl => animationControl;
        public CharacterController Body => body != null ? body : body = GetComponent<CharacterController>();
        public bool InputEnabled { get; set; } = true;
        public Vector3 InputDirection => InputEnabled && !PassHud.IsTyping ? inputDirection : Vector3.zero;
        public event System.Action Fell;
        public bool IsMoving { get; private set; }
        public bool IsSprinting { get; private set; }
        public string BufferedAction => pendingAction == DodgeAction ? "회피" : pendingAction > 0 ? "공격 " + pendingAction : "없음";

        public void Configure(PassAnimationController controller, Transform cameraTransform)
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
            energy = GetComponent<PassPlayerEnergy>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (animationControl == null || view == null || !InputEnabled || Time.timeScale <= 0f)
            { IsMoving = IsSprinting = false; inputDirection = Vector3.zero; ClearTouchInput(); return; }
            var keyboard = touchControlsEnabled ? null : Keyboard.current;
            var mouse = touchControlsEnabled ? null : Mouse.current;
            bool typing = PassHud.IsTyping;
            Vector2 input = typing ? Vector2.zero : touchMovement;
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
            // 강피격 중 회피 입력은 일어나기에만 소비해 같은 키로 곧바로 구르지 않도록 한다.
            bool recovering = !typing && animationControl.TryRecoverFromHeavy(direction.sqrMagnitude > 0.01f,
                touchDodgeRequested || keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
            bool sprintInput = touchSprint || keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            bool sprint = sprintInput;
            if (!sprint || direction.sqrMagnitude <= 0.01f) sprintExhausted = false;
            if (energy != null && !energy.CanSprint) sprintExhausted = true;
            sprint &= !typing && !sprintExhausted && (energy == null || energy.CanSprint);
            IsSprinting = sprint && direction.sqrMagnitude > 0.01f;
            ValidatePendingAction();
            if (typing || animationControl.IsPaused || recovering) ClearPendingAction();
            else
            {
                CollectActionInput(keyboard, mouse);
                TryBufferedAction(direction);
            }
            touchAttackRequested = touchDodgeRequested = false;
            // 달리기 키를 누르면 정지 중에도 납도한다. 진행 중인 공격은 끝까지 재생한다.
            if (!typing && sprintInput && animationControl.IsCombatRequested && !animationControl.IsActionPlaying &&
                actionExecutedFrame != Time.frameCount && animationControl.CanChangeEquipment)
                animationControl.ExitCombat();
            IsMoving = direction.sqrMagnitude > 0.01f && animationControl.CanMove &&
                (actionExecutedFrame != Time.frameCount || animationControl.IsEquipmentTransition);
            IsSprinting = sprint && IsMoving;
            if (IsSprinting && energy != null) IsSprinting = sprint = energy.SpendSprint(Time.deltaTime, direction);
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
            if (transform.position.y < respawnBelow) { ResetToSpawn(); Fell?.Invoke(); }
        }

        private void ProcessBufferedAction()
        {
            if (animationControl == null || !InputEnabled) return;
            if (PassHud.IsTyping || animationControl.IsPaused) ClearPendingAction();
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
            if (touchDodgeRequested) ConsiderAction(DodgeAction);
            if (touchAttackRequested) ConsiderAction(NextComboAction);
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) ConsiderAction(DodgeAction);
                if (keyboard.digit1Key.wasPressedThisFrame) ConsiderAction(1);
                if (keyboard.digit2Key.wasPressedThisFrame) ConsiderAction(2);
                if (keyboard.digit3Key.wasPressedThisFrame) ConsiderAction(3);
                if (keyboard.jKey.wasPressedThisFrame) ConsiderAction(NextComboAction);
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
            return action == HitAction ? 4 : action == DodgeAction ? 3 : action > 0 ? 2 : 0;
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
            return action > 0 && action <= 3 ? animationControl.SecondsUntilAttackInput(action) : float.PositiveInfinity;
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
                if (energy != null && !energy.CanDodge) { ClearPendingAction(); return; }
                accepted = animationControl.TryPlayDodge();
                if (accepted)
                {
                    if (energy != null) energy.SpendDodge();
                    dodgeDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
                    dodgeStartDirection = dodgeDirection;
                    dodgeProgress = 0f;
                    transform.rotation = Quaternion.LookRotation(dodgeDirection);
                }
            }
            else accepted = animationControl.TryAttackInput(pendingAction, direction.sqrMagnitude > 0.01f, IsSprinting);
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
            ClearTouchInput();
        }

        private void OnEnable()
        {
            if (animationControl != null) animationControl.BeforeAutomaticReturn += ProcessBufferedAction;
        }

        public void ResetToSpawn()
        {
            Teleport(spawnPosition, spawnRotation);
        }

        public void SetCheckpoint(Vector3 position, Quaternion rotation)
        {
            spawnPosition = position;
            spawnRotation = rotation;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (body == null) body = GetComponent<CharacterController>();
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            verticalSpeed = 0f;
            sprintExhausted = false;
            IsMoving = IsSprinting = false;
            ClearPendingAction();
            dodgeProgress = 0f;
            animationControl.ResetReactions();
            animationControl.PlayIdle();
        }
    }
}

