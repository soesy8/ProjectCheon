var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var canvas = veil.GetComponentsInChildren<Canvas>(true).First(c => c.name == "StairCloudVeil");
var images = canvas.GetComponentsInChildren<UnityEngine.UI.RawImage>();
var left = images.First(i => i.name == "Cloud1").rectTransform;
var right = images.First(i => i.name == "Cloud2").rectTransform;
var rows = new System.Collections.Generic.List<string>();
rows.Add("elapsed,alpha,leftX,rightX,leftY,rightY");
rows.Add(string.Join(",", new[] { -1f, canvas.GetComponent<CanvasGroup>().alpha,
    left.anchoredPosition.x, right.anchoredPosition.x, left.anchoredPosition.y, right.anchoredPosition.y }
    .Select(v => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))));
float startTime = Time.time;
float[] sampleTimes = { 0f, 0.7f, 1.4f, 2.1f, 2.85f };
int index = 0;
Canvas.WillRenderCanvases sample = null;
sample = () =>
{
    if (!Application.isPlaying)
    {
        Canvas.willRenderCanvases -= sample;
        return;
    }
    float elapsed = Time.time - startTime;
    if (elapsed < sampleTimes[index]) return;
    rows.Add(string.Join(",", new[] { elapsed, canvas.GetComponent<CanvasGroup>().alpha,
        left.anchoredPosition.x, right.anchoredPosition.x, left.anchoredPosition.y, right.anchoredPosition.y }
        .Select(v => v.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))));
    index++;
    if (index < sampleTimes.Length) return;
    System.IO.File.WriteAllLines("Assets/Temp/StairCloudVeil~/SidewaysClearingVerification.csv", rows);
    Canvas.willRenderCanvases -= sample;
};
Canvas.willRenderCanvases += sample;
var movement = game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();
movement.Teleport(new Vector3(5.654829f, 27.25f, 36.23096f), Quaternion.LookRotation(new Vector3(0.77002f, 0f, -0.638018f)));
movement.InputEnabled = false;
game.View.SnapBehind();
Time.timeScale = 0.25f;
return "Recording clearing motion at quarter speed for inspection.";
