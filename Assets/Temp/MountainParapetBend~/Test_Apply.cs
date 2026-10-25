var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(EditorApplication.isPlaying||scene.path!="Assets/Proto/MountainPass/Scenes/MountainPass.unity")throw new InvalidOperationException("프로토타입 편집 씬 확인 필요");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
System.IO.File.Copy(scene.path,"Assets/Temp/MountainParapetBend~/Test_Before.unity",false);
var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainParapetBend~/Test_Curves.json"));
System.Func<Vector3[],float,Vector3> spline=(p,index)=>{int i=Mathf.Min(Mathf.FloorToInt(index),p.Length-2);float t=index-i;var a=i>0?p[i-1]:p[0]*2-p[1];var b=p[i];var c=p[i+1];var d=i+2<p.Length?p[i+2]:p[i+1]*2-p[i];return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);};
var profile=new[]{new Vector2(-.32f,-.14f),new Vector2(.32f,-.14f),new Vector2(.38f,-.03f),new Vector2(.38f,.51f),new Vector2(.28f,.67f),new Vector2(-.28f,.67f),new Vector2(-.38f,.51f),new Vector2(-.38f,-.03f)};
var ground=GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
var records=new System.Collections.Generic.List<object>();
foreach(var rail in data){
string name=(string)rail["name"];bool left=name.Contains("Left");
var path=rail["path"].Select(p=>new Vector3((float)p[0],(float)p[1],(float)p[2])).ToArray();
var old=rail["original"].Select(p=>new Vector3((float)p[0],(float)p[1],(float)p[2])).ToArray();
var item=GameObject.Find(name);var filter=item.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
var asset=AssetDatabase.GetAssetPath(mesh);var guid=AssetDatabase.AssetPathToGUID(asset);
System.IO.File.Copy(asset,"Assets/Temp/MountainParapetBend~/Test_Before_"+name+".asset",false);
System.IO.File.Copy(asset+".meta","Assets/Temp/MountainParapetBend~/Test_Before_"+name+".asset.meta",false);
Undo.RegisterCompleteObjectUndo(mesh,"계단 돌벽의 급한 굽이 완화");
var vertices=mesh.vertices;int editedRings=0;float maxShift=0;
for(int i=0;i<vertices.Length/8;i++){
float sample=i*.25f;var point=spline(path,sample);var previous=spline(old,sample);
float shift=Vector3.Distance(point,previous);if(shift<.0001f)continue;
editedRings++;maxShift=Mathf.Max(maxShift,shift);
var tangent=spline(path,Mathf.Min(path.Length-1,sample+.03f))-spline(path,Mathf.Max(0,sample-.03f));tangent.y=0;
var normal=Vector3.Cross(Vector3.up,tangent.normalized);
for(int k=0;k<8;k++){
var position=point+normal*profile[k].x+Vector3.up*profile[k].y;
// 옮긴 벽의 밑면만 지형까지 내려 틈이 뜨지 않게 한다.
if(k<2){RaycastHit hit;if(ground.Raycast(new Ray(point+normal*profile[k].x+Vector3.up,Vector3.down),out hit,4))position.y=Mathf.Min(position.y,hit.point.y-.06f);}
vertices[i*8+k]=filter.transform.InverseTransformPoint(position);
}}
mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
var collision=item.GetComponent<MeshCollider>();Undo.RecordObject(collision,"돌벽 콜라이더 갱신");collision.sharedMesh=null;collision.sharedMesh=mesh;
var movedLamps=new System.Collections.Generic.List<string>();
for(int step=2;step<path.Length;step+=9){if(Vector3.Distance(path[step],old[step])<.005f)continue;var lamp=GameObject.Find("ApproachLanterns").transform.Find((left?"LeftLantern_":"RightLantern_")+step.ToString("00"));Undo.RecordObject(lamp,"돌벽 곡선에 등불 정렬");lamp.position=path[step]+Vector3.up*.69f;movedLamps.Add(lamp.name);}
int walls=0;var safety=GameObject.Find("ApproachSafetyWalls").transform;
for(int i=0;i<path.Length-1;i++){
if(Vector3.Distance(path[i],old[i])<.005f&&Vector3.Distance(path[i+1],old[i+1])<.005f)continue;
var wall=safety.Find((left?"LeftSafety_":"RightSafety_")+i.ToString("00"));var box=wall.GetComponent<BoxCollider>();Undo.RecordObjects(new UnityEngine.Object[]{wall,box},"돌벽 곡선에 추락 방지벽 정렬");
var direction=path[i+1]-path[i];direction.y=0;var middle=(path[i]+path[i+1])*.5f;middle.y=wall.position.y;
wall.SetPositionAndRotation(middle,Quaternion.LookRotation(direction));var size=box.size;size.z=direction.magnitude+.32f;box.size=size;walls++;
}
records.Add(new{name,guid,editedRings,maxShift,movedLamps,walls});
}
AssetDatabase.SaveAssets();Physics.SyncTransforms();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
System.IO.File.WriteAllText("Assets/Temp/MountainParapetBend~/Test_Application.json",Newtonsoft.Json.JsonConvert.SerializeObject(records,Newtonsoft.Json.Formatting.Indented));
return records;
