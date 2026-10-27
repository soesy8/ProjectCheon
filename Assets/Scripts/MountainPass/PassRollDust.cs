using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(-24)]
    public class PassRollDust : MonoBehaviour
    {
        [InspectorName("효과 수치 설정 에셋")]
        [SerializeField] private PassRollDustProfile profile;
        [InspectorName("플레이어 동작 제어기")]
        [SerializeField] private PassAnimationController animationControl;
        [InspectorName("플레이어 기준 위치")]
        [SerializeField] private Transform playerRoot;
        [InspectorName("흙먼지 설정")]
        [SerializeField] private ParticleSystem dust;
        private Transform hips;
        private uint sequence;
        private bool wasRolling, landed;
        private float previousFrame, emissionBudget;
        private Vector3 previousContact;
        private System.Random random = new System.Random(221);
        private readonly RaycastHit[] hits = new RaycastHit[24];
        public int LandingBursts { get; private set; }
        public int LandingParticles { get; private set; }
        public int TrailParticles { get; private set; }
        public int ParticleCount => dust != null ? dust.particleCount : 0;
        public float CurrentFrame { get; private set; }
        public float LastLandingFrame { get; private set; }
        public PassRollDustProfile Profile => profile;

        public void Configure(PassRollDustProfile settings, PassAnimationController control, Transform owner, ParticleSystem particles)
        {
            profile = settings; animationControl = control; playerRoot = owner; dust = particles;
            ConfigureParticles();
        }

        private void Start()
        {
            if (animationControl == null) animationControl = FindFirstObjectByType<PassAnimationController>();
            if (playerRoot == null && animationControl != null) playerRoot = animationControl.transform.parent;
            if (profile == null || animationControl == null || playerRoot == null || dust == null)
            {
                Debug.LogError("구르기 먼지의 설정과 캐릭터 연결을 확인하세요.", this);
                enabled = false;
                return;
            }
            hips = animationControl.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips);
            ConfigureParticles();
            dust.Play();
        }

        private void ConfigureParticles()
        {
            if (dust == null || profile == null) return;
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 192;
            main.startSpeed = 0;
            main.gravityModifier = 0;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = dust.emission; emission.enabled = false;
            var shape = dust.shape; shape.enabled = false;
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .5f, 1, 1.35f));
            var color = dust.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.6f, .5f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = profile.dustMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void LateUpdate()
        {
            if (profile == null || animationControl == null || dust == null) return;
            bool paused = animationControl.IsPaused || Time.timeScale <= 0;
            if (paused) { if (!dust.isPaused) dust.Pause(); return; }
            if (!dust.isPlaying) dust.Play();
            if (!animationControl.isActiveAndEnabled || animationControl.SelectedState != "ROLL")
            {
                wasRolling = false;
                emissionBudget = 0;
                CurrentFrame = 0;
                return;
            }
            if (!wasRolling || sequence != animationControl.PlaybackSequence)
            {
                sequence = animationControl.PlaybackSequence;
                wasRolling = true;
                landed = false;
                LandingBursts = LandingParticles = TrailParticles = 0;
                LastLandingFrame = -1;
                emissionBudget = 0;
                previousFrame = animationControl.GetPlaybackStartFrame(4);
                random = new System.Random(221);
            }
            CurrentFrame = animationControl.NormalizedTime * animationControl.GetPlaybackMaximumFrame(4);
            float start = Mathf.Max(profile.landingFrame, animationControl.GetPlaybackStartFrame(4));
            float end = Mathf.Min(profile.endFrame, animationControl.GetPlaybackEndFrame(4));
            // 마지막 접촉 프레임을 건너뛰어도 그 사이의 착지는 한 번만 처리한다.
            bool contactWindow = CurrentFrame >= start && previousFrame <= end && end > start;
            if (contactWindow)
            {
                Vector3 contact = hips != null ? hips.position : playerRoot.position;
                contact += playerRoot.forward * profile.contactForwardOffset;
                if (FindGround(ref contact))
                {
                    if (!landed)
                    {
                        EmitLanding(contact);
                        landed = true;
                        LastLandingFrame = CurrentFrame;
                        previousContact = contact;
                    }
                    float frameSpan = Mathf.Max(.0001f, CurrentFrame - previousFrame);
                    float contactSpan = Mathf.Max(0, Mathf.Min(CurrentFrame, end) - Mathf.Max(previousFrame, start));
                    float delta = Time.deltaTime * Mathf.Clamp01(contactSpan / frameSpan);
                    float distance = Vector3.Distance(previousContact, contact);
                    emissionBudget += profile.particlesPerSecond * delta + profile.particlesPerMeter * distance;
                    int count = Mathf.Min(64, Mathf.FloorToInt(emissionBudget));
                    emissionBudget -= count;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 point = Vector3.Lerp(previousContact, contact, (i + .5f) / Mathf.Max(1, count));
                        point += playerRoot.right * Range(-profile.trailWidth, profile.trailWidth);
                        Vector3 drift = -playerRoot.forward * profile.trailDrift + playerRoot.right * Range(-.25f, .25f);
                        if (EmitParticle(point, drift, false)) TrailParticles++;
                    }
                    previousContact = contact;
                }
                else emissionBudget = 0;
            }
            previousFrame = CurrentFrame;
        }

        private void EmitLanding(Vector3 contact)
        {
            int emitted = 0;
            for (int i = 0; i < profile.landingCount; i++)
            {
                float radians = (i + Range(.1f, .9f)) * Mathf.PI * 2 / Mathf.Max(1, profile.landingCount);
                Vector3 outward = new Vector3(Mathf.Cos(radians), 0, Mathf.Sin(radians));
                Vector3 point = contact + outward * Range(.05f, profile.landingRadius);
                if (EmitParticle(point, outward * profile.landingOutwardSpeed, true)) emitted++;
            }
            if (emitted > 0) LandingBursts++;
            LandingParticles += emitted;
        }

        private bool EmitParticle(Vector3 point, Vector3 velocity, bool landing)
        {
            if (!FindGround(ref point)) return false;
            Color tint = profile.color * Range(.85f, 1.1f);
            tint.a = profile.opacity;
            dust.Emit(new ParticleSystem.EmitParams
            {
                position = point,
                velocity = velocity + Vector3.up * profile.upwardSpeed * (landing ? 1 : .65f),
                startSize = Range(profile.size.x, profile.size.y) * (landing ? 1.15f : 1),
                startLifetime = Range(profile.lifetime.x, profile.lifetime.y),
                startColor = tint,
                rotation = Range(0, 360),
                randomSeed = (uint)random.Next(1, int.MaxValue)
            }, 1);
            return true;
        }

        private bool FindGround(ref Vector3 point)
        {
            Vector3 origin = new Vector3(point.x, playerRoot.position.y + .6f, point.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, .6f + profile.groundDistance,
                profile.groundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(playerRoot) || hit.normal.y < .4f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                point.y = hit.point.y + profile.groundOffset;
            }
            return nearest < float.PositiveInfinity;
        }

        private void OnDisable()
        {
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            wasRolling = false;
            emissionBudget = 0;
        }

        private float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }
}
