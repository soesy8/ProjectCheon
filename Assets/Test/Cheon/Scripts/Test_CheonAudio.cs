using UnityEngine;

namespace Cheon.Testing
{
    [RequireComponent(typeof(AudioSource))]
    public class Test_CheonAudio : MonoBehaviour
    {
        public enum Cue { Attack, Dodge, Spell, Seal, Hit, Hurt, Slam, Gate, Victory }
        [SerializeField, Range(0, 1)] private float musicVolume = .2f;
        [SerializeField, Range(0, 1)] private float effectVolume = .4f;
        public bool Muted { get; private set; }
        private AudioSource music, effects;
        private AudioClip[] cues;
        private AudioClip musicClip;

        private void Awake()
        {
            music = GetComponent<AudioSource>(); effects = gameObject.AddComponent<AudioSource>();
            music.spatialBlend = 0; effects.spatialBlend = 0;
            cues = new AudioClip[9];
            for (int i = 0; i < cues.Length; i++) cues[i] = SynthesizeCue((Cue)i);
            musicClip = CreateMusic(); music.clip = musicClip; music.loop = true; music.volume = musicVolume; music.Play();
        }

        // 외부 음원 없이 현을 뜯는 배음과 오음계로 수련지의 임시 배경음을 만든다.
        private AudioClip CreateMusic()
        {
            const int rate = 22050;
            const int seconds = 16;
            float[] samples = new float[rate * seconds];
            float[] notes = { 220, 261.63f, 293.66f, 392, 440, 392, 293.66f, 261.63f, 220, 293.66f, 392, 523.25f, 440, 392, 293.66f, 196 };
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < rate * 3 && n * rate + i < samples.Length; i++)
                {
                    float time = (float)i / rate;
                    float envelope = Mathf.Exp(-time * 2.2f) * Mathf.Min(1, time * 90);
                    float phase = time * notes[n] * Mathf.PI * 2;
                    samples[n * rate + i] += (Mathf.Sin(phase) + .35f * Mathf.Sin(phase * 2.001f) + .12f * Mathf.Sin(phase * 4.005f)) * envelope * .24f;
                }
            AudioClip clip = AudioClip.Create("Test_MountainPlucks", samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }

        private AudioClip SynthesizeCue(Cue cue)
        {
            const int rate = 22050;
            float length = cue == Cue.Gate || cue == Cue.Victory ? 1.3f : .28f;
            float[] samples = new float[(int)(rate * length)];
            var noise = new System.Random(47 + (int)cue);
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate, progress = time / length;
                float frequency = cue == Cue.Slam ? 90 : cue == Cue.Hurt ? 170 : cue == Cue.Hit ? 340 : cue == Cue.Victory ? 520 : cue == Cue.Spell ? 700 : 450;
                float pitch = cue == Cue.Victory ? 1 + Mathf.Floor(progress * 4) * .25f : 1 - progress * .6f;
                float wave = Mathf.Sin(time * frequency * pitch * Mathf.PI * 2);
                float hiss = (float)noise.NextDouble() * 2 - 1;
                samples[i] = (wave * .5f + hiss * (cue == Cue.Attack || cue == Cue.Dodge ? .65f : .16f)) * Mathf.Min(1, time * 90) * Mathf.Pow(1 - progress, 2) * .55f;
            }
            AudioClip clip = AudioClip.Create("Test_" + cue, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        public void PlayCue(Cue cue) { if (!Muted) effects.PlayOneShot(cues[(int)cue], effectVolume); }
        public void ToggleMute() { Muted = !Muted; music.mute = Muted; effects.mute = Muted; }
        private void OnDestroy() { if (cues != null) foreach (var clip in cues) Destroy(clip); if (musicClip != null) Destroy(musicClip); }
    }
}
