if (Application.isPlaying) throw new Exception("편집 모드에서 실행해야 합니다.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Test/CheonMountainMap/Scenes/Test_CheonMountainMap.unity") throw new Exception("대상 씬이 아닙니다.");
if (scene.isDirty) throw new Exception("저장되지 않은 씬 변경이 있습니다.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var settings = control.SavedPlaybackSettings;
if (settings == null) throw new Exception("재생 설정이 없습니다.");
Undo.RecordObjects(new UnityEngine.Object[] { movement, control, settings }, "걷기·달리기 속도 조정");
var serialized = new SerializedObject(movement);
serialized.FindProperty("moveSpeed").floatValue = 4.4f;
serialized.FindProperty("sprintSpeed").floatValue = 6.6f;
serialized.ApplyModifiedProperties();
// 동일한 클립의 보행 속도 비율은 유지하면서 접지 구간의 미끄러짐을 줄인다.
settings.run2Speed = 1.6f;
control.run2Speed = settings.run2Speed;
EditorUtility.SetDirty(settings);
AssetDatabase.SaveAssetIfDirty(settings);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new Exception("씬 저장 실패");
return new { moveSpeed = 4.4f, sprintSpeed = 6.6f, runAnimationSpeed = settings.run2Speed,
    walkAnimationSpeed = settings.run2Speed * 4.4f / 6.6f, scene = scene.path, settings = AssetDatabase.GetAssetPath(settings) };
