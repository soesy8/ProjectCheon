var records = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/Test_CheonBeforeExpansion.json"));
var changed = new System.Collections.Generic.List<string>();
var missing = new System.Collections.Generic.List<string>();
foreach (var record in records)
{
    var item = UnityEditor.EditorUtility.InstanceIDToObject((int)record["id"]) as UnityEngine.Transform;
    if (item == null) { missing.Add((string)record["name"]); continue; }
    if (item.name == "Test_MossTerrace" || item.name == "Test_SpiritTerrace") continue;
    var scale = record["scale"];
    var before = new UnityEngine.Vector3((float)scale[0], (float)scale[1], (float)scale[2]);
    if (UnityEngine.Vector3.Distance(before,item.lossyScale) > .0001f) changed.Add(item.name);
}
var world = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
var boss = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonBoss>(UnityEngine.FindObjectsInactive.Include);
var rig = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonCamera>();
var data = new UnityEditor.SerializedObject(rig);
var ground = world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>();
var additions = world.transform.Find("Test_ExpandedLandscape");
var result = new { preservedObjects = records.Count - missing.Count, missingOriginalObjects = missing.ToArray(), resizedOriginalProps = changed.ToArray(), ground = new[] {ground.size.x,ground.size.z}, floor = world.transform.Find("Test_MossTerrace").GetComponent<UnityEngine.Renderer>().bounds.size.ToString(), cameraSize = rig.GetComponent<UnityEngine.Camera>().orthographicSize, cameraBase = data.FindProperty("explorationSize").floatValue, battleRatio = data.FindProperty("battleDistanceMultiplier").floatValue, offset = data.FindProperty("offset").vector3Value.ToString(), player = player.transform.position.ToString(), boss = boss.transform.position.ToString(), addedObjects = additions.GetComponentsInChildren<UnityEngine.Transform>(true).Length, addedTrees = additions.GetComponentsInChildren<UnityEngine.Transform>(true).Count(t=>t.name=="Test_추가소나무"), addedLandmarks = additions.Cast<UnityEngine.Transform>().Where(t=>t.name.Contains("종각") || t.name.Contains("솟대") || t.name.Contains("환구") || t.name.Contains("석정원") || t.name.Contains("비단") || t.name.Contains("돌탑") || t.name.Contains("연못") || t.name.Contains("제단") || t.name.Contains("누각") || t.name.Contains("마당")).Select(t=>t.name).ToArray() };
System.IO.File.WriteAllText("Assets/Temp/Test_CheonExpansionAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return result;
