using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    public class PassAudio : MonoBehaviour
    {
        [InspectorName("효과음 음량 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField, InspectorName("부채 휘두르기")] private AudioClip fanWhoosh;
        [SerializeField, InspectorName("나무 직접 타격")] private AudioClip strike;
        [SerializeField, InspectorName("바람 적중")] private AudioClip windHit;
        [SerializeField, InspectorName("바람 적중음 재생")] private bool playWindImpact = true;
        [SerializeField, InspectorName("플레이어 피격")] private AudioClip hurt;
        [SerializeField, InspectorName("장승 내려찍기")] private AudioClip slam;
        [SerializeField, InspectorName("장승 콧바람")] private AudioClip snort;
        [SerializeField, Range(0f, 1f), InspectorName("장승 콧바람 음량 비율")] private float snortVolume = 0.85f;
        [SerializeField, InspectorName("산신각 종소리")] private AudioClip bell;
        [SerializeField, InspectorName("산바람 환경음")] private AudioClip wind;
        [SerializeField, InspectorName("발걸음")] private AudioClip footstep;
        [SerializeField, InspectorName("회피 옷자락")] private AudioClip dodge;
        [SerializeField, InspectorName("호리병 치유")] private AudioClip drink;
        [SerializeField, Range(0.005f, 0.15f), InspectorName("치유 소리 종료 페이드 (초)")] private float drinkFadeSeconds = 0.04f;
        [SerializeField, InspectorName("메뉴 선택")] private AudioClip confirm;
        [SerializeField, Range(0f, 1f), InspectorName("버튼 호버 북소리 음량")] private float menuHoverVolume = 0.18f;
        [SerializeField, Range(0f, 1f), InspectorName("Esc 북소리 음량")] private float menuBackVolume = 0.5f;
        [SerializeField, Min(0f), InspectorName("호버 소리 최소 간격 (초)")] private float menuHoverInterval = 0.06f;
        [SerializeField, InspectorName("장승 깨어남·소환")] private AudioClip spirit;
        [SerializeField, InspectorName("승리")] private AudioClip victory;
        [SerializeField, InspectorName("패배")] private AudioClip defeat;
        [SerializeField, InspectorName("부채 발도 · 옷 스침")] private AudioClip equipment;
        [SerializeField, InspectorName("부채 납도 · 옷 스침")] private AudioClip sheatheEquipment;
        [SerializeField, Range(0f, 1f), InspectorName("발도·납도 음량 비율")] private float equipmentVolume = 0.45f;
        [SerializeField, InspectorName("환경음 음량 비율"), Range(0f, 1f)] private float ambienceVolume = 0.16f;
        private AudioSource effects, ambience, movement;
        private AudioSource menu;
        private AudioSource drinkingSound;
        private float drinkingFade = 1f;
        private bool drinkingSoundPaused;
        private readonly System.Collections.Generic.List<AudioClip> generatedClips = new System.Collections.Generic.List<AudioClip>();
        private float nextStrikeTime, nextSlamTime;
        private float nextMenuHoverTime;
        public void ConfigureMenuAudio(AudioSource source) { menu = source; }
        private void Awake()
        {
            float userGain = PassAudioSettings.MasterVolume * PassAudioSettings.EffectsVolume;
            effects = gameObject.AddComponent<AudioSource>(); effects.playOnAwake = false; effects.volume = volume * userGain;
            movement = gameObject.AddComponent<AudioSource>(); movement.playOnAwake = false; movement.volume = volume * 0.7f * userGain;
            if (strike == null) strike = Tone("FanStrike", 240f, 0.16f, 0.3f);
            if (hurt == null) hurt = Tone("PlayerHurt", 95f, 0.28f, 0.2f);
            if (slam == null) slam = Tone("GuardianSlam", 52f, 0.7f, 0.35f);
            if (bell == null) bell = Tone("ShrineBell", 660f, 2.1f, 0.03f);
            ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false; ambience.loop = true; ambience.volume = volume * ambienceVolume * userGain;
            if (wind == null) wind = Tone("MountainWind", 65.4f, 4f, 0.12f, true);
            ambience.clip = wind; ambience.Play();
            // 공용 효과음은 계속 재생한 채 치유 소리만 중단할 수 있도록 자식에 별도 소스를 둔다.
            var healingObject = new GameObject("GourdHealingAudio");
            healingObject.transform.SetParent(transform, false);
            drinkingSound = healingObject.AddComponent<AudioSource>();
            drinkingSound.playOnAwake = false;
            drinkingSound.spatialBlend = 0f;
            drinkingSound.clip = drink;
        }
        private void LateUpdate()
        {
            if (drinkingSound == null || effects == null) return;
            drinkingSound.volume = effects.volume * 0.6f * drinkingFade;
            drinkingSound.priority = effects.priority;
        }
        // 외부 음원 없이 짧은 타격음과 바람을 합성하고, 생성한 클립은 씬 종료 시 정리한다.
        private AudioClip Tone(string title, float frequency, float duration, float noise, bool loop = false)
        {
            const int rate = 22050;
            float[] samples = new float[Mathf.CeilToInt(duration * rate)];
            var random = new System.Random(title.Length * 31);
            float filteredNoise = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate; float progress = i / (float)samples.Length;
                filteredNoise = Mathf.Lerp(filteredNoise, (float)random.NextDouble() * 2f - 1f, 0.04f);
                float envelope = loop ? 0.6f + 0.25f * Mathf.Sin(progress * Mathf.PI * 2f) : Mathf.Min(1f, time * 160f) * Mathf.Exp(-progress * 5f) * (1f - progress);
                float note = Mathf.Sin(time * frequency * Mathf.PI * 2f) * 0.4f + Mathf.Sin(time * frequency * 2.01f * Mathf.PI * 2f) * 0.12f;
                samples[i] = (note + filteredNoise * noise) * envelope;
            }
            var clip = AudioClip.Create(title, samples.Length, 1, rate, false); clip.SetData(samples, 0); generatedClips.Add(clip); return clip;
        }
        public void Strike(bool isWind = false, int attack = 1)
        {
            // 바람 적중음을 끈 경우 직접 타격용 나무 소리로도 대체하지 않는다.
            if (isWind && !playWindImpact) return;
            if (effects == null || Time.unscaledTime < nextStrikeTime) return;
            nextStrikeTime = Time.unscaledTime + 0.035f;
            Play(isWind && windHit != null ? windHit : strike, isWind ? 0.55f : attack == 3 ? 1f : 0.7f);
        }
        public void Hurt() { if (effects != null) effects.PlayOneShot(hurt, 0.7f); }
        public void Snort() => Play(snort, snortVolume);
        public void Slam()
        {
            if (Time.unscaledTime < nextSlamTime) return;
            nextSlamTime = Time.unscaledTime + 0.08f; Play(slam, 1f);
        }
        public void Bell() { if (effects != null) effects.PlayOneShot(bell, 0.8f); }
        public void Swing(int attack) => Play(fanWhoosh, attack == 3 ? 0.85f : 0.55f);
        public void Dodge() => Play(dodge, 0.65f);
        public void Drink()
        {
            if (drinkingSound == null || drink == null || effects == null) return;
            drinkingFade = 1f;
            drinkingSoundPaused = false;
            drinkingSound.clip = drink;
            drinkingSound.volume = effects.volume * 0.6f;
            drinkingSound.Play();
        }
        public void UpdateDrinkingSound(float remainingSeconds)
        {
            // 입에서 떼기 직전에만 음량을 줄여, 종료 순간에 소리가 튀지 않게 한다.
            drinkingFade = Mathf.Clamp01(remainingSeconds / Mathf.Max(0.005f, drinkFadeSeconds));
            if (remainingSeconds <= 0f) StopDrinkingSound();
        }
        public void SetDrinkingSoundPaused(bool paused)
        {
            if (drinkingSound == null || drinkingSoundPaused == paused) return;
            drinkingSoundPaused = paused;
            if (paused) drinkingSound.Pause(); else drinkingSound.UnPause();
        }
        public void StopDrinkingSound()
        {
            if (drinkingSound != null) drinkingSound.Stop();
            drinkingSoundPaused = false;
        }
        public void Confirm() => Play(confirm, 0.5f);
        public void MenuBack() => PlayMenu(menuBackVolume);
        public void MenuHover()
        {
            // 일시정지 중에도 동작하고, 빠르게 여러 버튼을 지나갈 때 과도하게 겹치지 않게 한다.
            if (Time.unscaledTime < nextMenuHoverTime) return;
            nextMenuHoverTime = Time.unscaledTime + menuHoverInterval;
            PlayMenu(menuHoverVolume);
        }
        private void PlayMenu(float gain)
        {
            if (menu != null && menu.clip != null) menu.PlayOneShot(menu.clip, gain);
        }
        public void Spirit() => Play(spirit, 0.8f);
        public void Victory() => Play(victory, 0.8f);
        public void Defeat() => Play(defeat, 0.65f);
        public void Equip(bool drawing = true) => Play(drawing || sheatheEquipment == null ? equipment : sheatheEquipment, equipmentVolume);
        public void Footstep(bool running)
        { if (movement != null && footstep != null) { movement.pitch = Random.Range(0.88f, 1.12f); movement.PlayOneShot(footstep, running ? 0.6f : 0.4f); } }
        private void Play(AudioClip clip, float gain) { if (effects != null && clip != null) effects.PlayOneShot(clip, gain); }
        private void OnDisable() => StopDrinkingSound();
        private void OnDestroy()
        {
            if (drinkingSound != null) Destroy(drinkingSound.gameObject);
            foreach (var clip in generatedClips) if (clip != null) Destroy(clip);
        }
    }
}
