using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Prototype.MountainPass
{
    // 이동을 유지하는 왼손 상반신 레이어와 회복 횟수를 함께 관리한다.
    [DefaultExecutionOrder(10)]
    public class PassGourdDrinking : MonoBehaviour
    {
        public enum DrinkingPhase { Holstered, Drawing, Drinking, Stowing, Returning }

        [InspectorName("플레이어 동작 제어기")]
        [SerializeField] private PassAnimationController animationControl;
        [InspectorName("플레이어 이동 제어기")]
        [SerializeField] private PassPlayerMovement movement;
        [InspectorName("생명력 제어기")]
        [SerializeField] private PassHealth health;
        [InspectorName("게임 진행 제어기")]
        [SerializeField] private PassGameFlow game;
        [InspectorName("호리병 장착 제어기")]
        [SerializeField] private PassGourdAttachment attachment;
        [InspectorName("호리병 꺼내기 원본 애니메이션")]
        [SerializeField] private AnimationClip drawClip;
        [InspectorName("마시기 원본 애니메이션")]
        [SerializeField] private AnimationClip drinkingClip;
        [Header("회복 수치")]
        [InspectorName("호리병 최대 사용 횟수")]
        [SerializeField, Min(1)] private int maximumUses = 5;
        [InspectorName("시작 시 호리병 사용 횟수")]
        [SerializeField, Min(0)] private int startingUses = 3;
        [InspectorName("호리병 1회 회복량")]
        [SerializeField, Min(0f)] private float healAmount = 40f;
        [Header("꺼내기 · 입에 대기 · 수납 재생 속도")]
        [InspectorName("호리병 동작 기본 배속")]
        [SerializeField, Min(0.1f)] private float playbackSpeed = 2f;
        [InspectorName("마시기 준비 구간 배속 비율")]
        [SerializeField, Min(0.1f)] private float preparationSpeedMultiplier = 0.75f;
        [InspectorName("실제 마시기·회복 시작 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float sippingStartProgress = 0.31f;
        [InspectorName("실제 마시기 종료 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float sippingEndProgress = 0.68f;
        [InspectorName("실제 마시기 구간 배속")]
        [SerializeField, Min(0.1f)] private float sippingSpeed = 1f;
        [Header("왼손 발도 구간 · 원본 프레임")]
        [InspectorName("호리병 꺼내기 시작 프레임")]
        [SerializeField, Min(0)] private int drawStartFrame;
        [InspectorName("호리병 꺼내기 종료 프레임")]
        [SerializeField, Min(1)] private int drawEndFrame = 22;
        [InspectorName("호리병을 잡는 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float grabProgress = 0.7f;
        [InspectorName("호리병 꺼내기 기준 시간 (초)")]
        [SerializeField, Min(0.1f)] private float drawDuration = 0.85f;
        [Header("마시기와 수납")]
        [InspectorName("마시기 원본 배속")]
        [SerializeField, Min(0.1f)] private float drinkingSpeed = 1f;
        [InspectorName("호리병 수납 기준 시간 (초)")]
        [SerializeField, Min(0.1f)] private float stowDuration = 0.85f;
        [InspectorName("피격·회복 동작 혼합 시간 (초)")]
        [SerializeField, Range(0.05f, 0.6f)] private float transitionSeconds = 0.3f;
        [InspectorName("꺼내기에서 마시기로 추가 혼합 시간 (초)")]
        [SerializeField, Min(0f)] private float drawToDrinkExtraTransitionSeconds = 0.45f;
        [InspectorName("호리병 동작 복귀 시간 (초)")]
        [SerializeField, Range(0.05f, 0.5f)] private float returnSeconds = 0.2f;

        private int layerIndex = -1;
        private float phaseTime;
        private bool sipApplied;
        private Animator Controller => animationControl != null ? animationControl.CharacterAnimator : null;
        public int RemainingUses { get; private set; }
        public int MaximumUses => maximumUses;
        public float HealAmount => healAmount;
        public DrinkingPhase Phase { get; private set; }
        public bool IsPlaying => Phase != DrinkingPhase.Holstered;
        public float Progress => Mathf.Clamp01(phaseTime / PhaseDuration);
        public bool IsSipping => Phase == DrinkingPhase.Drinking && Progress >= sippingStartProgress && Progress < sippingEndProgress;
        public float PlaybackSpeed => Mathf.Max(0.1f, (IsSipping ? sippingSpeed : playbackSpeed) *
            (Phase == DrinkingPhase.Drawing || Phase == DrinkingPhase.Drinking && Progress < sippingStartProgress ? preparationSpeedMultiplier : 1f));
        public float SipWeight => Phase == DrinkingPhase.Drinking ?
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, sippingStartProgress, Progress)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sippingEndProgress, 0.88f, Progress))) : 0f;
        public float ReachWeight => Phase == DrinkingPhase.Drawing ?
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, grabProgress - 0.08f, Progress)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grabProgress, 1f, Progress))) :
            Phase == DrinkingPhase.Stowing ? Mathf.Sin(Progress * Mathf.PI) : 0f;
        private float PhaseDuration => Phase switch
        {
            DrinkingPhase.Drawing => Mathf.Max(0.1f, drawDuration),
            DrinkingPhase.Drinking => drinkingClip == null ? 1f : drinkingClip.length / Mathf.Max(0.1f, drinkingSpeed),
            DrinkingPhase.Stowing => Mathf.Max(0.1f, stowDuration),
            DrinkingPhase.Returning => Mathf.Max(0.05f, returnSeconds),
            _ => 1f
        };

        public void Configure(PassAnimationController animation, PassPlayerMovement player, PassHealth vitality,
            PassGameFlow flow, PassGourdAttachment prop, AnimationClip draw, AnimationClip drink)
        { animationControl = animation; movement = player; health = vitality; game = flow; attachment = prop; drawClip = draw; drinkingClip = drink; }

        private void Awake() => RemainingUses = Mathf.Clamp(startingUses, 0, maximumUses);
        private void OnEnable()
        {
            if (animationControl != null) animationControl.StateChanging += OnStateChanging;
            if (health != null) health.Damaged += OnDamaged;
        }
        private void Start()
        {
            layerIndex = Controller != null ? Controller.GetLayerIndex("GourdUpperBody") : -1;
            if (layerIndex < 0 || attachment == null || drawClip == null || drinkingClip == null)
            { Debug.LogError("호리병 레이어와 마시기 에셋 연결을 확인하세요.", this); enabled = false; return; }
            CancelDrinking();
        }
        private void Update()
        {
            if (game == null || !game.IsRunning || animationControl.IsPaused) return;
            if (!PassHud.IsTyping && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) TryDrink();
            if (!IsPlaying) return;
            if (!movement.InputEnabled || animationControl.IsActionPlaying) { CancelDrinking(); return; }
            AdvancePlayback(Time.deltaTime);
        }

        public bool TryDrink()
        {
            if (layerIndex < 0 || !isActiveAndEnabled || IsPlaying || game == null || !game.IsRunning ||
                !movement.InputEnabled || animationControl.IsPaused || animationControl.IsActionPlaying || health.IsDead) return false;
            if (RemainingUses <= 0) { game.ShowNotice("호리병이 비었습니다 · 산신각에서 충전하세요", 2f); return false; }
            if (health.Current >= health.Maximum) { game.ShowNotice("생명력이 가득 차 있습니다", 1.5f); return false; }
            sipApplied = false;
            BeginPhase(DrinkingPhase.Drawing);
            return true;
        }

        private void AdvancePlayback(float seconds)
        {
            phaseTime += Mathf.Max(0f, seconds) * PlaybackSpeed;
            float progress = Progress;
            if (Phase == DrinkingPhase.Drawing)
            {
                Controller.SetLayerWeight(layerIndex, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / 0.18f)));
                SetDrawFrame("GourdDrawTime", progress);
                if (progress >= grabProgress) attachment.SetInHand(true);
            }
            else if (Phase == DrinkingPhase.Drinking)
            {
                Controller.SetFloat("GourdDrinkTime", progress);
                // 실제로 입에 대는 시점에 한 번만 차감한다. 이전에 피격·회피로 취소되면 사용하지 않는다.
                if (!sipApplied && progress >= sippingStartProgress)
                {
                    sipApplied = true;
                    if (!health.IsDead && RemainingUses > 0) { RemainingUses--; health.Heal(healAmount); }
                }
            }
            else if (Phase == DrinkingPhase.Stowing)
            {
                SetDrawFrame("GourdStowTime", 1f - progress);
                if (progress >= 1f - grabProgress) attachment.SetInHand(false);
            }
            else if (Phase == DrinkingPhase.Returning)
                Controller.SetLayerWeight(layerIndex, 1f - Mathf.SmoothStep(0f, 1f, progress));
            if (phaseTime < PhaseDuration) return;
            if (Phase == DrinkingPhase.Drawing) BeginPhase(DrinkingPhase.Drinking);
            else if (Phase == DrinkingPhase.Drinking) BeginPhase(DrinkingPhase.Stowing);
            else if (Phase == DrinkingPhase.Stowing) BeginPhase(DrinkingPhase.Returning);
            else CancelDrinking();
        }

        private void BeginPhase(DrinkingPhase phase)
        {
            Phase = phase; phaseTime = 0f;
            if (phase == DrinkingPhase.Drawing)
            {
                SetDrawFrame("GourdDrawTime", 0f);
                Controller.Play(Animator.StringToHash("GourdUpperBody.GOURD_DRAW"), layerIndex, 0f);
                Controller.SetLayerWeight(layerIndex, 0f);
            }
            else if (phase == DrinkingPhase.Drinking)
            {
                Controller.SetFloat("GourdDrinkTime", 0f);
                Controller.CrossFadeInFixedTime(Animator.StringToHash("GourdUpperBody.GOURD_DRINK"),
                    transitionSeconds / PlaybackSpeed + drawToDrinkExtraTransitionSeconds, layerIndex, 0f);
                attachment.SetInHand(true);
            }
            else if (phase == DrinkingPhase.Stowing)
            {
                SetDrawFrame("GourdStowTime", 1f);
                Controller.CrossFadeInFixedTime(Animator.StringToHash("GourdUpperBody.GOURD_STOW"), transitionSeconds / PlaybackSpeed, layerIndex, 0f);
            }
        }
        private void SetDrawFrame(string parameter, float progress) => Controller.SetFloat(parameter,
            drawClip == null ? 0f : Mathf.Lerp(drawStartFrame, drawEndFrame, progress) / (drawClip.frameRate * drawClip.length));
        private void OnStateChanging(string from, string to, float seconds)
        {
            if (to != "SHEATHED_IDLE" && to != "DRAWN_IDLE" && to != "RUN2") CancelDrinking();
        }
        private void OnDamaged(float damage) => CancelDrinking();
        public void CancelDrinking()
        {
            Phase = DrinkingPhase.Holstered; phaseTime = 0f;
            if (layerIndex >= 0 && Controller != null && Controller.isInitialized) Controller.SetLayerWeight(layerIndex, 0f);
            if (attachment != null) attachment.SetInHand(false, true);
        }
        public void ResetForJourney() { CancelDrinking(); RemainingUses = Mathf.Clamp(startingUses, 0, maximumUses); }
        public void Refill() { CancelDrinking(); RemainingUses = maximumUses; }
        private void OnDisable()
        {
            if (animationControl != null) animationControl.StateChanging -= OnStateChanging;
            if (health != null) health.Damaged -= OnDamaged;
            CancelDrinking();
        }
        private void OnValidate()
        { maximumUses = Mathf.Max(1, maximumUses); startingUses = Mathf.Clamp(startingUses, 0, maximumUses); }
    }
}
