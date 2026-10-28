var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var mist = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassMountainMist>();
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var canvas = veil.GetComponentsInChildren<Canvas>(true).First(c => c.name == "StairCloudVeil");
return new { frame = Time.frameCount, playing = Application.isPlaying, background = Application.runInBackground,
    state = game.State.ToString(), position = game.Player.position.ToString(), clarity = mist.SummitClarity,
    amount = mist.ClimbingMistAmount, alpha = canvas.GetComponent<CanvasGroup>().alpha,
    mode = canvas.renderMode.ToString(), order = canvas.sortingOrder, active = canvas.enabled,
    clouds = canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Length,
    canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Select(c => c.name + ":" + c.renderMode + ":" + c.sortingOrder).ToArray() };
