var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying || scene.path != "Assets/Proto/Scenes/PC_Prototype.unity")
    throw new System.InvalidOperationException("Open the prototype scene in Edit Mode first.");

var camera = Camera.main;
var player = GameObject.Find("Player").transform;
UnityEditor.Undo.IncrementCurrentGroup();
int undoGroup = UnityEditor.Undo.GetCurrentGroup();
UnityEditor.Undo.SetCurrentGroupName("Configure player follow camera");
var followCamera = camera.GetComponent<PlayerFollowCamera>();
if (followCamera == null)
    followCamera = UnityEditor.Undo.AddComponent<PlayerFollowCamera>(camera.gameObject);

var settings = new UnityEditor.SerializedObject(followCamera);
settings.FindProperty("target").objectReferenceValue = player;
settings.FindProperty("followOffset").vector3Value = new Vector3(10f, 16f, -15f);
settings.FindProperty("followSmoothTime").floatValue = 0.12f;
settings.ApplyModifiedProperties();
UnityEditor.Undo.RecordObject(camera, "Set player camera distance");
UnityEditor.Undo.RecordObject(camera.transform, "Center camera on player");
camera.orthographic = true;
camera.orthographicSize = 10f;
followCamera.SnapToTarget();
UnityEditor.Undo.CollapseUndoOperations(undoGroup);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
return new { scene = scene.path, target = player.name, cameraPosition = camera.transform.position.ToString(), camera.orthographicSize, saved = !scene.isDirty };
