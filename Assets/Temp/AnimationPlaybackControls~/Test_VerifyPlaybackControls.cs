if (!Application.isPlaying) throw new Exception("Play Mode가 필요합니다.");
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
var animator = control.CharacterAnimator;
var checks = new List<string>();
var reportPath = "Assets/Temp/AnimationPlaybackControls~/Test_PlaybackControlsReport.json";
void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
void Input(string name, string value)
{
    var field = GameObject.Find("Test_Input_" + name).GetComponent<TMPro.TMP_InputField>();
    field.SetTextWithoutNotify(value);
    field.onEndEdit.Invoke(value);
}
Input("RollSpeed", "-1");
Require(Mathf.Approximately(control.rollSpeed, 0.05f), "구르기 속도 최솟값 제한 실패");
Input("RollStartFrame", "999");
Input("RollEndFrame", "0");
Require(control.rollStartFrame == 39 && control.rollEndFrame == 40, "구르기 프레임 범위 제한 실패");
Input("RollStartFrame", "6"); Input("RollEndFrame", "30"); Input("RollSpeed", "1.5");
Input("Run2StartFrame", "3"); Input("Run2EndFrame", "18"); Input("Run2Speed", "1.25");
Require(control.rollStartFrame == 6 && control.rollEndFrame == 30 && Mathf.Approximately(control.rollSpeed, 1.5f), "구르기 UI 입력 실패");
Require(control.run2StartFrame == 3 && control.run2EndFrame == 18 && Mathf.Approximately(control.run2Speed, 1.25f), "달리기2 UI 입력 실패");
checks.Add("Numeric controls applied speeds and frame ranges; invalid values were clamped");
var sliders = GameObject.Find("Test_MovementSettingsPanel").GetComponentsInChildren<UnityEngine.UI.Slider>();
sliders[1].value = 1.5f;
Require(Mathf.Approximately(control.run2Speed, 1.5f), "달리기2 슬라이더 연결 실패");
checks.Add("Run2 speed slider updated playback settings");
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
void Press(UnityEngine.InputSystem.Key key) => UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
void Release() => UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
int phase = 0, lastFrame = -1, loops = 0, phaseFrame = Time.frameCount;
float previousRunFrame = 0f, pauseTime = 0f, maximumRollFrame = 0f;
bool rollAdvanced = false;
double began = EditorApplication.timeSinceStartup, phaseBegan = began;
TMPro.TMP_InputField focusedInput = null;
EditorApplication.CallbackFunction tick = null;
void Next(int value) { phase = value; phaseFrame = Time.frameCount; phaseBegan = EditorApplication.timeSinceStartup; }
void Finish(bool success, string reason)
{
    EditorApplication.update -= tick;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    if (focusedInput != null) focusedInput.DeactivateInputField();
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    if (control.IsPaused) control.TogglePause();
    control.rollSpeed = control.run2Speed = 1f;
    control.SetPlaybackFrameRange(4, 0, 40); control.SetPlaybackFrameRange(5, 0, 21);
    control.PlayIdle();
    System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new { success, reason, checks, maximumRollFrame, loops, elapsed = EditorApplication.timeSinceStartup - began, frame = Time.frameCount }, Newtonsoft.Json.Formatting.Indented));
}
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false, "検証中に再生が終了しました。"); return; }
        if (EditorApplication.timeSinceStartup - began > 25d) { Finish(false, "Timeout at phase " + phase); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (phase == 0)
        {
            if (control.SelectedState != "ROLL") return;
            Release();
            Require(!control.IsPaused && control.NormalizedTime * 40f >= 5.8f, "Spaceが指定開始フレームから 구르기를 재생하지 않았습니다.");
            Require(Mathf.Approximately(animator.GetFloat("RollSpeed"), 1.5f), "구르기 속도 파라미터 실패");
            checks.Add("Space started Roll at the configured frame and speed without pausing");
            Next(1);
        }
        else if (phase == 1)
        {
            if (control.SelectedState == "ROLL")
            {
                rollAdvanced |= control.PlaybackProgress > 0.2f;
                maximumRollFrame = Mathf.Max(maximumRollFrame, control.NormalizedTime * 40f);
                return;
            }
            if (control.SelectedState != "SHEATHED_IDLE" || animator.IsInTransition(0)) return;
            Require(rollAdvanced && maximumRollFrame < 31f, "구르기가 지정 구간에서 복귀하지 않았습니다.");
            checks.Add("Trimmed Roll advanced and returned to idle before leaving its selected segment");
            Press(UnityEngine.InputSystem.Key.LeftCtrl); Next(2);
        }
        else if (phase == 2)
        {
            if (control.SelectedState != "RUN2") return;
            Release();
            Require(control.NormalizedTime * 21f >= 2.8f, "달리기2 시작 프레임 실패");
            previousRunFrame = control.NormalizedTime * 21f;
            checks.Add("Left Ctrl started Run2 at the configured start frame");
            Next(3);
        }
        else if (phase == 3)
        {
            Require(control.SelectedState == "RUN2", "달리기2 반복이 중단되었습니다.");
            float frame = control.NormalizedTime * 21f;
            Require(frame >= 2.8f && frame <= 19f, "달리기2가 지정 프레임 구간을 벗어났습니다: " + frame);
            if (previousRunFrame > 9f && frame < previousRunFrame - 3f) loops++;
            previousRunFrame = frame;
            if (loops < 2) return;
            Input("Run2Speed", "2"); Next(4);
        }
        else if (phase == 4)
        {
            if (Time.frameCount < phaseFrame + 2) return;
            Require(Mathf.Approximately(animator.GetFloat("Run2Speed"), 2f), "실시간 속도 변경 실패");
            checks.Add("Run2 repeated inside the selected frame range and accepted a live speed change");
            Press(UnityEngine.InputSystem.Key.P); Next(5);
        }
        else if (phase == 5)
        {
            if (!control.IsPaused) return;
            Release(); pauseTime = control.NormalizedTime; Next(6);
        }
        else if (phase == 6)
        {
            Require(Mathf.Abs(control.NormalizedTime - pauseTime) < 0.001f, "일시정지 중 클립이 진행되었습니다.");
            if (EditorApplication.timeSinceStartup - phaseBegan < 0.15d) return;
            checks.Add("P paused playback without conflicting with Space");
            Press(UnityEngine.InputSystem.Key.Space); Next(7);
        }
        else if (phase == 7)
        {
            if (control.SelectedState != "ROLL" || control.IsPaused) return;
            Release(); Press(UnityEngine.InputSystem.Key.RightCtrl); Next(8);
        }
        else if (phase == 8)
        {
            if (control.SelectedState != "RUN2") return;
            Release();
            checks.Add("Space resumed into Roll and Right Ctrl selected Run2");
            focusedInput = GameObject.Find("Test_Input_Run2Speed").GetComponent<TMPro.TMP_InputField>();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(focusedInput.gameObject);
            focusedInput.ActivateInputField(); Next(9);
        }
        else if (phase == 9)
        {
            if (!focusedInput.isFocused || Time.frameCount < phaseFrame + 2) return;
            Press(UnityEngine.InputSystem.Key.Space); Next(10);
        }
        else if (phase == 10)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            Require(control.SelectedState == "RUN2" && !control.IsPaused, "입력란 편집 중 단축키가 실행되었습니다.");
            Release(); focusedInput.DeactivateInputField(); UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            checks.Add("Focused numeric inputs suppressed animation shortcuts");
            control.SetPlaybackFrameRange(5, 0, 21); control.PlayRun2(); Next(11);
        }
        else if (phase == 11)
        {
            if (control.NormalizedTime < 1.5f) return;
            Require(control.PlaybackProgress >= 0f && control.PlaybackProgress <= 1f, "전체 구간 진행률 실패");
            checks.Add("Full-range Run2 preserved its native loop and wrapped progress");
            Finish(true, "All playback controls and keyboard checks passed");
        }
    }
    catch (Exception error) { Finish(false, error.ToString()); }
};
control.RepeatSelectedAnimation = false;
control.PlayIdle();
UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
Press(UnityEngine.InputSystem.Key.Space);
EditorApplication.update += tick;
return new { started = true, reportPath };
