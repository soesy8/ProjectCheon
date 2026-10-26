using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    // 기존 먼지 머티리얼을 사용하고, 예고 표시 없이 실제 충돌 순간에만 흙먼지를 발생시킨다.
    public class PassBossImpactDust : MonoBehaviour
    {
        private PassBossPatternSettings settings;
        private ParticleSystem particles;
        private float bodyRadius;
        public int ImpactBursts { get; private set; }

        public void Configure(PassBossPatternSettings configuration, float radius)
        {
            settings = configuration; bodyRadius = radius;
            var item = new GameObject("ImpactDust"); item.transform.SetParent(transform, false);
            particles = item.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = false; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 2048;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            // 화면 밖에서 터진 일회성 효과도 실제 발생 시점부터 움직이고 수명이 끝나야 한다.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = 0; main.gravityModifier = 0;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .5f, 1, 1.5f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = settings.dust.material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        public void EmitLanding(Vector3 origin, float radius)
        {
            particles.Play();
            ImpactBursts++;
            for (int index = 0; index < settings.dust.landingParticles; index++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Emit(origin + new Vector3(offset.x, .06f, offset.y), new Vector3(offset.x, .6f, offset.y));
            }
        }

        public void EmitFall(CapsuleCollider body)
        {
            particles.Play();
            ImpactBursts++;
            Vector3 first = body.transform.TransformPoint(body.center - Vector3.up * (body.height * .5f - body.radius));
            Vector3 second = body.transform.TransformPoint(body.center + Vector3.up * (body.height * .5f - body.radius));
            for (int index = 0; index < settings.dust.fallParticles; index++)
            {
                float side = Random.value < .5f ? -1f : 1f;
                Vector3 origin = Vector3.Lerp(first, second, Random.value) + transform.right * bodyRadius * side;
                origin.y = transform.position.y + .06f;
                Emit(origin, transform.right * side * Random.Range(.5f, 1.5f) + Vector3.up * .6f);
            }
        }

        public void EmitExplosion(Vector3 origin, float radius)
        {
            particles.Play();
            ImpactBursts++;
            // 같은 흙먼지 재질을 중앙의 큰 분출, 안쪽 중간 무리, 바깥 작은 무리로 배치한다.
            for (int index = 0; index < settings.crack.centerDustCount; index++)
            {
                Vector2 offset = Random.insideUnitCircle * radius * .22f;
                Emit(origin + new Vector3(offset.x, .08f, offset.y), new Vector3(offset.x, Random.Range(.65f, 1f) * settings.crack.centerDustLift, offset.y), settings.crack.centerDustScale, settings.crack.explosionOpacity);
            }
            EmitExplosionRing(origin, radius * .58f, settings.crack.mediumDustClusters, settings.crack.mediumDustScale, 4);
            EmitExplosionRing(origin, radius * .95f, settings.crack.smallDustClusters, settings.crack.smallDustScale, 3);
        }

        private void EmitExplosionRing(Vector3 origin, float radius, int clusters, float sizeScale, int count)
        {
            float rotation = Random.Range(0f, Mathf.PI * 2f);
            for (int cluster = 0; cluster < clusters; cluster++)
            {
                float angle = rotation + cluster * Mathf.PI * 2f / clusters;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                for (int index = 0; index < count; index++)
                {
                    Vector2 jitter = Random.insideUnitCircle * radius * .12f;
                    Vector3 position = origin + outward * radius + new Vector3(jitter.x, .08f, jitter.y);
                    Emit(position, outward * Random.Range(.6f, 1.2f) + Vector3.up * Random.Range(.3f, .8f), sizeScale, settings.crack.explosionOpacity);
                }
            }
        }

        private void Emit(Vector3 origin, Vector3 velocity, float sizeScale = 1f, float opacity = -1f)
        {
            Color tint = settings.dust.color; tint.a = opacity >= 0 ? opacity : settings.dust.opacity;
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = origin, velocity = velocity,
                startColor = tint, startSize = Random.Range(settings.dust.size.x, settings.dust.size.y) * sizeScale,
                startLifetime = Random.Range(settings.dust.lifetime.x, settings.dust.lifetime.y), rotation = Random.Range(0, 360)
            }, 1);
        }

        public void Clear() { if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }
}
