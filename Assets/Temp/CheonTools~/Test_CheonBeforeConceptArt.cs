if(UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode before art editing");
var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if(scene.path!=Test_CheonSceneBuilder.ScenePath)throw new System.InvalidOperationException("Wrong scene");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.CopyAsset(scene.path,"Assets/Temp/Test_CheonBeforeConceptArt.unity");
var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var records=new System.Collections.Generic.List<object>();
var counts=new System.Collections.Generic.Dictionary<string,int>();
foreach(var item in world.GetComponentsInChildren<UnityEngine.Transform>(true))
{
    var mesh=item.GetComponent<UnityEngine.MeshFilter>();
    var col=item.GetComponent<UnityEngine.BoxCollider>();
    records.Add(new{id=item.GetInstanceID(),name=item.name,position=new[]{item.localPosition.x,item.localPosition.y,item.localPosition.z},rotation=new[]{item.localRotation.x,item.localRotation.y,item.localRotation.z,item.localRotation.w},scale=new[]{item.localScale.x,item.localScale.y,item.localScale.z},boxSize=col==null?null:new[]{col.size.x,col.size.y,col.size.z},meshBounds=mesh==null || mesh.sharedMesh==null?null:new[]{mesh.sharedMesh.bounds.min.x,mesh.sharedMesh.bounds.min.y,mesh.sharedMesh.bounds.min.z,mesh.sharedMesh.bounds.max.x,mesh.sharedMesh.bounds.max.y,mesh.sharedMesh.bounds.max.z}});
    if(mesh!=null){string key=item.name+" / "+mesh.sharedMesh.name;counts[key]=counts.TryGetValue(key,out int count)?count+1:1;}
}
System.IO.File.WriteAllText("Assets/Temp/Test_CheonBeforeConceptArt.json",Newtonsoft.Json.JsonConvert.SerializeObject(records));
return new{objects=records.Count,meshCounts=counts};
