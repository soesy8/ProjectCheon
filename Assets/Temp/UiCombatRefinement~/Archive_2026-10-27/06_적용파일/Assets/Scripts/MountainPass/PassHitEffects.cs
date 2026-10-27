using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    // 피격점의 월드 공간 입자만 사용한다. 적의 머티리얼이나 공격 궤적은 변경하지 않는다.
    public class PassHitEffects : MonoBehaviour
    {
        [SerializeField, InspectorName("피격 입자 머티리얼")] private Material impactMaterial;
        [SerializeField, InspectorName("기본 타격 입자 수"), Range(4, 24)] private int particleCount = 9;
        [SerializeField, InspectorName("피격 효과 크기"), Range(0.2f, 2f)] private float effectScale = 0.8f;
        private ParticleSystem sparks, flash;
        public int ActiveParticles => (sparks != null ? sparks.particleCount : 0) + (flash != null ? flash.particleCount : 0);
        public void Configure(Material material) => impactMaterial = material;
        private void Awake()
        {
            sparks = CreateSystem("HitSparks", 192, true);
            flash = CreateSystem("HitFlash", 24, false);
        }
        private ParticleSystem CreateSystem(string title, int capacity, bool stretch)
        {
            var item = new GameObject(title); item.transform.SetParent(transform, false);
            var particles = item.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = capacity;
            main.startSpeed = 0f; main.gravityModifier = stretch ? 0.12f : 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, stretch ? 0.1f : 1.5f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = impactMaterial;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 1.6f; renderer.velocityScale = 0.06f;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            particles.Play();
            return particles;
        }
        public void PlayHit(Vector3 point, Vector3 outward, bool wind, bool heavy, bool player)
        {
            if (impactMaterial == null || sparks == null) return;
            Vector3 normal = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.up;
            Vector3 origin = point + normal * 0.12f;
            Color tint = player ? new Color(1f, 0.4f, 0.27f) : wind ? new Color(0.55f, 0.85f, 1f) : new Color(1f, 0.8f, 0.39f);
            float strength = effectScale * (heavy ? 1.35f : wind ? 0.7f : 1f);
            flash.Emit(new ParticleSystem.EmitParams { position = origin, startColor = Color.Lerp(tint, Color.white, 0.55f),
                startSize = 0.48f * strength, startLifetime = 0.13f, velocity = Vector3.zero }, 1);
            int count = heavy ? particleCount + 5 : wind ? particleCount / 2 : particleCount;
            for (int i = 0; i < count; i++)
            {
                Vector3 scatter = Random.onUnitSphere;
                if (Vector3.Dot(scatter, normal) < 0f) scatter = -scatter;
                sparks.Emit(new ParticleSystem.EmitParams { position = origin,
                    velocity = (normal * 0.8f + scatter + Vector3.up * 0.35f) * Random.Range(1.1f, 2.8f) * strength,
                    startColor = tint, startSize = Random.Range(0.035f, 0.08f) * strength,
                    startLifetime = Random.Range(0.14f, 0.32f), rotation = Random.Range(0f, 360f) }, 1);
            }
        }
        private void OnDisable()
        {
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flash != null) flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
