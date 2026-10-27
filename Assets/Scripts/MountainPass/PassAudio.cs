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
        [SerializeField, InspectorName("플레이어 피격")] private AudioClip hurt;
        [SerializeField, InspectorName("장승 내려찍기")] private AudioClip slam;
        [SerializeField, InspectorName("산신각 종소리")] private AudioClip bell;
        [SerializeField, InspectorName("산바람 환경음")] private AudioClip wind;
        [SerializeField, InspectorName("발걸음")] private AudioClip footstep;
        [SerializeField, InspectorName("회피 옷자락")] private AudioClip dodge;
        [SerializeField, InspectorName("호리병 마시기")] private AudioClip drink;
        [SerializeField, InspectorName("메뉴 선택")] private AudioClip confirm;
        [SerializeField, InspectorName("장승 깨어남·소환")] private AudioClip spirit;
        [SerializeField, InspectorName("승리")] private AudioClip victory;
        [SerializeField, InspectorName("패배")] private AudioClip defeat;
        [SerializeField, InspectorName("부채 발도·납도")] private AudioClip equipment;
        [SerializeField, InspectorName("환경음 음량 비율"), Range(0f, 1f)] private float ambienceVolume = 0.16f;
        private AudioSource effects, ambience, movement;
        private readonly System.Collections.Generic.List<AudioClip> generatedClips = new System.Collections.Generic.List<AudioClip>();
        private float nextStrikeTime, nextSlamTime;
        private void Awake()
        {
            effects = gameObject.AddComponent<AudioSource>(); effects.playOnAwake = false; effects.volume = volume;
            movement = gameObject.AddComponent<AudioSource>(); movement.playOnAwake = false; movement.volume = volume * 0.7f;
            if (strike == null) strike = Tone("FanStrike", 240f, 0.16f, 0.3f);
            if (hurt == null) hurt = Tone("PlayerHurt", 95f, 0.28f, 0.2f);
            if (slam == null) slam = Tone("GuardianSlam", 52f, 0.7f, 0.35f);
            if (bell == null) bell = Tone("ShrineBell", 660f, 2.1f, 0.03f);
            ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false; ambience.loop = true; ambience.volume = volume * ambienceVolume;
            if (wind == null) wind = Tone("MountainWind", 65.4f, 4f, 0.12f, true);
            ambience.clip = wind; ambience.Play();
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
            if (effects == null || Time.unscaledTime < nextStrikeTime) return;
            nextStrikeTime = Time.unscaledTime + 0.035f;
            Play(isWind && windHit != null ? windHit : strike, isWind ? 0.55f : attack == 3 ? 1f : 0.7f);
        }
        public void Hurt() { if (effects != null) effects.PlayOneShot(hurt, 0.7f); }
        public void Slam()
        {
            if (Time.unscaledTime < nextSlamTime) return;
            nextSlamTime = Time.unscaledTime + 0.08f; Play(slam, 1f);
        }
        public void Bell() { if (effects != null) effects.PlayOneShot(bell, 0.8f); }
        public void Swing(int attack) => Play(fanWhoosh, attack == 3 ? 0.85f : 0.55f);
        public void Dodge() => Play(dodge, 0.65f);
        public void Drink() => Play(drink, 0.6f);
        public void Confirm() => Play(confirm, 0.5f);
        public void Spirit() => Play(spirit, 0.8f);
        public void Victory() => Play(victory, 0.8f);
        public void Defeat() => Play(defeat, 0.65f);
        public void Equip() => Play(equipment, 0.45f);
        public void Footstep(bool running)
        { if (movement != null && footstep != null) { movement.pitch = Random.Range(0.88f, 1.12f); movement.PlayOneShot(footstep, running ? 0.6f : 0.4f); } }
        private void Play(AudioClip clip, float gain) { if (effects != null && clip != null) effects.PlayOneShot(clip, gain); }
        private void OnDestroy() { foreach (var clip in generatedClips) if (clip != null) Destroy(clip); }
    }
}
