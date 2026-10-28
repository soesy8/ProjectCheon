var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "StairCloudVeil");
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.worldCamera = null;
return canvas.renderMode.ToString();
