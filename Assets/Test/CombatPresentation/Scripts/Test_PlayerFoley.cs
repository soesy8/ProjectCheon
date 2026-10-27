using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Tests.CombatPresentation
{
    // 테스트 씬의 이동 재질음과 플레이어 동작 소리 시점만 담당한다.
    public class Test_PlayerFoley : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("효과음")] private PassAudio sounds;
        [SerializeField, InspectorName("공격 효과 구간")] private PassFanAttackVfxProfile attackProfile;
        [SerializeField, InspectorName("흙 발걸음 변주")] private AudioClip[] dirtSteps;
        [SerializeField, InspectorName("돌 발걸음 변주")] private AudioClip[] stoneSteps;
        [SerializeField, Min(0f), InspectorName("왼발 착지 · Run2 원본 프레임")] private float leftContactFrame = 8f;
        [SerializeField, Min(0f), InspectorName("오른발 착지 · Run2 원본 프레임")] private float rightContactFrame = 18.5f;
        [SerializeField, Range(0f, 1f), InspectorName("걷기 크기")] private float walkGain = .7f;
        [SerializeField, Range(0f, 1f), InspectorName("달리기 크기")] private float sprintGain = .95f;
        [SerializeField, Range(0f, 4f), InspectorName("2타 휘두르기 선행 프레임")] private float secondSwingLeadFrames = 1f;
        [SerializeField, Min(.2f), InspectorName("발밑 탐색 거리")] private float groundProbeDistance = 1.2f;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private PassPlayerMovement movement;
        private PassAnimationController animationControl;
        private AudioSource movementAudio;
        private static readonly int LocomotionTime = Animator.StringToHash("LocomotionTime");
        private static readonly int MoveAmount = Animator.StringToHash("MoveAmount");
        private float previousStepFrame, previousStartFrame, previousEndFrame;
        private bool trackingSteps, hasLocomotionTime;
        private int pendingSwing, lastDirt = -1, lastStone = -1;
        private uint swingSequence;
        private bool initialized;

        public int PlayedStepCount { get; private set; }
        public AudioClip LastStepClip { get; private set; }
        public Test_FootstepSurface.GroundKind LastSurface { get; private set; }
        public float LastStepFrame { get; private set; }
        public bool LastStepWasLeft { get; private set; }
        public int PlayedSwingCount { get; private set; }
        public float LastSwingFrame { get; private set; }
        public int LastSwingAttack { get; private set; }

        public void Configure(PassGameFlow flow, PassAudio audio, PassFanAttackVfxProfile profile, AudioClip[] dirt, AudioClip[] stone)
        {
            game = flow; sounds = audio; attackProfile = profile; dirtSteps = dirt; stoneSteps = stone;
        }
        private void Start()
        {
            movement = GetComponent<PassPlayerMovement>();
            if (game == null || sounds == null || movement == null || attackProfile == null)
            { Debug.LogError("테스트 폴리: 게임·효과음·공격 설정 연결을 확인하세요.", this); enabled = false; return; }
            animationControl = movement.AnimationControl;
            var sources = sounds.GetComponents<AudioSource>();
            if (sources.Length != 3 || animationControl == null)
            { Debug.LogError("테스트 폴리: 동작 또는 발걸음 소스가 없습니다.", this); enabled = false; return; }
            // 기존 믹서와 일시정지 처리가 동일한 발걸음 소스를 제어하게 한다.
            movementAudio = sources[1];
            foreach (var parameter in animationControl.CharacterAnimator.parameters)
                if (parameter.nameHash == LocomotionTime) hasLocomotionTime = true;
            initialized = true;
            animationControl.StateChanging += OnStateChanging;
        }
        private void OnEnable() { if (initialized) animationControl.StateChanging += OnStateChanging; }
        private void OnDisable()
        {
            if (initialized) animationControl.StateChanging -= OnStateChanging;
            pendingSwing = 0; trackingSteps = false;
        }
        private void OnStateChanging(string from, string to, float duration)
        {
            pendingSwing = to == "ATTACK1" ? 1 : to == "ATTACK2" ? 2 : to == "ATTACK3" ? 3 : 0;
            swingSequence = 0;
            if (to == "ROLL") { sounds.Dodge(); trackingSteps = false; }
            if (pendingSwing > 0) trackingSteps = false;
            if (to == "DRAW" || to == "SHEATHE") sounds.Equip();
        }
        private void LateUpdate()
        {
            if (!initialized || !game.IsRunning || animationControl.IsPaused) return;
            if (pendingSwing > 0)
            {
                if (swingSequence == 0) swingSequence = animationControl.PlaybackSequence;
                if (animationControl.PlaybackSequence != swingSequence) pendingSwing = 0;
                else
                {
                    float frame = animationControl.NormalizedTime * animationControl.GetAttackMaximumFrame(pendingSwing);
                    // 2타는 긴 후딜까지 포함한 20% 대신 실제 부채 궤적의 시작을 기준으로 낸다.
                    bool ready = pendingSwing == 2
                        ? frame >= Mathf.Max(animationControl.GetAttackStartFrame(2), attackProfile.GetSweep(2).startFrame - secondSwingLeadFrames)
                        : animationControl.PlaybackProgress >= .2f;
                    if (ready)
                    {
                        sounds.Swing(pendingSwing);
                        LastSwingFrame = frame; LastSwingAttack = pendingSwing; PlayedSwingCount++;
                        pendingSwing = 0;
                    }
                }
            }
            if (!movement.InputEnabled || !movement.IsMoving || !movement.Body.isGrounded ||
                !animationControl.CanMove || !animationControl.IsRunning || animationControl.IsDodging)
            { trackingSteps = false; return; }
            UpdateFootContacts();
        }

        private void UpdateFootContacts()
        {
            var animator = animationControl.CharacterAnimator;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0)) state = animator.GetNextAnimatorStateInfo(0);
            bool runState = state.IsName("RUN2");
            if (!runState && !state.IsName("SHEATHED_IDLE") && !state.IsName("DRAWN_IDLE"))
            { trackingSteps = false; return; }
            // 대기에서 이동으로 섞이는 첫 순간에는 아직 발이 걷지 않으므로 재생을 기다린다.
            if (!runState && animator.GetFloat(MoveAmount) < .5f)
            { trackingSteps = false; return; }

            float start = animationControl.GetPlaybackStartFrame(5);
            float end = animationControl.GetPlaybackEndFrame(5);
            float length = Mathf.Max(1f, end - start);
            // 이 맵의 BlendTree는 normalizedTime 대신 LocomotionTime으로 실제 포즈를 평가한다.
            // 같은 값을 읽어야 걷기·달리기 속도 변경과 상체 발도 중에도 발 모션을 따라간다.
            float normalized = !runState && hasLocomotionTime && animationControl.UsesUpperBodyEquipment
                ? animator.GetFloat(LocomotionTime) : Mathf.Repeat(state.normalizedTime, 1f);
            float frame = start + Mathf.Repeat(normalized * animationControl.GetPlaybackMaximumFrame(5) - start, length);
            if (!trackingSteps || previousStartFrame != start || previousEndFrame != end)
            {
                trackingSteps = true;
                previousStepFrame = frame; previousStartFrame = start; previousEndFrame = end;
                return;
            }

            bool left = CrossedContact(leftContactFrame, frame, start, end);
            bool right = CrossedContact(rightContactFrame, frame, start, end);
            previousStepFrame = frame;
            if (!left && !right) return;
            // 프레임이 길어져 양발 시점을 모두 넘었으면 가장 최근 착지만 내어 중첩을 막는다.
            bool useLeft = left && (!right || Mathf.Repeat(frame - leftContactFrame, length) < Mathf.Repeat(frame - rightContactFrame, length));
            PlayFootstep(frame, useLeft);
        }

        private bool CrossedContact(float contact, float frame, float start, float end)
        {
            if (contact < start || contact >= end) return false;
            return frame >= previousStepFrame ? contact > previousStepFrame && contact <= frame :
                contact > previousStepFrame || contact <= frame;
        }

        private void PlayFootstep(float frame, bool left)
        {
            LastSurface = DetectSurface();
            AudioClip clip = LastSurface == Test_FootstepSurface.GroundKind.Stone
                ? SelectStep(stoneSteps, ref lastStone) : SelectStep(dirtSteps, ref lastDirt);
            if (clip != null)
            {
                movementAudio.pitch = Random.Range(.96f, 1.04f);
                movementAudio.PlayOneShot(clip, movement.IsSprinting ? sprintGain : walkGain);
                LastStepClip = clip; LastStepFrame = frame; LastStepWasLeft = left; PlayedStepCount++;
            }
        }
        private Test_FootstepSurface.GroundKind DetectSurface()
        {
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * .3f, Vector3.down,
                groundHits, groundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            var surface = Test_FootstepSurface.GroundKind.Dirt;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PassHealth>() != null ||
                    hit.normal.y < .35f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                var material = hit.collider.GetComponentInParent<Test_FootstepSurface>();
                surface = material != null ? material.Surface : Test_FootstepSurface.GroundKind.Dirt;
            }
            return surface;
        }
        private static AudioClip SelectStep(AudioClip[] clips, ref int previous)
        {
            if (clips == null || clips.Length == 0) return null;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == previous) index = (index + Random.Range(1, clips.Length)) % clips.Length;
            previous = index;
            return clips[index];
        }
    }
}
