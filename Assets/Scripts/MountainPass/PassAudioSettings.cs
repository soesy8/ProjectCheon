using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 연출용 기본 믹스는 유지하고, 사용자가 조절한 비율만 별도로 저장한다.
    public static class PassAudioSettings
    {
        private const string MasterKey = "Cheon.Audio.Master";
        private const string MusicKey = "Cheon.Audio.Music";
        private const string EffectsKey = "Cheon.Audio.Effects";
        private static bool loaded, changed;
        private static float masterVolume, musicVolume, effectsVolume;

        public static float MasterVolume { get { Load(); return masterVolume; } }
        public static float MusicVolume { get { Load(); return musicVolume; } }
        public static float EffectsVolume { get { Load(); return effectsVolume; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { loaded = changed = false; }

        private static void Load()
        {
            if (loaded) return;
            masterVolume = ReadVolume(MasterKey);
            musicVolume = ReadVolume(MusicKey);
            effectsVolume = ReadVolume(EffectsKey);
            loaded = true;
        }

        private static float ReadVolume(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 1f);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        }

        public static void SetMasterVolume(float value) { Load(); SetVolume(MasterKey, ref masterVolume, value); }
        public static void SetMusicVolume(float value) { Load(); SetVolume(MusicKey, ref musicVolume, value); }
        public static void SetEffectsVolume(float value) { Load(); SetVolume(EffectsKey, ref effectsVolume, value); }

        private static void SetVolume(string key, ref float current, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(current, value)) return;
            current = value;
            PlayerPrefs.SetFloat(key, value);
            changed = true;
        }

        public static void Save()
        {
            if (!changed) return;
            PlayerPrefs.Save();
            changed = false;
        }
    }
}
