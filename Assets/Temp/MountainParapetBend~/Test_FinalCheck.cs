var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Proto/MountainPass/Scenes/MountainPass.unity")throw new InvalidOperationException("프로토타입 편집 씬 확인 필요");
var records=GameObject.Find("ApproachStoneParapets").GetComponentsInChildren<MeshFilter>().Select(f=>{
var mesh=f.sharedMesh;var vertices=mesh.vertices;var rings=Enumerable.Range(0,vertices.Length/8).Select(i=>Enumerable.Range(0,8).Aggregate(Vector3.zero,(s,k)=>s+vertices[i*8+k])/8).ToArray();
float maximum=0;int start=f.name.Contains("Left")?58:46;int end=f.name.Contains("Left")?74:62;
for(int i=start*4;i<end*4;i+=4){var a=rings[i]-rings[i-4];var b=rings[i+4]-rings[i];a.y=0;b.y=0;maximum=Mathf.Max(maximum,Vector3.Angle(a,b));}
var asset=AssetDatabase.GetAssetPath(mesh);return new{name=f.name,maxTurnPerStep=maximum,colliderMatches=f.GetComponent<MeshCollider>().sharedMesh==mesh,metaPreserved=System.IO.File.ReadAllText(asset+".meta")==System.IO.File.ReadAllText("Assets/Temp/MountainParapetBend~/Test_Before_"+f.name+".asset.meta")};
}).ToArray();
AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
var report=new{scene=scene.path,saved=!scene.isDirty,rails=records};System.IO.File.WriteAllText("Assets/Temp/MountainParapetBend~/Test_FinalReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
