var groups = UnityEngine.Object.FindObjectsByType<UnityEngine.LODGroup>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
int restored = 0;
foreach (var group in groups)
{
    var mesh = group.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
    string path = UnityEditor.AssetDatabase.GetAssetPath(mesh);
    if (!path.Contains("/CheonMountainMap/")) continue;
    string name = mesh.name;
    bool background = name.StartsWith("Pine") || name.StartsWith("Mountain") || name.StartsWith("Granite") || name.StartsWith("Rocky") || name.StartsWith("Summit_Rim");
    if (background) continue;
    var levels = group.GetLODs();
    var original = levels[0].renderers[0];
    var generatedPaths = new System.Collections.Generic.List<string>();
    for (int i = 1; i < levels.Length; i++)
    foreach (var renderer in levels[i].renderers)
    {
        generatedPaths.Add(UnityEditor.AssetDatabase.GetAssetPath(renderer.GetComponent<UnityEngine.MeshFilter>().sharedMesh));
        UnityEngine.Object.DestroyImmediate(renderer.gameObject);
    }
    UnityEngine.Object.DestroyImmediate(group);
    original.enabled = true;
    foreach (string generated in generatedPaths) UnityEditor.AssetDatabase.DeleteAsset(generated);
    restored++;
}
UnityEditor.SessionState.SetBool("Cheon.VerifyMobileQuality", true);
UnityEngine.QualitySettings.SetQualityLevel(0, true);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return new { restored, remainingLods = UnityEngine.Object.FindObjectsByType<UnityEngine.LODGroup>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length };
