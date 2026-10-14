var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying || scene.path!=Test_CheonSceneBuilder.ScenePath) throw new System.InvalidOperationException("테스트 씬의 편집 상태에서 실행하세요.");
var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var records=world.GetComponentsInChildren<UnityEngine.Transform>(true).Select(t=>new {id=t.GetInstanceID(),name=t.name,scale=new[]{t.lossyScale.x,t.lossyScale.y,t.lossyScale.z}}).ToArray();
System.IO.File.WriteAllText("Assets/Temp/Test_CheonBeforeRealmLayout.json",Newtonsoft.Json.JsonConvert.SerializeObject(records,Newtonsoft.Json.Formatting.Indented));
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,"Assets/Temp/Test_CheonBeforeRealmLayout.unity",true);
return new {scene=scene.path,objects=records.Length,dirty=scene.isDirty,ground=world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>().size.ToString(),gate=world.transform.Find("Test_경계문").position.ToString()};
