if (Application.isPlaying) throw new Exception("편집 모드에서 실행해야 합니다.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
// 재생 설정을 불러오는 과정에서 바뀐 씬의 구르기 원본값은 작업 전 상태로 보존한다.
control.rollEndFrame = 40;
EditorUtility.SetDirty(control);
var scene = movement.gameObject.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
var serialized = new SerializedObject(movement);
var settings = control.SavedPlaybackSettings;
bool passed = Mathf.Approximately(serialized.FindProperty("moveSpeed").floatValue, 4.4f) &&
    Mathf.Approximately(serialized.FindProperty("sprintSpeed").floatValue, 6.6f) &&
    Mathf.Approximately(control.run2Speed, 1.6f) && Mathf.Approximately(settings.run2Speed, 1.6f);
if (!passed) throw new Exception("저장된 속도 설정 불일치");
return new { saved = passed, scene = scene.path, dirty = scene.isDirty,
    moveSpeed = serialized.FindProperty("moveSpeed").floatValue,
    sprintSpeed = serialized.FindProperty("sprintSpeed").floatValue,
    animationSpeed = settings.run2Speed, rollSpeed = settings.rollSpeed,
    rollEndFrame = settings.rollEndFrame };
