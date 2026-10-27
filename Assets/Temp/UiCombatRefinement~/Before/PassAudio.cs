using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    public class PassAudio : MonoBehaviour
    {
        [InspectorName("효과음 음량 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        private AudioSource effects, ambience;
        private AudioClip strike, hurt, slam, bell, wind;
        private void Awake()
        {
            effects = gameObject.AddComponent<AudioSource>(); effects.playOnAwake = false; effects.volume = volume;
            strike = Tone("FanStrike", 240f, 0.16f, 0.3f); hurt = Tone("PlayerHurt", 95f, 0.28f, 0.2f);
            slam = Tone("GuardianSlam", 52f, 0.7f, 0.35f); bell = Tone("ShrineBell", 660f, 2.1f, 0.03f);
            ambience = gameObject.AddComponent<AudioSource>(); ambience.playOnAwake = false; ambience.loop = true; ambience.volume = volume * 0.18f;
            wind = Tone("MountainWind", 65.4f, 4f, 0.12f, true); ambience.clip = wind; ambience.Play();
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
            var clip = AudioClip.Create(title, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        public void Strike() { if (effects != null) effects.PlayOneShot(strike, 0.6f); }
        public void Hurt() { if (effects != null) effects.PlayOneShot(hurt, 0.7f); }
        public void Slam() { if (effects != null) effects.PlayOneShot(slam, 1f); }
        public void Bell() { if (effects != null) effects.PlayOneShot(bell, 0.8f); }
        private void OnDestroy() { foreach (var clip in new[] {strike, hurt, slam, bell, wind}) if (clip != null) Destroy(clip); }
    }
}
