var hud = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassHud>();
var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
UnityEngine.Canvas.ForceUpdateCanvases();
var buttons = hud.GetComponentsInChildren<UnityEngine.UI.Button>(true)
    .Where(button => button.name == "QuitGame" || button.name == "CreditsButton")
    .Select(button => {
        var rect = (UnityEngine.RectTransform)button.transform;
        var center = UnityEngine.RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var label = button.GetComponentInChildren<TMPro.TMP_Text>();
        return new { name = button.name, parent = button.transform.parent.name,
            active = button.gameObject.activeInHierarchy, interactable = button.IsInteractable(),
            label = label.text, overflowing = label.isTextOverflowing, x = center.x, y = center.y };
    }).ToArray();
return new { state = game.State.ToString(), frame = UnityEngine.Time.frameCount, width = UnityEngine.Screen.width,
    height = UnityEngine.Screen.height, timeScale = UnityEngine.Time.timeScale, buttons };
