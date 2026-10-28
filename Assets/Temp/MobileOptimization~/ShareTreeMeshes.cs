var groups = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>>>(System.IO.File.ReadAllText("Assets/Temp/MobileOptimization~/DuplicateTrees.json"));
int replaced = 0, deleted = 0;
foreach (var group in groups)
{
    string prefix = "Assets/Arts/Environment/Mobile/Meshes/Test_CheonMountainMap_";
    string first = prefix + group[0]["name"] + "_LOD" + group[0]["level"] + ".asset";
    var shared = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(first);
    if (shared == null) throw new System.Exception("Shared tree mesh not found.");
    for (int index = 1; index < group.Count; index++)
    {
        string duplicate = prefix + group[index]["name"] + "_LOD" + group[index]["level"] + ".asset";
        var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(duplicate);
        foreach (var filter in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            if (filter.sharedMesh == mesh) { filter.sharedMesh = shared; replaced++; }
        if (UnityEditor.AssetDatabase.DeleteAsset(duplicate)) deleted++;
    }
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return new { replaced, deleted };
