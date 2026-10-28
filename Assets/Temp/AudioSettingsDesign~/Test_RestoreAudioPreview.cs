var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
var window = UnityEngine.Resources.FindObjectsOfTypeAll(gameViewType).First();
gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(window, 3);
Cheon.Prototype.MountainPass.PassAudioSettings.SetMasterVolume(1f);
Cheon.Prototype.MountainPass.PassAudioSettings.SetMusicVolume(1f);
Cheon.Prototype.MountainPass.PassAudioSettings.SetEffectsVolume(1f);
Cheon.Prototype.MountainPass.PassAudioSettings.Save();
return true;
