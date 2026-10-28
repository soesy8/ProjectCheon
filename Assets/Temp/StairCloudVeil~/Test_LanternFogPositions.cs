var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
if (!game.IsRunning) throw new System.Exception("Start the journey before verifying the fog range.");
var mist = game.GetComponent<Cheon.Prototype.MountainPass.PassMountainMist>();
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var canvas = veil.GetComponentsInChildren<Canvas>(true).First(c => c.name == "StairCloudVeil");
var mistFields = new SerializedObject(mist);
Vector3 start = ((Transform)mistFields.FindProperty("fadeStartPoint").objectReferenceValue).position;
Vector3 end = ((Transform)mistFields.FindProperty("fadeEndPoint").objectReferenceValue).position;
float density = mistFields.FindProperty("climbingDensity").floatValue;
float maxAlpha = new SerializedObject(veil).FindProperty("maximumOpacity").floatValue;
var volume = (MeshRenderer)new SerializedObject(mist.ApproachFog).FindProperty("volumeRenderer").objectReferenceValue;
var movement = game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>();
movement.InputEnabled = false;
Time.timeScale = 1f;
var labels = new[] { "before_start", "start", "quarter_up", "mid_up", "mid_after_3_seconds", "three_quarters_up",
    "end", "after_end", "three_quarters_down", "mid_down", "quarter_down", "start_again", "mid_jump" };
float[] fractions = { -0.15f, 0f, 0.25f, 0.5f, 0.5f, 0.75f, 1f, 1.2f, 0.75f, 0.5f, 0.25f, 0f, 0.5f };
float[] expected = { 0f, 0f, 0.15625f, 0.5f, 0.5f, 0.84375f, 1f, 1f, 0.84375f, 0.5f, 0.15625f, 0f, 0.5f };
System.Action<float, float> placePlayer = (fraction, extraHeight) =>
{
    Vector3 point = Vector3.LerpUnclamped(start, end, fraction);
    var hits = Physics.RaycastAll(new Vector3(point.x, 100f, point.z), Vector3.down, 120f)
        .Where(h => h.normal.y > 0.8f && !h.collider.transform.IsChildOf(game.Player))
        .OrderByDescending(h => h.point.y).ToArray();
    if (hits.Length == 0) throw new System.Exception("No floor at fog verification position.");
    movement.Teleport(hits[0].point + Vector3.up * extraHeight, Quaternion.LookRotation(end - start));
    game.View.SnapBehind();
};
var rows = new System.Collections.Generic.List<string> { "stage,path_fraction,clarity,world_density,cloud_alpha,world_sideways_opening" };
var failures = new System.Collections.Generic.List<string>();
var properties = new MaterialPropertyBlock();
int index = 0;
int waitFrames = 2;
int previousFrame = -1;
float waitUntil = 0f;
Canvas.WillRenderCanvases sample = null;
sample = () =>
{
    if (!Application.isPlaying || canvas == null)
    {
        Canvas.willRenderCanvases -= sample;
        return;
    }
    if (previousFrame == Time.frameCount) return;
    previousFrame = Time.frameCount;
    if (waitFrames-- > 0 || Time.time < waitUntil) return;
    volume.GetPropertyBlock(properties);
    float opening = properties.GetFloat("_OpeningDistance");
    float alpha = canvas.GetComponent<CanvasGroup>().alpha;
    rows.Add(labels[index] + "," + string.Join(",", new[] { fractions[index], mist.SummitClarity, mist.ApproachFog.Density, alpha, opening }
        .Select(v => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture))));
    if (Mathf.Abs(mist.SummitClarity - expected[index]) > 0.0001f ||
        Mathf.Abs(mist.ApproachFog.Density - density * (1f - expected[index])) > 0.0001f ||
        Mathf.Abs(alpha - maxAlpha * (1f - expected[index])) > 0.0001f || Mathf.Abs(opening) > 0.0001f)
        failures.Add(labels[index]);
    index++;
    if (index >= fractions.Length)
    {
        Canvas.willRenderCanvases -= sample;
        System.IO.File.WriteAllLines("Assets/Temp/StairCloudVeil~/LanternFogPositionVerification.csv", rows);
        string result = failures.Count == 0 ? "PASS: ascent, descent, stationary position, jump, shared fade range, no world splitting."
            : "FAIL: " + string.Join(", ", failures);
        System.IO.File.WriteAllText("Assets/Temp/StairCloudVeil~/LanternFogPositionVerification.txt", result);
        placePlayer(0.5f, 0.15f);
        return;
    }
    placePlayer(fractions[index], labels[index] == "mid_jump" ? 4f : 0.15f);
    waitFrames = 2;
    waitUntil = Time.time + (labels[index] == "mid_after_3_seconds" ? 3f : 0f);
};
placePlayer(fractions[0], 0.15f);
Canvas.willRenderCanvases += sample;
return "Verifying both fog layers at 13 positions, including descent and a stationary hold.";
