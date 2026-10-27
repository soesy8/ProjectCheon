using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Tests.CombatPresentation
{
    // 원본 효과음의 호출 시점은 유지하고 테스트 씬의 음악과 음량만 조절한다.
    [DefaultExecutionOrder(150)]
    public class Test_TraditionalSoundscape : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("기존 효과음")] private PassAudio sounds;
        [SerializeField, InspectorName("산길 · 가야금과 대금")] private AudioClip explorationMusic;
        [SerializeField, InspectorName("수문장 · 장구와 좌고")] private AudioClip combatMusic;
        [SerializeField, InspectorName("일시정지 메뉴음")] private AudioClip pauseConfirm;
        [SerializeField, Range(0f, 1f), InspectorName("전체 음량")] private float masterVolume = .85f;
        [SerializeField, Range(0f, 1f), InspectorName("배경 음악")] private float musicVolume = .44f;
        [SerializeField, Range(0f, 1f), InspectorName("전투 효과음")] private float effectsVolume = .65f;
        [SerializeField, Range(0f, 1f), InspectorName("발걸음")] private float movementVolume = .33f;
        [SerializeField, Range(0f, 1f), InspectorName("산바람")] private float ambienceVolume = .19f;
        [SerializeField, Min(.05f), InspectorName("이전 음악 종료 시간")] private float musicFadeOutSeconds = .22f;
        [SerializeField, Min(.05f), InspectorName("다음 음악 시작 시간")] private float musicFadeInSeconds = .12f;
        [SerializeField, Range(0f, 1f), InspectorName("일시정지 음악 비율")] private float pauseMusicRatio = .28f;
        [SerializeField, Range(0f, 1f), InspectorName("타격·전조 중 음악 비율")] private float impactMusicRatio = .62f;
        private AudioSource exploration, combat, menu, effects, movement, ambience;
        private AudioSource activeMusic;
        private float transitionGain, contextGain, duckGain = 1f, duckUntil;
        private float originalEffectsVolume, originalMovementVolume, originalAmbienceVolume;
        private int originalEffectsPriority, originalMovementPriority, originalAmbiencePriority;
        private bool previousPaused, initialized, subscribed;

        public float ExplorationLevel => exploration != null ? exploration.volume : 0f;
        public float CombatLevel => combat != null ? combat.volume : 0f;
        public bool MusicPlaying => (exploration != null && exploration.isPlaying) || (combat != null && combat.isPlaying);
        public float ImpactDuck => duckGain;
        public float MasterVolume => masterVolume;

        public void Configure(PassGameFlow flow, PassAudio audio, AudioClip calm, AudioClip battle, AudioClip confirm)
        {
            game = flow; sounds = audio;
            explorationMusic = calm; combatMusic = battle; pauseConfirm = confirm;
        }

        private void Start()
        {
            if (game == null || sounds == null || game.Boss == null || game.PlayerHealth == null ||
                explorationMusic == null || combatMusic == null)
            {
                Debug.LogError("테스트 사운드: 게임 참조와 탐색·전투 음악을 연결하세요.", this);
                enabled = false;
                return;
            }
            // 이 테스트 씬의 PassAudio.Awake가 생성하는 순서는 효과음, 발걸음, 환경음이다.
            // 추가 AudioSource는 별도 자식에 두어 원본의 소스 순서를 바꾸지 않는다.
            AudioSource[] sources = sounds.GetComponents<AudioSource>();
            if (sources.Length != 3)
            {
                Debug.LogError("테스트 사운드: PassAudio 소스 구성이 변경되어 연결을 확인해야 합니다.", this);
                enabled = false;
                return;
            }
            effects = sources[0]; movement = sources[1]; ambience = sources[2];
            originalEffectsVolume = effects.volume;
            originalMovementVolume = movement.volume;
            originalAmbienceVolume = ambience.volume;
            originalEffectsPriority = effects.priority;
            originalMovementPriority = movement.priority;
            originalAmbiencePriority = ambience.priority;
            exploration = CreateSource("Test_ExplorationMusic", explorationMusic, true, 160);
            combat = CreateSource("Test_CombatMusic", combatMusic, true, 160);
            menu = CreateSource("Test_PauseMenuAudio", pauseConfirm, false, 48);
            initialized = true;
            Subscribe();
        }

        private AudioSource CreateSource(string title, AudioClip clip, bool looping, int priority)
        {
            var item = new GameObject(title);
            item.transform.SetParent(transform, false);
            var source = item.AddComponent<AudioSource>();
            source.clip = clip; source.loop = looping; source.playOnAwake = false;
            source.spatialBlend = 0f; source.volume = 0f; source.priority = priority; source.dopplerLevel = 0f;
            return source;
        }

        private void OnEnable() { if (initialized) Subscribe(); }

        private void Subscribe()
        {
            if (subscribed) return;
            game.PlayerHealth.Damaged += OnImpact;
            game.Boss.Health.Damaged += OnImpact;
            effects.priority = 64; movement.priority = 150; ambience.priority = 180;
            subscribed = true;
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            bool paused = game.State == PassGameFlow.RunState.Paused;
            if (paused && !previousPaused)
            {
                // 전투 잔향은 끊되 메뉴 확인음은 전용 소스로 짧게 재생한다.
                effects.Stop(); movement.Stop();
                if (pauseConfirm != null)
                {
                    menu.volume = masterVolume * effectsVolume * .5f;
                    menu.Play();
                }
            }
            previousPaused = paused;
            float delta = Time.unscaledDeltaTime;
            bool ending = game.State == PassGameFlow.RunState.Victory || game.State == PassGameFlow.RunState.Defeat;
            float targetGain = ending ? 0f : paused ? pauseMusicRatio : game.State == PassGameFlow.RunState.Intro ? .72f : 1f;
            if (game.State == PassGameFlow.RunState.Cinematic) targetGain = .52f;
            contextGain = Mathf.MoveTowards(contextGain, targetGain, delta * (paused ? 3f : 1.5f));
            float duckTarget = Time.unscaledTime < duckUntil ? impactMusicRatio : 1f;
            duckGain = Mathf.MoveTowards(duckGain, duckTarget, delta * (duckTarget < duckGain ? 12f : 1.8f));
            float gain = masterVolume * musicVolume * contextGain * duckGain;
            UpdateMusic(gain, delta);
            effects.volume = masterVolume * effectsVolume;
            movement.volume = paused ? 0f : masterVolume * movementVolume;
            float ambientTarget = masterVolume * ambienceVolume * (game.InEncounter ? .55f : 1f) * (paused ? .4f : 1f);
            ambience.volume = Mathf.MoveTowards(ambience.volume, ambientTarget, delta * .25f);
        }

        private void UpdateMusic(float gain, float delta)
        {
            AudioSource desired = game.InEncounter ? combat : exploration;
            if (activeMusic != desired)
            {
                transitionGain = Mathf.MoveTowards(transitionGain, 0f, delta / Mathf.Max(.05f, musicFadeOutSeconds));
                if (transitionGain <= .0001f)
                {
                    // 템포가 다른 두 곡을 섞지 않고, 이전 곡을 완전히 끈 다음 새 곡의 첫 박부터 시작한다.
                    if (activeMusic != null) { activeMusic.Stop(); activeMusic.volume = 0f; }
                    activeMusic = desired;
                    activeMusic.timeSamples = 0;
                    transitionGain = 0f;
                }
            }
            else transitionGain = Mathf.MoveTowards(transitionGain, 1f, delta / Mathf.Max(.05f, musicFadeInSeconds));

            SetMusic(exploration, activeMusic == exploration ? gain * transitionGain : 0f);
            SetMusic(combat, activeMusic == combat ? gain * transitionGain : 0f);
        }

        private static void SetMusic(AudioSource source, float gain)
        {
            source.volume = gain;
            if (gain > .0001f) { if (!source.isPlaying) source.Play(); }
            else if (source.isPlaying) source.Stop();
        }

        public void EmphasizeCue(float duration)
        {
            if (isActiveAndEnabled) duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + duration);
        }
        private void OnImpact(float damage) => EmphasizeCue(.22f);

        private void OnDisable()
        {
            if (exploration != null) exploration.Stop();
            if (combat != null) combat.Stop();
            if (menu != null) menu.Stop();
            activeMusic = null;
            transitionGain = 0f;
            if (!initialized) return;
            if (subscribed)
            {
                if (game != null && game.PlayerHealth != null) game.PlayerHealth.Damaged -= OnImpact;
                if (game != null && game.Boss != null && game.Boss.Health != null) game.Boss.Health.Damaged -= OnImpact;
                subscribed = false;
            }
            if (effects != null) { effects.volume = originalEffectsVolume; effects.priority = originalEffectsPriority; }
            if (movement != null) { movement.volume = originalMovementVolume; movement.priority = originalMovementPriority; }
            if (ambience != null) { ambience.volume = originalAmbienceVolume; ambience.priority = originalAmbiencePriority; }
        }

        private void OnDestroy()
        {
            if (exploration != null) Destroy(exploration.gameObject);
            if (combat != null) Destroy(combat.gameObject);
            if (menu != null) Destroy(menu.gameObject);
        }
    }
}
