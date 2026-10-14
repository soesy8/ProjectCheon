var records=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/Test_CheonBeforeRealmLayout.json"));
var resized=new System.Collections.Generic.List<string>();var missing=new System.Collections.Generic.List<string>();
foreach(var record in records)
{
    var item=UnityEditor.EditorUtility.InstanceIDToObject((int)record["id"]) as UnityEngine.Transform;
    if(item==null){missing.Add((string)record["name"]);continue;}
    if(item.name=="Test_MossTerrace" || item.name=="Test_CliffSkirt" || item.name=="Test_SpiritTerrace")continue;
    var scale=record["scale"];var before=new UnityEngine.Vector3((float)scale[0],(float)scale[1],(float)scale[2]);
    if(UnityEngine.Vector3.Distance(before,item.lossyScale)>.0001f)resized.Add(item.name);
}
var widthRecords=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/Test_CheonBeforeSpiritWidth.json"));
var realityMoved=new System.Collections.Generic.List<string>();
foreach(var record in widthRecords)
{
    var item=UnityEditor.EditorUtility.InstanceIDToObject((int)record["id"]) as UnityEngine.Transform;
    var position=record["position"];var before=new UnityEngine.Vector3((float)position[0],(float)position[1],(float)position[2]);
    if(item!=null && before.z<Test_CheonMapLayout.BoundaryZ && UnityEngine.Vector3.Distance(item.position,before)>.0001f)realityMoved.Add(item.name);
}
var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var realGround=world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>();
var spiritGround=world.transform.Find(Test_CheonSpiritWidth.MarkerName+"/Test_SpiritWalkableGround").GetComponent<UnityEngine.BoxCollider>();
var terrain=world.transform.Find("Test_MossTerrace").GetComponent<UnityEngine.Renderer>().bounds;
var player=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
int beforeCount=world.GetComponentsInChildren<UnityEngine.Transform>(true).Length;var playerPosition=player.transform.position;
Test_CheonMapExpansion.Apply();Test_CheonSpiritWidth.Apply();
var result=new{realityWidth=realGround.size.x,spiritWidth=spiritGround.size.x,visibleDimensions=new[]{terrain.size.x,terrain.size.z},minimumZ=terrain.min.z,maximumZ=terrain.max.z,boundaryZ=world.BoundaryZ,realityLength=realGround.size.z,spiritLength=spiritGround.size.z,realityFraction=realGround.size.z/terrain.size.z,preservedObjects=records.Count-missing.Count,missingOriginalObjects=missing.ToArray(),resizedOriginalProps=resized.ToArray(),movedRealityProps=realityMoved.ToArray(),duplicateObjects=world.GetComponentsInChildren<UnityEngine.Transform>(true).Length-beforeCount,playerStayed=player.transform.position==playerPosition};
System.IO.File.WriteAllText("Assets/Temp/Test_CheonSpiritWidthAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return result;
