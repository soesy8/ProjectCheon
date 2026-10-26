using System.Collections.Generic;
using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Tests.FanAttackVfx
{
    // 직접 타격과 원격 바람은 각각 한 번 맞을 수 있다. 여러 콜라이더는 같은 체력 대상으로 묶는다.
    [DefaultExecutionOrder(-15)]
    public class Test_FanAttackHitDetection : MonoBehaviour
    {
        [SerializeField] private Test_FanAttackVfxProfile profile;
        [SerializeField] private Test_FanAttackDamageSettings damageSettings;
        [SerializeField] private Test_FanAttackVfx visualEffect;
        [SerializeField] private PassAnimationController animationControl;
        [SerializeField] private PassGameFlow game;
        [SerializeField] private PassEffects effects;
        [SerializeField] private PassAudio audioControl;
        [SerializeField] private LayerMask targetLayers = Physics.DefaultRaycastLayers;
        private readonly Collider[] candidates = new Collider[64];
        private readonly HashSet<PassHealth> directStruck = new HashSet<PassHealth>();
        private readonly HashSet<PassHealth> windStruck = new HashSet<PassHealth>();
        private uint sequence;
        private int previousAttack;
        private float previousFrame;
        private Vector3 windOrigin;
        private Quaternion windFacing;
        private bool windLaunched;
        public int HitsLanded { get; private set; }
        public int DirectHitsLanded { get; private set; }
        public int WindHitsLanded { get; private set; }
        public float LastHitFrame { get; private set; }
        public int LastHitAttack { get; private set; }
        public Test_FanAttackDamageSettings DamageSettings => damageSettings;

        public void Configure(Test_FanAttackVfxProfile settings, PassAnimationController control, PassGameFlow flow,
            PassEffects visuals, PassAudio sounds, Test_FanAttackDamageSettings damage = null, Test_FanAttackVfx vfx = null)
        {
            profile = settings; animationControl = control; game = flow; effects = visuals; audioControl = sounds;
            if (damage != null) damageSettings = damage;
            if (vfx != null) visualEffect = vfx;
        }

        private void LateUpdate()
        {
            if (profile == null || damageSettings == null || animationControl == null || animationControl.IsPaused ||
                game != null && !game.IsRunning) return;
            int attack = animationControl.SelectedState switch { "ATTACK1" => 1, "ATTACK2" => 2, "ATTACK3" => 3, _ => 0 };
            if (attack == 0) { previousAttack = 0; return; }
            float frame = animationControl.NormalizedTime * animationControl.GetAttackMaximumFrame(attack);
            if (sequence != animationControl.PlaybackSequence || previousAttack != attack)
            {
                sequence = animationControl.PlaybackSequence;
                previousAttack = attack;
                previousFrame = animationControl.GetAttackStartFrame(attack);
                directStruck.Clear();
                windStruck.Clear();
                windLaunched = false;
            }
            if (Mathf.Approximately(frame, previousFrame)) return;
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
            previousFrame = frame;
        }

        private void ApplyDamage(PassHealth health, Vector3 point, int attack, float frame, bool wind)
        {
            if (!health.TakeDamage(damageSettings.GetDamage(attack, wind))) return;
            (wind ? windStruck : directStruck).Add(health);
            HitsLanded++;
            if (wind) WindHitsLanded++; else DirectHitsLanded++;
            LastHitFrame = frame;
            LastHitAttack = attack;
            if (effects != null) effects.Impact(point, wind ? new Color(.58f, .9f, .81f) : new Color(.3f, 1, .8f), wind ? .45f : .65f);
            if (audioControl != null) audioControl.Strike();
        }

        private bool InWind(Test_FanAttackVfxProfile.Wind wind, int attack, float frame, Collider target)
        {
            float start = Mathf.Max(wind.startFrame, animationControl.GetAttackStartFrame(attack));
            float end = Mathf.Min(wind.endFrame, animationControl.GetAttackEndFrame(attack));
            if (!windLaunched || end <= start || frame < start || previousFrame > end) return false;
            Vector3 point = target.ClosestPoint(windOrigin + Vector3.up * wind.height);
            Vector3 local = Quaternion.Inverse(windFacing) * (point - windOrigin);
            if (Mathf.Abs(local.y) > 3) return false;
            local.z /= wind.forwardScale;
            float distance = new Vector2(local.x, local.z).magnitude;
            float progress = wind.EvaluateTravel(Mathf.InverseLerp(start, end, frame));
            float before = wind.EvaluateTravel(Mathf.InverseLerp(start, end, previousFrame));
            float front = Mathf.Lerp(wind.startRadius, wind.radius, progress);
            // 표시 중인 띠와 이전 프레임의 선단을 포함해 고속 재생에서도 지나친 대상을 놓치지 않는다.
            float back = Mathf.Lerp(wind.startRadius, wind.radius, Mathf.Max(0, before - wind.waveLength));
            return distance >= back && distance <= front &&
                Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) <= wind.angle * .5f;
        }

        private bool InSweep(Test_FanAttackVfxProfile.Sweep sweep, float frame, Vector3 local, float targetSize)
        {
            float start = sweep.hitStartFrame < 0 ? sweep.startFrame : sweep.hitStartFrame;
            float end = sweep.hitEndFrame < 0 ? sweep.endFrame : sweep.hitEndFrame;
            if (frame < start || previousFrame > end) return false;
            float progress, distance;
            if (sweep.plane == Test_FanAttackVfxProfile.SweepPlane.Vertical)
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
            float before = sweep.motionProgress.Evaluate(Mathf.InverseLerp(sweep.startFrame, sweep.endFrame, previousFrame));
            return progress <= head + tolerance && progress >= before - tolerance;
        }
    }
}
