using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    // 순간이동과 같은 흰 연기 재질을 코앞의 작은 김 덩어리로 재사용한다.
    public class PassBossSnortVfx : MonoBehaviour
    {
        private PassBossPatternSettings settings;
        private ParticleSystem steam;
        private Material material;
        private Coroutine emissionRoutine;
        public int Bursts { get; private set; }

        public void Configure(PassBossPatternSettings configuration)
        {
            settings = configuration;
            var item = new GameObject("NoseSteam");
            item.transform.SetParent(transform, false);
            steam = item.AddComponent<ParticleSystem>();
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = steam.main;
            main.playOnAwake = false; main.loop = false; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = 0; main.gravityModifier = 0; main.startSize3D = true; main.maxParticles = 128;
            var emission = steam.emission; emission.enabled = false;
            var shape = steam.shape; shape.enabled = false;
            var color = steam.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .06f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = steam.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1f, 1, settings.pursuitSlam.steamGrowth));
            var velocity = steam.velocityOverLifetime; velocity.enabled = true;
            // 처음의 빠른 하강을 점차 감속시켜, 김이 뒤집혀 올라가지 않고 아래에서 흩어지게 한다.
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1,
                AnimationCurve.EaseInOut(0, 1f, 1, settings.pursuitSlam.steamEndSpeedRatio));
            material = new Material(Resources.Load<Shader>("S_BossWhiteSmoke")) { name = "NoseSteam_Instance" };
            material.SetTexture("_BaseMap", settings.dust.material.GetTexture("_BaseMap"));
            var renderer = steam.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        public void EmitSnort()
        {
            if (!isActiveAndEnabled || steam == null) return;
            Clear(); steam.Play(); Bursts++;
            emissionRoutine = StartCoroutine(EmitSteamInSequence());
        }

        private IEnumerator EmitSteamInSequence()
        {
            int count = Mathf.Clamp(settings.pursuitSlam.steamParticles, 1, 128);
            float interval = settings.pursuitSlam.steamEmissionDuration / Mathf.Max(1, count - 1);
            var delay = new WaitForSeconds(Mathf.Max(.001f, interval));
            for (int index = 0; index < count; index++)
            {
                Vector3 nose = transform.position + transform.rotation * settings.pursuitSlam.noseOffset;
                float side = index % 2 == 0 ? -1f : 1f;
                float scale = Random.Range(.7f, 1.15f);
                Color tint = settings.linkedAttack.smokeColor; tint.a *= Random.Range(.25f, .45f);
                steam.Emit(new ParticleSystem.EmitParams
                {
                    position = nose + transform.right * (side * .14f + Random.Range(-.035f, .035f)) + Vector3.up * Random.Range(-.06f, .1f),
                    velocity = -Vector3.up * (settings.pursuitSlam.steamDownSpeed * Random.Range(.9f, 1.1f)) +
                        transform.forward * Random.Range(.15f, .3f) + transform.right * Random.Range(-.18f, .18f),
                    startSize3D = new Vector3(settings.pursuitSlam.steamSize.x, settings.pursuitSlam.steamSize.y,
                        settings.pursuitSlam.steamSize.x) * scale,
                    startLifetime = settings.pursuitSlam.steamLifetime * Random.Range(.75f, 1f),
                    rotation = Random.Range(-18f, 18f),
                    startColor = tint
                }, 1);
                // 한 번에 하나씩 양쪽 콧구멍을 번갈아 분출한다. 초기화 시 남은 분출도 취소한다.
                if (index + 1 < count) yield return delay;
            }
            emissionRoutine = null;
        }

        public void Clear()
        {
            if (emissionRoutine != null) { StopCoroutine(emissionRoutine); emissionRoutine = null; }
            if (steam != null) steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable() => Clear();
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
