using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Testing.PlayerFan
{
    // 입력 프레임부터 상태를 섞고 실제 클립 진행률로 부채 연출을 동기화한다.
    public class Test_PlayerFanAnimationController : MonoBehaviour
    {
        [Header("재생 대상")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Test_PlayerFanAttachment fanAttachment;
        [Header("공격별 속도 배율 — 재생 중 변경 가능")]
        [Min(0.05f)] public float attack1Speed = 1f;
        [Min(0.05f)] public float attack2Speed = 1f;
        [Min(0.05f)] public float attack3Speed = 1f;
        [Header("공격별 재생 구간 — 원본 FBX의 0부터 시작하는 프레임")]
        [SerializeField] private AnimationClip attack1Clip;
        [Min(0)] public int attack1StartFrame;
        [Min(1)] public int attack1EndFrame = 68;
        [SerializeField] private AnimationClip attack2Clip;
        [Min(0)] public int attack2StartFrame;
        [Min(1)] public int attack2EndFrame = 95;
        [SerializeField] private AnimationClip attack3Clip;
        [Min(0)] public int attack3StartFrame;
        [Min(1)] public int attack3EndFrame = 75;
        [Header("부드러운 전환 — 입력 즉시 전환 시작")]
        [SerializeField, Range(0.01f, 0.3f)] private float transitionDuration = 0.09f;
        [SerializeField, Range(0.01f, 0.4f)] private float returnBlendDuration = 0.18f;
        [SerializeField, Range(0.01f, 0.4f)] private float locomotionBlendDuration = 0.12f;
        [Header("발도 / 납도")]
        [SerializeField, Min(0.05f)] private float drawSpeed = 1f;
        [SerializeField, Min(0.05f)] private float sheatheSpeed = 1f;
        [SerializeField, Min(0.01f)] private float equipmentClipLength = 1.2333333f;
        [SerializeField, Range(0f, 1f)] private float drawGrabTime = 0.48f;
        [SerializeField, Range(0f, 1f)] private float drawOpenStart = 0.62f;
        [SerializeField, Range(0f, 1f)] private float drawOpenEnd = 0.94f;
        [SerializeField, Range(0f, 1f)] private float sheatheCloseStart = 0.06f;
        [SerializeField, Range(0f, 1f)] private float sheatheCloseEnd = 0.38f;
        [SerializeField, Range(0f, 1f)] private float sheatheStowTime = 0.52f;
        [Header("테스트 옵션")]
        [SerializeField] private bool repeatSelectedAnimation;
        [SerializeField] private bool enableKeyboardShortcuts = true;

        private string selectedState = "SHEATHED_IDLE";
        private bool combatRequested, running, paused;
        private static readonly int Attack1Speed = Animator.StringToHash("Attack1Speed");
        private static readonly int Attack2Speed = Animator.StringToHash("Attack2Speed");
        private static readonly int Attack3Speed = Animator.StringToHash("Attack3Speed");
        private static readonly int DrawSpeed = Animator.StringToHash("DrawSpeed");
        private static readonly int SheatheSpeed = Animator.StringToHash("SheatheSpeed");
        private static readonly int MoveAmount = Animator.StringToHash("MoveAmount");

        public Animator CharacterAnimator => characterAnimator;
        public string SelectedState => selectedState;
        public bool IsPaused => paused;
        public bool IsCombatRequested => combatRequested;
        public bool IsRunning => running;
        public bool CanAttack => combatRequested;
        public bool IsEquipmentTransition => selectedState == "DRAW" || selectedState == "SHEATHE";
        public bool CanPreviewFan => combatRequested && !IsEquipmentTransition;
        public bool RepeatSelectedAnimation { get => repeatSelectedAnimation; set => repeatSelectedAnimation = value; }
        public float NormalizedTime => TryGetSelectedState(out var state) ? state.normalizedTime : 0f;
        public float EquipmentProgress => Mathf.Clamp01(selectedState == "SHEATHE" ? 1f - NormalizedTime : NormalizedTime);
        public float PlaybackProgress
        {
            get
            {
                var clip = AttackClip(selectedState);
                if (clip == null) return IsEquipmentTransition ? EquipmentProgress : Mathf.Repeat(NormalizedTime, 1f);
                int index = selectedState == "ATTACK1" ? 1 : selectedState == "ATTACK2" ? 2 : 3;
                return Mathf.InverseLerp(GetAttackStartFrame(index), GetAttackEndFrame(index), NormalizedTime * clip.length * clip.frameRate);
            }
        }

        public void Configure(Animator animator, Test_PlayerFanAttachment attachment, float clipLength = 1.2333333f)
        {
            characterAnimator = animator; fanAttachment = attachment; equipmentClipLength = clipLength;
        }

        private void Start()
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null || fanAttachment == null)
            {
                Debug.LogError("캐릭터 Animator와 부채 연결을 확인하세요.", this);
                enabled = false; return;
            }
            characterAnimator.applyRootMotion = false;
            combatRequested = running = paused = false;
            selectedState = "SHEATHED_IDLE";
            characterAnimator.speed = 1f;
            ApplyAttackSpeeds();
            characterAnimator.SetFloat(MoveAmount, 0f);
            characterAnimator.Play(selectedState, 0, 0f);
            fanAttachment.SetSequencePose(false, 0f, true);
        }

        private void Update()
        {
            ApplyAttackSpeeds();
            if (enableKeyboardShortcuts) ReadKeyboard();
            if (!paused) characterAnimator.SetFloat(MoveAmount, running ? 1f : 0f, locomotionBlendDuration, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (paused || !TryGetSelectedState(out var state)) return;
            if (IsEquipmentTransition)
            {
                float progress = EquipmentProgress;
                UpdateEquipmentPose(progress);
                float finishAt = 1f - Mathf.Min(0.06f, returnBlendDuration * CurrentSpeed() / equipmentClipLength);
                if (progress >= finishAt)
                {
                    fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
                    BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", returnBlendDuration);
                }
                return;
            }
            if (!IsAction(selectedState) || characterAnimator.IsInTransition(0)) return;
            // 클립 끝에서 대기 포즈로 순간 이동하지 않도록 마지막 구간에서 섞기 시작한다.
            var attackClip = AttackClip(selectedState);
            float blendDuration = returnBlendDuration;
            float returnAt;
            if (attackClip != null)
            {
                int index = selectedState == "ATTACK1" ? 1 : selectedState == "ATTACK2" ? 2 : 3;
                float startTime = GetAttackStartFrame(index) / attackClip.frameRate;
                float endTime = GetAttackEndFrame(index) / attackClip.frameRate;
                blendDuration = Mathf.Min(returnBlendDuration, (endTime - startTime) / CurrentSpeed() * 0.25f);
                returnAt = (endTime - blendDuration * CurrentSpeed()) / attackClip.length;
            }
            else returnAt = 1f - Mathf.Min(0.15f, returnBlendDuration / Mathf.Max(0.01f, state.length));
            if (state.normalizedTime < returnAt) return;
            if (repeatSelectedAnimation) BeginState(selectedState, blendDuration);
            else BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", blendDuration);
        }

        public void ApplyAttackSpeeds()
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;
            characterAnimator.SetFloat(Attack1Speed, Mathf.Max(0.05f, attack1Speed));
            characterAnimator.SetFloat(Attack2Speed, Mathf.Max(0.05f, attack2Speed));
            characterAnimator.SetFloat(Attack3Speed, Mathf.Max(0.05f, attack3Speed));
            characterAnimator.SetFloat(DrawSpeed, Mathf.Max(0.05f, drawSpeed));
            // 속도 파라미터가 상태 속도를 대체하므로 역재생 부호를 파라미터에 직접 넣는다.
            characterAnimator.SetFloat(SheatheSpeed, -Mathf.Max(0.05f, sheatheSpeed));
        }

        private void BeginState(string state, float blendDuration, float offsetSeconds = -1f)
        {
            selectedState = state; paused = false;
            characterAnimator.speed = 1f; fanAttachment.SetPaused(false); ApplyAttackSpeeds();
            float startOffset = offsetSeconds >= 0f ? offsetSeconds : state == "SHEATHE" ? equipmentClipLength : AttackStartTime(state);
            // Exit Time을 기다리거나 입력을 큐에 저장하지 않고 곧바로 새 클립을 시작한다.
            characterAnimator.CrossFadeInFixedTime(state, blendDuration, 0, startOffset, 0f);
        }

        public void EnterCombat()
        {
            if (combatRequested) return;
            float offset = selectedState == "SHEATHE" ? Mathf.Clamp01(NormalizedTime) * equipmentClipLength : 0f;
            combatRequested = true; running = false;
            BeginState("DRAW", transitionDuration, offset);
        }

        public void ExitCombat()
        {
            if (!combatRequested) return;
            float offset = selectedState == "DRAW" ? Mathf.Clamp01(NormalizedTime) * equipmentClipLength : equipmentClipLength;
            if (!IsEquipmentTransition) fanAttachment.SetSequencePose(true, 1f);
            combatRequested = false; running = false;
            BeginState("SHEATHE", transitionDuration, offset);
        }

        public void PlayDrawSheathe() { if (combatRequested) ExitCombat(); else EnterCombat(); }
        public void PlayIdle()
        {
            running = false;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", transitionDuration);
        }
        public void PlayRun()
        {
            running = true;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", transitionDuration);
        }
        public void PlayAttack1() => PlayAttack("ATTACK1");
        public void PlayAttack2() => PlayAttack("ATTACK2");
        public void PlayAttack3() => PlayAttack("ATTACK3");
        private void PlayAttack(string state)
        {
            if (!CanAttack) return;
            running = false;
            fanAttachment.SetSequencePose(true, 1f);
            var clip = AttackClip(state);
            int index = state == "ATTACK1" ? 1 : state == "ATTACK2" ? 2 : 3;
            float segmentDuration = clip != null ? (GetAttackEndFrame(index) - GetAttackStartFrame(index)) / clip.frameRate / Mathf.Max(0.05f, CurrentAttackSpeed(index)) : 1f;
            BeginState(state, Mathf.Min(transitionDuration, segmentDuration * 0.25f));
        }
        public void PlayHit()
        {
            running = false;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            BeginState("HIT", transitionDuration);
        }
        public void TogglePause()
        {
            paused = !paused; characterAnimator.speed = paused ? 0f : 1f; fanAttachment.SetPaused(paused);
        }

        private bool TryGetSelectedState(out AnimatorStateInfo state)
        {
            state = default;
            if (characterAnimator == null || !characterAnimator.isInitialized) return false;
            if (characterAnimator.IsInTransition(0))
            {
                var next = characterAnimator.GetNextAnimatorStateInfo(0);
                if (next.IsName(selectedState)) { state = next; return true; }
            }
            state = characterAnimator.GetCurrentAnimatorStateInfo(0);
            return state.IsName(selectedState);
        }
        private void UpdateEquipmentPose(float progress)
        {
            if (selectedState == "DRAW")
                fanAttachment.SetSequencePose(progress >= drawGrabTime, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(drawOpenStart, drawOpenEnd, progress)));
            else
                fanAttachment.SetSequencePose(progress < sheatheStowTime, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sheatheCloseStart, sheatheCloseEnd, progress)));
        }
        private float CurrentSpeed() => selectedState switch
        {
            "ATTACK1" => Mathf.Max(0.05f, attack1Speed), "ATTACK2" => Mathf.Max(0.05f, attack2Speed),
            "ATTACK3" => Mathf.Max(0.05f, attack3Speed), "DRAW" => Mathf.Max(0.05f, drawSpeed),
            "SHEATHE" => Mathf.Max(0.05f, sheatheSpeed), _ => 1f
        };
        private static bool IsAction(string state) => state == "ATTACK1" || state == "ATTACK2" || state == "ATTACK3" || state == "HIT";
        private AnimationClip AttackClip(string state) => state == "ATTACK1" ? attack1Clip : state == "ATTACK2" ? attack2Clip : state == "ATTACK3" ? attack3Clip : null;
        private float CurrentAttackSpeed(int index) => index == 1 ? attack1Speed : index == 2 ? attack2Speed : attack3Speed;
        private float AttackStartTime(string state)
        {
            var clip = AttackClip(state);
            int index = state == "ATTACK1" ? 1 : state == "ATTACK2" ? 2 : 3;
            return clip != null ? GetAttackStartFrame(index) / clip.frameRate : 0f;
        }
        public int GetAttackStartFrame(int index)
        {
            int maximum = GetAttackMaximumFrame(index);
            int start = index == 1 ? attack1StartFrame : index == 2 ? attack2StartFrame : attack3StartFrame;
            return Mathf.Clamp(start, 0, maximum - 1);
        }
        public int GetAttackEndFrame(int index)
        {
            int maximum = GetAttackMaximumFrame(index);
            int end = index == 1 ? attack1EndFrame : index == 2 ? attack2EndFrame : attack3EndFrame;
            return Mathf.Clamp(end, GetAttackStartFrame(index) + 1, maximum);
        }
        public int GetAttackMaximumFrame(int index)
        {
            var clip = index == 1 ? attack1Clip : index == 2 ? attack2Clip : attack3Clip;
            return clip != null ? Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate)) : 1;
        }
        public void SetAttackFrameRange(int index, int startFrame, int endFrame)
        {
            // UI에서도 Inspector와 동일하게 원본 범위 안의 최소 한 프레임을 보장한다.
            int maximum = GetAttackMaximumFrame(index);
            int start = Mathf.Clamp(startFrame, 0, maximum - 1);
            int end = Mathf.Clamp(endFrame, start + 1, maximum);
            if (index == 1) { attack1StartFrame = start; attack1EndFrame = end; }
            else if (index == 2) { attack2StartFrame = start; attack2EndFrame = end; }
            else if (index == 3) { attack3StartFrame = start; attack3EndFrame = end; }
        }
        private void OnValidate()
        {
            if (attack1Clip != null) { attack1StartFrame = GetAttackStartFrame(1); attack1EndFrame = GetAttackEndFrame(1); }
            if (attack2Clip != null) { attack2StartFrame = GetAttackStartFrame(2); attack2EndFrame = GetAttackEndFrame(2); }
            if (attack3Clip != null) { attack3StartFrame = GetAttackStartFrame(3); attack3EndFrame = GetAttackEndFrame(3); }
        }
        private void ReadKeyboard()
        {
            // 숫자 입력 중 1~5, Space가 공격이나 전투 전환 단축키로 처리되지 않게 한다.
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.TryGetComponent<TMPro.TMP_InputField>(out var input) && input.isFocused) return;
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.digit5Key.wasPressedThisFrame) PlayDrawSheathe();
            if (keyboard.digit1Key.wasPressedThisFrame) PlayAttack1();
            if (keyboard.digit2Key.wasPressedThisFrame) PlayAttack2();
            if (keyboard.digit3Key.wasPressedThisFrame) PlayAttack3();
            if (keyboard.digit4Key.wasPressedThisFrame) PlayHit();
            if (keyboard.iKey.wasPressedThisFrame) PlayIdle();
            if (keyboard.rKey.wasPressedThisFrame) { if (running) PlayIdle(); else PlayRun(); }
            if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard.fKey.wasPressedThisFrame && CanPreviewFan) fanAttachment.ToggleFan();
        }
    }
}
