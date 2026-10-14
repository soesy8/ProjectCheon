var primitive=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
var sphere=primitive.GetComponent<UnityEngine.MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(primitive);
int fixedCount=0;
foreach(var filter in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))
{
    var renderer=filter.GetComponent<UnityEngine.Renderer>();
    if(renderer!=null && renderer.sharedMaterial!=null && renderer.sharedMaterial.name.Contains("ConceptSpiritFlame")){filter.sharedMesh=sphere;fixedCount++;}
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
return fixedCount;
