using System.Collections.Generic;
using UnityEngine;
using Cheon.Prototype.MountainPass;

namespace Cheon.Prototype.MountainPass
{
    public class PassBossPatternController : PassBossController
    {
        public enum PatternPhase { Dormant, Idle, JumpWindup, JumpFlight, JumpRecovery, FallWindup, Falling, FallHold, Rising, FallRecovery, SummonWindup, SummonRecovery, Turning, Dead, CrackWindup, CrackActive, CrackRecovery, LinkedAttack, Teleporting, CrackReturn, PursuitSlam }
        [InspectorName("보스 패턴 설정")]
        [SerializeField] private PassBossPatternSettings settings;
        [InspectorName("잡몹 장승 여부")]
        [SerializeField] private bool isMinion;
        private readonly List<PassBossPatternController> minions = new List<PassBossPatternController>();
        private readonly Collider[] queryResults = new Collider[64];
        private readonly RaycastHit[] groundResults = new RaycastHit[32];
        private Transform bodyPivot;
        private Collider originalBody;
        private PassBossImpactDust dust;
        private PassBossDeathVfx deathVfx;
        private PassBossTeleportVfx teleportVfx;
        private PassBossSnortVfx snort;
        private PassBossPatternController summonOwner;
        private bool summonInitialized;
        private PassBossLinkedAttack linkedFallCoordinator;
        private float fallWindupDuration;
        private Vector3 spawnPosition, spawnVisualScale, jumpStart, jumpTarget, turnOrigin;
        private Quaternion spawnRotation, turnStart, turnTarget;
        private float elapsed, flightDuration, jumpReadyAt, fallReadyAt, summonReadyAt, turnReadyAt, crackReadyAt, openingReadyAt, previousTilt;
        private bool hasHitPlayer, visualAttached, resetting;
        private PassBossLinkedAttack reservedBy;
        private int attackTotal;
        private enum NormalPattern { None = -1, Jump, Fall, Crack, PursuitSlam }
        private readonly float[] lastNormalUse = new float[4];
        private readonly List<Vector3> summonPositions = new List<Vector3>(2);
        private NormalPattern lastNormalPattern = NormalPattern.None, pendingNormalPattern = NormalPattern.None;
        private int consecutiveNormalAttacks, normalAttacksForNextLink, observedLinkedCompletions, nextMinionNumber;
        private bool normalAttackInProgress, linkedAttackRequested, linkedJumpOrdered;
        private float repeatBlockedSince = -1f;

        public PatternPhase Phase { get; private set; }
        public float PhaseElapsed => elapsed;
        public bool IsMinion => isMinion;
        public PassBossPatternSettings Settings => settings;
        public bool IsAlive => gameObject.activeInHierarchy && Health != null && !Health.IsDead && Body != null && (Body.enabled || Phase == PatternPhase.Teleporting);
        public bool EncounterRunning => game != null && game.IsRunning && game.InEncounter;
        public CapsuleCollider Body { get; private set; }
        public Vector3 LandingPosition => Phase == PatternPhase.PursuitSlam ? PursuitSlam.LandingPosition :
            Phase == PatternPhase.Teleporting ? teleportVfx.Destination : jumpTarget;
        public bool LandingReserved => Phase == PatternPhase.JumpWindup || Phase == PatternPhase.JumpFlight || Phase == PatternPhase.Teleporting ||
            Phase == PatternPhase.PursuitSlam && PursuitSlam.LandingReserved;
        public bool FallDamageActive => Phase == PatternPhase.Falling;
        public int JumpsCompleted { get; private set; }
        public int FallsCompleted { get; private set; }
        public int TurnsCompleted { get; private set; }
        public int SummonsCompleted { get; private set; }
        public int CracksCompleted { get; private set; }
        public int ConsecutiveTurns { get; private set; }
        public float TurnReadyAt => turnReadyAt;
        public float CrackReadyAt => crackReadyAt;
        public PassBossGroundCrack GroundCrack { get; private set; }
        public PassBossLinkedAttack LinkedAttack { get; private set; }
        public PassBossPursuitSlam PursuitSlam { get; private set; }
        public PassBossDeathVfx DeathVfx => deathVfx;
        public PassBossTeleportVfx TeleportVfx => teleportVfx;
        public int JumpLandings { get; private set; }
        public float LastJumpStartedAt { get; private set; }
        public float LastJumpLandedAt { get; private set; }
        public float LastJumpFinishedAt { get; private set; }
        public float LastFallFinishedAt { get; private set; }
        public float CurrentFallCooldown { get; private set; }
        public float JumpReadyAt => jumpReadyAt;
        public float FallReadyAt => fallReadyAt;
        public float SummonReadyAt => summonReadyAt;
        public int LivingMinions
        {
            get { int count = 0; foreach (var minion in minions) if (minion != null && minion.IsAlive) count++; return count; }
        }
        public int NormalAttacksCompleted { get; private set; }
        public int NormalAttacksSinceLinked { get; private set; }
        public override bool Enraged => false;
        public override int AttacksPerformed => attackTotal;
        public override string AttackLabel => "";

        public void ConfigurePattern(PassBossPatternSettings configuration, bool miniature)
        { settings = configuration; isMinion = miniature; }

