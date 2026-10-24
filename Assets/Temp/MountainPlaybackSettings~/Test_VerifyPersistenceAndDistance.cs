if (!Application.isPlaying) throw new Exception("마운틴 씬을 다시 재생해 주세요.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var settings = control.SavedPlaybackSettings;
string baseline = System.IO.File.ReadAllText("Assets/Temp/MountainPlaybackSettings~/Test_OriginalPlaybackSettings.json");
string expected = System.IO.File.ReadAllText("Assets/Temp/MountainPlaybackSettings~/Test_ExpectedAfterReentry.json");
var checks = new List<string>();
var distances = new List<float>();
void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); checks.Add(reason); }
var actual = ScriptableObject.CreateInstance<Cheon.Testing.PlayerFan.Test_PlayerPlaybackSettings>();
control.CopyPlaybackTo(actual);
Check(JsonUtility.ToJson(actual) == expected, "다시 Play한 뒤 저장한 속도·시작·종료 프레임 6개 모두 복원");
UnityEngine.Object.Destroy(actual);
Check(Mathf.Approximately(new SerializedObject(movement).FindProperty("dodgeDistance").floatValue, 7.5f), "구르기 거리 3m에서 7.5m로 변경");
var body = movement.GetComponent<CharacterController>();
var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
ground.name = "Test_TemporaryDodgeDistanceGround";
ground.hideFlags = HideFlags.DontSave;
ground.transform.position = new Vector3(0f, 150f, 0f);
ground.transform.localScale = Vector3.one * 10f;
var oldKeyboard = UnityEngine.InputSystem.Keyboard.current;
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_DodgeDistanceKeyboard");
var background = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.Key[] held = Array.Empty<UnityEngine.InputSystem.Key>();
Action feed = () => { keyboard.MakeCurrent(); UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(held)); };
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
UnityEngine.InputSystem.InputSystem.onBeforeUpdate += feed;
int phase = 0, lastFrame = -1, phaseFrame = Time.frameCount;
double began = EditorApplication.timeSinceStartup;
Vector3 start = Vector3.zero;
EditorApplication.CallbackFunction tick = null;
void PlaceOnGround()
{
    movement.ResetToSpawn();
    body.enabled = false;
    movement.transform.SetPositionAndRotation(new Vector3(0f, 150.05f, 0f), Quaternion.identity);
    body.enabled = true;
    Physics.SyncTransforms();
}
void RestoreSettings()
{
    JsonUtility.FromJsonOverwrite(baseline, settings);
    EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
    control.ReloadSavedPlaybackSettings();
    GameObject.Find("Test_ReloadPlayback").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
}
void Next(int value) { phase = value; phaseFrame = Time.frameCount; }
void Finish(bool success, string reason)
{
    EditorApplication.update -= tick;
    UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    if (oldKeyboard != null) oldKeyboard.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = background;
    RestoreSettings(); movement.ResetToSpawn();
    UnityEngine.Object.Destroy(ground);
    System.IO.File.WriteAllText("Assets/Temp/MountainPlaybackSettings~/Test_PersistenceAndDistanceReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { success, reason, checks, distances, elapsed = EditorApplication.timeSinceStartup - began }, Newtonsoft.Json.Formatting.Indented));
}
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false, "검증 중 재생 종료"); return; }
        if (EditorApplication.timeSinceStartup - began > 20d) { Finish(false, "Timeout at phase " + phase); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (phase == 0 || phase == 3)
        {
            if (Time.frameCount < phaseFrame + 6) return;
            start = movement.transform.position;
            held = new[] { UnityEngine.InputSystem.Key.Space };
            Next(phase == 0 ? 1 : 4);
        }
        else if (phase == 1 || phase == 4)
        {
            if (!control.IsDodging) return;
            held = Array.Empty<UnityEngine.InputSystem.Key>(); Next(phase == 1 ? 2 : 5);
        }
        else if (phase == 2 || phase == 5)
        {
            if (control.IsDodging || control.CharacterAnimator.IsInTransition(0)) return;
            float distance = Vector3.ProjectOnPlane(movement.transform.position - start, Vector3.up).magnitude;
            distances.Add(distance);
            Check(Mathf.Abs(distance - 7.5f) < 0.2f, (phase == 2 ? "변경한 속도·구간" : "기존 속도·전체 구간") + "에서 실제 회피 거리 7.5m 유지: " + distance.ToString("0.000"));
            if (phase == 2) { RestoreSettings(); PlaceOnGround(); Next(3); }
            else Finish(true, "실행 간 저장 유지 및 2.5배 회피 거리 검증 통과");
        }
    }
    catch (Exception error) { Finish(false, error.ToString()); }
};
UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
PlaceOnGround();
EditorApplication.update += tick;
return new { started = true };
