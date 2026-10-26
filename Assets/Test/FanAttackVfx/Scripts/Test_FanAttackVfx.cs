using System.Collections;
using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Tests.FanAttackVfx
{
    // 기존 전투 코드는 읽기만 하고 별도 VFX 오브젝트를 제어한다.
    [DefaultExecutionOrder(-25)]
    public class Test_FanAttackVfx : MonoBehaviour
    {
        [Header("테스트 전용 설정 — 에셋을 선택해 수치 조절")]
        [SerializeField] private Test_FanAttackVfxProfile profile;
        [Header("원본을 복사한 씬의 연결 대상")]
        [SerializeField] private PassAnimationController animationControl;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Test_FanSweepMesh[] sweeps;
        [SerializeField] private Test_FanSweepMesh windSector;
        [SerializeField] private Test_FanSweepMesh attack1WindSector;
        [SerializeField] private Test_FanWindParticles particles;
        [Header("미리보기")]
        [Tooltip("테스트 씬 시작 시 인트로를 넘기고 부채를 꺼냅니다.")]
        [SerializeField] private bool prepareOnStart = true;
        [Tooltip("기존 바닥 공격 범위를 함께 표시합니다. VFX의 크기를 비교할 때 켭니다.")]
        [SerializeField] private bool showOriginalRanges;
        [SerializeField] private PassAttackRangeEffects originalRanges;

        private uint sequence;
        private int attack, revision = -1;
        private float previousFrame, fadeAge, windFadeAge, lastHead;
        private bool emitting, windStarted, endDustSent;
        [SerializeField, HideInInspector] private bool previewPaused;
        private float freezeFrame = -1;
        private Vector3 emissionOrigin;
        private Quaternion emissionRotation;
        public float CurrentFrame { get; private set; }
        public int CurrentAttack => attack;
        public bool IsSweepVisible => attack > 0 && sweeps[attack - 1].IsVisible;
        public int WindParticleCount => particles != null ? particles.WindCount : 0;
        public int DustParticleCount => particles != null ? particles.DustCount : 0;
        public int FrontDustCount => particles != null ? particles.FrontDustCount : 0;
        public int EndDustCount => particles != null ? particles.EndDustCount : 0;
        public bool PreviewPaused => previewPaused;
        public Test_FanAttackVfxProfile Profile => profile;

        public bool TryGetWindPose(out Vector3 origin, out Quaternion rotation)
        {
            origin = emissionOrigin;
            rotation = emissionRotation;
            return attack > 0 && windStarted;
        }

        public void Configure(Test_FanAttackVfxProfile settings, PassAnimationController control, Transform owner,
            Test_FanSweepMesh[] attackSweeps, Test_FanSweepMesh wind, Test_FanWindParticles windParticles, PassAttackRangeEffects ranges,
            Test_FanSweepMesh firstWind = null)
        {
            profile = settings; animationControl = control; playerRoot = owner;
            sweeps = attackSweeps; windSector = wind; particles = windParticles; originalRanges = ranges;
            if (firstWind != null) attack1WindSector = firstWind;
        }

        private IEnumerator Start()
        {
            if (animationControl == null) animationControl = FindFirstObjectByType<PassAnimationController>();
            if (playerRoot == null && animationControl != null) playerRoot = animationControl.transform.parent;
            if (profile == null || animationControl == null || playerRoot == null)
            {
                Debug.LogError("테스트 VFX 설정과 캐릭터 연결을 확인하세요.", this);
                enabled = false;
                yield break;
            }
            RefreshGeometry();
            HideAll();
            if (prepareOnStart)
            {
                yield return null;
                var game = FindFirstObjectByType<PassGameFlow>();
                if (game != null) game.BeginJourney();
                animationControl.EnterCombat();
            }
        }

        public void RefreshGeometry()
        {
            if (profile == null || sweeps == null || sweeps.Length != 3 || windSector == null) return;
            for (int i = 0; i < sweeps.Length; i++) sweeps[i].Rebuild(profile.GetSweep(i + 1), profile.sweepMaterial);
            RebuildWind(windSector, profile.wind);
            if (attack1WindSector != null) RebuildWind(attack1WindSector, profile.attack1Wind);
            particles.Configure(profile, playerRoot);
            revision = profile.Revision;
        }

        private void RebuildWind(Test_FanSweepMesh mesh, Test_FanAttackVfxProfile.Wind wind)
        {
            var style = new Test_FanAttackVfxProfile.Sweep
            {
                radius = wind.radius, angle = wind.angle, innerRadius = wind.startRadius,
                height = AnimationCurve.Linear(0, wind.height, 1, wind.height), archHeight = 0
            };
            mesh.Rebuild(style, profile.windMaterial, true, wind.forwardScale);
        }

        private void LateUpdate()
        {
            if (profile == null || animationControl == null || playerRoot == null) return;
            if (revision != profile.Revision) RefreshGeometry();
            if (originalRanges != null && originalRanges.enabled != showOriginalRanges) originalRanges.enabled = showOriginalRanges;
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
            if (freezeFrame >= 0 && CurrentFrame >= freezeFrame)
            {
                freezeFrame = -1;
                previewPaused = true;
                Time.timeScale = 0;
                particles.SetPaused(true);
            }
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
                sweeps[attack - 1].Show(lastHead, style.tailLength, style.color, 1, style.intensity);
                fadeAge = 0;
            }
            else if (emitting && CurrentFrame > end)
            {
                fadeAge += Time.deltaTime;
                float opacity = 1 - Mathf.Clamp01(fadeAge / Mathf.Max(.01f, style.fadeSeconds));
                sweeps[attack - 1].Show(lastHead, style.tailLength, style.color, opacity, style.intensity);
            }
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
                mesh.Show(progress, wind.waveLength, wind.color, 1, wind.intensity);
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
                mesh.Show(1, wind.waveLength, wind.color, 1 - Mathf.Clamp01(windFadeAge / wind.fadeSeconds), wind.intensity);
            }
        }

        private void HideAll()
        {
            if (sweeps != null) foreach (var sweep in sweeps) if (sweep != null) sweep.Hide();
            if (windSector != null) windSector.Hide();
            if (attack1WindSector != null) attack1WindSector.Hide();
        }

        public void PlayPreview(int index, float holdAtFrame = -1)
        {
            if (!Application.isPlaying) { Debug.Log("Play Mode에서 미리보기를 실행하세요.", this); return; }
            StopAllCoroutines();
            StartCoroutine(PreviewRoutine(Mathf.Clamp(index, 1, 3), holdAtFrame));
        }

        private IEnumerator PreviewRoutine(int index, float holdAtFrame)
        {
            ResumePreview();
            particles.Clear();
            animationControl.PlayIdle();
            if (!animationControl.IsCombatRequested) animationControl.EnterCombat();
            while (animationControl.IsEquipmentTransition) yield return null;
            yield return new WaitForSeconds(.22f);
            freezeFrame = holdAtFrame;
            animationControl.TryPlayAttack(index);
        }

        [ContextMenu("미리보기/1타 재생")]
        public void PreviewAttack1() => PlayPreview(1);
        [ContextMenu("미리보기/2타 재생")]
        public void PreviewAttack2() => PlayPreview(2);
        [ContextMenu("미리보기/3타 재생")]
        public void PreviewAttack3() => PlayPreview(3);
        [ContextMenu("미리보기/일시정지 해제")]
        public void ResumePreview()
        {
            if (previewPaused) Time.timeScale = 1;
            previewPaused = false;
            freezeFrame = -1;
        }

        private void OnDisable()
        {
            ResumePreview();
            HideAll();
            if (particles != null) particles.Clear();
        }
    }
}
