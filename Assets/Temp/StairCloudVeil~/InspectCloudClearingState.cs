var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var fields = new[] { "wasShowing", "clearingProgress", "clearingSeconds", "openingDistance" }
    .ToDictionary(n => n, n => veil.GetType().GetField(n, flags).GetValue(veil));
var layers = veil.GetType().GetField("clouds", flags).GetValue(veil) as System.Array;
return new { fields, cachedLayers = layers != null ? layers.Length : 0, active = veil.isActiveAndEnabled, game.InEncounter,
    summit = game.GetComponent<Cheon.Prototype.MountainPass.PassMountainMist>().IsAtSummit, state = game.State.ToString(),
    frame = Time.frameCount, time = Time.time, timeScale = Time.timeScale };
