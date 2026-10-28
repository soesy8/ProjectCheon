// Unity CLI eval_file용 임시 실행문입니다.
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("편집 모드에서 실행하세요.");
var gourd = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGourdDrinking>();
var properties = new UnityEditor.SerializedObject(gourd);
var motion = (UnityEngine.AnimationClip)properties.FindProperty("drinkingClip").objectReferenceValue;
var audio = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassAudio>();
var sound = (UnityEngine.AudioClip)new UnityEditor.SerializedObject(audio).FindProperty("drink").objectReferenceValue;
float start = properties.FindProperty("sippingStartProgress").floatValue;
float end = properties.FindProperty("sippingEndProgress").floatValue;
float motionSpeed = properties.FindProperty("drinkingSpeed").floatValue;
float previousSpeed = properties.FindProperty("sippingSpeed").floatValue;
float sourceSeconds = motion.length / Mathf.Max(0.1f, motionSpeed) * (end - start);
// 음원의 끝이 프레임 경계나 종료 페이드에 잘리지 않도록 짧은 여유를 둔다.
float contactSeconds = sound.length + 0.08f;
float sippingSpeed = Mathf.Clamp(sourceSeconds / contactSeconds, 0.1f, previousSpeed);
const string backup = "Assets/Temp/GourdHealingAudio~/BeforeSipExtension_Main.unity";
if (!System.IO.File.Exists(backup))
{
    System.IO.File.Copy(gourd.gameObject.scene.path, backup);
    System.IO.File.Copy(gourd.gameObject.scene.path + ".meta", backup + ".meta");
}
UnityEditor.Undo.RecordObject(gourd, "회복 음원에 맞춰 호리병 마시기 연장");
properties.FindProperty("sippingSpeed").floatValue = sippingSpeed;
properties.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gourd.gameObject.scene);
if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(gourd.gameObject.scene))
    throw new System.InvalidOperationException("마시기 속도를 씬에 저장하지 못했습니다.");
return new { scene = gourd.gameObject.scene.path, previousContactSeconds = sourceSeconds / previousSpeed,
    contactSeconds = sourceSeconds / sippingSpeed, soundSeconds = sound.length, previousSpeed, sippingSpeed,
    drawingAndStowingSpeed = properties.FindProperty("playbackSpeed").floatValue };
