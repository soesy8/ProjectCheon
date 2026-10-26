using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Cheon.Tests.PlayerReactions
{
    // 기존 미리보기 그래프의 상반신 레이어만 조절해 이동·피격 재생과 출력을 공유한다.
    public class Test_GourdDrinking : MonoBehaviour
    {
        public enum DrinkingPhase { Holstered, Drawing, Drinking, Stowing, Returning }

        [SerializeField] private Test_PlayerReactionPreview preview;
        [SerializeField] private Test_GourdAttachment attachment;
        [SerializeField] private AnimationClip drawClip;
        [SerializeField] private AnimationClip drinkingClip;
        [Header("꺼내기 · 입에 대기 · 수납 재생 속도")]
        [SerializeField, Min(0.1f)] private float playbackSpeed = 2f;
        [Header("입에 댄 뒤 마시는 구간 · 진행률 / 배속")]
        [SerializeField, Range(0f, 1f)] private float sippingStartProgress = 0.31f;
        [SerializeField, Range(0f, 1f)] private float sippingEndProgress = 0.68f;
        [SerializeField, Min(0.1f)] private float sippingSpeed = 1f;
        [Header("왼손 발도 구간 · 원본 프레임")]
        [SerializeField, Min(0)] private int drawStartFrame;
        [SerializeField, Min(1)] private int drawEndFrame = 22;
        [SerializeField, Range(0f, 1f)] private float grabProgress = 0.7f;
        [SerializeField, Min(0.1f)] private float drawDuration = 0.85f;
        [Header("마시기와 수납")]
        [SerializeField, Min(0.1f)] private float drinkingSpeed = 1f;
        [SerializeField, Min(0.1f)] private float stowDuration = 0.85f;
        [SerializeField, Range(0.05f, 0.6f)] private float transitionSeconds = 0.3f;
        [SerializeField, Range(0.05f, 0.5f)] private float returnSeconds = 0.2f;

        private int layerIndex = -1;
        private float phaseTime;
        public DrinkingPhase Phase { get; private set; }
        public bool IsPlaying => Phase != DrinkingPhase.Holstered;
        public bool IsSipping => Phase == DrinkingPhase.Drinking && Progress >= sippingStartProgress && Progress < sippingEndProgress;
        // 병을 올리고 내리는 구간은 빠르게 유지하고, 입에 댄 구간에서만 원래 배속을 사용한다.
        public float PlaybackSpeed => Mathf.Max(0.1f, IsSipping ? sippingSpeed : playbackSpeed);
        public float Progress => Mathf.Clamp01(phaseTime / PhaseDuration);
        public float SipWeight => Phase == DrinkingPhase.Drinking ?
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, sippingStartProgress, Progress)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sippingEndProgress, 0.88f, Progress))) : 0f;
        public float ReachWeight => Phase == DrinkingPhase.Drawing ?
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, grabProgress - 0.08f, Progress)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grabProgress, 1f, Progress))) :
            Phase == DrinkingPhase.Stowing ? Mathf.Sin(Progress * Mathf.PI) : 0f;
        public string Status => Phase switch
        {
            DrinkingPhase.Drawing => "왼손으로 허리 뒤의 호리병 꺼내기",
            DrinkingPhase.Drinking => "왼손으로 마시는 중 · 하반신·오른팔은 이동 유지",
            DrinkingPhase.Stowing => "호리병을 허리춤에 다시 넣기",
            DrinkingPhase.Returning => "이동·대기 자세로 복귀",
            _ => "허리춤 장착 · G 또는 ‘호리병 마시기’"
        };
        private float PhaseDuration => Phase switch
        {
            DrinkingPhase.Drawing => Mathf.Max(0.1f, drawDuration),
            DrinkingPhase.Drinking => drinkingClip == null ? 1f : drinkingClip.length / Mathf.Max(0.1f, drinkingSpeed),
            DrinkingPhase.Stowing => Mathf.Max(0.1f, stowDuration),
            DrinkingPhase.Returning => Mathf.Max(0.05f, returnSeconds),
            _ => 1f
        };
        private AnimatorControllerPlayable Controller => preview.AnimationController;

        public void Configure(Test_PlayerReactionPreview target, Test_GourdAttachment prop, AnimationClip draw, AnimationClip drink)
        { preview = target; attachment = prop; drawClip = draw; drinkingClip = drink; }

        private void Start()
        {
            if (preview == null || attachment == null || !Controller.IsValid()) { enabled = false; return; }
            for (int index = 0; index < Controller.GetLayerCount(); index++)
                if (Controller.GetLayerName(index) == "GourdUpperBody") layerIndex = index;
            if (layerIndex < 0) { Debug.LogError("호리병 상반신 레이어가 없습니다.", this); enabled = false; return; }
            preview.BeforePoseEvaluation += AdvancePlayback;
            preview.PreviewReset += CancelDrinking;
            CancelDrinking();
        }

        private void Update()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponentInParent<TMP_InputField>() is TMP_InputField field && field.isFocused) return;
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) PlayDrinking();
        }

        public void PlayDrinking()
        {
            if (layerIndex < 0 || IsPlaying) return;
            if (preview.Phase != Test_PlayerReactionPreview.PreviewPhase.Idle &&
                preview.Phase != Test_PlayerReactionPreview.PreviewPhase.Moving &&
                preview.Phase != Test_PlayerReactionPreview.PreviewPhase.Complete) return;
            if (preview.AnimatorState != "IDLE") return;
            preview.SetPaused(false);
            preview.repeatSequence = false;
            BeginPhase(DrinkingPhase.Drawing);
        }

        private void AdvancePlayback(float seconds)
        {
            if (!IsPlaying || !Controller.IsValid()) return;
            // 피격·기상·회피가 시작되면 테스트 동작을 종료하고 기존 반응을 우선한다.
            if (preview.Phase == Test_PlayerReactionPreview.PreviewPhase.HeavyHit ||
                preview.Phase == Test_PlayerReactionPreview.PreviewPhase.DownHold ||
                preview.Phase == Test_PlayerReactionPreview.PreviewPhase.GetUp ||
                preview.Phase == Test_PlayerReactionPreview.PreviewPhase.Dodging ||
                preview.Phase == Test_PlayerReactionPreview.PreviewPhase.Dead)
            { CancelDrinking(); return; }
            // 단계별 타이밍과 고개·손 보정은 동일한 진행률을 공유하고, 이동 모션은 원래 속도를 유지한다.
            phaseTime += Mathf.Max(0f, seconds) * PlaybackSpeed;
            float progress = Progress;
            if (Phase == DrinkingPhase.Drawing)
            {
                Controller.SetLayerWeight(layerIndex, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / 0.18f)));
                SetDrawFrame("GourdDrawTime", progress);
                if (progress >= grabProgress) attachment.SetInHand(true);
            }
            else if (Phase == DrinkingPhase.Drinking) Controller.SetFloat("GourdDrinkTime", progress);
            else if (Phase == DrinkingPhase.Stowing)
            {
                SetDrawFrame("GourdStowTime", 1f - progress);
                if (progress >= 1f - grabProgress) attachment.SetInHand(false);
            }
            else if (Phase == DrinkingPhase.Returning) Controller.SetLayerWeight(layerIndex, 1f - Mathf.SmoothStep(0f, 1f, progress));
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
                Controller.CrossFadeInFixedTime(Animator.StringToHash("GourdUpperBody.GOURD_DRINK"), transitionSeconds / PlaybackSpeed, layerIndex, 0f);
                attachment.SetInHand(true);
            }
            else if (phase == DrinkingPhase.Stowing)
            {
                SetDrawFrame("GourdStowTime", 1f);
                Controller.CrossFadeInFixedTime(Animator.StringToHash("GourdUpperBody.GOURD_STOW"), transitionSeconds / PlaybackSpeed, layerIndex, 0f);
            }
        }

        private void SetDrawFrame(string parameter, float progress)
        {
            float frame = Mathf.Lerp(drawStartFrame, drawEndFrame, progress);
            Controller.SetFloat(parameter, drawClip == null ? 0f : frame / (drawClip.frameRate * drawClip.length));
        }

        public void CancelDrinking()
        {
            Phase = DrinkingPhase.Holstered; phaseTime = 0f;
            if (layerIndex >= 0 && Controller.IsValid()) Controller.SetLayerWeight(layerIndex, 0f);
            if (attachment != null) attachment.SetInHand(false, true);
        }

        private void OnDisable()
        {
            if (preview == null) return;
            preview.BeforePoseEvaluation -= AdvancePlayback;
            preview.PreviewReset -= CancelDrinking;
            CancelDrinking();
        }
    }
}
