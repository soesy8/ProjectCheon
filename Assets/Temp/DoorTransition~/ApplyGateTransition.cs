var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Test/CheonMountainMap/Scenes/Test_CheonMountainMap.unity")
    throw new System.InvalidOperationException("적용 대상 씬이 변경되었습니다.");
if (Application.isPlaying) throw new System.InvalidOperationException("Edit Mode에서 적용하세요.");
var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
var gate = transforms.First(t => t.name == "Gate_Threshold_1");
var camera = transforms.Select(t => t.GetComponent<Camera>()).First(c => c != null);
var sun = transforms.Select(t => t.GetComponent<Light>()).First(l => l != null && l.type == LightType.Directional);
var effectObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Gate Environment Transition");
if (effectObject == null)
{
    effectObject = new GameObject("Gate Environment Transition");
    Undo.RegisterCreatedObjectUndo(effectObject, "문 통과 환경 연출 추가");
}
Undo.RecordObject(effectObject.transform, "문 통과 판정 배치");
effectObject.transform.SetPositionAndRotation(gate.position + Vector3.up * 4f,
    Quaternion.LookRotation(Vector3.ProjectOnPlane(gate.up, Vector3.up), Vector3.up));
var effect = effectObject.GetComponent<Cheon.World.GateEnvironmentTransition>();
if (effect == null) effect = Undo.AddComponent<Cheon.World.GateEnvironmentTransition>(effectObject);
var settings = new SerializedObject(effect);
settings.FindProperty("worldCamera").objectReferenceValue = camera;
settings.FindProperty("sun").objectReferenceValue = sun;
settings.FindProperty("passageSize").vector2Value = new Vector2(6f, 9f);
settings.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
Selection.activeGameObject = effectObject;
return new { scene = scene.path, effectObject.name, position = effectObject.transform.position.ToString(), forward = effectObject.transform.forward.ToString(), camera = camera.name, sun = sun.name, script = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(effect)) };
