var keys = new[] { "Cheon.Audio.Master", "Cheon.Audio.Music", "Cheon.Audio.Effects" };
var backup = keys.Select(key => new { key, exists = UnityEngine.PlayerPrefs.HasKey(key), value = UnityEngine.PlayerPrefs.GetFloat(key, 1f) }).ToArray();
System.IO.File.WriteAllText("Assets/Temp/BuildPreparation/Test_OriginalAudioPreferences.json", Newtonsoft.Json.JsonConvert.SerializeObject(backup));
return backup;
