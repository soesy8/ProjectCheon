using System.Collections.Generic;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 입력 프레임부터 상태를 섞고 실제 클립 진행률로 부채 연출을 동기화한다.
    public class PassAnimationController : MonoBehaviour
    {
        [Header("재생 대상")]
        [InspectorName("캐릭터 애니메이터")]
        [SerializeField] private Animator characterAnimator;
        [InspectorName("부채 장착 제어기")]
        [SerializeField] private PassFanAttachment fanAttachment;
        [Header("공격별 속도 배율 — 재생 중 변경 가능")]
        // 플레이 중 조절하고 저장한 수치를 새 컴포넌트의 기본값으로 사용한다.
        [InspectorName("1타 재생 배속")]
        [Min(0.05f)] public float attack1Speed = 1.8f;
        [InspectorName("2타 재생 배속")]
        [Min(0.05f)] public float attack2Speed = 1.3f;
        [InspectorName("3타 재생 배속")]
        [Min(0.05f)] public float attack3Speed = 1.5f;
        [Header("공격별 재생 구간 — 원본 FBX의 0부터 시작하는 프레임")]
        [InspectorName("1타 원본 애니메이션")]
        [SerializeField] private AnimationClip attack1Clip;
        [InspectorName("1타 시작 프레임")]
        [Min(0)] public int attack1StartFrame = 6;
        [InspectorName("1타 종료 프레임")]
        [Min(1)] public int attack1EndFrame = 68;
        [InspectorName("2타 원본 애니메이션")]
        [SerializeField] private AnimationClip attack2Clip;
        [InspectorName("2타 시작 프레임")]
        [Min(0)] public int attack2StartFrame = 20;
        [InspectorName("2타 종료 프레임")]
        [Min(1)] public int attack2EndFrame = 95;
        [InspectorName("3타 원본 애니메이션")]
        [SerializeField] private AnimationClip attack3Clip;
        [InspectorName("3타 시작 프레임")]
        [Min(0)] public int attack3StartFrame = 5;
        [InspectorName("3타 종료 프레임")]
        [Min(1)] public int attack3EndFrame = 75;
        [Header("구르기 속도 / 재생 구간 — 원본 FBX 프레임")]
        [InspectorName("구르기 원본 애니메이션")]
        [SerializeField] private AnimationClip rollClip;
        [InspectorName("구르기 무적 시간 (초)")]
        [SerializeField, Min(0f)] private float dodgeInvulnerabilitySeconds = .35f;
        [InspectorName("구르기 재생 배속")]
        [Min(0.05f)] [HideInInspector] public float rollSpeed = 1.25f;
        [InspectorName("구르기 시작 프레임")]
        [Min(0)] [HideInInspector] public int rollStartFrame;
        [InspectorName("구르기 종료 프레임")]
        [Min(1)] [HideInInspector] public int rollEndFrame = 28;
        [Tooltip("발도·납도 상태 모두 구르기 원본 클립의 프레임을 기준으로 공격 입력을 이만큼 먼저 허용합니다.")]
        [InspectorName("구르기 후 공격 전환 선행 프레임")]
        [SerializeField, Min(0)] private int dodgeAttackAdvanceFrames = 7;
        [Header("달리기2 속도 / 재생 구간 — 원본 FBX 프레임")]
        [InspectorName("달리기 원본 애니메이션")]
        [SerializeField] private AnimationClip run2Clip;
        [InspectorName("달리기 재생 배속")]
        [Min(0.05f)] [HideInInspector] public float run2Speed = 1.6f;
        [InspectorName("달리기 시작 프레임")]
        [Min(0)] [HideInInspector] public int run2StartFrame;
        [InspectorName("달리기 종료 프레임")]
        [Min(1)] [HideInInspector] public int run2EndFrame = 21;
        [Header("부드러운 전환 — 입력 즉시 전환 시작")]
        [InspectorName("동작·환경 전환 시간 (초)")]
        [SerializeField, Range(0.01f, 0.3f)] private float transitionDuration = 0.09f;
        [InspectorName("대기 복귀 혼합 시간 (초)")]
        [SerializeField, Range(0.01f, 0.4f)] private float returnBlendDuration = 0.18f;
        [InspectorName("이동 동작 혼합 시간 (초)")]
        [SerializeField, Range(0.01f, 0.4f)] private float locomotionBlendDuration = 0.12f;
        [Header("발도 / 납도")]
        [InspectorName("발도 재생 배속")]
        [SerializeField, Min(0.05f)] private float drawSpeed = 1f;
        [Tooltip("이동 중 발도 배속입니다. 1.9는 기존보다 90% 빠른 재생입니다.")]
        [InspectorName("이동 중 발도 추가 배속")]
        [SerializeField, Min(1f)] private float movingDrawSpeedMultiplier = 1.9f;
        [InspectorName("납도 재생 배속")]
        [SerializeField, Min(0.05f)] private float sheatheSpeed = 1f;
        [InspectorName("발도·납도 원본 길이 (초)")]
        [SerializeField, Min(0.01f)] private float equipmentClipLength = 1.2333333f;
        [InspectorName("부채를 손에 쥐는 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float drawGrabTime = 0.48f;
        [InspectorName("부채 펼치기 시작 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float drawOpenStart = 0.62f;
        [InspectorName("부채 펼치기 종료 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float drawOpenEnd = 0.94f;
        [InspectorName("부채 접기 시작 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float sheatheCloseStart = 0.06f;
        [InspectorName("부채 접기 종료 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float sheatheCloseEnd = 0.38f;
        [InspectorName("부채 허리 수납 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float sheatheStowTime = 0.52f;
        [Header("입력 취소·동작 전환")]
        [InspectorName("입력 취소·동작 전환 설정")]
        [SerializeField] private PassActionTimings timingSettings;
        [InspectorName("이동 중 발도·납도 사용")]
        [SerializeField] private bool equipmentWhileMoving;
        [Header("맵 플레이용 재생 수치")]
        [InspectorName("달리기·구르기 재생 설정")]
        [SerializeField] private PassPlaybackSettings playbackSettings;
        [Header("강피격 · 일어나기 · 사망")]
        [InspectorName("강피격·기상·사망 설정")]
        [SerializeField] private PassPlayerReactionSettings reactionSettings;

        private PassActionTimings runtimeTimings;
        private readonly Dictionary<int, float> appliedParameters = new Dictionary<int, float>(10);
        private RuntimeAnimatorController parameterController;
        private bool hasRollState, hasRunState;
        private int equipmentLayerIndex = -1;
        private float upperBodyBlendDuration = 0.12f;
        private float locomotionSpeedMultiplier = 1f;
        private float locomotionCycle;
        private bool hasLocomotionTime;
        private bool movingDrawActive;
        private float movingDrawProgress;
        private float reactionFrame, landedSeconds = -1f, downHoldElapsed, fadingReactionFrame, reactionFadeRemaining;
        private string fadingReactionState;

        private string selectedState = "SHEATHED_IDLE";
        private bool combatRequested, running, paused;
        private float dodgeStartedAt;
        private static readonly int Attack1Speed = Animator.StringToHash("Attack1Speed");
        private static readonly int Attack2Speed = Animator.StringToHash("Attack2Speed");
        private static readonly int Attack3Speed = Animator.StringToHash("Attack3Speed");
        private static readonly int RollSpeed = Animator.StringToHash("RollSpeed");
        private static readonly int Run2Speed = Animator.StringToHash("Run2Speed");
        private static readonly int DrawSpeed = Animator.StringToHash("DrawSpeed");
        private static readonly int SheatheSpeed = Animator.StringToHash("SheatheSpeed");
        private static readonly int MoveAmount = Animator.StringToHash("MoveAmount");
        private static readonly int LocomotionSpeed = Animator.StringToHash("LocomotionSpeed");
        private static readonly int LocomotionTime = Animator.StringToHash("LocomotionTime");

        public Animator CharacterAnimator => characterAnimator;
        public string SelectedState => selectedState;
        public bool IsPaused => paused;
        public bool IsCombatRequested => combatRequested;
        public bool IsRunning => running;
        public bool IsDodging => selectedState == "ROLL";
        public bool IsDodgeInvulnerable => IsDodging && Time.time - dodgeStartedAt < dodgeInvulnerabilitySeconds;
        public uint PlaybackSequence { get; private set; }
        public event System.Action BeforeAutomaticReturn;
        public event System.Action<string, string, float> StateChanging;
        public PassPlayerReactionSettings ReactionSettings => reactionSettings;
        public bool IsReactionPlaying => reactionSettings != null && IsReactionState(selectedState);
        public float ReactionFrame => reactionFrame;
        public float LandedSeconds => Mathf.Max(0f, landedSeconds);
        public bool CanRecoverFromHeavy => reactionSettings != null && selectedState == "HEAVY_HIT" && !paused && landedSeconds >= reactionSettings.landedRecoveryDelay;
        public float RollDuration => rollClip != null ?
            (GetPlaybackEndFrame(4) - GetPlaybackStartFrame(4)) / rollClip.frameRate / Mathf.Max(0.05f, rollSpeed) : 1f;
        public bool CanAttack => !IsReactionPlaying && combatRequested && (runtimeTimings == null || !paused);
        public bool IsActionPlaying => IsReactionPlaying || IsAction(selectedState) || IsEquipmentTransition;
        public PassActionTimings RuntimeTimings => runtimeTimings;
        public PassActionTimings SavedTimings => timingSettings;
        public PassPlaybackSettings SavedPlaybackSettings => playbackSettings;
        public bool UsesUpperBodyEquipment => equipmentWhileMoving && equipmentLayerIndex >= 0;
        public bool CanMove => !paused && (IsReactionPlaying ? selectedState == "GET_UP" && PlaybackProgress + 0.00001f >= reactionSettings.getUpMoveCancelProgress :
            UsesUpperBodyEquipment && IsEquipmentTransition || CurrentTiming == null || PlaybackProgress >= CurrentTiming.moveCancel);
        public bool CanDodge => !paused && !IsStatePlaying("ROLL") && (IsReactionPlaying ? selectedState == "GET_UP" && PlaybackProgress + 0.00001f >= reactionSettings.getUpDodgeCancelProgress :
            CurrentTiming == null || PlaybackProgress >= CurrentTiming.dodgeCancel);
        public bool CanChangeEquipment => !IsReactionPlaying && !paused && (CurrentTiming == null || PlaybackProgress >= CurrentTiming.equipmentCancel);
        private PassActionTimings.ActionTiming CurrentTiming => runtimeTimings != null ? runtimeTimings.Get(selectedState) : null;
        public bool IsEquipmentTransition => selectedState == "DRAW" || selectedState == "SHEATHE";
        public bool CanPreviewFan => combatRequested && !IsEquipmentTransition;
        public float NormalizedTime => IsReactionPlaying ? reactionFrame / (reactionSettings.GetRange(selectedState).FrameRate * reactionSettings.GetRange(selectedState).clip.length) :
            TryGetSelectedState(out var state) ? state.normalizedTime : 0f;
        public float EquipmentProgress => Mathf.Clamp01(selectedState == "SHEATHE" ? 1f - NormalizedTime : NormalizedTime);
        public float PlaybackProgress
        {
            get
            {
                if (IsReactionPlaying)
                {
                    var range = reactionSettings.GetRange(selectedState);
                    return Mathf.InverseLerp(range.startFrame, range.endFrame, reactionFrame);
                }
                var clip = PlaybackClip(selectedState);
                if (clip == null) return IsEquipmentTransition ? EquipmentProgress :
                    IsAction(selectedState) ? Mathf.Clamp01(NormalizedTime) : Mathf.Repeat(NormalizedTime, 1f);
                int index = PlaybackIndex(selectedState);
                float time = IsFullRun2Loop ? Mathf.Repeat(NormalizedTime, 1f) : NormalizedTime;
                return Mathf.InverseLerp(GetPlaybackStartFrame(index), GetPlaybackEndFrame(index), time * clip.length * clip.frameRate);
            }
        }

        public bool IsStatePlaying(string state)
        {
            // Animator가 평가되기 전의 새 요청과 전환 중 남아 있는 동작도 중복으로 본다.
            if (selectedState == state) return true;
            if (characterAnimator == null || !characterAnimator.isInitialized) return false;
            int layer = UsesUpperBodyEquipment && (state == "DRAW" || state == "SHEATHE") ? equipmentLayerIndex : 0;
            if (layer > 0 && characterAnimator.GetLayerWeight(layer) <= 0.001f) return false;
            return characterAnimator.GetCurrentAnimatorStateInfo(layer).IsName(state) ||
                characterAnimator.IsInTransition(layer) && characterAnimator.GetNextAnimatorStateInfo(layer).IsName(state);
        }

        public float SecondsUntilAttackCancel(int index)
        {
            if (paused || index < 1 || index > 3 || !CanAttack || IsStatePlaying("ATTACK" + index))
                return float.PositiveInfinity;
            return SecondsUntilCancel(AdvanceDodgeAttackCancel(CurrentTiming != null ? CurrentTiming.AttackCancel(index) : 0f));
        }

        public float SecondsUntilAttackInput(int index)
        {
            if (index < 1 || index > 3) return float.PositiveInfinity;
            if (combatRequested) return SecondsUntilAttackCancel(index);
            if (paused || IsReactionPlaying) return float.PositiveInfinity;
            // 납도한 채 구른 뒤의 발도·이동 공격도 같은 원본 프레임만큼 먼저 예약하고 실행한다.
            return SecondsUntilCancel(AdvanceDodgeAttackCancel(CurrentTiming != null ? CurrentTiming.equipmentCancel : 0f));
        }

        private float AdvanceDodgeAttackCancel(float cancelProgress)
        {
            if (!IsDodging) return cancelProgress;
            int frameCount = Mathf.Max(1, GetPlaybackEndFrame(4) - GetPlaybackStartFrame(4));
            return Mathf.Max(0f, cancelProgress - (float)dodgeAttackAdvanceFrames / frameCount);
        }

        public float SecondsUntilDodgeCancel()
        {
            if (IsReactionPlaying) return CanDodge ? 0f : float.PositiveInfinity;
            if (paused || IsStatePlaying("ROLL") || !characterAnimator.HasState(0, Animator.StringToHash("ROLL")))
                return float.PositiveInfinity;
            return SecondsUntilCancel(CurrentTiming != null ? CurrentTiming.dodgeCancel : 0f);
        }

        public float SecondsUntilEquipmentCancel()
        {
            return paused || IsReactionPlaying ? float.PositiveInfinity : SecondsUntilCancel(CurrentTiming != null ? CurrentTiming.equipmentCancel : 0f);
        }

        private float SecondsUntilCancel(float cancelProgress)
        {
            if (CurrentTiming == null || PlaybackProgress >= cancelProgress) return 0f;
            float duration;
            var clip = PlaybackClip(selectedState);
            if (clip != null)
            {
                int index = PlaybackIndex(selectedState);
                duration = (GetPlaybackEndFrame(index) - GetPlaybackStartFrame(index)) / clip.frameRate / CurrentSpeed();
            }
            else if (IsEquipmentTransition) duration = equipmentClipLength / CurrentSpeed();
            else duration = TryGetSelectedState(out var state) ? state.length / CurrentSpeed() : 0f;
            // 시작·종료 프레임과 배속이 달라도 실제 초 단위로 같은 예약 창을 사용한다.
            return Mathf.Max(0f, cancelProgress - PlaybackProgress) * duration;
        }

        public void Configure(Animator animator, PassFanAttachment attachment, float clipLength = 1.2333333f)
        {
            characterAnimator = animator; fanAttachment = attachment; equipmentClipLength = clipLength;
        }

        public void ConfigureMapPlayback(PassActionTimings settings, bool useUpperBodyEquipment = false)
        {
            timingSettings = settings;
            equipmentWhileMoving = useUpperBodyEquipment;
        }

        public void ConfigurePlaybackSettings(PassPlaybackSettings settings)
        {
            playbackSettings = settings;
            ReloadSavedPlaybackSettings();
        }
        public void ConfigureReactions(PassPlayerReactionSettings settings) => reactionSettings = settings;

        public void CopyPlaybackTo(PassPlaybackSettings settings)
        {
            settings.rollSpeed = rollSpeed;
            settings.rollStartFrame = GetPlaybackStartFrame(4);
            settings.rollEndFrame = GetPlaybackEndFrame(4);
            settings.run2Speed = run2Speed;
            settings.run2StartFrame = GetPlaybackStartFrame(5);
            settings.run2EndFrame = GetPlaybackEndFrame(5);
            settings.Validate(GetPlaybackMaximumFrame(4), GetPlaybackMaximumFrame(5));
        }

        public void ReloadSavedPlaybackSettings()
        {
            if (playbackSettings == null) return;
            rollSpeed = Mathf.Max(0.05f, playbackSettings.rollSpeed);
            run2Speed = Mathf.Max(0.05f, playbackSettings.run2Speed);
            SetPlaybackFrameRange(4, playbackSettings.rollStartFrame, playbackSettings.rollEndFrame);
            SetPlaybackFrameRange(5, playbackSettings.run2StartFrame, playbackSettings.run2EndFrame);
        }

        private void Awake()
        {
            ReloadSavedPlaybackSettings();
            // 상태 판단용 사본을 사용하고 통합 설정 창에서 에셋을 바꾸면 함께 갱신한다.
            if (timingSettings != null)
            {
                runtimeTimings = Instantiate(timingSettings);
                runtimeTimings.name = timingSettings.name;
            }
        }

        private void OnDestroy()
        {
            if (runtimeTimings != null) Destroy(runtimeTimings);
        }

        private void Start()
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null || fanAttachment == null)
            {
                Debug.LogError("캐릭터 Animator와 부채 연결을 확인하세요.", this);
                enabled = false; return;
            }
            characterAnimator.applyRootMotion = false;
            equipmentLayerIndex = equipmentWhileMoving ? characterAnimator.GetLayerIndex("EquipmentUpperBody") : -1;
            if (equipmentWhileMoving && equipmentLayerIndex < 0)
                Debug.LogError("이동 중 발도·납도에 필요한 EquipmentUpperBody 레이어가 없습니다.", this);
            if (UsesUpperBodyEquipment) characterAnimator.SetLayerWeight(equipmentLayerIndex, 0f);
            foreach (var parameter in characterAnimator.parameters)
                if (parameter.nameHash == LocomotionTime) hasLocomotionTime = true;
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
            UpdateReactionPlayback(Time.deltaTime);
            ApplyAttackSpeeds();
            if (!paused) characterAnimator.SetFloat(MoveAmount, running ? 1f : 0f, locomotionBlendDuration, Time.deltaTime);
            if (!paused && UsesUpperBodyEquipment)
            {
                if (movingDrawActive)
                {
                    movingDrawProgress = Mathf.Clamp01(movingDrawProgress + Time.deltaTime * drawSpeed * movingDrawSpeedMultiplier / equipmentClipLength);
                    // 공격은 기본 레이어에서 즉시 시작하고 빠른 발도 상체를 짧게 겹친다.
                    fanAttachment.SetSequencePose(true, Mathf.SmoothStep(0f, 1f, movingDrawProgress));
                    if (movingDrawProgress >= 1f) movingDrawActive = false;
                }
                // 하체는 기본 레이어에서 계속 이동하고 상체 레이어의 영향만 부드럽게 켜고 끈다.
                float targetWeight = IsEquipmentTransition ? 1f : movingDrawActive ?
                    0.65f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.8f, movingDrawProgress))) : 0f;
                characterAnimator.SetLayerWeight(equipmentLayerIndex, Mathf.MoveTowards(
                    characterAnimator.GetLayerWeight(equipmentLayerIndex), targetWeight, Time.deltaTime / Mathf.Max(0.01f, upperBodyBlendDuration)));
                UpdateLocomotionTime();
            }
        }

        private void UpdateLocomotionTime()
        {
            if (!hasLocomotionTime || run2Clip == null) return;
            int start = GetPlaybackStartFrame(5);
            int end = GetPlaybackEndFrame(5);
            float duration = running ? (end - start) / run2Clip.frameRate :
                Mathf.Max(0.01f, characterAnimator.GetCurrentAnimatorStateInfo(0).length);
            float speed = running ? Mathf.Max(0.05f, run2Speed * locomotionSpeedMultiplier) : 1f;
            locomotionCycle = Mathf.Repeat(locomotionCycle + Time.deltaTime * speed / duration, 1f);
            // BlendTree도 원본 FBX의 지정 구간 안에서 평가해 이동 중 발도·납도와 함께 사용할 수 있다.
            float time = running ? Mathf.Lerp(start, end, locomotionCycle) / GetPlaybackMaximumFrame(5) : locomotionCycle;
            characterAnimator.SetFloat(LocomotionTime, time);
        }

        private void LateUpdate()
        {
            if (IsReactionPlaying)
            {
                if (!paused && selectedState == "HEAVY_HIT" && reactionFrame >= reactionSettings.heavyHit.endFrame &&
                    downHoldElapsed >= reactionSettings.downHoldSeconds && CanRecoverFromHeavy) BeginGetUp();
                else if (!paused && selectedState == "GET_UP" && PlaybackProgress + 0.00001f >= reactionSettings.getUpMoveCancelProgress)
                    BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", reactionSettings.returnToIdleSeconds);
                return;
            }
            if (paused || !TryGetSelectedState(out var state)) return;
            // 예약을 먼저 소비해 캔슬 100%와 자동 복귀가 같은 프레임에 겹쳐도 입력을 잃지 않는다.
            uint sequence = PlaybackSequence;
            BeforeAutomaticReturn?.Invoke();
            if (PlaybackSequence != sequence) return;
            if (CurrentTiming != null)
            {
                if (IsEquipmentTransition) UpdateEquipmentPose(EquipmentProgress);
                if (PlaybackProgress >= CurrentTiming.autoReturn)
                {
                    fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
                    BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", returnBlendDuration);
                }
                return;
            }
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
            if ((!IsAction(selectedState) && selectedState != "RUN2") || characterAnimator.IsInTransition(0)) return;
            // 전체 구간 달리기는 FBX의 자연스러운 루프를 유지하고, 잘라낸 구간만 직접 되감는다.
            if (IsFullRun2Loop) return;
            // 클립 끝에서 대기 포즈로 순간 이동하지 않도록 마지막 구간에서 섞기 시작한다.
            var clip = PlaybackClip(selectedState);
            float blendDuration = returnBlendDuration;
            float returnAt;
            if (clip != null)
            {
                int index = PlaybackIndex(selectedState);
                float startTime = GetPlaybackStartFrame(index) / clip.frameRate;
                float endTime = GetPlaybackEndFrame(index) / clip.frameRate;
                blendDuration = Mathf.Min(returnBlendDuration, (endTime - startTime) / CurrentSpeed() * 0.25f);
                returnAt = (endTime - blendDuration * CurrentSpeed()) / clip.length;
            }
            else returnAt = 1f - Mathf.Min(0.15f, returnBlendDuration / Mathf.Max(0.01f, state.length));
            if (state.normalizedTime < returnAt) return;
            if (selectedState == "RUN2") BeginState(selectedState, blendDuration);
            else BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", blendDuration);
        }

        public void ApplyAttackSpeeds()
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;
            if (parameterController != characterAnimator.runtimeAnimatorController)
            {
                parameterController = characterAnimator.runtimeAnimatorController; appliedParameters.Clear();
                hasRollState = characterAnimator.HasState(0, Animator.StringToHash("ROLL"));
                hasRunState = characterAnimator.HasState(0, Animator.StringToHash("RUN2"));
            }
            SetSpeedParameter(Attack1Speed, Mathf.Max(0.05f, attack1Speed));
            SetSpeedParameter(Attack2Speed, Mathf.Max(0.05f, attack2Speed));
            SetSpeedParameter(Attack3Speed, Mathf.Max(0.05f, attack3Speed));
            // 같은 스크립트를 사용하는 맵 전용 Animator에는 추가 상태가 없을 수 있다.
            if (hasRollState) SetSpeedParameter(RollSpeed, Mathf.Max(0.05f, rollSpeed));
            if (hasRunState) SetSpeedParameter(Run2Speed, Mathf.Max(0.05f, run2Speed));
            if (UsesUpperBodyEquipment)
                SetSpeedParameter(LocomotionSpeed, running ? Mathf.Max(0.05f, run2Speed * locomotionSpeedMultiplier) : 1f);
            SetSpeedParameter(DrawSpeed, Mathf.Max(0.05f, drawSpeed * (movingDrawActive ? movingDrawSpeedMultiplier : 1f)));
            // 속도 파라미터가 상태 속도를 대체하므로 역재생 부호를 파라미터에 직접 넣는다.
            SetSpeedParameter(SheatheSpeed, -Mathf.Max(0.05f, sheatheSpeed));
        }

        private void SetSpeedParameter(int parameter, float value)
        {
            if (appliedParameters.TryGetValue(parameter, out float previous) && Mathf.Approximately(previous, value)) return;
            appliedParameters[parameter] = value;
            characterAnimator.SetFloat(parameter, value);
        }

        private void BeginState(string state, float blendDuration, float offsetSeconds = -1f)
        {
            // 애니메이션 배속·재생 구간과 별개로 구르기 시작부터 지정한 게임 시간만 무적이다.
            if (state == "ROLL") dodgeStartedAt = Time.time;
            movingDrawActive = false;
            if (CurrentTiming != null && !IsReactionState(state)) blendDuration = CurrentTiming.blendDuration;
            if (reactionSettings != null && selectedState == "GET_UP" && (state == "ROLL" || running))
                blendDuration = reactionSettings.inputTransitionSeconds;
            StateChanging?.Invoke(selectedState, state, blendDuration);
            if (IsReactionPlaying && selectedState != state)
            { fadingReactionState = selectedState; fadingReactionFrame = reactionFrame; reactionFadeRemaining = blendDuration; }
            if (reactionSettings != null && IsReactionState(state))
            {
                reactionFrame = reactionSettings.GetRange(state).startFrame;
                if (state != "GET_UP") landedSeconds = state == "HEAVY_HIT" && reactionFrame >= reactionSettings.landingFrame ? 0f : -1f;
                downHoldElapsed = 0f;
                SetReactionFrame(state, reactionFrame);
            }
            upperBodyBlendDuration = blendDuration;
            PlaybackSequence++;
            selectedState = state; paused = false;
            characterAnimator.speed = 1f; fanAttachment.SetPaused(false); ApplyAttackSpeeds();
            float startOffset = offsetSeconds >= 0f ? offsetSeconds : state == "SHEATHE" ? equipmentClipLength : PlaybackStartTime(state);
            // 고정 시간 오프셋에는 상태 속도가 곱해지므로, 원본 시작 프레임을 유지하도록 보정한다.
            if (PlaybackClip(state) != null) startOffset /= CurrentSpeed();
            // Exit Time을 기다리거나 입력을 큐에 저장하지 않고 곧바로 새 클립을 시작한다.
            if (UsesUpperBodyEquipment && IsEquipmentTransition)
            {
                // 발도·납도 클립은 상체에만 재생하고 기본 레이어에는 이동 BlendTree를 유지한다.
                BlendBaseLocomotion(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", blendDuration);
                characterAnimator.CrossFadeInFixedTime(state, blendDuration, equipmentLayerIndex, startOffset, 0f);
            }
            else if (UsesUpperBodyEquipment && (state == "DRAWN_IDLE" || state == "SHEATHED_IDLE"))
                BlendBaseLocomotion(state, blendDuration);
            else characterAnimator.CrossFadeInFixedTime(state, blendDuration, 0, startOffset, 0f);
        }

        private void BlendBaseLocomotion(string state, float blendDuration)
        {
            var current = characterAnimator.GetCurrentAnimatorStateInfo(0);
            if (characterAnimator.IsInTransition(0))
            {
                if (characterAnimator.GetNextAnimatorStateInfo(0).IsName(state)) return;
            }
            else if (current.IsName(state)) return;

            // 발도 상태만 바뀌어도 동일한 달리기 클립의 보행 주기는 이어서 재생한다.
            bool wasLocomotion = current.IsName("DRAWN_IDLE") || current.IsName("SHEATHED_IDLE");
            float offset = running && wasLocomotion ? Mathf.Repeat(current.normalizedTime, 1f) * current.length : 0f;
            characterAnimator.CrossFadeInFixedTime(state, blendDuration, 0, offset, 0f);
        }

        public void EnterCombat()
        {
            if (combatRequested) return;
            float offset = selectedState == "SHEATHE" ? Mathf.Clamp01(NormalizedTime) * equipmentClipLength : 0f;
            combatRequested = true;
            if (!UsesUpperBodyEquipment) running = false;
            BeginState("DRAW", transitionDuration, offset);
        }

        public bool TryAttackInput(int index, bool moving, bool sprinting)
        {
            if (combatRequested) return TryPlayAttack(index);
            if (SecondsUntilAttackInput(index) > 0f) return false;
            if (!moving)
            {
                EnterCombat();
                return true;
            }

            // 납도 상태의 이동 공격은 걷기 1타, 달리기 2타를 같은 프레임에 시작한다.
            combatRequested = true;
            int openingAttack = sprinting ? 2 : 1;
            PlayAttack("ATTACK" + openingAttack, true);
            if (UsesUpperBodyEquipment)
            {
                movingDrawActive = true;
                movingDrawProgress = 0f;
                upperBodyBlendDuration = transitionDuration;
                ApplyAttackSpeeds();
                characterAnimator.CrossFadeInFixedTime("DRAW", transitionDuration, equipmentLayerIndex, 0f, 0f);
                fanAttachment.SetSequencePose(true, 0f);
            }
            return true;
        }

        public void ExitCombat()
        {
            if (!combatRequested) return;
            float offset = selectedState == "DRAW" ? Mathf.Clamp01(NormalizedTime) * equipmentClipLength : equipmentClipLength;
            if (!IsEquipmentTransition) fanAttachment.SetSequencePose(true, 1f);
            combatRequested = false;
            if (!UsesUpperBodyEquipment) running = false;
            BeginState("SHEATHE", transitionDuration, offset);
        }

        public void PlayDrawSheathe() { if (combatRequested) ExitCombat(); else EnterCombat(); }
        public bool TryToggleEquipment()
        {
            if (!CanChangeEquipment) return false;
            PlayDrawSheathe();
            return true;
        }

        public bool CanPlayAttack(int index)
        {
            return index >= 1 && index <= 3 && CanAttack && !IsStatePlaying("ATTACK" + index) &&
                (CurrentTiming == null || PlaybackProgress >= AdvanceDodgeAttackCancel(CurrentTiming.AttackCancel(index)));
        }

        public bool TryPlayAttack(int index)
        {
            if (!CanPlayAttack(index)) return false;
            PlayAttack("ATTACK" + index);
            return true;
        }

        public void SetLocomotion(bool moving, float speedMultiplier = 1f)
        {
            if (!moving) { running = false; return; }
            if (!CanMove) return;
            locomotionSpeedMultiplier = Mathf.Max(0.05f, speedMultiplier);
            if (UsesUpperBodyEquipment && IsEquipmentTransition) running = true;
            else if (IsActionPlaying)
            {
                running = true;
                fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
                BeginState(combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE", transitionDuration);
            }
            else running = true;
        }

        public void ReloadSavedTimings()
        {
            if (timingSettings != null && runtimeTimings != null)
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(timingSettings), runtimeTimings);
        }
        public void PlayIdle()
        {
            running = false;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            string state = combatRequested ? "DRAWN_IDLE" : "SHEATHED_IDLE";
            if (selectedState == state && !paused) return;
            BeginState(state, transitionDuration);
        }
        public void PlayRun() => PlayRun2();
        public void PlayRun2()
        {
            if (IsStatePlaying("RUN2") || !characterAnimator.HasState(0, Animator.StringToHash("RUN2"))) return;
            running = true;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            BeginState("RUN2", PlaybackBlendDuration("RUN2", locomotionBlendDuration));
        }
        public void PlayRoll()
        {
            if (IsStatePlaying("ROLL") || !characterAnimator.HasState(0, Animator.StringToHash("ROLL"))) return;
            running = false;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            BeginState("ROLL", PlaybackBlendDuration("ROLL", transitionDuration));
        }
        public bool TryPlayDodge()
        {
            if (!CanDodge || !characterAnimator.HasState(0, Animator.StringToHash("ROLL"))) return false;
            PlayRoll();
            return true;
        }
        public void PlayAttack1() => PlayAttack("ATTACK1");
        public void PlayAttack2() => PlayAttack("ATTACK2");
        public void PlayAttack3() => PlayAttack("ATTACK3");
        private void PlayAttack(string state, bool openingAttack = false)
        {
            int index = state == "ATTACK1" ? 1 : state == "ATTACK2" ? 2 : 3;
            if (!openingAttack && !CanPlayAttack(index)) return;
            running = false;
            fanAttachment.SetSequencePose(true, 1f);
            var clip = PlaybackClip(state);
            float segmentDuration = clip != null ? (GetAttackEndFrame(index) - GetAttackStartFrame(index)) / clip.frameRate / Mathf.Max(0.05f, CurrentAttackSpeed(index)) : 1f;
            BeginState(state, Mathf.Min(transitionDuration, segmentDuration * 0.25f));
        }
        public void PlayHit()
        {
            if (IsReactionPlaying || IsStatePlaying("HIT")) return;
            running = false;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            BeginState("HIT", transitionDuration);
        }

        public static bool IsReactionState(string state) => state == "HEAVY_HIT" || state == "GET_UP" || state == "DEATH";
        public AnimationClip ReactionPoseClip(string state) => reactionSettings == null ? null : IsReactionState(state) ? reactionSettings.GetRange(state).clip :
            state == "DRAWN_IDLE" ? reactionSettings.drawnIdleClip : state == "SHEATHED_IDLE" ? reactionSettings.sheathedIdleClip : PlaybackClip(state);
        public float ReactionPoseStartTime(string state) => IsReactionState(state) && reactionSettings != null ?
            reactionSettings.GetRange(state).startFrame / reactionSettings.GetRange(state).FrameRate : PlaybackStartTime(state);
        public void PlayHeavyHit(bool lethal = false)
        {
            if (reactionSettings == null) { PlayHit(); return; }
            running = false;
            fanAttachment.SetSequencePose(combatRequested, combatRequested ? 1f : 0f);
            if (UsesUpperBodyEquipment) characterAnimator.SetLayerWeight(equipmentLayerIndex, 0f);
            BeginState(lethal ? "DEATH" : "HEAVY_HIT", reactionSettings.transitionSeconds);
        }
        public bool TryRecoverFromHeavy(bool movement, bool dodge)
        {
            if (!CanRecoverFromHeavy || !movement && !dodge) return false;
            BeginGetUp(); return true;
        }
        private void BeginGetUp() => BeginState("GET_UP", reactionSettings.transitionSeconds);
        public void ResetReactions()
        {
            fadingReactionState = null; reactionFadeRemaining = 0f; landedSeconds = -1f; downHoldElapsed = 0f;
            GetComponent<PassPlayerReactionPose>()?.ResetPose();
        }
        private void UpdateReactionPlayback(float seconds)
        {
            if (reactionSettings == null || paused || seconds <= 0f) return;
            if (reactionFadeRemaining > 0f && !string.IsNullOrEmpty(fadingReactionState))
            {
                var fading = reactionSettings.GetRange(fadingReactionState);
                fadingReactionFrame = Mathf.Min(fading.endFrame, fadingReactionFrame + seconds * fading.FrameRate * fading.speed);
                SetReactionFrame(fadingReactionState, fadingReactionFrame);
                reactionFadeRemaining -= seconds;
            }
            if (!IsReactionPlaying) return;
            var range = reactionSettings.GetRange(selectedState);
            float previousFrame = reactionFrame;
            reactionFrame = Mathf.Min(range.endFrame, reactionFrame + seconds * range.FrameRate * range.speed);
            SetReactionFrame(selectedState, reactionFrame);
            if (selectedState != "HEAVY_HIT") return;
            if (landedSeconds >= 0f) landedSeconds += seconds;
            else if (reactionFrame >= reactionSettings.landingFrame)
                landedSeconds = Mathf.Max(0f, seconds - (reactionSettings.landingFrame - previousFrame) / (range.FrameRate * range.speed));
            if (previousFrame >= range.endFrame) downHoldElapsed += seconds;
        }
        private void SetReactionFrame(string state, float frame)
        {
            var range = reactionSettings.GetRange(state);
            characterAnimator.SetFloat(state == "GET_UP" ? "GetUpTime" : state == "DEATH" ? "DeathTime" : "HeavyTime", frame / (range.FrameRate * range.clip.length));
        }
        public void TogglePause()
        {
            paused = !paused; characterAnimator.speed = paused ? 0f : 1f; fanAttachment.SetPaused(paused);
        }

        private bool TryGetSelectedState(out AnimatorStateInfo state)
        {
            state = default;
            if (characterAnimator == null || !characterAnimator.isInitialized) return false;
            int layer = UsesUpperBodyEquipment && IsEquipmentTransition ? equipmentLayerIndex : 0;
            if (characterAnimator.IsInTransition(layer))
            {
                var next = characterAnimator.GetNextAnimatorStateInfo(layer);
                if (next.IsName(selectedState)) { state = next; return true; }
            }
            state = characterAnimator.GetCurrentAnimatorStateInfo(layer);
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
            "SHEATHE" => Mathf.Max(0.05f, sheatheSpeed), "ROLL" => Mathf.Max(0.05f, rollSpeed),
            "RUN2" => Mathf.Max(0.05f, run2Speed), _ => 1f
        };
        private static bool IsAction(string state) => state == "ATTACK1" || state == "ATTACK2" || state == "ATTACK3" || state == "HIT" || state == "ROLL";
        private AnimationClip PlaybackClip(string state) => state switch
        {
            "ATTACK1" => attack1Clip, "ATTACK2" => attack2Clip, "ATTACK3" => attack3Clip,
            "ROLL" => rollClip, "RUN2" => run2Clip, _ => null
        };
        // 조절 UI의 순서: 공격 1~3, 구르기, 달리기2.
        private static int PlaybackIndex(string state) => state switch
        {
            "ATTACK1" => 1, "ATTACK2" => 2, "ATTACK3" => 3, "ROLL" => 4, "RUN2" => 5, _ => 0
        };
        private bool IsFullRun2Loop => selectedState == "RUN2" && GetPlaybackStartFrame(5) == 0 && GetPlaybackEndFrame(5) == GetPlaybackMaximumFrame(5);
        private float CurrentAttackSpeed(int index) => index == 1 ? attack1Speed : index == 2 ? attack2Speed : attack3Speed;
        private float PlaybackStartTime(string state)
        {
            var clip = PlaybackClip(state);
            return clip != null ? GetPlaybackStartFrame(PlaybackIndex(state)) / clip.frameRate : 0f;
        }
        private float PlaybackBlendDuration(string state, float requestedDuration)
        {
            var clip = PlaybackClip(state);
            if (clip == null) return requestedDuration;
            int index = PlaybackIndex(state);
            float speed = state == "ROLL" ? rollSpeed : run2Speed;
            float duration = (GetPlaybackEndFrame(index) - GetPlaybackStartFrame(index)) / clip.frameRate / Mathf.Max(0.05f, speed);
            return Mathf.Min(requestedDuration, duration * 0.25f);
        }
        public int GetPlaybackMaximumFrame(int index)
        {
            if (index <= 3) return GetAttackMaximumFrame(index);
            var clip = index == 4 ? rollClip : run2Clip;
            return clip != null ? Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate)) : 1;
        }
        public int GetPlaybackStartFrame(int index)
        {
            if (index <= 3) return GetAttackStartFrame(index);
            return Mathf.Clamp(index == 4 ? rollStartFrame : run2StartFrame, 0, GetPlaybackMaximumFrame(index) - 1);
        }
        public int GetPlaybackEndFrame(int index)
        {
            if (index <= 3) return GetAttackEndFrame(index);
            return Mathf.Clamp(index == 4 ? rollEndFrame : run2EndFrame, GetPlaybackStartFrame(index) + 1, GetPlaybackMaximumFrame(index));
        }
        public void SetPlaybackFrameRange(int index, int startFrame, int endFrame)
        {
            if (index <= 3) { SetAttackFrameRange(index, startFrame, endFrame); return; }
            int maximum = GetPlaybackMaximumFrame(index);
            int start = Mathf.Clamp(startFrame, 0, maximum - 1);
            int end = Mathf.Clamp(endFrame, start + 1, maximum);
            if (index == 4) { rollStartFrame = start; rollEndFrame = end; }
            else if (index == 5) { run2StartFrame = start; run2EndFrame = end; }
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
            if (rollClip != null) SetPlaybackFrameRange(4, rollStartFrame, rollEndFrame);
            if (run2Clip != null) SetPlaybackFrameRange(5, run2StartFrame, run2EndFrame);
        }
    }
}
