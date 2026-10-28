var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var canvas = veil.GetComponentsInChildren<Canvas>(true).First(c => c.name == "StairCloudVeil");
UnityEditor.SessionState.SetBool("Cheon.StairCloud.PreviousRunInBackground", Application.runInBackground);
Application.runInBackground = true;
Canvas.WillRenderCanvases sample = null;
sample = () =>
{
    if (!Application.isPlaying)
    {
        Canvas.willRenderCanvases -= sample;
        return;
    }
    if (game.State != Cheon.Prototype.MountainPass.PassGameFlow.RunState.Playing) return;
    float alpha = canvas.GetComponent<CanvasGroup>().alpha;
    string result = "frame=" + Time.frameCount + ", timeScale=" + Time.timeScale + ", deltaTime=" + Time.deltaTime
        + ", alpha=" + alpha + ", clouds=" + canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>().Length;
    UnityEditor.SessionState.SetString("Cheon.Cloud.StartVerification", result);
    System.IO.File.WriteAllText("Assets/Temp/StairCloudVeil~/JourneyStartVerification.txt", result);
    Canvas.willRenderCanvases -= sample;
};
Canvas.willRenderCanvases += sample;
game.BeginJourney();
return "First gameplay frame verification armed.";
