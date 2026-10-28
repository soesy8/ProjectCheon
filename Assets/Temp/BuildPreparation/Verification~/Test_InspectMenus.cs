var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var hud = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassHud>();
var buttons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsSortMode.None)
    .Select(button => new { name = button.name, text = button.GetComponentInChildren<TMPro.TMP_Text>().text,
        interactable = button.IsInteractable(),
        position = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, ((UnityEngine.RectTransform)button.transform).TransformPoint(((UnityEngine.RectTransform)button.transform).rect.center)) }).ToArray();
var sliders = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Slider>(UnityEngine.FindObjectsSortMode.None)
    .Select(slider => new { name = slider.transform.parent.name, slider.value, interactable = slider.IsInteractable(),
        position = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, ((UnityEngine.RectTransform)slider.transform).TransformPoint(((UnityEngine.RectTransform)slider.transform).rect.center)) }).ToArray();
return Newtonsoft.Json.JsonConvert.SerializeObject(new { state = game.State.ToString(), settingsOpen = hud.IsAudioSettingsOpen,
    frame = UnityEngine.Time.frameCount, timeScale = UnityEngine.Time.timeScale,
    width = UnityEngine.Screen.width, height = UnityEngine.Screen.height,
    preferences = new { master = Cheon.Prototype.MountainPass.PassAudioSettings.MasterVolume, music = Cheon.Prototype.MountainPass.PassAudioSettings.MusicVolume, effects = Cheon.Prototype.MountainPass.PassAudioSettings.EffectsVolume },
    buttons, sliders,
    audio = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(UnityEngine.FindObjectsSortMode.None)
        .Select(source => new { name = source.name, source.volume, source.isPlaying, time = source.clip != null ? source.time : 0f, clip = source.clip != null ? source.clip.name : null }).ToArray() }, new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore });
