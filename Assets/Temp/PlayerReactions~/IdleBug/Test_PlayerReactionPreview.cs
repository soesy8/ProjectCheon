using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Cheon.Tests.PlayerReactions
{
    public class Test_PlayerReactionPreview : MonoBehaviour
    {
        public enum PreviewMode { HeavyThenGetUp, Death, HeavyOnly, GetUpOnly }
        public enum PreviewPhase { Idle, HeavyHit, DownHold, GetUp, Dead, Complete }

        [SerializeField] private Animator characterAnimator;
        [SerializeField] private RuntimeAnimatorController reactionController;
        [SerializeField] private Test_PlayerReactionSettings savedSettings;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform viewTarget;
        [SerializeField] private float cameraDistance = 5.5f;
        [SerializeField] private float cameraYaw = 155f;
        [SerializeField] private float cameraHeight = 2.3f;
        public bool repeatSequence;

        private static readonly string[] StateNames = { "HEAVY_HIT", "GET_UP", "DEATH" };
        private static readonly string[] TimeParameters = { "HeavyTime", "GetUpTime", "DeathTime" };
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

        public Test_PlayerReactionSettings Settings => runtimeSettings != null ? runtimeSettings : savedSettings;
        public PreviewPhase Phase { get; private set; } = PreviewPhase.Idle;
        public bool IsPaused => paused;
        public bool IsBlending => graph.IsValid() && controller.IsInTransition(0);
        public float CurrentFrame => currentFrame;
        public int ActiveRangeIndex { get; private set; }
        public PreviewMode Mode => mode;
        public Animator CharacterAnimator => characterAnimator;
        public string AnimatorState => graph.IsValid() ? activeState : "";
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
            cameraFocus = viewTarget != null ? viewTarget.position : characterAnimator.transform.position;
            CreatePoseSampler();
            characterAnimator.runtimeAnimatorController = reactionController;
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
        public void AdvancePlayback(float seconds)
        {
            if (!graph.IsValid()) return;
            Settings.Validate();
            if (paused) { EvaluatePose(0f); return; }
            seconds = Mathf.Max(0f, seconds);
            if (fadingRangeIndex >= 0)
            {
                // 나가는 모션도 전환이 끝날 때까지 진행시켜 마지막 자세가 갑자기 굳지 않게 한다.
                var source = Settings.GetRange(fadingRangeIndex);
                fadingFrame = Mathf.Clamp(fadingFrame + seconds * source.FrameRate * source.speed, source.startFrame, source.endFrame);
                SetMotionFrame(fadingRangeIndex, fadingFrame);
                if (Phase == PreviewPhase.Complete) currentFrame = fadingFrame;
            }
            if (Phase == PreviewPhase.HeavyHit || Phase == PreviewPhase.GetUp)
            {
                var range = Settings.GetRange(ActiveRangeIndex);
                currentFrame = Mathf.Clamp(currentFrame + seconds * range.FrameRate * range.speed, range.startFrame, range.endFrame);
                SetMotionFrame(ActiveRangeIndex, currentFrame);
            }
            blendElapsed += seconds;
            EvaluatePose(seconds);
            if (blendElapsed >= blendDuration) fadingRangeIndex = -1;
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
                var range = Settings.getUp;
                float remaining = (range.endFrame - currentFrame) / range.FrameRate / range.speed;
                if (remaining <= IdleBlendDuration()) ReturnToIdle();
            }
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
        private float IdleBlendDuration() => Mathf.Min(Settings.returnToIdleSeconds, Settings.getUp.Duration * 0.45f);
        private void ReturnToIdle()
        {
            float idleTime = FindClosestIdleTime();
            SwitchState("IDLE", -1, idleTime, IdleBlendDuration(), true);
            Phase = PreviewPhase.Complete; phaseElapsed = 0f;
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
            EvaluatePose(0f);
        }
        private void SetMotionFrame(int index, float frame)
        {
            var range = Settings.GetRange(index);
            controller.SetFloat(TimeParameters[index], frame / (range.FrameRate * range.clip.length));
        }
        private void EvaluatePose(float seconds)
        {
            graph.Evaluate(seconds);
            float blend = blendDuration <= 0f ? 1f : Mathf.Clamp01(blendElapsed / blendDuration);
            appliedRootOffset = Vector3.Lerp(previousRootOffset, currentRootOffset, blend);
            characterAnimator.transform.localPosition = actorPosition + appliedRootOffset;
            characterAnimator.transform.localRotation = actorRotation;
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
            currentFrame = Mathf.Clamp(frame, 0, range.MaximumFrame);
            SwitchState(StateNames[rangeIndex], rangeIndex, currentFrame / range.FrameRate, 0f, false);
            ActiveRangeIndex = rangeIndex; paused = true;
            mode = rangeIndex == 1 ? PreviewMode.GetUpOnly : rangeIndex == 2 ? PreviewMode.Death : PreviewMode.HeavyOnly;
            Phase = rangeIndex == 1 ? PreviewPhase.GetUp : PreviewPhase.HeavyHit;
        }
        public void StepFrame(int index, int direction) => Scrub(index, (index == ActiveRangeIndex ? Mathf.Round(currentFrame) : Settings.GetRange(index).startFrame) + direction);
        public void ResetPreview()
        {
            if (!graph.IsValid()) return;
            Phase = PreviewPhase.Idle; paused = false; currentFrame = 0f; phaseElapsed = 0f;
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
