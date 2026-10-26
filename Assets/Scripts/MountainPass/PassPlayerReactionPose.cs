using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Cheon.Prototype.MountainPass
{
    // 피격 클립의 수평 이동을 실제 충돌 몸체에 전달하고, 전환 위치와 머리 자세를 맞춘다.
    [DefaultExecutionOrder(50)]
    public class PassPlayerReactionPose : MonoBehaviour
    {
        [SerializeField] private PassAnimationController animationControl;
        [SerializeField] private CharacterController body;
        private Animator animator, samplingAnimator;
        private Transform visual, hips, neck, head;
        private GameObject samplingRoot;
        private PlayableGraph samplingGraph;
        private AnimationPlayableOutput samplingOutput;
        private AnimationClipPlayable samplingPlayable;
        private AnimationClip sampledClip;
        private Vector3 basePosition, sourceOffset, targetOffset, transferredOffset, hipAnchor;
        private float elapsed, duration, outgoingGetUpFrame;
        private bool active, incomingGetUp, outgoingGetUp, headAdjusted;
        private Quaternion neckBeforeBow, headBeforeBow;

        public void Configure(PassAnimationController controller, CharacterController collisionBody)
        { animationControl = controller; body = collisionBody; }

        private void Awake()
        {
            animator = animationControl.CharacterAnimator;
            visual = animator.transform; basePosition = visual.localPosition;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        private void OnEnable() { if (animationControl != null) animationControl.StateChanging += PrepareTransition; }
        private void OnDisable()
        {
            if (animationControl != null) animationControl.StateChanging -= PrepareTransition;
            ResetPose();
        }
        private void Update() => RestoreHeadBow();

        private void PrepareTransition(string from, string to, float seconds)
        {
            if (!PassAnimationController.IsReactionState(to) && !active) return;
            var settings = animationControl.ReactionSettings;
            if (settings == null) return;
            var clip = animationControl.ReactionPoseClip(to);
            if (clip == null) { ResetPose(); return; }
            if (!active)
            {
                hipAnchor = body.transform.InverseTransformPoint(hips.position);
                hipAnchor.y = 0f;
            }
            Vector3 outgoingHip = hips.position;
            sourceOffset = visual.localPosition - basePosition;
            Vector3 sampledHip = SampleHip(clip, animationControl.ReactionPoseStartTime(to));
            Vector3 currentPosition = visual.localPosition;
            visual.localPosition = basePosition;
            Vector3 difference = outgoingHip - visual.TransformPoint(sampledHip);
            difference.y = 0f;
            targetOffset = visual.parent.InverseTransformVector(difference);
            visual.localPosition = currentPosition;
            transferredOffset = Vector3.zero;
            outgoingGetUp = from == "GET_UP";
            outgoingGetUpFrame = animationControl.ReactionFrame;
            incomingGetUp = to == "GET_UP";
            duration = seconds; elapsed = 0f; active = true;
        }

        private void LateUpdate()
        {
            if (!active || visual == null) return;
            float seconds = animationControl.IsPaused ? 0f : Time.deltaTime;
            elapsed += seconds;
            float blend = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            visual.localPosition = basePosition + Vector3.Lerp(sourceOffset, targetOffset, blend) - transferredOffset;

            // 메시 안의 이동을 몸체로 옮긴다. 벽에 막힌 만큼은 메시에서도 제거해 뚫고 나가지 않게 한다.
            Vector3 displacement = hips.position - body.transform.TransformPoint(hipAnchor);
            displacement.y = 0f;
            Vector3 localDisplacement = visual.parent.InverseTransformVector(displacement);
            if (body.enabled) body.Move(displacement);
            transferredOffset += localDisplacement;
            visual.localPosition -= localDisplacement;

            var settings = animationControl.ReactionSettings;
            if (outgoingGetUp)
                outgoingGetUpFrame = Mathf.Min(settings.getUp.endFrame, outgoingGetUpFrame + seconds * settings.getUp.FrameRate * settings.getUp.speed);
            float bow = Mathf.Lerp(outgoingGetUp ? HeadBowWeight(outgoingGetUpFrame) : 0f,
                incomingGetUp ? HeadBowWeight(animationControl.ReactionFrame) : 0f, blend);
            ApplyHeadBow(settings.getUpHeadBowDegrees * bow);
            if (!animationControl.IsReactionPlaying && blend >= 1f)
            {
                // 대기·이동으로 돌아온 뒤에는 기존 Animator와 이동 코드가 계속 제어한다.
                Vector3 remaining = visual.parent.TransformVector(visual.localPosition - basePosition);
                remaining.y = 0f;
                if (body.enabled) body.Move(remaining);
                visual.localPosition = basePosition; active = false;
            }
        }

        private float HeadBowWeight(float frame)
        {
            var range = animationControl.ReactionSettings.getUp;
            float progress = Mathf.InverseLerp(range.startFrame, range.endFrame, frame);
            return Mathf.SmoothStep(0f, 1f, progress / 0.15f) * (1f - Mathf.SmoothStep(0f, 1f, (progress - 0.7f) / 0.3f));
        }
        private void ApplyHeadBow(float degrees)
        {
            if (head == null || degrees <= 0f) return;
            if (neck != null) neckBeforeBow = neck.localRotation;
            headBeforeBow = head.localRotation;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(degrees * 0.25f, visual.right) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(degrees * (neck != null ? 0.75f : 1f), visual.right) * head.rotation;
            headAdjusted = true;
        }
        private void RestoreHeadBow()
        {
            if (!headAdjusted) return;
            if (neck != null) neck.localRotation = neckBeforeBow;
            if (head != null) head.localRotation = headBeforeBow;
            headAdjusted = false;
        }
        public void ResetPose()
        {
            RestoreHeadBow();
            if (visual != null) visual.localPosition = basePosition;
            active = false; incomingGetUp = outgoingGetUp = false;
        }

        private Vector3 SampleHip(AnimationClip clip, float time)
        {
            if (!samplingGraph.IsValid())
            {
                samplingRoot = Instantiate(animator.gameObject, visual.parent);
                samplingRoot.name = "PlayerReactionPoseSampler"; samplingRoot.hideFlags = HideFlags.HideAndDontSave;
                foreach (var renderer in samplingRoot.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var behaviour in samplingRoot.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                samplingAnimator = samplingRoot.GetComponent<Animator>();
                foreach (var other in samplingRoot.GetComponentsInChildren<Animator>(true))
                    if (other != samplingAnimator) other.enabled = false;
                samplingAnimator.runtimeAnimatorController = null; samplingAnimator.applyRootMotion = false;
                samplingAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                samplingGraph = PlayableGraph.Create("PlayerReactionPoseSampler");
                samplingGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                samplingOutput = AnimationPlayableOutput.Create(samplingGraph, "Pose", samplingAnimator);
                samplingGraph.Play();
            }
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
        private void OnDestroy()
        {
            if (samplingGraph.IsValid()) samplingGraph.Destroy();
            if (samplingRoot != null) Destroy(samplingRoot);
        }
    }
}
