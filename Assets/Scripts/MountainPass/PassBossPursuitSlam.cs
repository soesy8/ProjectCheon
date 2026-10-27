using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 각 이륙 직전에 목적지를 확정하고, 두 추적 점프와 등으로 찍는 강점프를 진행한다.
    public class PassBossPursuitSlam : MonoBehaviour
    {
        public enum SlamStage { None, Snort, TrackingFlight, TrackingRecovery, HeavyRise, HeavyFall, Recovery }
        private PassBossPatternSettings settings;
        private PassBossPatternController owner;
        private PassPlayerMovement player;
        private PassBossImpactDust dust;
        private PassBossSnortVfx snort;
        private PassAudio audioControl;
        private PassCamera view;
        private readonly Collider[] hits = new Collider[32];
        private Vector3 jumpStart;
        private float elapsed;
        private bool finalHitPlayer;

        public SlamStage Stage { get; private set; }
        public float StageElapsed => elapsed;
        public bool IsActive => Stage != SlamStage.None;
        public bool LandingReserved => Stage == SlamStage.TrackingFlight || Stage == SlamStage.HeavyRise || Stage == SlamStage.HeavyFall;
        public Vector3 LandingPosition { get; private set; }
        public int TrackingLandings { get; private set; }
        public int CompletedCount { get; private set; }
        public bool FinalHitWasBody { get; private set; }
        private PassBossPatternSettings.PursuitSlamSettings Timing => settings.pursuitSlam;

        public void Configure(PassBossPatternSettings configuration, PassBossPatternController actor,
            PassPlayerMovement target, PassBossImpactDust impactDust, PassBossSnortVfx noseSteam,
            PassAudio sounds, PassCamera cameraControl)
        {
            settings = configuration; owner = actor; player = target;
            dust = impactDust; snort = noseSteam; audioControl = sounds; view = cameraControl;
        }

        public void Begin()
        {
            TrackingLandings = 0; finalHitPlayer = FinalHitWasBody = false;
            FacePlayer(); SetPose(0); snort.EmitSnort(); SetStage(SlamStage.Snort);
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            float previousElapsed = elapsed;
            elapsed += deltaTime;
            switch (Stage)
            {
                case SlamStage.Snort:
                    if (elapsed >= Timing.windup) BeginTrackingJump();
                    break;
                case SlamStage.TrackingFlight:
                    float flight = Mathf.Clamp01(elapsed / Mathf.Max(.01f, Timing.trackingFlight));
                    transform.position = Vector3.Lerp(jumpStart, LandingPosition, flight) +
                        Vector3.up * (4f * Timing.trackingHeight * flight * (1f - flight));
                    SetPose(0);
                    if (flight >= 1f)
                    {
                        transform.position = LandingPosition; SetPose(0); Physics.SyncTransforms();
                        if (PlayerInShockwave(Timing.trackingRadius)) owner.DealPatternDamage(Timing.trackingDamage);
                        EmitImpact(Timing.trackingRadius, false);
                        TrackingLandings++; SetStage(SlamStage.TrackingRecovery);
                    }
                    break;
                case SlamStage.TrackingRecovery:
                    if (elapsed >= Timing.landingPause)
                    {
                        if (TrackingLandings < Timing.trackingJumps) BeginTrackingJump();
                        else BeginHeavyJump();
                    }
                    break;
                case SlamStage.HeavyRise:
                    float rise = Mathf.Clamp01(elapsed / Mathf.Max(.01f, Timing.heavyRise));
                    transform.position = Vector3.Lerp(jumpStart, LandingPosition, Mathf.SmoothStep(0, 1, rise)) +
                        Vector3.up * (Timing.heavyHeight * Mathf.Sin(rise * Mathf.PI * .5f));
                    SetPose(0);
                    if (rise >= 1f) SetStage(SlamStage.HeavyFall);
                    break;
                case SlamStage.HeavyFall:
                    UpdateHeavyFall(previousElapsed);
                    break;
                case SlamStage.Recovery:
                    // 7초 후딜의 마지막 구간에만 일어나므로 기상 시간이 추가로 붙지 않는다.
                    float standDuration = Mathf.Min(Timing.recovery, Mathf.Max(.01f, Timing.standUpDuration));
                    float stand = Mathf.Clamp01((elapsed - (Timing.recovery - standDuration)) / standDuration);
                    SetPose(Mathf.Lerp(270f, 360f, Mathf.SmoothStep(0, 1, stand)));
                    if (elapsed >= Timing.recovery)
                    { SetPose(0); CompletedCount++; SetStage(SlamStage.None); }
                    break;
            }
        }

        private void BeginTrackingJump()
        {
            LockLandingPosition(); jumpStart = transform.position;
            SetStage(SlamStage.TrackingFlight);
        }

        private void BeginHeavyJump()
        {
            LockLandingPosition(); jumpStart = transform.position;
            finalHitPlayer = FinalHitWasBody = false;
            SetStage(SlamStage.HeavyRise);
        }

        private void LockLandingPosition()
        {
            FacePlayer();
            Vector3 desired = player.transform.position;
            if (owner.FindPatternGround(desired, out Vector3 ground) && owner.IsPatternLandingFree(ground))
            { LandingPosition = ground; return; }
            // 플레이어가 낭떠러지나 다른 장승 옆에 있어도 유효한 지면에 착지하도록 근처를 탐색한다.
            for (int ring = 1; ring <= 4; ring++)
                for (int index = 0; index < 12; index++)
                {
                    float angle = index * Mathf.PI / 6f;
                    Vector3 candidate = desired + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * owner.Body.radius);
                    if (!owner.FindPatternGround(candidate, out ground) || !owner.IsPatternLandingFree(ground)) continue;
                    LandingPosition = ground; return;
                }
            LandingPosition = owner.FindPatternGround(transform.position, out ground) ? ground : transform.position;
        }

        private void FacePlayer()
        {
            Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        private void UpdateHeavyFall(float previousElapsed)
        {
            float before = Mathf.Clamp01(previousElapsed / Mathf.Max(.01f, Timing.heavyFall));
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, Timing.heavyFall));
            int samples = Mathf.Max(1, Mathf.CeilToInt((progress - before) * 270f / settings.collision.sweepDegrees));
            // 프레임 사이를 나눠 검사해 빠른 전방 회전 중 몸체가 플레이어를 통과해도 직접 충돌을 놓치지 않는다.
            for (int sample = 1; sample <= samples; sample++)
            {
                float step = Mathf.Lerp(before, progress, sample / (float)samples);
                transform.position = LandingPosition + Vector3.up * (Timing.heavyHeight * (1f - step * step));
                SetPose(270f * step); Physics.SyncTransforms();
                CheckBodyHit();
            }
            if (progress < 1f) return;
            transform.position = LandingPosition; SetPose(270f); Physics.SyncTransforms();
            CheckBodyHit();
            // 몸체 직접 충돌을 먼저 확정해 같은 찍기에 1.5배 피해와 충격파 피해가 중복되지 않게 한다.
            if (!finalHitPlayer && PlayerInShockwave(Timing.heavyRadius))
                finalHitPlayer = owner.DealPatternDamage(Timing.heavyDamage, true);
            EmitImpact(Timing.heavyRadius, true); SetStage(SlamStage.Recovery);
        }

        private void CheckBodyHit()
        {
            if (finalHitPlayer) return;
            owner.GetBodySegment(out Vector3 first, out Vector3 second);
            int count = Physics.OverlapCapsuleNonAlloc(first, second, owner.Body.radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                if (hits[index].GetComponentInParent<PassPlayerMovement>() != player) continue;
                if (owner.DealPatternDamage(Timing.heavyDamage * Timing.bodyDamageMultiplier, true))
                    finalHitPlayer = FinalHitWasBody = true;
                return;
            }
        }

        private bool PlayerInShockwave(float radius)
        {
            Vector3 difference = player.transform.position - LandingPosition;
            return Mathf.Abs(difference.y) <= player.Body.height &&
                Vector3.ProjectOnPlane(difference, Vector3.up).magnitude <= radius + player.Body.radius;
        }

        private void SetPose(float degrees)
        {
            Transform pivot = owner.Body.transform;
            pivot.localRotation = Quaternion.Euler(degrees, 0, 0);
            float verticalExtent = owner.Body.radius + (owner.Body.height * .5f - owner.Body.radius) *
                Mathf.Abs(Mathf.Cos(degrees * Mathf.Deg2Rad));
            // 몸체 중심을 착지점 위에 두어 270도 회전해 등을 대도 몸이 지면에 묻히지 않는다.
            pivot.position = transform.position + Vector3.up * verticalExtent - pivot.TransformVector(owner.Body.center);
        }

        private void EmitImpact(float radius, bool heavy)
        {
            dust.EmitPursuitLanding(LandingPosition, radius, heavy);
            audioControl.Slam(); view.AddShake(heavy ? 1.2f : .65f);
        }

        private void SetStage(SlamStage stage) { Stage = stage; elapsed = 0; }
        public void Displace(Vector3 displacement) { jumpStart += displacement; LandingPosition += displacement; }
        public void Clear()
        {
            SetStage(SlamStage.None); TrackingLandings = CompletedCount = 0;
            finalHitPlayer = FinalHitWasBody = false;
            if (snort != null) snort.Clear();
        }
    }
}
