var ids = new System.Collections.Generic.HashSet<string>(Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(System.IO.File.ReadAllText("Assets/Temp/MobileOptimization~/IgnoredCollisionIds.json")));
int restored = 0;
foreach (var transform in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var id = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(transform.gameObject).targetObjectId.ToString();
    if (ids.Contains(id)) { transform.gameObject.layer = 2; restored++; }
}
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return new { restored };
