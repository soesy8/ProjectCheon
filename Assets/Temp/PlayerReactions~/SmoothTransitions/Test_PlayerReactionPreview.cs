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
        [SerializeField] private Test_PlayerReactionSettings savedSettings;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform viewTarget;
        [SerializeField] private float cameraDistance = 5.5f;
        [SerializeField] private float cameraYaw = 155f;
        [SerializeField] private float cameraHeight = 2.3f;
        public bool repeatSequence;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable currentPlayable, previousPlayable;
        private Test_PlayerReactionSettings runtimeSettings;
        private Vector3 actorPosition;
        private Vector3 previousRootOffset, currentRootOffset, appliedRootOffset, cameraFocus;
        private Quaternion actorRotation;
        private float currentFrame, phaseElapsed, blendElapsed, idleTime;
        private bool paused;
        private PreviewMode mode;

        public Test_PlayerReactionSettings Settings => runtimeSettings != null ? runtimeSettings : savedSettings;
        public PreviewPhase Phase { get; private set; } = PreviewPhase.Idle;
        public bool IsPaused => paused;
        public float CurrentFrame => currentFrame;
        public int ActiveRangeIndex { get; private set; }
        public PreviewMode Mode => mode;
        public Animator CharacterAnimator => characterAnimator;
        public string SettingsMessage { get; private set; } = "조절 후 ‘설정 저장’을 누르면 다음 실행에도 유지됩니다.";

        public void Configure(Animator animator, Test_PlayerReactionSettings settings, Camera camera, Transform target)
        { characterAnimator = animator; savedSettings = settings; previewCamera = camera; viewTarget = target; }

        private void Awake()
        {
            if (characterAnimator == null || savedSettings == null || characterAnimator.avatar == null || !characterAnimator.avatar.isValid)
            { Debug.LogError("플레이어 애니메이션 또는 Avatar 설정이 없습니다.", this); enabled = false; return; }
            // 재생 중 수치를 바꾸어도 저장 버튼을 누르기 전에는 원본 설정을 수정하지 않는다.
            runtimeSettings = Instantiate(savedSettings);
            runtimeSettings.hideFlags = HideFlags.DontSave;
            actorPosition = characterAnimator.transform.localPosition;
            actorRotation = characterAnimator.transform.localRotation;
            cameraFocus = viewTarget != null ? viewTarget.position : characterAnimator.transform.position;
            characterAnimator.runtimeAnimatorController = null;
            characterAnimator.applyRootMotion = false;
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Test_PlayerReactions");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var output = AnimationPlayableOutput.Create(graph, "Player", characterAnimator);
            output.SetSourcePlayable(mixer);
            graph.Play();
            SettingsMessage = "조절 후 ‘설정 저장’을 누르면 다음 실행에도 유지됩니다.";
            ResetPreview();
        }

        private void Update() => AdvancePlayback(Time.unscaledDeltaTime);

        public void AdvancePlayback(float seconds)
        {
            if (!graph.IsValid()) return;
            Settings.Validate();
            if (!paused)
            {
                blendElapsed += seconds;
                if (Phase == PreviewPhase.Idle)
                {
                    idleTime += seconds;
                    if (Settings.idleClip != null) currentPlayable.SetTime(idleTime % Settings.idleClip.length);
                }
                else if (Phase == PreviewPhase.DownHold)
                {
                    phaseElapsed += seconds;
                    if (phaseElapsed >= Settings.downHoldSeconds) BeginMotion(1, PreviewPhase.GetUp);
                }
                else if (Phase == PreviewPhase.Complete)
                {
                    phaseElapsed += seconds;
                    if (repeatSequence && mode != PreviewMode.Death && phaseElapsed >= 0.6f) Play(mode);
                }
                else if (Phase == PreviewPhase.HeavyHit || Phase == PreviewPhase.GetUp)
                {
                    var range = Settings.GetRange(ActiveRangeIndex);
                    currentFrame = Mathf.Clamp(currentFrame + seconds * range.FrameRate * range.speed, range.startFrame, range.endFrame);
                    currentPlayable.SetTime(currentFrame / range.FrameRate);
                    if (currentFrame >= range.endFrame) { EvaluatePose(); FinishMotion(); }
                }
            }
            EvaluatePose();
        }

        public void Play(PreviewMode selectedMode)
        {
            if (!graph.IsValid()) return;
            mode = selectedMode; paused = false; Settings.Validate();
            BeginMotion(mode == PreviewMode.GetUpOnly ? 1 : mode == PreviewMode.Death ? 2 : 0,
                mode == PreviewMode.GetUpOnly ? PreviewPhase.GetUp : PreviewPhase.HeavyHit);
            EvaluatePose();
        }

        private void BeginMotion(int index, PreviewPhase phase)
        {
            ActiveRangeIndex = index; Phase = phase; phaseElapsed = 0f;
            var range = Settings.GetRange(index);
            currentFrame = range.startFrame;
            SetClip(range.clip, currentFrame / range.FrameRate, phase == PreviewPhase.GetUp && mode == PreviewMode.HeavyThenGetUp);
        }

        private void FinishMotion()
        {
            phaseElapsed = 0f;
            if (mode == PreviewMode.Death) { Phase = PreviewPhase.Dead; paused = true; return; }
            if (Phase == PreviewPhase.HeavyHit && mode == PreviewMode.HeavyThenGetUp)
            { Phase = PreviewPhase.DownHold; return; }
            Phase = PreviewPhase.Complete;
            if (mode == PreviewMode.HeavyThenGetUp) { idleTime = 0f; SetClip(Settings.idleClip, 0f, true); }
            else paused = true;
        }

        private void SetClip(AnimationClip clip, float time, bool alignPosition = false)
        {
            if (clip == null) return;
            var hips = characterAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 outgoingHips = hips.position;
            previousRootOffset = appliedRootOffset;
            if (previousPlayable.IsValid()) { graph.Disconnect(mixer, 1); graph.DestroyPlayable(previousPlayable); }
            if (currentPlayable.IsValid())
            {
                graph.Disconnect(mixer, 0);
                previousPlayable = currentPlayable;
                graph.Connect(previousPlayable, 0, mixer, 1);
            }
            currentPlayable = AnimationClipPlayable.Create(graph, clip);
            currentPlayable.SetApplyFootIK(false); currentPlayable.SetApplyPlayableIK(false);
            currentPlayable.SetSpeed(0); currentPlayable.SetTime(time);
            graph.Connect(currentPlayable, 0, mixer, 0);
            currentRootOffset = Vector3.zero;
            if (alignPosition)
            {
                // 두 FBX의 원점이 달라도 쓰러진 골반의 수평 위치에서 일어나도록 연결한다.
                characterAnimator.transform.localPosition = actorPosition;
                characterAnimator.transform.localRotation = actorRotation;
                mixer.SetInputWeight(0, 1f); mixer.SetInputWeight(1, 0f); graph.Evaluate(0f);
                Vector3 difference = outgoingHips - hips.position;
                difference.y = 0f;
                var parent = characterAnimator.transform.parent;
                currentRootOffset = parent == null ? difference : parent.InverseTransformVector(difference);
            }
            blendElapsed = 0f;
        }

        private void EvaluatePose()
        {
            float blend = !previousPlayable.IsValid() || Settings.transitionSeconds <= 0f ? 1f : Mathf.Clamp01(blendElapsed / Settings.transitionSeconds);
            mixer.SetInputWeight(0, blend); mixer.SetInputWeight(1, 1f - blend);
            graph.Evaluate(0f);
            // 루트 이동 때문에 프레임 탐색 때마다 캐릭터가 미끄러지지 않도록 테스트 위치를 고정한다.
            appliedRootOffset = Vector3.Lerp(previousRootOffset, currentRootOffset, blend);
            characterAnimator.transform.localPosition = actorPosition + appliedRootOffset;
            characterAnimator.transform.localRotation = actorRotation;
        }

        public void SetPaused(bool value) => paused = value;
        public void TogglePaused() => paused = !paused;
        public void Scrub(int rangeIndex, float frame)
        {
            if (!graph.IsValid()) return;
            Settings.Validate();
            var range = Settings.GetRange(rangeIndex);
            bool changed = ActiveRangeIndex != rangeIndex || Phase == PreviewPhase.Idle || Phase == PreviewPhase.Complete;
            ActiveRangeIndex = rangeIndex; paused = true;
            mode = rangeIndex == 1 ? PreviewMode.GetUpOnly : rangeIndex == 2 ? PreviewMode.Death : PreviewMode.HeavyOnly;
            Phase = rangeIndex == 1 ? PreviewPhase.GetUp : PreviewPhase.HeavyHit;
            // 탐색은 원본 전체 프레임을 볼 수 있어 잘라낼 구간을 정확히 고를 수 있다.
            currentFrame = Mathf.Clamp(frame, 0, range.MaximumFrame);
            if (changed) SetClip(range.clip, currentFrame / range.FrameRate);
            else currentPlayable.SetTime(currentFrame / range.FrameRate);
            previousRootOffset = currentRootOffset = Vector3.zero;
            blendElapsed = Settings.transitionSeconds;
            EvaluatePose();
        }
        public void StepFrame(int index, int direction) => Scrub(index, (index == ActiveRangeIndex ? Mathf.Round(currentFrame) : Settings.GetRange(index).startFrame) + direction);
        public void ResetPreview()
        {
            if (!graph.IsValid()) return;
            Phase = PreviewPhase.Idle; paused = false; idleTime = 0f; currentFrame = 0f; phaseElapsed = 0f;
            SetClip(Settings.idleClip, 0f); blendElapsed = Settings.transitionSeconds; EvaluatePose();
        }

        public void SaveSettings()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(savedSettings, "플레이어 피격 테스트 설정 저장");
            UnityEditor.EditorUtility.CopySerialized(runtimeSettings, savedSettings);
            savedSettings.hideFlags = HideFlags.None;
            savedSettings.name = "Test_PlayerReactionSettings";
            UnityEditor.EditorUtility.SetDirty(savedSettings);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(savedSettings);
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
        public void SetView(float yaw, float distance)
        { cameraYaw = yaw; cameraDistance = distance; }
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
            if (runtimeSettings != null) Destroy(runtimeSettings);
        }
    }
}
