
using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 공격 애니메이션의 원본 프레임과 VFX를 동기화한다.
    [DefaultExecutionOrder(-25)]
    public class PassFanAttackVfx : MonoBehaviour
    {
        [Header("공격 VFX 설정")]
        [InspectorName("효과 수치 설정 에셋")]
        [SerializeField] private PassFanAttackVfxProfile profile;
        [Header("메인 플레이어 연결")]
        [InspectorName("플레이어 동작 제어기")]
        [SerializeField] private PassAnimationController animationControl;
        [InspectorName("플레이어 기준 위치")]
        [SerializeField] private Transform playerRoot;
        [InspectorName("1·2·3타 베기 궤적 메시")]
        [SerializeField] private PassFanSweepMesh[] sweeps;
        [InspectorName("3타 바람 범위 메시")]
        [SerializeField] private PassFanSweepMesh windSector;
        [InspectorName("1타 바람 범위 메시")]
        [SerializeField] private PassFanSweepMesh attack1WindSector;
        [InspectorName("바람·먼지 입자 제어기")]
        [SerializeField] private PassFanWindParticles particles;
        [InspectorName("구름 소용돌이 설정")]
        [SerializeField] private PassFanCloudSpiralVfx cloudSpiral;

        private uint sequence;
        private int attack, revision = -1;
        private float previousFrame, fadeAge, windFadeAge, lastHead;
        private bool emitting, windStarted, endDustSent;

        private Vector3 emissionOrigin;
        private Quaternion emissionRotation;
        public float CurrentFrame { get; private set; }
        public int CurrentAttack => attack;
        public bool IsSweepVisible => attack > 0 && (profile.windDesign == PassFanAttackVfxProfile.WindDesign.C
            ? cloudSpiral != null && cloudSpiral.IsSweepVisible : sweeps[attack - 1].IsVisible);
        public int CloudWindBranchCount => cloudSpiral != null ? cloudSpiral.VisibleWindBranches : 0;
        public int WindParticleCount => particles != null ? particles.WindCount : 0;
        public int DustParticleCount => particles != null ? particles.DustCount : 0;
        public int FrontDustCount => particles != null ? particles.FrontDustCount : 0;
        public int EndDustCount => particles != null ? particles.EndDustCount : 0;
        public PassFanAttackVfxProfile Profile => profile;

        public bool TryGetWindPose(out Vector3 origin, out Quaternion rotation)
        {
            origin = emissionOrigin;
            rotation = emissionRotation;
            return attack > 0 && windStarted;
        }

        public void Configure(PassFanAttackVfxProfile settings, PassAnimationController control, Transform owner,
            PassFanSweepMesh[] attackSweeps, PassFanSweepMesh wind, PassFanWindParticles windParticles, PassAttackRangeEffects ranges,
            PassFanSweepMesh firstWind = null)
        {
            profile = settings; animationControl = control; playerRoot = owner;
            sweeps = attackSweeps; windSector = wind; particles = windParticles;
            if (firstWind != null) attack1WindSector = firstWind;
        }

        private void Start()
        {
            if (animationControl == null) animationControl = FindFirstObjectByType<PassAnimationController>();
            if (playerRoot == null && animationControl != null) playerRoot = animationControl.transform.parent;
            if (profile == null || animationControl == null || playerRoot == null)
            {
                Debug.LogError("공격 VFX 설정과 캐릭터 연결을 확인하세요.", this);
                enabled = false;
                return;
            }
            RefreshGeometry();
            HideAll();
        }

        public void RefreshGeometry()
        {
            if (profile == null || sweeps == null || sweeps.Length != 3 || windSector == null) return;
            HideAll();
            if (cloudSpiral == null) cloudSpiral = GetComponent<PassFanCloudSpiralVfx>();
            if (cloudSpiral == null && profile.windDesign == PassFanAttackVfxProfile.WindDesign.C)
                cloudSpiral = gameObject.AddComponent<PassFanCloudSpiralVfx>();
            if (cloudSpiral != null) cloudSpiral.Configure(profile.cloudSpiral, playerRoot);
            for (int i = 0; i < sweeps.Length; i++)
            {
                // 같은 구름 문양도 각 타격의 폭과 크기에 맞춘 머티리얼로 조절한다.
                sweeps[i].Rebuild(profile.GetSweep(i + 1), profile.GetSweepMaterial(i + 1));
            }
            RebuildWind(windSector, profile.wind);
            if (attack1WindSector != null) RebuildWind(attack1WindSector, profile.attack1Wind);
            particles.Configure(profile, playerRoot);
            revision = profile.Revision;
        }

        private void RebuildWind(PassFanSweepMesh mesh, PassFanAttackVfxProfile.Wind wind)
        {
            var style = new PassFanAttackVfxProfile.Sweep
            {
                radius = wind.radius, angle = wind.angle, innerRadius = wind.startRadius,
                height = AnimationCurve.Linear(0, wind.height, 1, wind.height), archHeight = 0
            };
            mesh.Rebuild(style, profile.GetWindMaterial(), true, wind.forwardScale);
            mesh.SetWindBranches(wind.branchCount);
        }

        private void LateUpdate()
        {
            if (profile == null || animationControl == null || playerRoot == null) return;
            if (revision != profile.Revision) RefreshGeometry();
            bool paused = animationControl.IsPaused || Time.timeScale <= 0;
            particles.SetPaused(paused);
            if (paused) return;

            int selectedAttack = animationControl.SelectedState switch { "ATTACK1" => 1, "ATTACK2" => 2, "ATTACK3" => 3, _ => 0 };
            if (selectedAttack != attack || sequence != animationControl.PlaybackSequence)
            {
                // 회피나 다른 공격으로 바뀔 때 이전 궤적이 다음 동작에 붙지 않게 한다.
                HideAll();
                attack = selectedAttack;
                sequence = animationControl.PlaybackSequence;
                previousFrame = attack > 0 ? animationControl.GetAttackStartFrame(attack) : 0;
                emitting = windStarted = endDustSent = false;
                fadeAge = windFadeAge = 0;
            }
            if (attack == 0) { CurrentFrame = 0; return; }
            CurrentFrame = animationControl.NormalizedTime * animationControl.GetAttackMaximumFrame(attack);
            transform.SetPositionAndRotation(playerRoot.position, Quaternion.Euler(0, playerRoot.eulerAngles.y, 0));
            UpdateSweep();
            UpdateWind();
            previousFrame = CurrentFrame;

        }

        private void UpdateSweep()
        {
            var style = profile.GetSweep(attack);
            float start = Mathf.Max(style.startFrame, animationControl.GetAttackStartFrame(attack));
            float end = Mathf.Min(style.endFrame, animationControl.GetAttackEndFrame(attack));
            if (end <= start) { HideAll(); return; }
            bool inWindow = CurrentFrame >= start && CurrentFrame <= end;
            bool crossedWindow = previousFrame <= end && CurrentFrame > end;
            if (inWindow || crossedWindow)
            {
                if (!emitting)
                {
                    emitting = true;
                }
                float progress = crossedWindow ? 1 : Mathf.InverseLerp(start, end, CurrentFrame);
                lastHead = Mathf.Clamp01(style.motionProgress.Evaluate(progress));
                ShowSweep(style, 1);
                fadeAge = 0;
            }
            else if (emitting && CurrentFrame > end)
            {
                fadeAge += Time.deltaTime;
                float opacity = 1 - Mathf.Clamp01(fadeAge / Mathf.Max(.01f, style.fadeSeconds));
                ShowSweep(style, opacity);
            }
        }

        private void ShowSweep(PassFanAttackVfxProfile.Sweep style, float opacity)
        {
            if (profile.windDesign == PassFanAttackVfxProfile.WindDesign.C)
            {
                if (cloudSpiral != null) cloudSpiral.ShowSweep(style, lastHead, opacity);
            }
            else sweeps[attack - 1].Show(lastHead, style.tailLength, style.color, opacity, style.intensity);
        }

        private void UpdateWind()
        {
            var wind = profile.GetWind(attack);
            var mesh = attack == 1 ? attack1WindSector : windSector;
            if (wind == null || mesh == null) return;
            float start = Mathf.Max(wind.startFrame, animationControl.GetAttackStartFrame(attack));
            float end = Mathf.Min(wind.endFrame, animationControl.GetAttackEndFrame(attack));
            if (end <= start || CurrentFrame < start) return;
            bool inWindow = CurrentFrame <= end;
            bool crossedWindow = previousFrame <= end && CurrentFrame > end;
            if (inWindow || crossedWindow)
            {
                if (!windStarted)
                {
                    emissionOrigin = playerRoot.position;
                    emissionRotation = Quaternion.Euler(0, playerRoot.eulerAngles.y, 0);
                    particles.BeginEmission();
                    mesh.transform.SetPositionAndRotation(emissionOrigin, emissionRotation);
                    if (wind.conformToGround) mesh.ConformToGround(playerRoot, profile.dust.groundMask, wind.height);
                    windStarted = true;
                }
                float travelTime = crossedWindow ? 1 : Mathf.InverseLerp(start, end, CurrentFrame);
                float progress = wind.EvaluateTravel(travelTime);
                mesh.transform.SetPositionAndRotation(emissionOrigin, emissionRotation);
                ShowWind(mesh, wind, progress, 1);
                particles.Emit(Time.deltaTime, progress, emissionOrigin, emissionRotation, wind, travelTime);
                if (progress >= 1 && !endDustSent)
                {
                    particles.EmitEndDust(emissionOrigin, emissionRotation, wind);
                    endDustSent = true;
                }
                windFadeAge = 0;
            }
            else if (windStarted)
            {
                if (!endDustSent)
                {
                    particles.EmitEndDust(emissionOrigin, emissionRotation, wind);
                    endDustSent = true;
                }
                windFadeAge += Time.deltaTime;
                mesh.transform.SetPositionAndRotation(emissionOrigin, emissionRotation);
                ShowWind(mesh, wind, 1, 1 - Mathf.Clamp01(windFadeAge / wind.fadeSeconds));
            }
        }

        private void ShowWind(PassFanSweepMesh mesh, PassFanAttackVfxProfile.Wind wind, float progress, float opacity)
        {
            if (profile.windDesign == PassFanAttackVfxProfile.WindDesign.C)
            {
                if (cloudSpiral != null)
                    cloudSpiral.ShowWind(wind, progress, emissionOrigin, emissionRotation,
                        mesh.GetComponent<MeshFilter>().sharedMesh, opacity);
            }
            else mesh.Show(progress, wind.waveLength, wind.color, opacity, wind.intensity);
        }

        private void HideAll()
        {
            if (sweeps != null) foreach (var sweep in sweeps) if (sweep != null) sweep.Hide();
            if (windSector != null) windSector.Hide();
            if (attack1WindSector != null) attack1WindSector.Hide();
            if (cloudSpiral != null) cloudSpiral.Hide();
        }

        private void OnDisable()
        {
            HideAll();
            if (particles != null) particles.Clear();
        }
    }
}
