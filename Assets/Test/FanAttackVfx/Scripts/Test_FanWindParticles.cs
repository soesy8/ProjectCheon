using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Tests.FanAttackVfx
{
    public class Test_FanWindParticles : MonoBehaviour
    {
        [SerializeField] private ParticleSystem windStreaks;
        [SerializeField] private ParticleSystem groundDust;
        private Test_FanAttackVfxProfile profile;
        private Transform owner;
        private System.Random random = new System.Random(127);
        private float windBudget, dustBudget;
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        public int WindCount => windStreaks != null ? windStreaks.particleCount : 0;
        public int DustCount => groundDust != null ? groundDust.particleCount : 0;
        public int FrontDustCount { get; private set; }
        public int EndDustCount { get; private set; }

        public void Configure(Test_FanAttackVfxProfile settings, Transform player, ParticleSystem wind = null, ParticleSystem dust = null)
        {
            profile = settings;
            owner = player;
            if (wind != null) windStreaks = wind;
            if (dust != null) groundDust = dust;
            ConfigureSystem(windStreaks, settings.GetWindParticleMaterial(), true);
            ConfigureSystem(groundDust, settings.dustMaterial, false);
        }

        private static void ConfigureSystem(ParticleSystem particles, Material material, bool wind)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 4;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = wind ? 512 : 256;
            main.startSpeed = 0;
            main.gravityModifier = 0;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.65f, .45f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, wind ? AnimationCurve.EaseInOut(0, .55f, 1, 1.2f) : AnimationCurve.EaseInOut(0, .35f, 1, 1.35f));
            var noise = particles.noise;
            noise.enabled = wind;
            noise.strength = .15f;
            noise.frequency = .8f;
            noise.scrollSpeed = .6f;
            // 새 입자뿐 아니라 이미 날아가는 바람결도 수명 후반에 감속한다.
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = wind;
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .12f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = .025f;
            renderer.lengthScale = 3.2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            if (Application.isPlaying) particles.Play();
        }

        public void BeginEmission()
        {
            windBudget = dustBudget = 0;
            FrontDustCount = EndDustCount = 0;
            random = new System.Random(127);
        }

        public void Emit(float delta, float progress, Vector3 origin, Quaternion rotation, Test_FanAttackVfxProfile.Wind wind, float travelTime = 0)
        {
            if (profile == null || delta <= 0) return;
            windBudget += delta * Mathf.Max(0, wind.particlesPerSecond);
            dustBudget += delta * Mathf.Max(0, profile.dust.particlesPerSecond);
            int windCount = Mathf.Min(64, Mathf.FloorToInt(windBudget));
            int dustCount = Mathf.Min(48, Mathf.FloorToInt(dustBudget));
            windBudget -= windCount;
            dustBudget -= dustCount;
            for (int i = 0; i < windCount; i++)
            {
                float angle = Range(-wind.angle * .5f, wind.angle * .5f);
                Vector3 direction = rotation * wind.StretchDirection(Quaternion.Euler(0, angle, 0) * Vector3.forward);
                float distance = Range(wind.startRadius, Mathf.Lerp(wind.startRadius, wind.radius, progress));
                float speed = Mathf.Max(.1f, wind.speed * wind.EvaluateSpeed(travelTime) / wind.EvaluateSpeed(0));
                Vector3 position = origin + direction * distance;
                Vector3 ahead = position + direction.normalized;
                bool grounded = FindGround(ref position);
                bool aheadGrounded = FindGround(ref ahead);
                float slope = grounded && aheadGrounded ? Mathf.Clamp(ahead.y - position.y, -1, 1) : 0;
                if (!wind.conformToGround) { position.y = origin.y; slope = 0; }
                position.y += Range(wind.height * .6f, wind.height * 1.7f);
                var particle = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = direction * speed + Vector3.up * (speed * direction.magnitude * slope + Range(-.1f, .25f)),
                    startLifetime = Mathf.Clamp((wind.radius - distance) / (speed * .56f), .04f, .65f),
                    startSize = Range(wind.size.x, wind.size.y),
                    startColor = wind.color,
                    rotation = Range(0, 360),
                    randomSeed = (uint)random.Next(1, int.MaxValue)
                };
                windStreaks.Emit(particle, 1);
            }
            for (int i = 0; i < dustCount; i++)
            {
                bool front = random.NextDouble() < profile.dust.frontShare;
                float side = random.Next(2) == 0 ? -1 : 1;
                float angle = front ? Range(-wind.angle * .5f, wind.angle * .5f) : side * wind.angle * Range(.4f, .52f);
                Vector3 direction = rotation * wind.StretchDirection(Quaternion.Euler(0, angle, 0) * Vector3.forward);
                float distance = Mathf.Lerp(wind.startRadius, wind.radius, progress) * (front ? Range(.94f, 1f) : Range(.68f, 1f));
                Vector3 point = origin + direction * distance;
                Vector3 velocity = front ? direction.normalized * profile.dust.frontSpeed :
                    direction.normalized * Range(.2f, .7f) + rotation * Vector3.right * side * profile.dust.outwardSpeed * Range(.4f, 1f);
                if (EmitDust(point, velocity, front) && front) FrontDustCount++;
            }
        }

        public void EmitEndDust(Vector3 origin, Quaternion rotation, Test_FanAttackVfxProfile.Wind wind)
        {
            // 끝의 원호 전체에 한 번 뿜어 양옆뿐 아니라 중앙 끝에서도 먼지가 일어난다.
            for (int i = 0; i < profile.dust.endBurstCount; i++)
            {
                float fraction = (i + Range(.1f, .9f)) / Mathf.Max(1, profile.dust.endBurstCount);
                float angle = Mathf.Lerp(-wind.angle * .5f, wind.angle * .5f, fraction);
                Vector3 direction = rotation * wind.StretchDirection(Quaternion.Euler(0, angle, 0) * Vector3.forward);
                Vector3 point = origin + direction * wind.radius;
                if (!EmitDust(point, direction.normalized * profile.dust.frontSpeed, true)) continue;
                FrontDustCount++;
                EndDustCount++;
            }
        }

        private bool EmitDust(Vector3 point, Vector3 velocity, bool front)
        {
            if (!FindGround(ref point)) return false;
            Color tint = profile.dust.color * Range(.82f, 1.12f);
            tint.a = profile.dust.opacity;
            var particle = new ParticleSystem.EmitParams
            {
                position = point,
                velocity = velocity + Vector3.up * profile.dust.upwardSpeed,
                startSize = Range(profile.dust.size.x, profile.dust.size.y) * (front ? 1.15f : 1),
                startLifetime = Range(profile.dust.lifetime.x, profile.dust.lifetime.y),
                startColor = tint,
                rotation = Range(0, 360),
                randomSeed = (uint)random.Next(1, int.MaxValue)
            };
            groundDust.Emit(particle, 1);
            return true;
        }

        private bool FindGround(ref Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, groundHits, 10f,
                profile.dust.groundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (owner != null && hit.transform.IsChildOf(owner) || hit.normal.y < .4f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                point.y = hit.point.y + profile.dust.groundOffset;
            }
            return nearest < float.PositiveInfinity;
        }

        public void SetPaused(bool paused)
        {
            foreach (var system in new[] { windStreaks, groundDust })
            {
                if (system == null) continue;
                if (paused && !system.isPaused) system.Pause();
                else if (!paused && !system.isPlaying) system.Play();
            }
        }

        public void Clear()
        {
            if (windStreaks != null) windStreaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (groundDust != null) groundDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            windBudget = dustBudget = 0;
        }

        private float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }
}
