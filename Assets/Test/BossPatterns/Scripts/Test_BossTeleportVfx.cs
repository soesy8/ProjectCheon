using UnityEngine;
using UnityEngine.Rendering;
using Cheon.Prototype.MountainPass;

namespace Cheon.Tests.BossPatterns
{
    // 출발지의 작은 점프 → 숨김 / 도착지 연기 → 플레이어를 향한 재등장을 담당한다.
    public class Test_BossTeleportVfx : MonoBehaviour
    {
        private Test_BossPatternSettings settings;
        private Test_BossPatternController owner;
        private PassPlayerMovement player;
        private Renderer[] modelRenderers;
        private bool[] rendererStates;
        private ParticleSystem smoke;
        private Material smokeMaterial;
        private Vector3 origin;
        private float elapsed, duration;
        private bool savedInvulnerability;

        public bool IsTeleporting { get; private set; }
        public bool ModelHidden { get; private set; }
        public bool Succeeded { get; private set; }
        public Vector3 Destination { get; private set; }

        public void Configure(Test_BossPatternSettings configuration, Test_BossPatternController actor, Transform model, PassPlayerMovement target)
        {
            settings = configuration; owner = actor; player = target;
            modelRenderers = model.GetComponentsInChildren<Renderer>(true);
            rendererStates = new bool[modelRenderers.Length];
            var item = new GameObject("Test_TeleportWhiteSmoke"); item.transform.SetParent(transform, false);
            smoke = item.AddComponent<ParticleSystem>(); smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.playOnAwake = false; main.loop = false; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = 0; main.gravityModifier = 0; main.maxParticles = 1024;
            var emission = smoke.emission; emission.enabled = false;
            var shape = smoke.shape; shape.enabled = false;
            var color = smoke.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .04f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = smoke.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .6f, 1, 1.5f));
            // Resources에 둔 전용 셰이더로 흙먼지 텍스처의 알파를 재사용한다.
            smokeMaterial = new Material(Resources.Load<Shader>("Test_S_WhiteSmoke")) { name = "Test_WhiteSmoke_Instance" };
            smokeMaterial.SetTexture("_BaseMap", settings.dust.material.GetTexture("_BaseMap"));
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        public void Begin(Vector3 target)
        {
            ResetEffect();
            origin = transform.position; Destination = target;
            duration = Mathf.Max(.001f, Mathf.Min(settings.linkedAttack.teleportDuration, settings.jump.minimumFlight * .9f));
            savedInvulnerability = owner.Health.Invulnerable;
            for (int index = 0; index < modelRenderers.Length; index++) rendererStates[index] = modelRenderers[index].enabled;
            IsTeleporting = true;
            EmitSmoke(origin);
        }

        public void Tick(float deltaTime)
        {
            if (!IsTeleporting) return;
            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float vanish = Mathf.Clamp(settings.linkedAttack.vanishFraction, .05f, .8f);
            float appear = Mathf.Clamp(settings.linkedAttack.appearFraction, vanish + .05f, .95f);
            if (progress < vanish)
            {
                transform.position = origin + Vector3.up * (settings.linkedAttack.teleportHopHeight * Mathf.Sin(progress / vanish * Mathf.PI * .5f));
                return;
            }
            if (!ModelHidden && !Succeeded)
            {
                if (!owner.TryResolveTeleportDestination(Destination, out Vector3 resolved)) { Cancel(true); return; }
                Destination = resolved;
                SetModelVisible(false); ModelHidden = true;
                owner.Body.enabled = false; owner.Health.Invulnerable = true;
                transform.position = Destination + Vector3.up * settings.linkedAttack.teleportHopHeight;
                EmitSmoke(Destination);
                // 저프레임에서도 도착지 연기를 먼저 한 번 보여 준 뒤 나타난다.
                elapsed = Mathf.Min(elapsed, duration * vanish);
                return;
            }
            if (ModelHidden && progress >= appear)
            {
                // 연기 속에 플레이어가 들어왔으면 겹쳐 나타나지 않고 연계를 중단한다.
                if (!owner.TeleportDestinationFree(Destination)) { Cancel(true); return; }
                Vector3 facing = Vector3.ProjectOnPlane(player.transform.position - Destination, Vector3.up);
                if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
                owner.Health.Invulnerable = savedInvulnerability;
                owner.Body.enabled = true; SetModelVisible(true); ModelHidden = false; Succeeded = true;
            }
            if (Succeeded)
            {
                float settling = Mathf.Clamp01((progress - appear) / (1f - appear));
                transform.position = Destination + Vector3.up * (settings.linkedAttack.teleportHopHeight * (1f - settling));
                if (progress >= 1f) { transform.position = Destination; IsTeleporting = false; }
            }
            Physics.SyncTransforms();
        }

        public void Cancel(bool restoreCollision)
        {
            if (IsTeleporting && ModelHidden)
            {
                if (owner.TryResolveTeleportDestination(origin, out Vector3 resolved)) transform.position = resolved;
                SetModelVisible(true);
            }
            else if (IsTeleporting && !Succeeded) transform.position = origin;
            else if (IsTeleporting) transform.position = Destination;
            if (IsTeleporting) owner.Health.Invulnerable = savedInvulnerability;
            IsTeleporting = ModelHidden = Succeeded = false; elapsed = 0;
            if (restoreCollision && owner.Health != null && !owner.Health.IsDead) owner.Body.enabled = true;
        }

        public void ResetEffect()
        {
            Cancel(false);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void DisplaceForCollision(Vector3 displacement)
        {
            if (Succeeded) Destination += displacement;
            else if (!ModelHidden) origin += displacement;
        }

        private void SetModelVisible(bool visible)
        {
            for (int index = 0; index < modelRenderers.Length; index++)
                if (modelRenderers[index] != null) modelRenderers[index].enabled = visible && rendererStates[index];
        }

        private void EmitSmoke(Vector3 ground)
        {
            smoke.Play();
            for (int index = 0; index < settings.linkedAttack.smokeParticles; index++)
            {
                Vector2 offset = Random.insideUnitCircle * owner.Body.radius;
                Vector3 outward = new Vector3(offset.x, Random.Range(.25f, .8f), offset.y);
                smoke.Emit(new ParticleSystem.EmitParams
                {
                    position = ground + new Vector3(offset.x, Random.Range(0f, owner.Body.height), offset.y),
                    velocity = outward * settings.linkedAttack.smokeSpeed,
                    startColor = settings.linkedAttack.smokeColor,
                    startSize = Random.Range(settings.linkedAttack.smokeSize.x, settings.linkedAttack.smokeSize.y),
                    startLifetime = settings.linkedAttack.smokeLifetime, rotation = Random.Range(0, 360)
                }, 1);
            }
        }

        private void OnDestroy() { if (smokeMaterial != null) Destroy(smokeMaterial); }
    }
}
