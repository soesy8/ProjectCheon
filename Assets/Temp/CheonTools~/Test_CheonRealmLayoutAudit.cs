var records=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/Test_CheonBeforeRealmLayout.json"));
var resized=new System.Collections.Generic.List<string>(); var missing=new System.Collections.Generic.List<string>();
foreach(var record in records)
{
    var item=UnityEditor.EditorUtility.InstanceIDToObject((int)record["id"]) as UnityEngine.Transform;
    if(item==null) {missing.Add((string)record["name"]);continue;}
    if(item.name=="Test_MossTerrace" || item.name=="Test_CliffSkirt" || item.name=="Test_SpiritTerrace") continue;
    var scale=record["scale"]; var before=new UnityEngine.Vector3((float)scale[0],(float)scale[1],(float)scale[2]);
    if(UnityEngine.Vector3.Distance(before,item.lossyScale)>.0001f) resized.Add(item.name);
}
var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var player=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
var ground=world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>();
var floor=world.transform.Find("Test_MossTerrace").GetComponent<UnityEngine.Renderer>().bounds;
float reality=world.BoundaryZ-floor.min.z; float spirit=floor.max.z-world.BoundaryZ;
int beforeCount=world.GetComponentsInChildren<UnityEngine.Transform>(true).Length; var beforePosition=player.transform.position;
Test_CheonMapLayout.Apply();
var result=new {dimensions=new[]{ground.size.x,ground.size.z},visibleDimensions=new[]{floor.size.x,floor.size.z},minimumZ=floor.min.z,maximumZ=floor.max.z,boundaryZ=world.BoundaryZ,realityLength=reality,spiritLength=spirit,realityFraction=reality/(reality+spirit),preservedObjects=records.Count-missing.Count,missingOriginalObjects=missing.ToArray(),resizedOriginalProps=resized.ToArray(),duplicateObjects=world.GetComponentsInChildren<UnityEngine.Transform>(true).Length-beforeCount,playerStayed=player.transform.position==beforePosition};
System.IO.File.WriteAllText("Assets/Temp/Test_CheonRealmLayoutAudit.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return result;
