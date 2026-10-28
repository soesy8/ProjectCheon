var hud = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassHud>();
var root = hud.transform.Find("MountainPassHUD/AudioSettings");
var sheet = (UnityEngine.RectTransform)root.Find("Sheet");
UnityEngine.Canvas.ForceUpdateCanvases();
var sliders = root.GetComponentsInChildren<UnityEngine.UI.Slider>(true)
    .Select(slider => new { name = slider.transform.parent.name, slider.value,
        handle = slider.handleRect.rect.size.ToString(), interactable = slider.IsInteractable(),
        position = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, ((UnityEngine.RectTransform)slider.transform).TransformPoint(((UnityEngine.RectTransform)slider.transform).rect.center)).ToString() }).ToArray();
var labels = root.GetComponentsInChildren<TMPro.TMP_Text>(true)
    .Select(label => new { label.name, label.text, label.isTextOverflowing }).ToArray();
return new { frame = UnityEngine.Time.frameCount, open = hud.IsAudioSettingsOpen,
    width = UnityEngine.Screen.width, height = UnityEngine.Screen.height, sheetScale = sheet.localScale.x,
    selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject?.name,
    sliders, labels,
    preferences = new { master = Cheon.Prototype.MountainPass.PassAudioSettings.MasterVolume,
        music = Cheon.Prototype.MountainPass.PassAudioSettings.MusicVolume,
        effects = Cheon.Prototype.MountainPass.PassAudioSettings.EffectsVolume } };