        protected override void Awake()
        {
            base.Awake();
            spawnPosition = transform.position; spawnRotation = transform.rotation;
            spawnVisualScale = visual.localScale;
            Health.Configure(isMinion ? settings.combat.minionHealth : settings.combat.bossHealth, settings.combat.enemyHitRecovery);
            originalBody = GetComponent<Collider>();
            if (originalBody != null) originalBody.enabled = false;
            // 동적 생성 시 처음부터 키네마틱 몸체에 콜라이더가 등록되도록 몸체를 먼저 만든다.
            var rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true; rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var scaleRoot = new GameObject("BodyScale").transform;
            scaleRoot.SetParent(transform, false);
            Vector3 scale = transform.lossyScale;
            scaleRoot.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            bodyPivot = new GameObject("BodyPivot").transform;
            bodyPivot.SetParent(scaleRoot, false);
            Body = bodyPivot.gameObject.AddComponent<CapsuleCollider>();
            float size = isMinion ? settings.summon.minionScale : 1f;
            Body.radius = settings.collision.radius * size;
            Body.height = settings.collision.height * size;
            Body.center = Vector3.up * Body.height * .5f;
            dust = gameObject.AddComponent<PassBossImpactDust>(); dust.Configure(settings, Body.radius);
            deathVfx = gameObject.AddComponent<PassBossDeathVfx>(); deathVfx.Configure(settings, visual, isMinion);
            if (isMinion)
            {
                teleportVfx = gameObject.AddComponent<PassBossTeleportVfx>();
                teleportVfx.Configure(settings, this, visual, player);
            }
            if (!isMinion)
            {
                GroundCrack = gameObject.AddComponent<PassBossGroundCrack>(); GroundCrack.Configure(settings, this, player, dust);
                LinkedAttack = gameObject.AddComponent<PassBossLinkedAttack>(); LinkedAttack.Configure(settings, this);
                snort = gameObject.AddComponent<PassBossSnortVfx>(); snort.Configure(settings);
                PursuitSlam = gameObject.AddComponent<PassBossPursuitSlam>();
                PursuitSlam.Configure(settings, this, player, dust, snort, audioControl, view);
            }
            settings.Register(this);
            if (playerHealth != null) playerHealth.Died += OnPlayerDefeated;
        }

        private void Start()
        {
            if (isMinion && summonInitialized) return;
            ResetEncounter();
            if (!isMinion) return;
            WakeUp();
        }

        public override void ResetEncounter()
        {
            resetting = true;
            if (teleportVfx != null) teleportVfx.ResetEffect();
            if (deathVfx != null) deathVfx.ResetEffect();
            if (LinkedAttack != null) LinkedAttack.Clear();
            if (GroundCrack != null) GroundCrack.Clear();
            if (PursuitSlam != null) PursuitSlam.Clear();
            reservedBy = null;
            linkedJumpOrdered = normalAttackInProgress = linkedAttackRequested = false;
            NormalAttacksCompleted = NormalAttacksSinceLinked = consecutiveNormalAttacks = observedLinkedCompletions = nextMinionNumber = 0;
            normalAttacksForNextLink = ChooseLinkedInterval();
            lastNormalPattern = pendingNormalPattern = NormalPattern.None;
            for (int index = 0; index < lastNormalUse.Length; index++) lastNormalUse[index] = float.NegativeInfinity;
            repeatBlockedSince = -1f; summonPositions.Clear();
            linkedFallCoordinator = null; fallWindupDuration = settings.fall.windup;
            RemoveMinions(false);
            if (visualAttached) { visual.SetParent(transform, true); visualAttached = false; }
            // 비균일 배율의 원래 부모로 돌아갈 때 생긴 배율 변화를 재도전까지 남기지 않는다.
            visual.localScale = spawnVisualScale;
            base.ResetEncounter();
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (originalBody != null) originalBody.enabled = false;
            Body.enabled = true; SetTilt(0);
            Health.Configure(isMinion ? settings.combat.minionHealth : settings.combat.bossHealth, settings.combat.enemyHitRecovery);
            Health.Restore(); Health.Invulnerable = true;
            JumpsCompleted = FallsCompleted = TurnsCompleted = SummonsCompleted = attackTotal = 0;
            CracksCompleted = ConsecutiveTurns = 0;
            jumpReadyAt = fallReadyAt = summonReadyAt = turnReadyAt = crackReadyAt = 0;
            LastJumpFinishedAt = LastFallFinishedAt = 0;
            JumpLandings = 0; LastJumpStartedAt = LastJumpLandedAt = 0;
            hasHitPlayer = false;
            Phase = PatternPhase.Dormant; elapsed = 0;
            if (dust != null) dust.Clear();
            resetting = false;
        }

        public override void WakeUp()
        {
            if (Phase != PatternPhase.Dormant) return;
            if (!isMinion && audioControl != null) audioControl.Spirit();
            // 등장의 Timeline이 끝난 뒤에만 모델을 피벗 아래로 옮겨 원래 애니메이션 경로를 보존한다.
            visual.SetParent(bodyPivot, true); visualAttached = true;
            Health.Invulnerable = false;
            openingReadyAt = Time.time + settings.turn.openingDelay;
            CurrentFallCooldown = ChooseFallCooldown();
            SetPhase(PatternPhase.Idle);
        }

        private void Update()
        {
            if (!EncounterRunning || Phase == PatternPhase.Dormant || Phase == PatternPhase.Dead) return;
            if (GroundCrack != null) GroundCrack.Tick(Time.deltaTime);
            if (LinkedAttack != null) LinkedAttack.Tick(Time.deltaTime);
            if (LinkedAttack != null && LinkedAttack.CompletedCount > observedLinkedCompletions)
            {
                observedLinkedCompletions = LinkedAttack.CompletedCount;
                NormalAttacksSinceLinked = 0; linkedAttackRequested = false;
                normalAttacksForNextLink = ChooseLinkedInterval();
            }
            elapsed += Time.deltaTime;
            switch (Phase)
            {
                case PatternPhase.Idle: ChoosePattern(); break;
                case PatternPhase.Teleporting:
                    teleportVfx.Tick(Time.deltaTime);
                    if (!teleportVfx.IsTeleporting) FinishPattern();
                    break;
                case PatternPhase.Turning:
                    UpdateTurning();
                    break;
                case PatternPhase.JumpWindup:
                    if (linkedJumpOrdered && reservedBy != null) TrackPlayerForFall();
                    if (elapsed >= settings.jump.windup)
                    {
                        // 연계 점프는 준비를 시작했을 때의 오래된 위치 대신 이륙 직전 위치를 노린다.
                        if (linkedJumpOrdered && reservedBy != null)
                        {
                            if (!TryChooseLanding(out jumpTarget)) { FinishPattern(); break; }
                            float distance = Vector3.ProjectOnPlane(jumpTarget - transform.position, Vector3.up).magnitude;
                            flightDuration = Mathf.Lerp(settings.jump.minimumFlight, settings.jump.maximumFlight, distance / settings.jump.maximumDistance);
                        }
                        jumpStart = transform.position; LastJumpStartedAt = Time.time;
                        SetPhase(PatternPhase.JumpFlight);
                    }
                    break;
                case PatternPhase.JumpFlight: UpdateFlight(); break;
                case PatternPhase.JumpRecovery:
                    if (elapsed >= settings.jump.recovery)
                    {
                        JumpsCompleted++; LastJumpFinishedAt = Time.time;
                        jumpReadyAt = Time.time + (isMinion ? settings.jump.minionCooldown : settings.jump.bossCooldown);
                        linkedJumpOrdered = false; CompleteNormalAttack();
                        FinishPattern();
                    }
                    break;
                case PatternPhase.FallWindup:
                    TrackPlayerForFall();
                    float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, fallWindupDuration));
                    float split = settings.fall.forwardFraction;
                    SetTilt(progress < split ? Mathf.Lerp(0, settings.fall.forwardAngle, progress / split) :
                        Mathf.Lerp(settings.fall.forwardAngle, -settings.fall.backwardAngle, (progress - split) / (1f - split)));
                    if (elapsed >= fallWindupDuration && (linkedFallCoordinator == null || linkedFallCoordinator.CanStartBossHit))
                    {
                        // 마지막 선딜레이 프레임까지 추적하고, 내려치기가 시작되면 정면을 고정한다.
                        previousTilt = -settings.fall.backwardAngle; hasHitPlayer = false;
                        SetPhase(PatternPhase.Falling);
                    }
                    break;
                case PatternPhase.Falling: UpdateFall(); break;
                case PatternPhase.FallHold:
                    if (elapsed >= settings.fall.holdDuration) SetPhase(PatternPhase.Rising);
                    break;
                case PatternPhase.Rising:
                    SetTilt(Mathf.Lerp(settings.fall.finalAngle, 0, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / settings.fall.riseDuration))));
                    if (elapsed >= settings.fall.riseDuration) { SetTilt(0); SetPhase(PatternPhase.FallRecovery); }
                    break;
                case PatternPhase.FallRecovery:
                    if (elapsed >= settings.fall.recovery)
                    {
                        FallsCompleted++; LastFallFinishedAt = Time.time;
                        CompleteNormalAttack();
                        linkedFallCoordinator = null;
                        CurrentFallCooldown = ChooseFallCooldown();
                        fallReadyAt = Time.time + CurrentFallCooldown;
                        FinishPattern();
                    }
                    break;
                case PatternPhase.SummonWindup:
                    if (elapsed >= settings.summon.windup) { SpawnMinions(); SetPhase(PatternPhase.SummonRecovery); }
                    break;
                case PatternPhase.SummonRecovery:
                    if (elapsed >= settings.summon.recovery) FinishPattern();
                    break;
                case PatternPhase.CrackWindup:
                    TrackPlayerForFall();
                    SetTilt(settings.crack.windupTilt * Mathf.SmoothStep(0, 1,
                        Mathf.Clamp01(elapsed / Mathf.Max(.01f, settings.crack.windup))));
                    if (elapsed >= settings.crack.windup)
                    {
                        // 앞으로 기울인 자세에서 뒤로 젖히기 시작하는 순간 첫 폭발도 발생시킨다.
                        GroundCrack.Begin(transform.position, transform.forward);
                        audioControl.Slam(); view.AddShake(.6f);
                        SetPhase(PatternPhase.CrackActive);
                    }
                    break;
                case PatternPhase.CrackActive:
                    SetTilt(Mathf.Lerp(settings.crack.windupTilt, -settings.crack.strikeTilt,
                        Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, settings.crack.recoilDuration)))));
                    // 젖히는 동작을 끝낸 뒤 지정한 시간만큼 유지하며 남은 폭발 판정도 기다린다.
                    if (elapsed >= settings.crack.recoilDuration + settings.crack.holdDuration && GroundCrack.AttackFinished)
                        SetPhase(PatternPhase.CrackReturn);
                    break;
                case PatternPhase.CrackReturn:
                    SetTilt(Mathf.Lerp(-settings.crack.strikeTilt, 0,
                        Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, settings.crack.returnDuration)))));
                    if (elapsed >= settings.crack.returnDuration)
                    {
                        SetTilt(0); SetPhase(PatternPhase.CrackRecovery);
                    }
                    break;
                case PatternPhase.CrackRecovery:
                    if (elapsed >= settings.crack.recovery)
                    {
                        SetTilt(0); CracksCompleted++; crackReadyAt = Time.time + settings.crack.cooldown;
                        CompleteNormalAttack();
                        FinishPattern();
                    }
                    break;
                case PatternPhase.PursuitSlam:
                    PursuitSlam.Tick(Time.deltaTime);
                    State = PursuitSlam.Stage == PassBossPursuitSlam.SlamStage.Recovery ? BossState.Recovering : BossState.Windup;
                    if (!PursuitSlam.IsActive)
                    { SetTilt(0); CompleteNormalAttack(); FinishPattern(); }
                    break;
            }
        }

        private void ChoosePattern()
        {
            if (Time.time < openingReadyAt || reservedBy != null || LinkedAttack != null && LinkedAttack.IsActive) return;
            Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            float distance = direction.magnitude;
            if (isMinion)
            {
                bool canFall = Time.time >= fallReadyAt && distance <= Mathf.Max(Body.height, settings.jump.attackRadius) + Body.radius;
                if (!canFall && Time.time < jumpReadyAt) return;
                if (TryBeginAlignment(direction)) return;
                if (canFall) BeginFall(); else TryBeginJump();
                return;
            }
            // 필요한 보충 → 협동 → 일반 공격 순서이며 방향 정렬은 이 순서를 가로막지 않는다.
            if (NormalAttacksCompleted >= settings.summon.requiredNormalAttacks && LivingMinions < settings.summon.maximumAlive &&
                Time.time >= summonReadyAt && CanSummon())
            { pendingNormalPattern = NormalPattern.None; SetPhase(PatternPhase.SummonWindup); return; }
            if (LinkedAttack != null && (linkedAttackRequested || NormalAttacksSinceLinked >= normalAttacksForNextLink) && LinkedAttack.TryBegin())
            { pendingNormalPattern = NormalPattern.None; return; }
            TrySelectNormalAttack(direction, distance);
        }

        private bool TryBeginAlignment(Vector3 direction)
        {
            if (direction.sqrMagnitude <= .001f || Vector3.Angle(transform.forward, direction) <= settings.turn.triggerAngle ||
                Time.time < turnReadyAt || ConsecutiveTurns >= settings.turn.maximumConsecutive) return false;
            turnStart = transform.rotation; turnTarget = Quaternion.LookRotation(direction);
            turnOrigin = transform.position; SetPhase(PatternPhase.Turning); return true;
        }

        private bool IsNormalPatternAvailable(NormalPattern pattern, float distance)
        {
            switch (pattern)
            {
                case NormalPattern.Jump: return Time.time >= jumpReadyAt;
                case NormalPattern.Fall: return Time.time >= fallReadyAt && distance <= Mathf.Max(Body.height, settings.jump.attackRadius) + Body.radius;
                case NormalPattern.Crack:
                    float playerRadius = player.Body.radius;
                    float minimum = Mathf.Max(0, settings.crack.stepDistance - settings.crack.radius - playerRadius);
                    float maximum = settings.crack.stepCount * settings.crack.stepDistance + settings.crack.radius + playerRadius;
                    return CanUseCrack() && distance >= minimum && distance <= maximum;
                case NormalPattern.PursuitSlam: return settings.pursuitSlam.enabled && PursuitSlam != null;
                default: return false;
            }
        }

        private int GetDistancePreference(NormalPattern pattern, float distance)
        {
            if (pattern == NormalPattern.PursuitSlam) return 3;
            if (distance <= settings.selection.closeRange) return pattern == NormalPattern.Fall ? 0 : pattern == NormalPattern.Jump ? 1 : 2;
            return pattern == NormalPattern.Crack ? 0 : pattern == NormalPattern.Jump ? 1 : 2;
        }

        private void TrySelectNormalAttack(Vector3 direction, float distance)
        {
            // 마지막 사용 시각이 오래된 공격을 고르고, 사용 시각이 같을 때만 거리별 역할을 우선한다.
            bool relaxRepeat = repeatBlockedSince >= 0f && Time.time - repeatBlockedSince >= settings.selection.repeatFallbackWait;
            int excluded = 0;
            for (int attempt = 0; attempt < lastNormalUse.Length; attempt++)
            {
                NormalPattern selected = NormalPattern.None; float oldest = float.PositiveInfinity; int rank = int.MaxValue;
                if (pendingNormalPattern != NormalPattern.None && IsNormalPatternAvailable(pendingNormalPattern, distance)) selected = pendingNormalPattern;
                pendingNormalPattern = NormalPattern.None;
                if (selected == NormalPattern.None)
                    for (int index = 0; index < lastNormalUse.Length; index++)
                    {
                        var candidate = (NormalPattern)index;
                        if ((excluded & (1 << index)) != 0 || !IsNormalPatternAvailable(candidate, distance)) continue;
                        if (!relaxRepeat && candidate == lastNormalPattern && consecutiveNormalAttacks >= settings.selection.maximumConsecutive) continue;
                        float used = lastNormalUse[index]; int candidateRank = GetDistancePreference(candidate, distance);
                        if (selected == NormalPattern.None || used < oldest || used == oldest && candidateRank < rank)
                        { selected = candidate; oldest = used; rank = candidateRank; }
                    }
                if (selected == NormalPattern.None) break;
                if (selected != NormalPattern.Jump && selected != NormalPattern.PursuitSlam && TryBeginAlignment(direction))
                { pendingNormalPattern = selected; return; }
                bool started = true;
                if (selected == NormalPattern.Jump) started = TryBeginJump();
                else if (selected == NormalPattern.Fall) BeginFall();
                else if (selected == NormalPattern.PursuitSlam) BeginPursuitSlam();
                else BeginCrack();
                if (started) { repeatBlockedSince = -1f; return; }
                excluded |= 1 << (int)selected;
            }
            if (repeatBlockedSince < 0f) repeatBlockedSince = Time.time;
        }

        private void RecordNormalAttack(NormalPattern pattern)
        {
            if (isMinion) return;
            normalAttackInProgress = true;
            lastNormalUse[(int)pattern] = Time.time;
            consecutiveNormalAttacks = lastNormalPattern == pattern ? consecutiveNormalAttacks + 1 : 1;
            lastNormalPattern = pattern;
        }
        private void CompleteNormalAttack()
        {
            if (isMinion || !normalAttackInProgress) return;
            normalAttackInProgress = false; NormalAttacksCompleted++; NormalAttacksSinceLinked++;
        }
        private int ChooseLinkedInterval() => Random.Range(settings.linkedAttack.minimumNormalAttacks, settings.linkedAttack.maximumNormalAttacks + 1);

        private bool CanUseCrack() => !isMinion && settings.crack.enabled && Time.time >= crackReadyAt;

        private void BeginCrack()
        {
            ConsecutiveTurns = 0; attackTotal++; RecordNormalAttack(NormalPattern.Crack);
            SetPhase(PatternPhase.CrackWindup);
        }

        private void BeginPursuitSlam()
        {
            ConsecutiveTurns = 0; attackTotal++; RecordNormalAttack(NormalPattern.PursuitSlam);
            SetPhase(PatternPhase.PursuitSlam); PursuitSlam.Begin();
        }

        private void BeginFall(bool linked = false)
        {
            linkedFallCoordinator = null; fallWindupDuration = settings.fall.windup;
            ConsecutiveTurns = 0; attackTotal++;
            if (!linked) RecordNormalAttack(NormalPattern.Fall);
            SetPhase(PatternPhase.FallWindup);
        }

        private bool TryBeginJump(bool linked = false)
        {
            if (TryChooseLanding(out jumpTarget))
            {
                float distance = Vector3.ProjectOnPlane(jumpTarget - transform.position, Vector3.up).magnitude;
                flightDuration = Mathf.Lerp(settings.jump.minimumFlight, settings.jump.maximumFlight, distance / settings.jump.maximumDistance);
                ConsecutiveTurns = 0; linkedJumpOrdered = linked; TrackPlayerForFall();
                if (!linked) RecordNormalAttack(NormalPattern.Jump);
                attackTotal++; SetPhase(PatternPhase.JumpWindup);
                return true;
            }
            return false;
        }

        private float ChooseFallCooldown()
        {
            return isMinion ? Random.Range(settings.fall.minionCooldownMinimum, settings.fall.minionCooldownMaximum) :
                Random.Range(settings.fall.bossCooldownMinimum, settings.fall.bossCooldownMaximum);
        }

        private void TrackPlayerForFall()
        {
            Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        private void UpdateTurning()
        {
            float progress = Mathf.Clamp01(elapsed / settings.turn.duration);
            transform.position = turnOrigin + Vector3.up * (4f * settings.turn.height * progress * (1f - progress));
            transform.rotation = Quaternion.Slerp(turnStart, turnTarget, Mathf.SmoothStep(0, 1, progress));
            if (progress < 1f) return;
            transform.position = turnOrigin;
            dust.EmitLanding(transform.position, Body.radius);
            TurnsCompleted++; ConsecutiveTurns++; turnReadyAt = Time.time + settings.turn.cooldown;
            FinishPattern();
        }

        private void UpdateFlight()
        {
            float progress = Mathf.Clamp01(elapsed / flightDuration);
            Vector3 position = Vector3.Lerp(jumpStart, jumpTarget, progress);
            position.y += 4f * settings.jump.height * progress * (1f - progress);
            transform.position = position;
            if (progress < 1f) return;
            transform.position = jumpTarget;
            JumpLandings++; LastJumpLandedAt = Time.time;
            Physics.SyncTransforms();
            Vector3 delta = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            var playerBody = player.GetComponent<CharacterController>();
            if (delta.magnitude <= settings.jump.attackRadius + playerBody.radius && Mathf.Abs(player.transform.position.y - transform.position.y) <= playerBody.height)
                DamagePlayer(isMinion ? settings.combat.minionJumpDamage : settings.combat.bossJumpDamage);
            dust.EmitLanding(transform.position, settings.jump.attackRadius);
            audioControl.Slam(); view.AddShake(isMinion ? .2f : .8f);
            SetPhase(PatternPhase.JumpRecovery);
        }

        private void UpdateFall()
        {
            float angle = Mathf.Lerp(-settings.fall.backwardAngle, settings.fall.finalAngle, Mathf.Clamp01(elapsed / settings.fall.fallDuration));
            int samples = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(angle - previousTilt) / settings.collision.sweepDegrees));
            // 저프레임에서도 긴 몸통이 플레이어를 건너뛰지 않도록 지난 회전 구간 전체를 검사한다.
            for (int index = 1; index <= samples; index++)
            {
                SetTilt(Mathf.Lerp(previousTilt, angle, index / (float)samples));
                Physics.SyncTransforms();
                CheckFallingHit();
                PushPlayerOutsideBody();
            }
            previousTilt = angle;
            if (elapsed < settings.fall.fallDuration) return;
            dust.EmitFall(Body); audioControl.Slam(); view.AddShake(isMinion ? .2f : .8f);
            // 멈춤·기상·후딜레이에는 이 검사 함수를 호출하지 않아 타격 판정이 남지 않는다.
            SetPhase(PatternPhase.FallHold);
        }

        private void CheckFallingHit()
        {
            if (hasHitPlayer) return;
            GetBodySegment(out Vector3 first, out Vector3 second);
            int count = Physics.OverlapCapsuleNonAlloc(first, second, Body.radius, queryResults, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
                if (queryResults[index].GetComponentInParent<PassPlayerMovement>() == player)
                {
                    if (DamagePlayer(isMinion ? settings.combat.minionFallDamage : settings.combat.bossFallDamage, !isMinion))
                    {
                        hasHitPlayer = true;
                        MovePlayerSidewaysFromBody(settings.fall.hitSidePushDistance);
                    }
                    break;
                }
        }

        private bool DamagePlayer(float damage, bool heavyHit = false)
        {
            if (player.AnimationControl.IsDodgeInvulnerable) return false;
            return playerHealth.TakeDamage(damage, heavyHit);
        }

        private void SetTilt(float degrees)
        {
            bodyPivot.localRotation = Quaternion.Euler(degrees, 0, 0);
            // 수평으로 누운 캡슐이 지면 아래에 묻히지 않게 밑면을 지면에 맞춘다.
            Vector3 lift = Vector3.up * (Body.radius * (1f - Mathf.Cos(degrees * Mathf.Deg2Rad)));
            bodyPivot.position = transform.position + lift;
        }

        private void SetPhase(PatternPhase phase)
        {
            if (phase == PatternPhase.SummonWindup && audioControl != null) audioControl.Spirit();
            Phase = phase; elapsed = 0;
            State = phase == PatternPhase.Dormant ? BossState.Dormant : phase == PatternPhase.Dead ? BossState.Defeated :
                phase == PatternPhase.JumpRecovery || phase == PatternPhase.FallRecovery || phase == PatternPhase.FallHold || phase == PatternPhase.Rising ? BossState.Recovering :
                phase == PatternPhase.Idle || phase == PatternPhase.Turning ? BossState.Pursuing : BossState.Windup;
        }

        private void FinishPattern()
        {
            SetPhase(PatternPhase.Idle);
        }

        internal bool CanJoinLinkedAttack => isMinion && isActiveAndEnabled && IsAlive && reservedBy == null &&
            Phase != PatternPhase.Dormant && Phase != PatternPhase.Dead && Phase != PatternPhase.Teleporting;
        internal bool ReserveForLinkedAttack(PassBossLinkedAttack coordinator)
        {
            if (!CanJoinLinkedAttack) return false;
            reservedBy = coordinator; return true;
        }
        internal void ReleaseLinkedAttack(PassBossLinkedAttack coordinator)
        {
            if (reservedBy != coordinator) return;
            reservedBy = null; linkedJumpOrdered = false;
            if (Phase == PatternPhase.Teleporting)
            {
                teleportVfx.ResetEffect(); Body.enabled = !Health.IsDead;
                SetTilt(0); FinishPattern();
            }
        }
        internal bool ReadyForLinkedTeleport(PassBossLinkedAttack coordinator) => reservedBy == coordinator && Phase == PatternPhase.Idle;
        internal bool BeginLinkedTeleport(PassBossLinkedAttack coordinator, Vector3 destination)
        {
            if (reservedBy != coordinator || Phase != PatternPhase.Idle || teleportVfx == null) return false;
            SetTilt(0); teleportVfx.Begin(destination); SetPhase(PatternPhase.Teleporting); return true;
        }
        internal bool StartLinkedJump(PassBossLinkedAttack coordinator)
        { return reservedBy == coordinator && Phase == PatternPhase.Idle && Time.time >= jumpReadyAt && TryBeginJump(true); }
        internal void EnterLinkedAttack() { linkedFallCoordinator = null; SetPhase(PatternPhase.LinkedAttack); }
        internal bool StartLinkedFall(PassBossLinkedAttack coordinator)
        {
            if (Phase != PatternPhase.LinkedAttack || !IsAlive || Time.time < fallReadyAt) return false;
            BeginFall(true); linkedFallCoordinator = coordinator;
            // 본체 전조를 미리 시작하고, 짧은 설정에서도 마지막 착지 후 지정한 판정 시점에 맞춘다.
            fallWindupDuration = Mathf.Min(settings.fall.windup, settings.jump.windup + settings.jump.minimumFlight + settings.linkedAttack.attackGap);
            return true;
        }
        internal void CancelLinkedFall(PassBossLinkedAttack coordinator)
        {
            if (linkedFallCoordinator != coordinator) return;
            linkedFallCoordinator = null;
            if (Phase == PatternPhase.FallWindup) { SetTilt(0); FinishPattern(); }
        }
        internal void LeaveLinkedAttack() { if (Phase == PatternPhase.LinkedAttack) FinishPattern(); }
        internal bool DealPatternDamage(float damage, bool heavyHit = false) => DamagePlayer(damage, heavyHit);
        internal bool FindPatternGround(Vector3 position, out Vector3 ground) => TryGround(position, out ground);
        internal bool IsPatternLandingFree(Vector3 position) => PositionFree(position, false);
        internal bool TeleportDestinationFree(Vector3 point) => TryGround(point, out Vector3 ground) && PositionFree(ground, true);
        internal bool TryResolveTeleportDestination(Vector3 preferred, out Vector3 result)
        {
            if (TryGround(preferred, out result) && PositionFree(result, true)) return true;
            for (int ring = 1; ring <= 2; ring++)
                for (int index = 0; index < 8; index++)
                {
                    float angle = index * Mathf.PI * .25f;
                    Vector3 candidate = preferred + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (settings.linkedAttack.teleportSearchRadius * ring * .5f);
                    if (TryGround(candidate, out result) && PositionFree(result, true)) return true;
                }
            result = preferred; return false;
        }

        private bool TryChooseLanding(out Vector3 result)
        {
            for (int attempt = 0; attempt < 48; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(settings.jump.offsetMinimum, settings.jump.offsetMaximum);
                Vector3 candidate = player.transform.position + new Vector3(offset.x, 0, offset.y);
                Vector3 travel = Vector3.ProjectOnPlane(candidate - transform.position, Vector3.up);
                candidate = transform.position + Vector3.ClampMagnitude(travel, settings.jump.maximumDistance);
                if (TryGround(candidate, out candidate) && PositionFree(candidate, true)) { result = candidate; return true; }
            }
            result = transform.position; return false;
        }

        private bool TryGround(Vector3 candidate, out Vector3 result)
        {
            result = candidate;
            int count = Physics.RaycastNonAlloc(new Vector3(candidate.x, settings.ArenaCenter.y + settings.collision.groundTolerance + 1f, candidate.z), Vector3.down,
                groundResults, settings.collision.groundTolerance * 2f + 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                var hit = groundResults[index];
                if (hit.collider.GetComponentInParent<PassHealth>() != null || hit.normal.y < .65f || hit.distance >= best) continue;
                if (Mathf.Abs(hit.point.y - settings.ArenaCenter.y) > settings.collision.groundTolerance) continue;
                best = hit.distance; result = hit.point;
            }
            return best < float.PositiveInfinity;
        }

        private bool PositionFree(Vector3 position, bool includePlayer, float radiusOverride = -1, float heightOverride = -1)
        {
            float radius = radiusOverride > 0 ? radiusOverride : Body.radius;
            float height = heightOverride > 0 ? heightOverride : Body.height;
            int count = Physics.OverlapCapsuleNonAlloc(position + Vector3.up * (radius + settings.collision.clearance + .05f), position + Vector3.up * (height - radius), radius + settings.collision.clearance,
                queryResults, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider other = queryResults[index];
                if (other.transform.IsChildOf(transform)) continue;
                if (!includePlayer && other.GetComponentInParent<PassPlayerMovement>() != null) continue;
                return false;
            }
            foreach (var enemy in settings.Enemies)
            {
                if (enemy == this || !enemy.IsAlive || !enemy.LandingReserved) continue;
                if (Vector3.ProjectOnPlane(enemy.LandingPosition - position, Vector3.up).magnitude < radius + enemy.Body.radius + settings.collision.clearance) return false;
            }
            return true;
        }

        private bool CanSummon() => CollectSummonPositions() > 0;

        private int CollectSummonPositions()
        {
            summonPositions.Clear();
            minions.RemoveAll(item => item == null);
            int available = Mathf.Max(0, Mathf.Max(settings.summon.maximumAlive, settings.summon.poolCapacity) - minions.Count);
            foreach (var enemy in minions) if (!enemy.gameObject.activeSelf) available++;
            int missing = Mathf.Min(Mathf.Min(settings.summon.spawnCount, settings.summon.maximumAlive - LivingMinions), available);
            if (missing <= 0) return 0;
            float radius = settings.collision.radius * settings.summon.minionScale;
            float height = settings.collision.height * settings.summon.minionScale;
            // 비어 있는 좌우 지점을 먼저 채우고, 막힌 경우에만 근처의 안전한 지점을 찾는다.
            for (int pass = 0; pass < 2 && summonPositions.Count < missing; pass++)
                for (int slot = 0; slot < settings.summon.maximumAlive && summonPositions.Count < missing; slot++)
                {
                    Vector3 preferred = GetSummonPosition(slot);
                    int attempts = pass == 0 ? 1 : 16;
                    for (int attempt = 0; attempt < attempts; attempt++)
                    {
                        float angle = attempt * Mathf.PI * .25f;
                        float search = settings.linkedAttack.teleportSearchRadius * (attempt < 8 ? .5f : 1f);
                        Vector3 candidate = pass == 0 ? preferred : preferred + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * search;
                        if (!TryGround(candidate, out candidate) || !PositionFree(candidate, true, radius, height)) continue;
                        bool overlaps = false;
                        foreach (var position in summonPositions)
                            if (Vector3.ProjectOnPlane(position - candidate, Vector3.up).sqrMagnitude < Mathf.Pow(radius * 2f + settings.collision.clearance, 2f)) { overlaps = true; break; }
                        if (overlaps) continue;
                        summonPositions.Add(candidate); break;
                    }
                }
            return summonPositions.Count;
        }

        private void SpawnMinions()
        {
            if (isMinion || CollectSummonPositions() == 0) return;
            for (int index = 0; index < summonPositions.Count; index++)
            {
                Vector3 position = summonPositions[index];
                PassBossPatternController enemy = null;
                foreach (var candidate in minions)
                    if (!candidate.gameObject.activeSelf) { enemy = candidate; break; }
                if (enemy == null) enemy = CreateMinion(position);
                enemy.name = "소환 장승 " + (++nextMinionNumber).ToString("00");
                enemy.spawnPosition = position; enemy.spawnRotation = transform.rotation;
                enemy.transform.SetPositionAndRotation(position, transform.rotation);
                enemy.summonInitialized = true;
                enemy.gameObject.SetActive(true);
                enemy.Body.radius = settings.collision.radius * settings.summon.minionScale;
                enemy.Body.height = settings.collision.height * settings.summon.minionScale;
                enemy.Body.center = Vector3.up * enemy.Body.height * .5f;
                enemy.spawnVisualScale = settings.summon.modelScale * settings.summon.minionScale;
                // 최초 생성과 재사용 모두 같은 초기화 경로를 거치고, 그 뒤에만 소환 연기를 뿜는다.
                enemy.ResetEncounter(); enemy.WakeUp(); enemy.teleportVfx.PlaySpawnSmoke();
                enemy.Health.Died -= OnMinionDied;
                enemy.Health.Died += OnMinionDied;
                dust.EmitLanding(position, settings.collision.radius);
            }
            SummonsCompleted++; linkedAttackRequested = true;
            summonReadyAt = Time.time + settings.summon.replenishCooldown;
            Physics.SyncTransforms();
        }

        private PassBossPatternController CreateMinion(Vector3 position)
        {
            var root = new GameObject("소환 장승");
            root.SetActive(false); root.transform.SetPositionAndRotation(position, transform.rotation);
            var model = Instantiate(settings.summon.visualPrefab, root.transform);
            model.name = "MiniVisual";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = settings.summon.modelScale * settings.summon.minionScale;
            var health = root.AddComponent<PassHealth>(); health.Configure(settings.combat.minionHealth, settings.combat.enemyHitRecovery);
            var enemy = root.AddComponent<PassBossPatternController>();
            enemy.Configure(player, playerHealth, game, effects, view, audioControl, model.transform);
            enemy.ConfigurePattern(settings, true); enemy.summonOwner = this;
            minions.Add(enemy);
            return enemy;
        }

        public bool TryReturnToSummonPool()
        {
            if (!isMinion || summonOwner == null) return false;
            gameObject.SetActive(false);
            if (teleportVfx != null) teleportVfx.ResetEffect();
            if (dust != null) dust.Clear();
            if (deathVfx != null) deathVfx.ResetEffect();
            return true;
        }

        private Vector3 GetSummonPosition(int index)
        {
            float side = index % 2 == 0 ? -1f : 1f;
            return transform.position + transform.right * settings.summon.sideDistance * side * (index / 2 + 1);
        }

        private void OnMinionDied()
        {
            if (!resetting) summonReadyAt = Mathf.Max(summonReadyAt, Time.time + settings.summon.replenishCooldown);
        }

        private void RemoveMinions(bool kill)
        {
            foreach (var enemy in minions)
            {
                if (enemy == null) continue;
                enemy.Health.Died -= OnMinionDied;
                if (kill) { if (enemy.IsAlive) enemy.Health.Kill(); }
                else enemy.TryReturnToSummonPool();
            }
            // 본체 사망 시 소멸 효과를 끝까지 재생하고, 재도전 시에는 즉시 풀로 되돌린다.
        }

        public void DisplaceForCollision(Vector3 displacement)
        {
            displacement.y = 0;
            transform.position += displacement;
            if (Phase == PatternPhase.JumpFlight) { jumpStart += displacement; jumpTarget += displacement; }
            if (Phase == PatternPhase.Turning) turnOrigin += displacement;
            if (Phase == PatternPhase.Teleporting) teleportVfx.DisplaceForCollision(displacement);
            if (Phase == PatternPhase.PursuitSlam) PursuitSlam.Displace(displacement);
        }

        public void PushPlayerOutsideBody()
        {
            if (!IsAlive || !Body.enabled) return;
            var controller = player.GetComponent<CharacterController>();
            if (!controller.enabled) return;
            for (int pass = 0; pass < 4; pass++)
            {
                if (!Physics.ComputePenetration(controller, controller.transform.position, controller.transform.rotation,
                    Body, Body.transform.position, Body.transform.rotation, out Vector3 direction, out float distance)) break;
                if (Phase == PatternPhase.Falling || Phase == PatternPhase.PursuitSlam || direction.y < -.1f)
                {
                    MovePlayerSidewaysFromBody(0f);
                    continue;
                }
                controller.Move(direction * (distance + settings.collision.clearance));
                Physics.SyncTransforms();
            }
        }

        private void MovePlayerSidewaysFromBody(float minimumDistance)
        {
            var controller = player.GetComponent<CharacterController>();
            if (!controller.enabled) return;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            float lateral = Vector3.Dot(controller.bounds.center - transform.position, right);
            float side = lateral < 0f ? -1f : 1f;
            float required = Body.radius + controller.radius * Mathf.Max(controller.transform.lossyScale.x, controller.transform.lossyScale.z) + settings.collision.clearance;
            float distance = Mathf.Max(minimumDistance, required - Mathf.Abs(lateral));
            bool wasIgnored = Physics.GetIgnoreCollision(controller, Body);
            // 몸통 안에서 Move를 호출하면 CharacterController가 아래로 침투를 해소할 수 있다.
            // 옆으로 빠져나오는 동기 처리 동안만 이 몸통과의 충돌을 제외하고 지면·벽 충돌은 유지한다.
            Physics.IgnoreCollision(controller, Body, true);
            try
            {
                controller.Move(right * side * distance);
                float newLateral = Vector3.Dot(controller.bounds.center - transform.position, right);
                if (Mathf.Abs(newLateral) < required - .01f)
                    controller.Move(-right * side * (required + side * newLateral));
                if (TryGround(controller.transform.position, out Vector3 ground))
                {
                    float foot = controller.transform.TransformPoint(controller.center).y - controller.height * controller.transform.lossyScale.y * .5f;
                    if (foot < ground.y) controller.Move(Vector3.up * (ground.y - foot + settings.collision.clearance));
                }
            }
            finally { Physics.IgnoreCollision(controller, Body, wasIgnored); }
            Physics.SyncTransforms();
        }

        public void GetBodySegment(out Vector3 first, out Vector3 second)
        {
            Vector3 center = Body.transform.TransformPoint(Body.center);
            float half = Mathf.Max(0, Body.height * .5f - Body.radius);
            first = center - Body.transform.up * half;
            second = center + Body.transform.up * half;
        }

        private void OnPlayerDefeated()
        {
            // 패배 화면에서는 패턴 Tick이 멈추므로 남은 충격파와 먼지를 먼저 회수한다.
            if (GroundCrack != null) GroundCrack.Clear();
            if (dust != null) dust.Clear();
            if (snort != null) snort.Clear();
            if (teleportVfx != null) teleportVfx.ResetEffect();
        }

        protected override void OnDefeated()
        {
            if (teleportVfx != null) teleportVfx.ResetEffect();
            if (PursuitSlam != null) PursuitSlam.Clear();
            SetPhase(PatternPhase.Dead);
            if (LinkedAttack != null) LinkedAttack.Clear();
            if (GroundCrack != null) GroundCrack.Clear();
            Body.enabled = false;
            if (originalBody != null) originalBody.enabled = false;
            if (dust != null) dust.Clear();
            if (deathVfx != null) deathVfx.Play();
            RemoveMinions(true);
        }

        protected override void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDefeated;
            if (LinkedAttack != null) LinkedAttack.Clear();
            foreach (var enemy in minions)
            {
                if (enemy == null) continue;
                enemy.Health.Died -= OnMinionDied;
                Destroy(enemy.gameObject);
            }
            minions.Clear();
            if (settings != null) settings.Enemies.Remove(this);
            base.OnDestroy();
        }
    }
}
