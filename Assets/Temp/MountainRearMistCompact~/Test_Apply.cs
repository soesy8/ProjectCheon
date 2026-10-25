var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Proto/MountainPass/Scenes/MountainPass.unity")throw new InvalidOperationException("프로토타입 편집 씬 확인 필요");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);System.IO.File.Copy(scene.path,"Assets/Temp/MountainRearMistCompact~/Test_Before.unity",false);
var fog=UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassMountainMist>().RearFog;var values=new SerializedObject(fog);
System.IO.File.WriteAllText("Assets/Temp/MountainRearMistCompact~/Test_BeforeSettings.json",JsonUtility.ToJson(fog,true));
var data=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainPassRefinement~/Test_StepGeometry.json"));var a=data[0]["center"];var b=data[1]["center"];
var start=new Vector3((float)a[0],(float)a[1],(float)a[2]);var backward=new Vector3((float)a[0]-(float)b[0],0,(float)a[2]-(float)b[2]).normalized;
Undo.RecordObjects(new UnityEngine.Object[]{fog,fog.transform},"계단 끝 안개를 작은 영역으로 축소");
// 계단 폭은 가리면서, 플레이어 머리 위로 높게 솟은 안개 벽은 남기지 않는다.
fog.transform.position=start+backward*1.25f+Vector3.up*2.2f;
values.FindProperty("areaSize").vector2Value=new Vector2(10,10);
values.FindProperty("volumeDepth").floatValue=10f;
values.FindProperty("heightFade").floatValue=2.2f;
values.FindProperty("edgeFade").floatValue=2f;
values.FindProperty("noiseScale").floatValue=.3f;
values.FindProperty("noiseStrength").floatValue=.55f;
values.ApplyModifiedProperties();var boundary=(Transform)values.FindProperty("densityBoundary").objectReferenceValue;boundary.SetPositionAndRotation(start+backward*.28f,Quaternion.LookRotation(backward));fog.ApplySettings();
AssetDatabase.ImportAsset("Assets/Proto/MountainPass/Shaders/S_LocalMountainFog.shader",ImportAssetOptions.ForceSynchronousImport);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{settings=JsonUtility.ToJson(fog),position=fog.transform.position.ToString(),shaderErrors=UnityEditor.ShaderUtil.GetShaderMessages(Shader.Find("Cheon/Prototype/LocalMountainFog")).Count(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)};

