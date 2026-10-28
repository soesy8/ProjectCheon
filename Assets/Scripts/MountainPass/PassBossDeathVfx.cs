using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    // 전투 종료 상태에서도 독립적으로 재생하고, 재도전 때 원래 외형을 복구한다.
    public class PassBossDeathVfx : MonoBehaviour
    {
        private sealed class RendererState
        {
            public Renderer renderer;
            public Material[] materials;
            public Material[] replacements;
            public MaterialPropertyBlock[] properties;
            public bool enabled;
            public ShadowCastingMode shadows;
        }

        private PassBossPatternSettings settings;
        private Transform visual;
        private bool minion;
        private PassBossPatternController owner;
        private bool materialsApplied;
        private ParticleSystem particles;
        private readonly List<RendererState> originals = new List<RendererState>();
        private readonly List<Material> dissolveMaterials = new List<Material>();
        private MaterialPropertyBlock properties;
        private float age, particleRemainder;

        public bool IsPlaying { get; private set; }
        public bool ModelHidden { get; private set; }
        public float TintProgress { get; private set; }
        public float DissolveProgress { get; private set; }
        public int ParticlesEmitted { get; private set; }

        public void Configure(PassBossPatternSettings configuration, Transform model, bool miniature)
        {
            settings = configuration; visual = model; minion = miniature;
            owner = GetComponent<PassBossPatternController>();
            properties = new MaterialPropertyBlock();
            var item = new GameObject("DeathPowder"); item.transform.SetParent(transform, false);
            particles = item.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = false; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = 0; main.gravityModifier = -.02f; main.maxParticles = 2048;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var noise = particles.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .6f;
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, .15f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = settings.death.powderMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        public void Play()
        {
            if (IsPlaying) return;
            ResetEffect();
            bool rebuild = originals.Count == 0;
            foreach (var state in originals)
            {
                if (state.renderer == null) continue;
                var current = state.renderer.sharedMaterials;
                if (current.Length != state.materials.Length) { rebuild = true; break; }
                state.materials = current;
            }
            if (rebuild)
            {
                originals.Clear(); DisposeMaterials(); PrepareRendererStates();
            }
            foreach (var state in originals)
            {
                if (state.renderer == null) continue;
                state.enabled = state.renderer.enabled; state.shadows = state.renderer.shadowCastingMode;
                for (int slot = 0; slot < state.materials.Length; slot++)
                {
                    state.renderer.GetPropertyBlock(state.properties[slot], slot);
                    ConfigureDissolveMaterial(state.replacements[slot], state.materials[slot]);
                }
                state.renderer.sharedMaterials = state.replacements;
                state.renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            materialsApplied = true;
            IsPlaying = true;
            particles.Play();
            ApplyProgress();
        }

        private void PrepareRendererStates()
        {
            // 모델과 슬롯이 같으면 소멸 재질·프로퍼티 버퍼를 다음 사망에서도 재사용한다.
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var state = new RendererState
                {
                    renderer = renderer, materials = renderer.sharedMaterials, enabled = renderer.enabled,
                    shadows = renderer.shadowCastingMode
                };
                state.properties = new MaterialPropertyBlock[state.materials.Length];
                state.replacements = new Material[state.materials.Length];
                for (int slot = 0; slot < state.replacements.Length; slot++)
                {
                    state.properties[slot] = new MaterialPropertyBlock();
                    var material = new Material(settings.death.material) { name = "DeathDissolve_Instance" };
                    state.replacements[slot] = material; dissolveMaterials.Add(material);
                }
                originals.Add(state);
            }
        }

        private void ConfigureDissolveMaterial(Material material, Material source)
        {
            material.shader = settings.death.material.shader;
            material.CopyPropertiesFromMaterial(settings.death.material);
            if (source == null) return;
            string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (source.HasProperty(map))
            {
                material.SetTexture("_BaseMap", source.GetTexture(map));
                material.SetTextureScale("_BaseMap", source.GetTextureScale(map));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset(map));
            }
            string color = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (source.HasProperty(color)) material.SetColor("_BaseColor", source.GetColor(color));
        }

        private void Update()
        {
            if (!IsPlaying) return;
            age += Time.deltaTime;
            float duration = settings.death.tintDuration + settings.death.dissolveDuration;
            TintProgress = Mathf.Clamp01(age / Mathf.Max(.01f, settings.death.tintDuration));
            DissolveProgress = Mathf.Clamp01((age - settings.death.tintDuration) / Mathf.Max(.01f, settings.death.dissolveDuration));
            ApplyProgress();
            if (age < duration)
            {
                float scale = minion ? settings.summon.minionScale : 1f;
                float rate = settings.death.particlesPerSecond * Mathf.Max(.25f, scale * scale);
                particleRemainder += rate * (DissolveProgress > 0 ? 1f : .25f) * Time.deltaTime;
                while (particleRemainder >= 1f) { EmitPowder(scale); particleRemainder--; }
            }
            else if (!ModelHidden)
            {
                foreach (var state in originals) if (state.renderer != null) state.renderer.enabled = false;
                ModelHidden = true;
            }
            float tail = Mathf.Max(settings.death.particleTail, settings.death.particleLifetime);
            if (minion) tail = Mathf.Max(tail, settings.summon.deathLifetime);
            if (age < duration + tail) return;
            IsPlaying = false;
            if (minion && (owner == null || !owner.TryReturnToSummonPool())) Destroy(gameObject);
        }

        private void ApplyProgress()
        {
            foreach (var state in originals)
            {
                if (state.renderer == null) continue;
                for (int slot = 0; slot < state.materials.Length; slot++)
                {
                    state.renderer.GetPropertyBlock(properties, slot);
                    properties.SetColor("_DeathColor", settings.death.tint);
                    properties.SetColor("_EdgeColor", settings.death.edgeColor);
                    properties.SetFloat("_NoiseScale", settings.death.noiseScale);
                    properties.SetFloat("_EdgeWidth", settings.death.edgeWidth);
                    properties.SetFloat("_TintProgress", TintProgress);
                    properties.SetFloat("_DissolveProgress", DissolveProgress);
                    state.renderer.SetPropertyBlock(properties, slot);
                }
            }
        }

        private void EmitPowder(float scale)
        {
            if (!particles.isPlaying) particles.Play();
            if (originals.Count == 0) return;
            Renderer renderer = originals[Random.Range(0, originals.Count)].renderer;
            if (renderer == null) return;
            Bounds bounds = renderer.bounds;
            Vector3 point = bounds.center + Vector3.Scale(Random.insideUnitSphere, bounds.extents);
            Vector3 spread = Random.insideUnitSphere; spread.y = Mathf.Abs(spread.y);
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = point, velocity = (spread + settings.death.particleDrift) * settings.death.particleSpeed,
                startColor = settings.death.particleColor,
                startSize = Random.Range(settings.death.particleSize.x, settings.death.particleSize.y) * Mathf.Sqrt(scale),
                startLifetime = settings.death.particleLifetime, rotation = Random.Range(0, 360)
            }, 1);
            ParticlesEmitted++;
        }

        public void ResetEffect()
        {
            IsPlaying = ModelHidden = false;
            age = particleRemainder = TintProgress = DissolveProgress = 0; ParticlesEmitted = 0;
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!materialsApplied) return;
            foreach (var state in originals)
            {
                if (state.renderer == null) continue;
                state.renderer.sharedMaterials = state.materials;
                state.renderer.enabled = state.enabled; state.renderer.shadowCastingMode = state.shadows;
                for (int slot = 0; slot < state.properties.Length; slot++) state.renderer.SetPropertyBlock(state.properties[slot], slot);
            }
            materialsApplied = false;
        }

        private void DisposeMaterials()
        {
            foreach (var material in dissolveMaterials) if (material != null) Destroy(material);
            dissolveMaterials.Clear();
        }

        private void OnDestroy() => DisposeMaterials();
    }
}
