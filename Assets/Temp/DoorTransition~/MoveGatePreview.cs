var effect = UnityEngine.Object.FindFirstObjectByType<Cheon.World.GateEnvironmentTransition>();
var preview = GameObject.Find("Gate Crossing Preview");
if (preview == null)
{
    preview = new GameObject("Gate Crossing Preview");
    preview.transform.position = effect.transform.TransformPoint(new Vector3(0f, -3f, -2f));
    var playerField = effect.GetType().GetField("player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    playerField.SetValue(effect, preview.transform);
}
else
{
    Vector3 current = effect.transform.InverseTransformPoint(preview.transform.position);
    preview.transform.position = effect.transform.TransformPoint(new Vector3(0f, -3f, -current.z));
}
return new { frame = Time.frameCount, previewPosition = preview.transform.position.ToString(), effect.IsSpiritWorld, effect.SpiritAmount };
