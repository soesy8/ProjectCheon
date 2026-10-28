var backup = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/BuildPreparation/Test_OriginalAudioPreferences.json"));
foreach (var item in backup)
{
    string key = (string)item["key"];
    if ((bool)item["exists"]) UnityEngine.PlayerPrefs.SetFloat(key, (float)item["value"]);
    else UnityEngine.PlayerPrefs.DeleteKey(key);
}
UnityEngine.PlayerPrefs.Save();
typeof(Cheon.Prototype.MountainPass.PassAudioSettings).GetMethod("ResetCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
return new { master = Cheon.Prototype.MountainPass.PassAudioSettings.MasterVolume, music = Cheon.Prototype.MountainPass.PassAudioSettings.MusicVolume, effects = Cheon.Prototype.MountainPass.PassAudioSettings.EffectsVolume };
