using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    // 입장 중에만 몸 둘레에서 불꽃을 뿜고, 종료 뒤 남은 입자는 자연스럽게 소멸시킨다.
    public class PassBossEntranceAura : MonoBehaviour
    {
        [Serializable]
        public class Settings
        {
            [InspectorName("입장 오라 사용")] public bool enabled = true;
            [InspectorName("짙은 보라색")] public Color shadowColor = new Color(.1f, .015f, .19f, .7f);
            [InspectorName("보라색 불꽃")] public Color flameColor = new Color(.32f, .055f, .54f, .7f);
            [InspectorName("불씨 색상")] public Color emberColor = new Color(.65f, .25f, .9f, .8f);
            [InspectorName("몸에서 떨어진 거리 (m)"), Min(0)] public float surfaceOffset = .16f;
            [InspectorName("불꽃 높이 (m)"), Min(.1f)] public float flameHeight = 2.4f;
            [InspectorName("불꽃 너비 (m)"), Min(.05f)] public float flameWidth = 1.4f;
            [InspectorName("위로 피어오르는 속도 (m/s)"), Min(0)] public float riseSpeed = 1.7f;
            [InspectorName("초당 불꽃 수"), Range(10, 180)] public int particlesPerSecond = 95;
            [InspectorName("피어오르는 시간 (초)"), Min(.01f)] public float fadeInDuration = .7f;
            [InspectorName("사그라드는 시간 (초)"), Min(.01f)] public float fadeOutDuration = .8f;
        }

        private Settings settings;
        private Renderer[] modelRenderers;
        private ParticleSystem flames, shadows, embers;
        private Material flameMaterial, emberMaterial;
        private float intensity, emissionRemainder, emberRemainder;
        private bool emitting;

        public void Play(Transform model, Settings configuration)
        {
            Clear();
            settings = configuration;
            if (settings == null || !settings.enabled || model == null) return;
            if (flames == null && !CreateParticles()) return;
            modelRenderers = Array.FindAll(model.GetComponentsInChildren<Renderer>(true),
                item => item is MeshRenderer || item is SkinnedMeshRenderer);
            if (modelRenderers.Length == 0) return;
            enabled = true;
            emitting = true;
            flames.Play(); shadows.Play(); embers.Play();
        }

        public void FadeOut() => emitting = false;

        public void Clear()
        {
            emitting = false;
            intensity = emissionRemainder = emberRemainder = 0;
            if (flames != null) flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (shadows != null) shadows.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (embers != null) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            if (settings == null || flames == null || modelRenderers == null) return;
            float duration = emitting ? settings.fadeInDuration : settings.fadeOutDuration;
            intensity = Mathf.MoveTowards(intensity, emitting ? 1f : 0f, Time.deltaTime / Mathf.Max(.01f, duration));
            if (intensity <= 0)
            {
                // 남은 불씨까지 사라지면 시뮬레이션도 멈추고, 다음 입장 때 다시 켠다.
                if (!emitting && flames.particleCount == 0 && shadows.particleCount == 0 && embers.particleCount == 0)
                    enabled = false;
                return;
            }
            // 프레임이 지연되어도 한꺼번에 많은 입자가 쏟아지지 않도록 보정량을 제한한다.
            float deltaTime = Mathf.Min(Time.deltaTime, .1f);
            emissionRemainder += Mathf.Clamp(settings.particlesPerSecond, 10, 180) * intensity * deltaTime;
            while (emissionRemainder >= 1)
            {
                EmitFlame();
                emissionRemainder--;
            }
            emberRemainder += settings.particlesPerSecond * .16f * intensity * deltaTime;
            while (emberRemainder >= 1)
            {
                EmitEmber();
                emberRemainder--;
            }
        }

        private Vector3 GetSurfacePosition(out Vector3 outward)
        {
            // 몸통, 머리, 관모의 실제 경계를 사용하여 흔들리는 등장 애니메이션도 따라간다.
            Renderer surface = modelRenderers[UnityEngine.Random.Range(0, modelRenderers.Length)];
            Bounds bounds = surface.bounds;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            return bounds.center + new Vector3(outward.x * (bounds.extents.x * .85f + settings.surfaceOffset),
                UnityEngine.Random.Range(-.9f, .65f) * bounds.extents.y,
                outward.z * (bounds.extents.z * .85f + settings.surfaceOffset));
        }

        private void EmitFlame()
        {
            Vector3 position = GetSurfacePosition(out Vector3 outward);
            float scale = UnityEngine.Random.Range(.65f, 1.2f);
            Color tint = settings.flameColor; tint.a *= intensity;
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.up * (settings.riseSpeed * UnityEngine.Random.Range(.7f, 1.2f)) + outward * .2f,
                startSize3D = new Vector3(settings.flameWidth, settings.flameHeight, settings.flameWidth) * scale,
                startLifetime = UnityEngine.Random.Range(.65f, 1.15f),
                rotation = UnityEngine.Random.Range(-9f, 9f),
                startColor = tint
            };
            flames.Emit(particle, 1);
            particle.position -= Vector3.up * .18f;
            particle.startSize3D = Vector3.Scale(particle.startSize3D, new Vector3(1.8f, 1.2f, 1.8f));
            tint = settings.shadowColor; tint.a *= intensity;
            particle.startColor = tint;
            shadows.Emit(particle, 1);
        }

        private void EmitEmber()
        {
            Vector3 position = GetSurfacePosition(out Vector3 outward);
            float width = UnityEngine.Random.Range(.035f, .075f);
            Color tint = settings.emberColor; tint.a *= intensity;
            embers.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.up * (settings.riseSpeed * 1.5f) + outward * UnityEngine.Random.Range(.1f, .5f),
                startSize3D = new Vector3(width, width * 2.8f, width),
                startLifetime = UnityEngine.Random.Range(.6f, 1.4f),
                startColor = tint
            }, 1);
        }

        private bool CreateParticles()
        {
            Shader shader = Resources.Load<Shader>("S_BossEntranceAura");
            if (shader == null) return false;
            flameMaterial = new Material(shader) { name = "M_BossEntranceAura_Instance" };
            emberMaterial = new Material(shader) { name = "M_BossEntranceEmber_Instance" };
            emberMaterial.SetFloat("_Ember", 1);
            shadows = CreateLayer("EntranceAura_ShadowFlames", flameMaterial, 384);
            flames = CreateLayer("EntranceAura_VioletFlames", flameMaterial, 384);
            embers = CreateLayer("EntranceAura_Embers", emberMaterial, 96);
            shadows.GetComponent<ParticleSystemRenderer>().sortingFudge = -.1f;
            return true;
        }

        private ParticleSystem CreateLayer(string layerName, Material material, int capacity)
        {
            var item = new GameObject(layerName) { layer = gameObject.layer };
            item.transform.SetParent(transform, false);
            var particles = item.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = 0; main.startSize3D = true; main.maxParticles = capacity;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f),
                    new GradientAlphaKey(.7f, .55f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true; size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1f, 1, .15f));
            size.y = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .65f, 1, 1.3f));
            size.z = 1;
            var noise = particles.noise; noise.enabled = true;
            noise.strength = .22f; noise.frequency = .8f; noise.scrollSpeed = .7f;
            noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Low;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return particles;
        }

        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (flameMaterial != null) Destroy(flameMaterial);
            if (emberMaterial != null) Destroy(emberMaterial);
        }
    }
}
