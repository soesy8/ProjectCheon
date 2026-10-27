using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Tests.CombatPresentation
{
    // 보스의 준비 상태를 관찰해 경고음만 재생한다. 패턴과 피해 판정은 바꾸지 않는다.
    public class Test_BossAudioCues : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("수문장")] private PassBossPatternController boss;
        [SerializeField, InspectorName("음악 제어")] private Test_TraditionalSoundscape soundscape;
        [SerializeField, InspectorName("점프 · 흙 밀림")] private AudioClip jumpCue;
        [SerializeField, InspectorName("넘어찍기·추적 · 흙 압력")] private AudioClip heavyCue;
        [SerializeField, InspectorName("균열 · 흙 갈라짐")] private AudioClip crackCue;
        [SerializeField, Range(0f, 1f), InspectorName("공격 전조 음량")] private float cueVolume = .48f;
        private AudioSource source;
        private PassBossPatternController.PatternPhase previousPhase;
        public int PlayedCueCount { get; private set; }
        public AudioClip LastCue { get; private set; }

        public void Configure(PassGameFlow flow, PassBossPatternController guardian, Test_TraditionalSoundscape mix,
            AudioClip jump, AudioClip heavy, AudioClip crack)
        {
            game = flow; boss = guardian; soundscape = mix;
            jumpCue = jump; heavyCue = heavy; crackCue = crack;
        }

        private void Start()
        {
            if (game == null || boss == null || soundscape == null) { enabled = false; return; }
            var item = new GameObject("Test_BossWarningAudio");
            item.transform.SetParent(boss.transform, false);
            source = item.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.priority = 32;
            source.spatialBlend = .35f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 8f;
            source.maxDistance = 45f;
            source.dopplerLevel = 0f;
            previousPhase = boss.Phase;
        }

        private void LateUpdate()
        {
            if (source == null) return;
            var phase = boss.Phase;
            if (!game.IsRunning)
            {
                source.Stop();
                previousPhase = phase;
                return;
            }
            if (phase == previousPhase) return;
            previousPhase = phase;
            AudioClip cue = null;
            if (phase == PassBossPatternController.PatternPhase.JumpWindup) cue = jumpCue;
            else if (phase == PassBossPatternController.PatternPhase.FallWindup ||
                     phase == PassBossPatternController.PatternPhase.PursuitSlam) cue = heavyCue;
            else if (phase == PassBossPatternController.PatternPhase.CrackWindup) cue = crackCue;
            if (cue == null) return;
            source.volume = cueVolume * soundscape.MasterVolume;
            source.PlayOneShot(cue);
            soundscape.EmphasizeCue(cue.length + .1f);
            LastCue = cue;
            PlayedCueCount++;
        }

        private void OnDisable() { if (source != null) source.Stop(); }
        private void OnDestroy() { if (source != null) Destroy(source.gameObject); }
    }
}
