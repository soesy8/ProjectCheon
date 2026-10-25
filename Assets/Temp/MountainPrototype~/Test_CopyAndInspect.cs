var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (EditorApplication.isPlaying) throw new System.InvalidOperationException("편집 모드가 필요합니다.");
if (!scene.path.Contains("Assets/Test/CheonMountainMap/")) throw new System.InvalidOperationException("원본 씬을 확인하세요.");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
foreach (var folder in new[]{"Assets/Proto/MountainPass", "Assets/Proto/MountainPass/Scenes", "Assets/Proto/MountainPass/Scripts", "Assets/Proto/MountainPass/Settings", "Assets/Proto/MountainPass/Materials"}) {
 if (!AssetDatabase.IsValidFolder(folder)) { var index = folder.LastIndexOf('/'); AssetDatabase.CreateFolder(folder.Substring(0,index), folder.Substring(index+1)); }
}
var destination = "Assets/Proto/MountainPass/Scenes/MountainPass.unity";
if (System.IO.File.Exists(destination)) throw new System.InvalidOperationException("동일한 프로토타입 씬이 이미 있습니다.");
System.IO.File.WriteAllText("Assets/Temp/MountainPrototype~/Test_SourceScene.sha256", System.BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.IO.File.ReadAllBytes(scene.path))));
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, destination, true);
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(destination);
var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var roots = current.GetRootGameObjects();
var map = GameObject.Find("Test_CheonMountainMap");
var boss = roots.First(g=>g.name == "JangseungBoss");
return new { scene=current.path, fog = JsonUtility.ToJson(UnityEngine.Object.FindFirstObjectByType<Cheon.World.HeightFogVolume>()), gate = JsonUtility.ToJson(UnityEngine.Object.FindFirstObjectByType<Cheon.World.GateEnvironmentTransition>()), bossPosition=boss.transform.position.ToString(), bossScale=boss.transform.localScale.ToString(), bossParts = boss.GetComponentsInChildren<Renderer>().Select(r=>new {r.name, min=r.bounds.min.ToString(),max=r.bounds.max.ToString()}).ToArray(), mapParts = map.GetComponentsInChildren<Renderer>().Where(r=>!r.name.Contains("Row")).Select(r=>new{r.name,min=r.bounds.min.ToString(),max=r.bounds.max.ToString()}).Take(45).ToArray(), fonts=AssetDatabase.FindAssets("t:TMP_FontAsset",new[]{"Assets/Test/CheonMountainMap"}).Select(AssetDatabase.GUIDToAssetPath).ToArray() };

