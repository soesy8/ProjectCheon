using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Cheon.Tests.PlayerReactions
{
    public class Test_PlayerReactionPreview : MonoBehaviour
    {
        public enum PreviewMode { HeavyThenGetUp, Death, HeavyOnly, GetUpOnly }
        public enum PreviewPhase { Idle, HeavyHit, DownHold, GetUp, Dead, Complete, Moving, Dodging }

        [SerializeField] private Animator characterAnimator;
        [SerializeField] private RuntimeAnimatorController reactionController;
        [SerializeField] private Test_PlayerReactionSettings savedSettings;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform viewTarget;
        [SerializeField] private float cameraDistance = 5.5f;
        [SerializeField] private float cameraYaw = 155f;
        [SerializeField] private float cameraHeight = 2.3f;
        public bool repeatSequence;

        private static readonly string[] StateNames = { "HEAVY_HIT", "GET_UP", "DEATH", "DODGE" };
        private static readonly string[] TimeParameters = { "HeavyTime", "GetUpTime", "DeathTime", "DodgeTime" };
        private static readonly HumanBodyBones[] MatchingBones = {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm
        };
        private PlayableGraph graph, samplingGraph;
        private AnimatorControllerPlayable controller;
        private AnimationClipPlayable samplingPlayable;
        private AnimationPlayableOutput samplingOutput;
        private AnimationClip sampledClip;
        private Animator samplingAnimator;
        private GameObject samplingRoot;
        private Test_PlayerReactionSettings runtimeSettings;
        private Vector3 actorPosition, previousRootOffset, currentRootOffset, appliedRootOffset, cameraFocus;
        private Quaternion actorRotation;
        private float currentFrame, phaseElapsed, blendElapsed, blendDuration, fadingFrame;
        private int stateRangeIndex = -1, fadingRangeIndex = -1;
        private string activeState = "IDLE";
        private bool paused;
        private PreviewMode mode;
        private Vector2 movementInput;
        private Vector3 dodgeDirection;
        private float landedElapsed = -1f, moveBlend;
        private bool dodgePressed, followMovement, headAdjusted;
        private Transform neck, head;
        private Quaternion neckBeforeBow, headBeforeBow;

        public Test_PlayerReactionSettings Settings => runtimeSettings != null ? runtimeSettings : savedSettings;
        public PreviewPhase Phase { get; private set; } = PreviewPhase.Idle;
        public bool IsPaused => paused;
        public bool IsBlending => graph.IsValid() && controller.IsInTransition(0);
        public float CurrentFrame => currentFrame;
        public int ActiveRangeIndex { get; private set; }
        public PreviewMode Mode => mode;
        public Animator CharacterAnimator => characterAnimator;
        public AnimatorControllerPlayable AnimationController => controller;
        public event System.Action<float> BeforePoseEvaluation;
        public event System.Action AfterPoseEvaluation;
        public event System.Action PreviewReset;
        public string AnimatorState => graph.IsValid() ? activeState : "";
        public float LandedSeconds => Mathf.Max(0f, landedElapsed);
        public bool CanRecover => mode != PreviewMode.Death && landedElapsed + 0.00001f >= Settings.landedRecoveryDelay &&
            (Phase == PreviewPhase.HeavyHit || Phase == PreviewPhase.DownHold);
        public float GetUpProgress => RangeProgress(1, stateRangeIndex == 1 ? currentFrame : fadingRangeIndex == 1 ? fadingFrame : Settings.getUp.endFrame);
        public bool CanDodge => !paused && (Phase == PreviewPhase.GetUp ? GetUpProgress + 0.00001f >= Settings.getUpDodgeCancelProgress :
            Phase == PreviewPhase.Idle || Phase == PreviewPhase.Complete || Phase == PreviewPhase.Moving);
        public bool CanMove => !paused && (Phase == PreviewPhase.GetUp ? GetUpProgress + 0.00001f >= Settings.getUpMoveCancelProgress :
            Phase == PreviewPhase.Idle || Phase == PreviewPhase.Complete || Phase == PreviewPhase.Moving);
        public string LastInputResult { get; private set; } = "WASD / 방향키: 이동 · Space: 회피";
        public float IdleNormalizedTime => graph.IsValid() && activeState == "IDLE" ?
            (IsBlending ? controller.GetNextAnimatorStateInfo(0).normalizedTime : controller.GetCurrentAnimatorStateInfo(0).normalizedTime) : 0f;
        public string SettingsMessage { get; private set; } = "조절 후 ‘설정 저장’을 누르면 다음 실행에도 유지됩니다.";

        public void Configure(Animator animator, Test_PlayerReactionSettings settings, Camera camera, Transform target)
        { characterAnimator = animator; savedSettings = settings; previewCamera = camera; viewTarget = target; }
        public void ConfigureController(RuntimeAnimatorController value) => reactionController = value;

        private void Awake()
        {
            if (characterAnimator == null || reactionController == null || savedSettings == null || characterAnimator.avatar == null || !characterAnimator.avatar.isValid)
            { Debug.LogError("플레이어 Animator Controller 또는 Avatar 설정이 없습니다.", this); enabled = false; return; }
            runtimeSettings = Instantiate(savedSettings);
            runtimeSettings.hideFlags = HideFlags.DontSave;
            actorPosition = characterAnimator.transform.localPosition;
            actorRotation = characterAnimator.transform.localRotation;
            neck = characterAnimator.GetBoneTransform(HumanBodyBones.Neck);
            head = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
            cameraFocus = viewTarget != null ? viewTarget.position : characterAnimator.transform.position;
            CreatePoseSampler();
            // 기본 Animator 그래프가 매 프레임 IDLE을 덮어쓰지 않도록 재생 출력을 하나로 제한한다.
            // 컨트롤러와 블렌드트리는 아래 AnimatorControllerPlayable에서 그대로 사용한다.
            characterAnimator.runtimeAnimatorController = null;
            characterAnimator.applyRootMotion = false;
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Test_PlayerReactionController");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            controller = AnimatorControllerPlayable.Create(graph, reactionController);
            AnimationPlayableOutput.Create(graph, "Player", characterAnimator).SetSourcePlayable(controller);
            graph.Play();
            ResetPreview();
        }

        private void Update() => AdvancePlayback(Time.unscaledDeltaTime);
        public void SetInput(Vector2 movement, bool dodge)
        { movementInput = Vector2.ClampMagnitude(movement, 1f); dodgePressed |= dodge; }

        public void AdvancePlayback(float seconds)
        {
            if (!graph.IsValid()) return;
            Settings.Validate();
            bool requestedDodge = dodgePressed; dodgePressed = false;
            if (paused) { EvaluatePose(0f); return; }
            seconds = Mathf.Max(0f, seconds);
            float previousFrame = currentFrame;
            if (fadingRangeIndex >= 0)
            {
                // 나가는 모션도 전환이 끝날 때까지 진행시켜 마지막 자세가 갑자기 굳지 않게 한다.
                var source = Settings.GetRange(fadingRangeIndex);
                fadingFrame = Mathf.Clamp(fadingFrame + seconds * source.FrameRate * source.speed, source.startFrame, source.endFrame);
                SetMotionFrame(fadingRangeIndex, fadingFrame);
                if (Phase == PreviewPhase.Complete) currentFrame = fadingFrame;
            }
            if (Phase == PreviewPhase.HeavyHit || Phase == PreviewPhase.GetUp || Phase == PreviewPhase.Dodging)
            {
                var range = Settings.GetRange(ActiveRangeIndex);
                currentFrame = Mathf.Clamp(currentFrame + seconds * range.FrameRate * range.speed, range.startFrame, range.endFrame);
                SetMotionFrame(ActiveRangeIndex, currentFrame);
            }
            if (Phase == PreviewPhase.HeavyHit && mode != PreviewMode.Death)
            {
                float landing = Mathf.Clamp(Settings.landingFrame, Settings.heavyHit.startFrame, Settings.heavyHit.endFrame);
                if (landedElapsed >= 0f) landedElapsed += seconds;
                else if (currentFrame >= landing)
                    landedElapsed = Mathf.Max(0f, seconds - (landing - previousFrame) / (Settings.heavyHit.FrameRate * Settings.heavyHit.speed));
            }
            else if (Phase == PreviewPhase.DownHold) landedElapsed += seconds;
            UpdateLocomotion(seconds, previousFrame);
            blendElapsed += seconds;
            EvaluatePose(seconds);
            if (blendElapsed >= blendDuration) fadingRangeIndex = -1;
            if (CanRecover && (movementInput.sqrMagnitude > 0.01f || requestedDodge))
            {
                LastInputResult = requestedDodge ? "착지 후 회피 입력 → 일어나기" : "착지 후 이동 입력 → 일어나기";
                BeginMotion(1, PreviewPhase.GetUp, true); return;
            }
            if (requestedDodge && CanDodge) { BeginDodge(); return; }
            if (movementInput.sqrMagnitude > 0.01f && CanMove && Phase == PreviewPhase.GetUp)
            { LastInputResult = "일어나기 90% 이후 → 이동 캔슬"; ReturnToLocomotion(true); return; }
            if (Phase == PreviewPhase.DownHold)
            {
                phaseElapsed += seconds;
                if (phaseElapsed >= Settings.downHoldSeconds) BeginMotion(1, PreviewPhase.GetUp, true);
            }
            else if (Phase == PreviewPhase.Complete)
            {
                phaseElapsed += seconds;
                if (repeatSequence && mode != PreviewMode.Death && phaseElapsed >= Mathf.Max(0.6f, blendDuration + 0.15f)) Play(mode);
            }
            else if (Phase == PreviewPhase.GetUp)
            {
                // 배속과 트림 구간이 바뀌어도 재생 구간의 90% 전에는 대기로 빠지지 않는다.
                if (GetUpProgress + 0.00001f >= Settings.getUpMoveCancelProgress) ReturnToLocomotion(false);
            }
            else if (Phase == PreviewPhase.Dodging && currentFrame >= Settings.dodge.endFrame)
                ReturnToLocomotion(movementInput.sqrMagnitude > 0.01f);
            else if (Phase == PreviewPhase.HeavyHit && currentFrame >= Settings.GetRange(ActiveRangeIndex).endFrame)
            {
                phaseElapsed = 0f;
                if (mode == PreviewMode.Death) { Phase = PreviewPhase.Dead; paused = true; }
                else if (mode == PreviewMode.HeavyThenGetUp) Phase = PreviewPhase.DownHold;
                else { Phase = PreviewPhase.Complete; paused = true; }
            }
        }
        public void Play(PreviewMode selectedMode)
        {
            if (!graph.IsValid()) return;
            mode = selectedMode; paused = false; Settings.Validate();
            dodgePressed = false; moveBlend = 0f; controller.SetFloat("MoveAmount", 0f);
            landedElapsed = -1f; LastInputResult = "입력 대기";
            BeginMotion(mode == PreviewMode.GetUpOnly ? 1 : mode == PreviewMode.Death ? 2 : 0,
                mode == PreviewMode.GetUpOnly ? PreviewPhase.GetUp : PreviewPhase.HeavyHit);
        }
        private void BeginMotion(int index, PreviewPhase phase, bool preservePosition = false)
        {
            var range = Settings.GetRange(index);
            float duration = Mathf.Min(Settings.transitionSeconds, range.Duration * 0.45f);
            SwitchState(StateNames[index], index, range.startFrame / range.FrameRate, duration, preservePosition);
            ActiveRangeIndex = index; currentFrame = range.startFrame; Phase = phase; phaseElapsed = 0f;
        }
        private float IdleBlendDuration() => Settings.returnToIdleSeconds;
        private void ReturnToLocomotion(bool moving)
        {
            float idleTime = FindClosestIdleTime();
            float duration = moving || Phase == PreviewPhase.Dodging ? Settings.inputTransitionSeconds : IdleBlendDuration();
            SwitchState("IDLE", -1, idleTime, duration, true);
            Phase = moving ? PreviewPhase.Moving : PreviewPhase.Complete; phaseElapsed = 0f;
            if (moving) { followMovement = true; repeatSequence = false; }
        }
        private void BeginDodge()
        {
            LastInputResult = Phase == PreviewPhase.GetUp || fadingRangeIndex == 1 ? "일어나기 45% 이후 → 회피 캔슬" : "회피";
            dodgeDirection = InputDirection();
            if (dodgeDirection.sqrMagnitude < 0.01f) dodgeDirection = characterAnimator.transform.forward;
            actorRotation = Quaternion.LookRotation(dodgeDirection);
            var range = Settings.dodge;
            SwitchState("DODGE", 3, range.startFrame / range.FrameRate, Settings.inputTransitionSeconds, true);
            ActiveRangeIndex = 3; currentFrame = range.startFrame; Phase = PreviewPhase.Dodging; phaseElapsed = 0f;
            followMovement = true; repeatSequence = false;
        }
        private float RangeProgress(int index, float frame)
        {
            var range = Settings.GetRange(index);
            return Mathf.Clamp01((frame - range.startFrame) / Mathf.Max(1f, range.endFrame - range.startFrame));
        }
        private Vector3 InputDirection()
        {
            var forward = previewCamera != null ? Vector3.ProjectOnPlane(previewCamera.transform.forward, Vector3.up).normalized : Vector3.forward;
            var right = previewCamera != null ? Vector3.ProjectOnPlane(previewCamera.transform.right, Vector3.up).normalized : Vector3.right;
            return forward * movementInput.y + right * movementInput.x;
        }
        private void UpdateLocomotion(float seconds, float previousFrame)
        {
            bool locomotion = activeState == "IDLE" && CanMove;
            bool moving = locomotion && movementInput.sqrMagnitude > 0.01f;
            moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, seconds / Settings.inputTransitionSeconds);
            controller.SetFloat("MoveAmount", moveBlend);
            Vector3 displacement = Vector3.zero;
            if (moving)
            {
                Vector3 direction = InputDirection();
                actorRotation = Quaternion.Slerp(actorRotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-14f * seconds));
                displacement = direction * Settings.moveSpeed * seconds;
                Phase = PreviewPhase.Moving; followMovement = true; repeatSequence = false;
            }
            else if (Phase == PreviewPhase.Moving) Phase = PreviewPhase.Idle;
            if (Phase == PreviewPhase.Dodging)
            {
                float before = RangeProgress(3, previousFrame), after = RangeProgress(3, currentFrame);
                displacement = dodgeDirection * Settings.dodgeDistance * (Mathf.Pow(1f - before, 2f) - Mathf.Pow(1f - after, 2f));
            }
            var parent = characterAnimator.transform.parent;
            actorPosition += parent == null ? displacement : parent.InverseTransformVector(displacement);
        }
        private void SwitchState(string state, int index, float time, float duration, bool preservePosition)
        {
            Vector3 outgoingHips = characterAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
            previousRootOffset = appliedRootOffset;
            currentRootOffset = Vector3.zero;
            if (preservePosition)
            {
                var clip = index < 0 ? Settings.idleClip : Settings.GetRange(index).clip;
                Vector3 hipOffset = SamplePose(clip, time);
                characterAnimator.transform.localPosition = actorPosition;
                characterAnimator.transform.localRotation = actorRotation;
                Vector3 difference = outgoingHips - characterAnimator.transform.TransformPoint(hipOffset);
                difference.y = 0f;
                var parent = characterAnimator.transform.parent;
                currentRootOffset = parent == null ? difference : parent.InverseTransformVector(difference);
            }
            fadingRangeIndex = stateRangeIndex;
            fadingFrame = currentFrame;
            if (index >= 0) SetMotionFrame(index, time * Settings.GetRange(index).FrameRate);
            bool immediate = duration <= 0f || activeState == state;
            blendDuration = immediate ? 0f : duration; blendElapsed = 0f;
            int stateHash = Animator.StringToHash("Base Layer." + state);
            if (immediate)
            {
                float normalized = index < 0 ? time / Settings.idleClip.length : time / Settings.GetRange(index).clip.length;
                controller.Play(stateHash, 0, normalized); fadingRangeIndex = -1;
            }
            else controller.CrossFadeInFixedTime(stateHash, duration, 0, time);
            activeState = state; stateRangeIndex = index;
            if (index >= 0) currentFrame = time * Settings.GetRange(index).FrameRate;
            EvaluatePose(0f);
        }
        private void SetMotionFrame(int index, float frame)
        {
            var range = Settings.GetRange(index);
            controller.SetFloat(TimeParameters[index], frame / (range.FrameRate * range.clip.length));
        }
        private void EvaluatePose(float seconds)
        {
            // 정지 상태에서 반복 평가해도 숙임이 누적되지 않도록 이전 추가 회전을 먼저 되돌린다.
            if (headAdjusted)
            {
                if (neck != null) neck.localRotation = neckBeforeBow;
                if (head != null) head.localRotation = headBeforeBow;
                headAdjusted = false;
            }
            BeforePoseEvaluation?.Invoke(seconds);
            graph.Evaluate(seconds);
            float blend = blendDuration <= 0f ? 1f : Mathf.Clamp01(blendElapsed / blendDuration);
            appliedRootOffset = Vector3.Lerp(previousRootOffset, currentRootOffset, blend);
            characterAnimator.transform.localPosition = actorPosition + appliedRootOffset;
            characterAnimator.transform.localRotation = actorRotation;
            ApplyGetUpHeadBow(blend);
            AfterPoseEvaluation?.Invoke();
        }
        private void ApplyGetUpHeadBow(float blend)
        {
            float incoming = stateRangeIndex == 1 ? HeadBowWeight(currentFrame) : 0f;
            float outgoing = fadingRangeIndex == 1 ? HeadBowWeight(fadingFrame) : 0f;
            float angle = Settings.getUpHeadBowDegrees * Mathf.Lerp(outgoing, incoming, blend);
            if (head == null || angle <= 0f) return;
            if (neck != null) neckBeforeBow = neck.localRotation;
            headBeforeBow = head.localRotation;
            Vector3 axis = characterAnimator.transform.right;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(angle * 0.25f, axis) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(angle * (neck != null ? 0.75f : 1f), axis) * head.rotation;
            headAdjusted = true;
        }
        private float HeadBowWeight(float frame)
        {
            float progress = RangeProgress(1, frame);
            return Mathf.SmoothStep(0f, 1f, progress / 0.15f) * (1f - Mathf.SmoothStep(0f, 1f, (progress - 0.7f) / 0.3f));
        }
        private void CreatePoseSampler()
        {
            // 화면에 보이지 않는 복제 리그에서만 자세를 읽어 실제 Animator의 전환을 끊지 않는다.
            samplingRoot = Instantiate(characterAnimator.gameObject, transform);
            samplingRoot.name = "Test_ReactionPoseSampler"; samplingRoot.hideFlags = HideFlags.HideAndDontSave;
            foreach (var renderer in samplingRoot.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            samplingAnimator = samplingRoot.GetComponent<Animator>();
            foreach (var animator in samplingRoot.GetComponentsInChildren<Animator>(true))
                if (animator != samplingAnimator) animator.enabled = false;
            samplingAnimator.runtimeAnimatorController = null; samplingAnimator.applyRootMotion = false;
            samplingAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            samplingGraph = PlayableGraph.Create("Test_ReactionPoseSampler");
            samplingGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            samplingOutput = AnimationPlayableOutput.Create(samplingGraph, "Sampler", samplingAnimator);
            samplingGraph.Play();
        }
        private Vector3 SamplePose(AnimationClip clip, float time)
        {
            if (sampledClip != clip)
            {
                if (samplingPlayable.IsValid()) samplingGraph.DestroyPlayable(samplingPlayable);
                samplingPlayable = AnimationClipPlayable.Create(samplingGraph, clip);
                samplingPlayable.SetApplyFootIK(false); samplingPlayable.SetApplyPlayableIK(false); samplingPlayable.SetSpeed(0f);
                samplingOutput.SetSourcePlayable(samplingPlayable); sampledClip = clip;
            }
            samplingPlayable.SetTime(time); samplingGraph.Evaluate(0f);
            return samplingAnimator.transform.InverseTransformPoint(samplingAnimator.GetBoneTransform(HumanBodyBones.Hips).position);
        }
        private float FindClosestIdleTime()
        {
            float bestTime = 0f, bestScore = float.PositiveInfinity;
            // 회복 마지막 자세와 가까운 대기 사이클의 시점을 골라 팔다리의 꺾임을 줄인다.
            for (int sample = 0; sample < 16; sample++)
            {
                float time = sample * Settings.idleClip.length / 16f;
                SamplePose(Settings.idleClip, time);
                float score = 0f;
                foreach (var bone in MatchingBones)
                {
                    var source = characterAnimator.GetBoneTransform(bone);
                    var target = samplingAnimator.GetBoneTransform(bone);
                    if (source != null && target != null) { float angle = Quaternion.Angle(source.localRotation, target.localRotation); score += angle * angle; }
                }
                if (score < bestScore) { bestScore = score; bestTime = time; }
            }
            return bestTime;
        }
        public void SetPaused(bool value) => paused = value;
        public void TogglePaused() => paused = !paused;
        public void Scrub(int rangeIndex, float frame)
        {
            if (!graph.IsValid()) return;
            Settings.Validate(); var range = Settings.GetRange(rangeIndex);
            float targetFrame = Mathf.Clamp(frame, 0, range.MaximumFrame);
            // 보정할 프레임을 확정한 뒤 자세를 평가한다.
            currentFrame = targetFrame;
            SwitchState(StateNames[rangeIndex], rangeIndex, targetFrame / range.FrameRate, 0f, false);
            ActiveRangeIndex = rangeIndex; paused = true;
            mode = rangeIndex == 1 ? PreviewMode.GetUpOnly : rangeIndex == 2 ? PreviewMode.Death : PreviewMode.HeavyOnly;
            Phase = rangeIndex == 1 ? PreviewPhase.GetUp : PreviewPhase.HeavyHit;
        }
        public void StepFrame(int index, int direction) => Scrub(index, (index == ActiveRangeIndex ? Mathf.Round(currentFrame) : Settings.GetRange(index).startFrame) + direction);
        public void ResetPreview()
        {
            PreviewReset?.Invoke();
            if (!graph.IsValid()) return;
            Phase = PreviewPhase.Idle; paused = false; currentFrame = 0f; phaseElapsed = 0f;
            actorPosition = Vector3.zero; actorRotation = Quaternion.identity;
            cameraFocus = characterAnimator.transform.parent != null ? characterAnimator.transform.parent.TransformPoint(actorPosition) : actorPosition;
            landedElapsed = -1f; movementInput = Vector2.zero; dodgePressed = false; followMovement = false;
            moveBlend = 0f; controller.SetFloat("MoveAmount", 0f); LastInputResult = "입력 대기";
            SwitchState("IDLE", -1, 0f, 0f, false);
        }
        public void SaveSettings()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(savedSettings, "플레이어 피격 테스트 설정 저장");
            UnityEditor.EditorUtility.CopySerialized(runtimeSettings, savedSettings);
            savedSettings.hideFlags = HideFlags.None; savedSettings.name = "Test_PlayerReactionSettings";
            UnityEditor.EditorUtility.SetDirty(savedSettings); UnityEditor.AssetDatabase.SaveAssetIfDirty(savedSettings);
            SettingsMessage = "설정을 저장했습니다. 다음 실행에도 유지됩니다.";
#else
            SettingsMessage = "설정 저장은 Unity 에디터에서 사용할 수 있습니다.";
#endif
        }
        public void ReloadSettings()
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(savedSettings), runtimeSettings);
            Settings.Validate(); ResetPreview(); SettingsMessage = "저장된 설정을 불러왔습니다.";
        }
        public void SetView(float yaw, float distance) { cameraYaw = yaw; cameraDistance = distance; }
        private void LateUpdate()
        {
            if (previewCamera == null || viewTarget == null) return;
            if (followMovement) cameraFocus = Vector3.Lerp(cameraFocus, viewTarget.position, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            var focus = cameraFocus + Vector3.up * 0.85f;
            var offset = Quaternion.Euler(0, cameraYaw, 0) * new Vector3(0, 0, -cameraDistance);
            previewCamera.transform.position = focus + offset + Vector3.up * cameraHeight;
            previewCamera.transform.LookAt(focus);
        }
        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (samplingGraph.IsValid()) samplingGraph.Destroy();
            if (samplingRoot != null) Destroy(samplingRoot);
            if (runtimeSettings != null) Destroy(runtimeSettings);
        }
    }
}
