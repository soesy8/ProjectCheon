if (!Application.isPlaying) throw new Exception("플레이 모드가 필요합니다.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var body = movement.GetComponent<CharacterController>();
var cameraControl = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerCamera>();
var view = Camera.main.transform;
var cameraPosition = view.position;
var cameraRotation = view.rotation;
bool cameraEnabled = cameraControl.enabled;
cameraControl.enabled = false;
view.rotation = Quaternion.identity;
float oldCapture = Time.captureDeltaTime;
Time.captureDeltaTime = 1f / 60f;
var oldKeyboard = UnityEngine.InputSystem.Keyboard.current;
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_LocomotionSpeedKeyboard");
var background = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
ground.name = "Test_TemporaryLocomotionGround";
ground.hideFlags = HideFlags.DontSave;
ground.transform.position = new Vector3(0, 150, 0);
ground.transform.localScale = Vector3.one * 30;
movement.ResetToSpawn();
body.enabled = false;
movement.transform.SetPositionAndRotation(new Vector3(0, 150.04f, 0), Quaternion.identity);
body.enabled = true;
Physics.SyncTransforms();
UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
var held = Array.Empty<UnityEngine.InputSystem.Key>();
Action feed = () => { keyboard.MakeCurrent(); UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(held)); };
UnityEngine.InputSystem.InputSystem.onBeforeUpdate += feed;
var cycleField = control.GetType().GetField("locomotionCycle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var names = new [] { "납도 걷기", "납도 달리기", "달리며 발도", "발도 달리기", "발도 걷기", "걸으며 납도" };
var reports = new List<object>();
int testCase = 0, frames = 0, lastFrame = -1;
float duration = 0, distance = 0, cycles = 0;
Vector3 previous = movement.transform.position;
float previousCycle = (float)cycleField.GetValue(control);
double began = EditorApplication.timeSinceStartup;
EditorApplication.CallbackFunction tick = null;
void SetKeys()
{
    bool sprint = testCase >= 1 && testCase <= 3;
    var keys = new List<UnityEngine.InputSystem.Key> { UnityEngine.InputSystem.Key.W };
    if (sprint) keys.Add(UnityEngine.InputSystem.Key.LeftShift);
    if ((testCase == 2 || testCase == 5) && frames == 0) keys.Add(UnityEngine.InputSystem.Key.F);
    held = keys.ToArray();
}
void Finish(bool success, string message)
{
    EditorApplication.update -= tick;
    UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    if (oldKeyboard != null) oldKeyboard.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = background;
    Time.captureDeltaTime = oldCapture;
    movement.ResetToSpawn();
    view.SetPositionAndRotation(cameraPosition, cameraRotation);
    cameraControl.enabled = cameraEnabled;
    UnityEngine.Object.Destroy(ground);
    System.IO.File.WriteAllText("Assets/Temp/LocomotionSpeedTuning~/Test_LiveLocomotionReport.json",
        Newtonsoft.Json.JsonConvert.SerializeObject(new { success, message, reports, elapsed = EditorApplication.timeSinceStartup - began }, Newtonsoft.Json.Formatting.Indented));
}
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false, "재생 종료"); return; }
        if (EditorApplication.timeSinceStartup - began > 45d) { Finish(false, "검증 시간 초과"); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        var position = movement.transform.position;
        float cycle = (float)cycleField.GetValue(control);
        if (frames >= 30)
        {
            duration += Time.deltaTime;
            distance += Vector3.ProjectOnPlane(position - previous, Vector3.up).magnitude;
            cycles += Mathf.Repeat(cycle - previousCycle, 1f);
        }
        previous = position;
        previousCycle = cycle;
        frames++;
        if (frames >= 90)
        {
            bool sprint = testCase >= 1 && testCase <= 3;
            float expectedSpeed = sprint ? 6.6f : 4.4f;
            float expectedCycles = (sprint ? 1.6f : 1.6f * 4.4f / 6.6f) / 0.7f;
            float actualSpeed = distance / duration;
            float actualCycles = cycles / duration;
            bool passed = Mathf.Abs(actualSpeed - expectedSpeed) < 0.03f && Mathf.Abs(actualCycles - expectedCycles) < 0.03f;
            reports.Add(new { mode = names[testCase], actualSpeed, expectedSpeed, actualCycles, expectedCycles, passed });
            if (!passed) { Finish(false, names[testCase] + " 속도 불일치"); return; }
            testCase++;
            if (testCase == names.Length) { Finish(true, "걷기·달리기·장비 전환 중 이동 및 재생 주기 확인 완료"); return; }
            frames = 0; duration = distance = cycles = 0;
        }
        SetKeys();
    }
    catch (Exception ex) { Finish(false, ex.ToString()); }
};
SetKeys();
EditorApplication.update += tick;
return new { started = true, cases = names };
