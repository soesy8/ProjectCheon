string path = "Assets/Arts/Environment/CheonMountainMap/Models/Test_CheonMountainMap.fbx";
var meshes = new System.Collections.Generic.List<UnityEngine.Mesh>();
foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
    if (asset is UnityEngine.Mesh mesh && mesh.name.StartsWith("Pine")) meshes.Add(mesh);
var canonical = meshes.Find(m => m.name == "Pine_13");
int renderers = 0, colliders = 0;
foreach (var filter in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    if (filter.sharedMesh != canonical && meshes.Contains(filter.sharedMesh)) { filter.sharedMesh = canonical; renderers++; }
foreach (var collider in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshCollider>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    if (collider.sharedMesh != canonical && meshes.Contains(collider.sharedMesh)) { collider.sharedMesh = canonical; colliders++; }
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return new { renderers, colliders };
