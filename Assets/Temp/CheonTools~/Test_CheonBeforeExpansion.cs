var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying || scene.path != Test_CheonSceneBuilder.ScenePath)
    throw new System.InvalidOperationException("테스트 씬의 Edit Mode에서 실행하세요.");
var world = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
var boss = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonBoss>(UnityEngine.FindObjectsInactive.Include);
var camera = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonCamera>();
var transforms = world.GetComponentsInChildren<UnityEngine.Transform>(true).Concat(player.GetComponentsInChildren<UnityEngine.Transform>(true)).Concat(boss.GetComponentsInChildren<UnityEngine.Transform>(true));
var records = transforms.Select(t => new { id = t.GetInstanceID(), name = t.name, position = new[] { t.position.x, t.position.y, t.position.z }, scale = new[] { t.lossyScale.x, t.lossyScale.y, t.lossyScale.z } }).ToArray();
System.IO.File.WriteAllText("Assets/Temp/Test_CheonBeforeExpansion.json", Newtonsoft.Json.JsonConvert.SerializeObject(records, Newtonsoft.Json.Formatting.Indented));
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Temp/Test_CheonBeforeExpansion.unity", true);
var settings = new UnityEditor.SerializedObject(camera);
return new { objects = records.Length, explorationSize = settings.FindProperty("explorationSize").floatValue, battleSize = settings.FindProperty("battleSize").floatValue, offset = settings.FindProperty("offset").vector3Value.ToString(), floor = world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>().size.ToString() };
