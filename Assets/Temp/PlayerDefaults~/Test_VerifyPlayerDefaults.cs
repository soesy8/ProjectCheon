if (Application.isPlaying) throw new Exception("편집 모드에서 확인하세요.");
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
var beforeTimings = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Temp/PlayerDefaults~/Test_SavedTimingsBefore.json"));
var beforePlayback = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Temp/PlayerDefaults~/Test_SavedPlaybackBefore.json"));
var beforeControl = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Temp/PlayerDefaults~/Test_ControllerBefore.json"));
var checks = new List<string>();
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks.Add(label);
}
Check(Newtonsoft.Json.Linq.JToken.DeepEquals(beforeTimings, Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(control.SavedTimings))), "저장된 캔슬 수치 보존");
Check(Newtonsoft.Json.Linq.JToken.DeepEquals(beforePlayback, Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(control.SavedPlaybackSettings))), "저장된 재생 수치 보존");
var freshTimings = ScriptableObject.CreateInstance<Cheon.Testing.PlayerFan.Test_PlayerActionTimings>();
var freshPlayback = ScriptableObject.CreateInstance<Cheon.Testing.PlayerFan.Test_PlayerPlaybackSettings>();
var temporary = new GameObject("Test_TemporaryDefaultValues");
temporary.hideFlags = HideFlags.HideAndDontSave;
temporary.SetActive(false);
try
{
    var freshControl = temporary.AddComponent<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
    Check(Newtonsoft.Json.Linq.JToken.DeepEquals(beforeTimings, Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(freshTimings))), "새 타이밍 에셋 기본값과 저장값 일치");
    Check(Newtonsoft.Json.Linq.JToken.DeepEquals(beforePlayback, Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(freshPlayback))), "새 재생 에셋 기본값과 저장값 일치");
    var freshValues = new SerializedObject(freshControl);
    var currentValues = new SerializedObject(control);
    foreach (string name in new[] { "attack1Speed", "attack2Speed", "attack3Speed", "attack1StartFrame", "attack1EndFrame", "attack2StartFrame", "attack2EndFrame", "attack3StartFrame", "attack3EndFrame" })
    {
        var value = freshValues.FindProperty(name);
        double fresh = value.propertyType == SerializedPropertyType.Float ? value.floatValue : value.intValue;
        Check(Math.Abs(fresh - (double)beforeControl[name]) < 0.000001, "새 컴포넌트 " + name + " 기본값 일치");
        value = currentValues.FindProperty(name);
        double current = value.propertyType == SerializedPropertyType.Float ? value.floatValue : value.intValue;
        Check(Math.Abs(current - (double)beforeControl[name]) < 0.000001, "현재 " + name + " 보존");
    }
    foreach (var property in beforePlayback.Properties())
    {
        var value = freshValues.FindProperty(property.Name);
        double fresh = value.propertyType == SerializedPropertyType.Float ? value.floatValue : value.intValue;
        Check(Math.Abs(fresh - (double)property.Value) < 0.000001, "새 컴포넌트 " + property.Name + " 기본값 일치");
    }
}
finally
{
    UnityEngine.Object.DestroyImmediate(temporary);
    UnityEngine.Object.DestroyImmediate(freshTimings);
    UnityEngine.Object.DestroyImmediate(freshPlayback);
}
Undo.RecordObject(control, "Use Saved Playback Values As Defaults");
control.ReloadSavedPlaybackSettings();
EditorUtility.SetDirty(control);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(control.gameObject.scene);
if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(control.gameObject.scene)) throw new Exception("씬 저장 실패");
var result = new { success = true, checks, saved = !control.gameObject.scene.isDirty, roll = new { control.rollSpeed, control.rollStartFrame, control.rollEndFrame }, run = new { control.run2Speed, control.run2StartFrame, control.run2EndFrame } };
System.IO.File.WriteAllText("Assets/Temp/PlayerDefaults~/Test_Verification.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
return Newtonsoft.Json.JsonConvert.SerializeObject(result);
