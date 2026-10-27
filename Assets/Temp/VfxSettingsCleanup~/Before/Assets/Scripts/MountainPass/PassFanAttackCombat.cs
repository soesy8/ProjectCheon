using System.Collections.Generic;
using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 직접 타격과 원격 바람은 각각 한 번 맞을 수 있다. 여러 콜라이더는 같은 체력 대상으로 묶는다.
    [DefaultExecutionOrder(-15)]
    public class PassFanAttackCombat : PassPlayerCombat
    {
        [InspectorName("효과 수치 설정 에셋")]
        [SerializeField] private PassFanAttackVfxProfile profile;
        [InspectorName("공격별 피해량 설정")]
        [SerializeField] private PassFanAttackDamageSettings damageSettings;
        [InspectorName("공격 시각 효과 제어기")]
        [SerializeField] private PassFanAttackVfx visualEffect;
        [InspectorName("공격 대상 레이어")]
        [SerializeField] private LayerMask targetLayers = Physics.DefaultRaycastLayers;
        private readonly Collider[] candidates = new Collider[64];
        private readonly HashSet<PassHealth> directStruck = new HashSet<PassHealth>();
        private readonly HashSet<PassHealth> windStruck = new HashSet<PassHealth>();
        private uint attackSequence;
        private int previousAttack;
        private float previousAttackFrame;
        private Vector3 windOrigin;
        private Quaternion windFacing;
        private bool windLaunched;
        public int DirectHitsLanded { get; private set; }
        public int WindHitsLanded { get; private set; }
        public float LastHitFrame { get; private set; }
        public int LastHitAttack { get; private set; }
        public PassFanAttackDamageSettings DamageSettings => damageSettings;

        public void ConfigureVfxCombat(PassFanAttackVfxProfile settings, PassFanAttackDamageSettings damage, PassFanAttackVfx vfx)
        {
            profile = settings; damageSettings = damage; visualEffect = vfx;
        }

        protected override void LateUpdate()
        {
            if (profile == null || damageSettings == null || animationControl == null || animationControl.IsPaused ||
                game == null || !game.IsRunning || !game.InEncounter) return;
            int attack = animationControl.SelectedState switch { "ATTACK1" => 1, "ATTACK2" => 2, "ATTACK3" => 3, _ => 0 };
            if (attack == 0) { previousAttack = 0; return; }
            if (view != null && view.BossLocked && boss != null && !boss.Health.IsDead)
            {
                Vector3 direction = Vector3.ProjectOnPlane(boss.transform.position - transform.position, Vector3.up);
                if (direction.sqrMagnitude > .1f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720f * Time.deltaTime);
            }
            float frame = animationControl.NormalizedTime * animationControl.GetAttackMaximumFrame(attack);
            if (attackSequence != animationControl.PlaybackSequence || previousAttack != attack)
            {
                attackSequence = animationControl.PlaybackSequence;
                previousAttack = attack;
                previousAttackFrame = animationControl.GetAttackStartFrame(attack);
                directStruck.Clear();
                windStruck.Clear();
                windLaunched = false;
            }
            if (Mathf.Approximately(frame, previousAttackFrame)) return;
            var sweep = profile.GetSweep(attack);
            var wind = profile.GetWind(attack);
            if (wind != null && frame >= Mathf.Max(wind.startFrame, animationControl.GetAttackStartFrame(attack)) && !windLaunched)
            {
                // 바람이 실제로 출발한 위치를 VFX와 공유한다. 이후 캐릭터가 돌아도 발사 방향을 유지한다.
                if (visualEffect == null || !visualEffect.TryGetWindPose(out windOrigin, out windFacing))
                {
                    windOrigin = transform.position;
                    windFacing = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                }
                windLaunched = true;
            }
            float reach = sweep.radius + Mathf.Abs(sweep.pivotHeight) + 1;
            if (wind != null) reach = Mathf.Max(reach, wind.radius * Mathf.Max(1, wind.forwardScale) + 1);
            Vector3 queryOrigin = windLaunched ? (transform.position + windOrigin) * .5f : transform.position;
            if (windLaunched) reach += Vector3.Distance(transform.position, windOrigin) * .5f;
            int count = Physics.OverlapSphereNonAlloc(queryOrigin + Vector3.up, reach, candidates, targetLayers, QueryTriggerInteraction.Ignore);
            Quaternion inverseFacing = Quaternion.Inverse(Quaternion.Euler(0, transform.eulerAngles.y, 0));
            for (int i = 0; i < count; i++)
            {
                var collider = candidates[i];
                if (collider.transform.IsChildOf(transform)) continue;
                var health = collider.GetComponentInParent<PassHealth>();
                if (health == null || health.IsDead) continue;
                var enemy = collider.GetComponentInParent<PassBossPatternController>();
                if (enemy == null || !enemy.IsAlive) continue;
                Vector3 center = transform.position + Vector3.up * sweep.pivotHeight;
                Vector3 point = collider.ClosestPoint(center);
                Vector3 local = inverseFacing * (point - transform.position);
                var extents = collider.bounds.extents;
                float targetSize = new Vector2(extents.x, extents.z).magnitude;
                if (!directStruck.Contains(health) && InSweep(sweep, frame, local, targetSize))
                    ApplyDamage(health, point, attack, frame, false);
                // else를 사용하지 않는다. 같은 프레임에도 두 판정을 모두 적용한다.
                if (wind != null && !windStruck.Contains(health) && InWind(wind, attack, frame, collider))
                    ApplyDamage(health, point, attack, frame, true);
            }
            previousAttackFrame = frame;
        }

        private void ApplyDamage(PassHealth health, Vector3 point, int attack, float frame, bool wind)
        {
            // 각 공격의 직접 타격·바람 기록으로 중복을 막는다. 적의 일반 피격 간격이 두 판정 중 하나를 지우지 않게 한다.
            if (!health.TakeDamage(damageSettings.GetDamage(attack, wind), ignoreHitRecovery: true)) return;
            (wind ? windStruck : directStruck).Add(health);
            HitsLanded++;
            if (wind) WindHitsLanded++; else DirectHitsLanded++;
            LastHitFrame = frame;
            LastHitAttack = attack;
            if (effects != null) effects.Impact(point, wind ? new Color(.58f, .9f, .81f) : new Color(.3f, 1, .8f), wind ? .45f : .65f);
            if (audioControl != null) audioControl.Strike();
            if (view != null) view.AddShake(wind ? .15f : attack == 3 ? .7f : .35f);
        }

        public override void ResetHits()
        {
            base.ResetHits();
            DirectHitsLanded = WindHitsLanded = 0;
            LastHitFrame = 0f; LastHitAttack = previousAttack = 0;
            directStruck.Clear(); windStruck.Clear(); windLaunched = false;
        }

        private bool InWind(PassFanAttackVfxProfile.Wind wind, int attack, float frame, Collider target)
        {
            float start = Mathf.Max(wind.startFrame, animationControl.GetAttackStartFrame(attack));
            float end = Mathf.Min(wind.endFrame, animationControl.GetAttackEndFrame(attack));
            if (!windLaunched || end <= start || frame < start || previousAttackFrame > end) return false;
            Vector3 point = target.ClosestPoint(windOrigin + Vector3.up * wind.height);
            Vector3 local = Quaternion.Inverse(windFacing) * (point - windOrigin);
            if (Mathf.Abs(local.y) > 3) return false;
            local.z /= wind.forwardScale;
            float distance = new Vector2(local.x, local.z).magnitude;
            float progress = wind.EvaluateTravel(Mathf.InverseLerp(start, end, frame));
            float before = wind.EvaluateTravel(Mathf.InverseLerp(start, end, previousAttackFrame));
            float front = Mathf.Lerp(wind.startRadius, wind.radius, progress);
            // 표시 중인 띠와 이전 프레임의 선단을 포함해 고속 재생에서도 지나친 대상을 놓치지 않는다.
            float back = Mathf.Lerp(wind.startRadius, wind.radius, Mathf.Max(0, before - wind.waveLength));
            return distance >= back && distance <= front &&
                Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) <= wind.angle * .5f;
        }

        private bool InSweep(PassFanAttackVfxProfile.Sweep sweep, float frame, Vector3 local, float targetSize)
        {
            float start = sweep.hitStartFrame < 0 ? sweep.startFrame : sweep.hitStartFrame;
            float end = sweep.hitEndFrame < 0 ? sweep.endFrame : sweep.hitEndFrame;
            if (frame < start || previousAttackFrame > end) return false;
            float progress, distance;
            if (sweep.plane == PassFanAttackVfxProfile.SweepPlane.Vertical)
            {
                Vector3 planePoint = Quaternion.Inverse(Quaternion.Euler(0, sweep.yawOffset, sweep.roll)) *
                    (local - Vector3.up * (sweep.pivotHeight + sweep.heightOffset));
                if (Mathf.Abs(planePoint.x) > sweep.verticalHitHalfWidth + targetSize) return false;
                distance = new Vector2(planePoint.y, planePoint.z).magnitude;
                float pitch = Mathf.Atan2(planePoint.y, planePoint.z) * Mathf.Rad2Deg;
                progress = Mathf.DeltaAngle(pitch, sweep.startPitch) / sweep.angle;
            }
            else
            {
                if (Mathf.Abs(local.y) > 3) return false;
                distance = new Vector2(local.x, local.z).magnitude;
                float angle = Mathf.DeltaAngle(sweep.yawOffset, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
                progress = angle / sweep.angle + .5f;
            }
            if (distance > sweep.radius + targetSize || distance < sweep.innerRadius - targetSize) return false;
            float tolerance = Mathf.Asin(Mathf.Clamp01(targetSize / Mathf.Max(.1f, distance))) * Mathf.Rad2Deg / sweep.angle;
            if (progress < -tolerance || progress > 1 + tolerance) return false;
            if (sweep.reverse) progress = 1 - progress;
            float head = sweep.motionProgress.Evaluate(Mathf.InverseLerp(sweep.startFrame, sweep.endFrame, frame));
            float before = sweep.motionProgress.Evaluate(Mathf.InverseLerp(sweep.startFrame, sweep.endFrame, previousAttackFrame));
            return progress <= head + tolerance && progress >= before - tolerance;
        }
    }
}
