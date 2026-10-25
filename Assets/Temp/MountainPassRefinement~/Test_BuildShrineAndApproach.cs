var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(EditorApplication.isPlaying||scene.path!="Assets/Proto/MountainPass/Scenes/MountainPass.unity")throw new System.InvalidOperationException("프로토타입 편집 씬을 확인하세요.");
var shrineRoot=GameObject.Find("MountainPassEnvironment/Shrine_Building_Placement/BuildingBase_Parts");
foreach(string name in new[]{"Stairs","Foundation","Floor","Pillars","Walls","DoorFrame","Doors_Left","Doors_Right","Altar"}){
 var item=shrineRoot.transform.Find(name).gameObject;var collider=item.GetComponent<MeshCollider>();if(collider==null)collider=item.AddComponent<MeshCollider>();collider.sharedMesh=item.GetComponent<MeshFilter>().sharedMesh;collider.convex=false;
}
foreach(var name in new[]{"SpiritLanterns","GuardianArenaBoundary","ShrineEmberCircle"}){var item=GameObject.Find(name);if(item!=null)UnityEngine.Object.DestroyImmediate(item);}
var systems=GameObject.Find("MountainPassSystems");var game=systems.GetComponent<Cheon.Prototype.MountainPass.PassGameFlow>();
var player=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassPlayerMovement>();
var shrineObject=new GameObject("MountainShrineInteraction");
var sanctuary=shrineObject.AddComponent<Cheon.Prototype.MountainPass.PassShrineInteraction>();
var prayer=new GameObject("ShrinePrayerPoint");prayer.transform.SetParent(shrineObject.transform);prayer.transform.position=new Vector3(22.7f,28.69f,18.8f);
var respawn=new GameObject("ShrineRespawnPoint");respawn.transform.SetParent(shrineObject.transform);respawn.transform.SetPositionAndRotation(new Vector3(21.3f,28.72f,19.95f),Quaternion.LookRotation(new Vector3(-1f,0f,1f)));
sanctuary.Configure(game,prayer.transform,respawn.transform);
var axis=new Vector3(1f,0f,-1f).normalized;var right=Vector3.Cross(Vector3.up,axis);
var start=new Vector3(19.9701f,27.4775f,21.3232f)-axis*1.65f;start.y=27.15f;
var end=new Vector3(23.602f,28.68f,17.7017f)-axis*2.55f;
var vertices=new[]{start-right*1.4f,start+right*1.4f,end-right*1.4f,end+right*1.4f,start-right*1.4f-Vector3.up*0.3f,start+right*1.4f-Vector3.up*0.3f,end-right*1.4f-Vector3.up*0.3f,end+right*1.4f-Vector3.up*0.3f};
var ramp=new Mesh{name="ShrineStairWalkway"};ramp.vertices=vertices;ramp.triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};ramp.RecalculateNormals();ramp.RecalculateBounds();
AssetDatabase.CreateAsset(ramp,"Assets/Proto/MountainPass/Environment/ShrineStairWalkway.asset");
var walkway=new GameObject("ShrineStairWalkwayCollider");walkway.AddComponent<MeshCollider>().sharedMesh=ramp;
var stairData=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));
var centers=new Vector3[stairData.Count];var rights=new Vector3[stairData.Count];var lefts=new float[stairData.Count];var widths=new float[stairData.Count];
for(int i=0;i<stairData.Count;i++){var d=stairData[i];centers[i]=new Vector3((float)d["center"][0],(float)d["center"][1],(float)d["center"][2]);rights[i]=new Vector3((float)d["right"][0],0f,(float)d["right"][2]);lefts[i]=(float)d["left"];widths[i]=(float)d["rightWidth"];}
var leftPoints=new Vector3[centers.Length];var rightPoints=new Vector3[centers.Length];
for(int i=0;i<centers.Length;i++){
 float left=0,rightWidth=0;int count=0;
 for(int j=Mathf.Max(0,i-1);j<=Mathf.Min(centers.Length-1,i+1);j++){left+=lefts[j];rightWidth+=widths[j];count++;}
 leftPoints[i]=centers[i]+rights[i]*(left/count-0.08f);rightPoints[i]=centers[i]+rights[i]*(rightWidth/count+0.08f);
}
var safety=new GameObject("ApproachSafetyWalls");var parapets=new GameObject("ApproachStoneParapets");var lanterns=new GameObject("ApproachLanterns");
var stone=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Proto/MountainPass/Materials/M_EmberStone.mat")){name="M_StairParapet"};stone.SetColor("_BaseColor",new Color(0.21f,0.24f,0.25f));
AssetDatabase.CreateAsset(stone,"Assets/Proto/MountainPass/Materials/M_StairParapet.mat");
var warm=AssetDatabase.LoadAssetAtPath<Material>("Assets/Proto/MountainPass/Materials/M_GoldenEmber.mat");
var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var cubeMesh=cube.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(cube);
int barriers=0;
for(int side=0;side<2;side++){
 var points=side==0?leftPoints:rightPoints;var combined=new System.Collections.Generic.List<CombineInstance>();
 for(int i=0;i<points.Length-1;i++){
  Vector3 a=points[i],b=points[i+1],difference=b-a;Vector3 horizontal=difference;horizontal.y=0;
  if(horizontal.sqrMagnitude<0.001f)continue;
  combined.Add(new CombineInstance{mesh=cubeMesh,transform=Matrix4x4.TRS((a+b)*0.5f+Vector3.up*0.35f,Quaternion.LookRotation(difference,Vector3.up),new Vector3(0.36f,0.7f,difference.magnitude+0.16f))});
  var wall=new GameObject((side==0?"LeftSafety_":"RightSafety_")+i.ToString("00"));wall.transform.SetParent(safety.transform);wall.layer=2;
  wall.transform.SetPositionAndRotation((a+b)*0.5f+Vector3.up*1.65f,Quaternion.LookRotation(horizontal));
  wall.AddComponent<BoxCollider>().size=new Vector3(0.42f,3.8f+Mathf.Abs(difference.y),horizontal.magnitude+0.28f);barriers++;
 }
 var mesh=new Mesh{name=side==0?"ApproachLeftParapet":"ApproachRightParapet"};mesh.CombineMeshes(combined.ToArray(),true,true);mesh.RecalculateBounds();
 AssetDatabase.CreateAsset(mesh,"Assets/Proto/MountainPass/Environment/"+mesh.name+".asset");
 var model=new GameObject(mesh.name);model.transform.SetParent(parapets.transform);model.AddComponent<MeshFilter>().sharedMesh=mesh;model.AddComponent<MeshRenderer>().sharedMaterial=stone;
}
var lights=new System.Collections.Generic.List<Light>();
for(int step=2;step<centers.Length;step+=9)for(int side=0;side<2;side++){
 var edge=(side==0?leftPoints:rightPoints)[step];var root=new GameObject((side==0?"LeftLantern_":"RightLantern_")+step.ToString("00"));root.transform.SetParent(lanterns.transform);root.transform.position=edge+Vector3.up*0.72f;
 var types=new[]{PrimitiveType.Cube,PrimitiveType.Cylinder,PrimitiveType.Sphere,PrimitiveType.Cube};
 var offsets=new[]{new Vector3(0,.08f,0),new Vector3(0,.3f,0),new Vector3(0,.65f,0),new Vector3(0,.97f,0)};
 var scales=new[]{new Vector3(.52f,.16f,.52f),new Vector3(.16f,.18f,.16f),new Vector3(.3f,.42f,.3f),new Vector3(.6f,.12f,.6f)};
 for(int part=0;part<types.Length;part++){var item=GameObject.CreatePrimitive(types[part]);item.name=part==2?"LanternGlow":"LanternStone";item.transform.SetParent(root.transform,false);item.transform.localPosition=offsets[part];item.transform.localScale=scales[part];item.GetComponent<Renderer>().sharedMaterial=part==2?warm:stone;UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());}
 var lightObject=new GameObject("WarmLight");lightObject.transform.SetParent(root.transform,false);lightObject.transform.localPosition=Vector3.up*.65f;
 var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1f,.58f,.22f);light.intensity=2.6f;light.range=6f;light.shadows=LightShadows.None;lights.Add(light);
}
var atmosphere=lanterns.AddComponent<Cheon.Prototype.MountainPass.PassStairAtmosphere>();
atmosphere.Configure(player.transform,UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassWorldTransition>(),lights.ToArray(),centers[0].y,centers.Last().y);
game.ConfigureRefinement(sanctuary,null);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{shrineColliders=shrineRoot.GetComponentsInChildren<Collider>().Length,safetyBarriers=barriers,lanterns=lights.Count,walkwayStart=start.ToString(),walkwayEnd=end.ToString(),prayer=prayer.transform.position.ToString()};
