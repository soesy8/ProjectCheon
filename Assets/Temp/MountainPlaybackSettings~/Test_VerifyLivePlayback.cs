if (!Application.isPlaying) throw new Exception("마운틴 씬을 재생해 주세요.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var animator = control.CharacterAnimator;
var settings = control.SavedPlaybackSettings;
string baseline = System.IO.File.ReadAllText("Assets/Temp/MountainPlaybackSettings~/Test_OriginalPlaybackSettings.json");
var checks = new List<string>();
var metrics = new Dictionary<string, float>();
void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); checks.Add(reason); }
void Input(string name, string value)
{
    var field = GameObject.Find("Test_Playback" + name).GetComponent<TMPro.TMP_InputField>();
    field.SetTextWithoutNotify(value); field.onEndEdit.Invoke(value);
}
void Click(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
Input("Speed_Roll", "-2"); Input("Start_Roll", "999"); Input("End_Roll", "0");
Check(Mathf.Approximately(control.rollSpeed, 0.05f) && control.rollStartFrame == 39 && control.rollEndFrame == 40, "잘못된 속도·프레임 입력 제한");
Input("Start_Roll", "8"); Input("End_Roll", "32"); Input("Speed_Roll", "1.5");
Input("Start_Run", "4"); Input("End_Run", "17"); Input("Speed_Run", "1.4");
Check(control.rollStartFrame == 8 && control.rollEndFrame == 32 && Mathf.Approximately(control.rollSpeed, 1.5f) &&
    control.run2StartFrame == 4 && control.run2EndFrame == 17 && Mathf.Approximately(control.run2Speed, 1.4f), "두 애니메이션의 숫자 입력 즉시 적용");
var slider = GameObject.Find("Test_PlaybackSlider_Roll").GetComponent<UnityEngine.UI.Slider>();
slider.value = 1.6f;
Check(Mathf.Approximately(control.rollSpeed, 1.6f), "구르기 속도 슬라이더 연결");
Input("Speed_Roll", "1.5"); Click("Test_SavePlayback");
Input("Speed_Run", "2.5"); Input("Start_Run", "0"); Input("End_Run", "21"); Click("Test_ReloadPlayback");
Check(Mathf.Approximately(control.run2Speed, 1.4f) && control.run2StartFrame == 4 && control.run2EndFrame == 17 &&
    Mathf.Approximately(settings.rollSpeed, 1.5f), "재생 수치 저장·불러오기");
control.PlayRoll(); animator.Update(0f);
Check(Mathf.Abs(control.NormalizedTime * 40f - 8f) < 0.05f, "속도 1.5배에서도 원본 시작 프레임 8F 정확히 유지");
control.PlayIdle(); animator.Play("SHEATHED_IDLE", 0, 0f); animator.Update(0f);
var oldKeyboard = UnityEngine.InputSystem.Keyboard.current;
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_PlaybackSettingsKeyboard");
var background = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.Key[] held = Array.Empty<UnityEngine.InputSystem.Key>();
Action feed = () => { keyboard.MakeCurrent(); UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(held)); };
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
UnityEngine.InputSystem.InputSystem.onBeforeUpdate += feed;
int phase = 0, lastFrame = -1, phaseFrame = Time.frameCount, loops = 0, liveLoops = 0;
float previousFrame = 0f, sampledTime = 0f, accumulatedFrames = 0f, rollMaximum = 0f;
double began = EditorApplication.timeSinceStartup;
var leg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
Quaternion previousLeg = leg.localRotation;
float boneMotion = 0f;
TMPro.TMP_InputField focused = null;
EditorApplication.CallbackFunction tick = null;
void Next(int value) { phase = value; phaseFrame = Time.frameCount; }
void Finish(bool success, string reason)
{
    EditorApplication.update -= tick;
    UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    if (oldKeyboard != null) oldKeyboard.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = background;
    if (focused != null) focused.DeactivateInputField();
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    if (!success) { JsonUtility.FromJsonOverwrite(baseline, settings); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings); }
    control.ReloadSavedPlaybackSettings(); movement.ResetToSpawn();
    if (success) System.IO.File.WriteAllText("Assets/Temp/MountainPlaybackSettings~/Test_ExpectedAfterReentry.json", JsonUtility.ToJson(settings));
    System.IO.File.WriteAllText("Assets/Temp/MountainPlaybackSettings~/Test_LivePlaybackReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { success, reason, checks, metrics, loops, liveLoops, elapsed = EditorApplication.timeSinceStartup - began }, Newtonsoft.Json.Formatting.Indented));
}
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false, "검증 중 재생 종료"); return; }
        if (EditorApplication.timeSinceStartup - began > 25d) { Finish(false, "Timeout at phase " + phase); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        float frame = animator.GetFloat("LocomotionTime") * 21f;
        if (phase == 0)
        {
            if (Time.frameCount < phaseFrame + 6 || !movement.IsMoving) return;
            Check(frame >= 3.99f && frame <= 17.01f, "달리기 지정 구간에서 평가 시작");
            previousFrame = frame; sampledTime = Time.time; Next(1);
        }
        else if (phase == 1)
        {
            if (frame < 3.99f || frame > 17.01f) throw new Exception("달리기 프레임 이탈: " + frame);
            float step = frame - previousFrame;
            if (step < -2f) { loops++; step += 13f; }
            accumulatedFrames += Mathf.Max(0f, step);
            previousFrame = frame;
            boneMotion += Quaternion.Angle(previousLeg, leg.localRotation); previousLeg = leg.localRotation;
            if (loops < 2) return;
            float speed = accumulatedFrames / (Time.time - sampledTime) / 30f;
            metrics["observedRunSpeed"] = speed; metrics["legMotionDegrees"] = boneMotion;
            Check(Mathf.Abs(speed - 1.4f) < 0.12f && boneMotion > 10f, "지정 구간 2회 반복과 실제 재생 속도·본 움직임");
            Input("Start_Run", "8"); Input("End_Run", "12"); Input("Speed_Run", "0.8"); Next(2);
        }
        else if (phase == 2)
        {
            if (Time.frameCount < phaseFrame + 5) return;
            if (frame < 7.99f || frame > 12.01f) throw new Exception("실시간 변경 구간 이탈: " + frame);
            previousFrame = frame; Next(3);
        }
        else if (phase == 3)
        {
            if (frame < 7.99f || frame > 12.01f) throw new Exception("변경된 달리기 프레임 이탈: " + frame);
            if (previousFrame - frame > 1f) liveLoops++;
            previousFrame = frame;
            if (liveLoops < 2) return;
            Check(Mathf.Approximately(control.run2Speed, 0.8f), "이동 중 속도·시작·종료 프레임 변경 반영");
            Click("Test_ReloadPlayback");
            held = new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift, UnityEngine.InputSystem.Key.F }; Next(4);
        }
        else if (phase == 4)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            Check(control.SelectedState == "DRAW" && movement.IsMoving, "달리기 구간 제어 중 발도·하체 이동 유지");
            held = new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift }; Next(5);
        }
        else if (phase == 5)
        {
            if (control.SelectedState != "DRAWN_IDLE" || animator.IsInTransition(0)) return;
            held = Array.Empty<UnityEngine.InputSystem.Key>(); movement.ResetToSpawn(); Next(6);
        }
        else if (phase == 6)
        {
            if (Time.frameCount < phaseFrame + 5) return;
            held = new[] { UnityEngine.InputSystem.Key.Space }; Next(7);
        }
        else if (phase == 7)
        {
            if (!control.IsDodging) return;
            Check(control.NormalizedTime * 40f >= 7.9f && Mathf.Approximately(animator.GetFloat("RollSpeed"), 1.5f), "구르기 지정 시작 프레임과 속도 적용");
            held = Array.Empty<UnityEngine.InputSystem.Key>(); Next(8);
        }
        else if (phase == 8)
        {
            if (control.IsDodging) { rollMaximum = Mathf.Max(rollMaximum, control.NormalizedTime * 40f); return; }
            if (animator.IsInTransition(0)) return;
            metrics["lastRollFrame"] = rollMaximum;
            Check(rollMaximum > 20f && rollMaximum < 33f, "구르기 지정 종료 프레임에서 대기 복귀");
            focused = GameObject.Find("Test_PlaybackStart_Run").GetComponent<TMPro.TMP_InputField>();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(focused.gameObject); focused.ActivateInputField(); Next(9);
        }
        else if (phase == 9)
        {
            if (!focused.isFocused || Time.frameCount < phaseFrame + 3) return;
            held = new[] { UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift }; Next(10);
        }
        else if (phase == 10)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            Check(!control.IsDodging && !movement.IsMoving, "재생 수치 입력 중 회피·이동 단축키 차단");
            held = Array.Empty<UnityEngine.InputSystem.Key>();
            Finish(true, "실시간 재생 수치 조절·저장 검증 통과");
        }
    }
    catch (Exception error) { Finish(false, error.ToString()); }
};
movement.ResetToSpawn();
UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
held = new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift };
EditorApplication.update += tick;
return new { started = true };
