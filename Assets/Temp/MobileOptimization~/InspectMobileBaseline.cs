var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var report = new System.Text.StringBuilder();
report.AppendLine("Scene=" + scene.path + "; dirty=" + scene.isDirty + "; quality=" + UnityEngine.QualitySettings.GetQualityLevel());
foreach (var filter in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var mesh = filter.sharedMesh;
    if (mesh == null) continue;
    var renderer = filter.GetComponent<UnityEngine.Renderer>();
    report.AppendLine("Mesh|" + filter.name + "|" + UnityEditor.AssetDatabase.GetAssetPath(mesh) + "|" + mesh.name + "|vertices=" + mesh.vertexCount + "|triangles=" + (mesh.triangles.Length / 3) + "|bounds=" + mesh.bounds + "|enabled=" + (renderer != null && renderer.enabled) + "|active=" + filter.gameObject.activeInHierarchy + "|layer=" + filter.gameObject.layer);
}
foreach (var camera in UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    report.AppendLine("Camera|" + camera.name + "|position=" + camera.transform.position + "|rotation=" + camera.transform.eulerAngles);
System.IO.Directory.CreateDirectory("Assets/Temp/MobileOptimization~");
System.IO.File.WriteAllText("Assets/Temp/MobileOptimization~/Baseline.txt", report.ToString());
return new { scene = scene.path, dirty = scene.isDirty, quality = UnityEngine.QualitySettings.GetQualityLevel(), meshCount = UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length };
