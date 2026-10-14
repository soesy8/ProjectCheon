var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path != Test_CheonSceneBuilder.ScenePath || UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("테스트 씬의 편집 상태가 아닙니다.");
var rig=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonCamera>();
var camera=rig.GetComponent<UnityEngine.Camera>();
var player=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>();
var world=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
var objects=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<UnityEngine.Transform>(true)).Select(t=>t.gameObject).ToArray();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new {scene=scene.path,playing=UnityEditor.EditorApplication.isPlaying,cameraSize=camera.orthographicSize,player=player.transform.position.ToString(),ground=world.transform.Find("Test_WalkableGround").GetComponent<UnityEngine.BoxCollider>().size.ToString(),missingScripts=objects.Sum(UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount),objects=objects.Length};
