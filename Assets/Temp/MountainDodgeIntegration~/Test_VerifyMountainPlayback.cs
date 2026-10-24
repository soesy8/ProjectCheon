if (!Application.isPlaying) throw new Exception("마운틴 씬을 재생해 주세요.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var animator = control.CharacterAnimator;
var profile = control.RuntimeTimings;
var saved = control.SavedTimings;
var savedBaseline = JsonUtility.ToJson(saved);
var runtimeBaseline = JsonUtility.ToJson(profile);
var checks = new List<string>();
var metrics = new Dictionary<string, float>();
const string report = "Assets/Temp/MountainDodgeIntegration~/Test_MountainPlaybackReport.json";
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks.Add(message); }
void Input(int index, string value)
{
    var field = GameObject.Find("Test_TimingInput_" + index).GetComponent<TMPro.TMP_InputField>();
    field.SetTextWithoutNotify(value); field.onEndEdit.Invoke(value);
}
void Click(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
float Distance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
void SnapAttack(float progress)
{
    float frame = Mathf.Lerp(control.GetAttackStartFrame(1), control.GetAttackEndFrame(1), progress);
    animator.Play("ATTACK1", 0, frame / control.GetAttackMaximumFrame(1));
    animator.Update(0f); animator.speed = 0f;
}
Check(Mathf.Approximately(control.rollSpeed, 1.25f) && control.rollStartFrame == 0 && control.rollEndFrame == 40 &&
    Mathf.Approximately(control.run2Speed, 1f) && control.run2StartFrame == 0 && control.run2EndFrame == 21, "현재 구르기·달리기 수치 적용");
Check(Mathf.Approximately(control.attack1Speed, 1.8f) && Mathf.Approximately(control.attack2Speed, 1.3f) && Mathf.Approximately(control.attack3Speed, 1.5f), "공격 속도 유지");
Check(profile.Get("ROLL") != null && control.UsesUpperBodyEquipment, "회피 타이밍 및 상체 장착 레이어 연결");
Input(0, "85"); Input(1, "25");
Check(Mathf.Approximately(profile.Get("ATTACK1").moveCancel, 0.85f) && Mathf.Approximately(profile.Get("ATTACK1").dodgeCancel, 0.25f), "일반 이동·회피 UI 값 독립 변경");
Click("Test_SaveTimings");
profile.Get("ATTACK1").moveCancel = 0.1f; profile.Get("ATTACK1").dodgeCancel = 0.9f;
Click("Test_ReloadTimings");
Check(Mathf.Approximately(profile.Get("ATTACK1").moveCancel, 0.85f) && Mathf.Approximately(profile.Get("ATTACK1").dodgeCancel, 0.25f), "두 캔슬 시점 별도 저장·불러오기");
var oldKeyboard = UnityEngine.InputSystem.Keyboard.current;
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_MountainDodgeKeyboard");
var background = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.Key[] held = Array.Empty<UnityEngine.InputSystem.Key>();
Action feed = () => { keyboard.MakeCurrent(); UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(held)); };
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
UnityEngine.InputSystem.InputSystem.onBeforeUpdate += feed;
int phase = 0, lastFrame = -1, phaseFrame = Time.frameCount;
float phaseTime = Time.time;
double began = EditorApplication.timeSinceStartup;
Vector3 phasePosition = movement.transform.position;
TMPro.TMP_InputField focused = null;
EditorApplication.CallbackFunction tick = null;
void Next(int next) { phase = next; phaseFrame = Time.frameCount; phaseTime = Time.time; phasePosition = movement.transform.position; }
void Finish(bool success, string reason)
{
    EditorApplication.update -= tick;
    held = Array.Empty<UnityEngine.InputSystem.Key>();
    UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    if (oldKeyboard != null) oldKeyboard.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = background;
    if (focused != null) focused.DeactivateInputField();
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    JsonUtility.FromJsonOverwrite(savedBaseline, saved); EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved);
    if (profile != null) JsonUtility.FromJsonOverwrite(runtimeBaseline, profile);
    if (control != null) { animator.speed = 1f; control.PlayIdle(); movement.ResetToSpawn(); }
    System.IO.File.WriteAllText(report, Newtonsoft.Json.JsonConvert.SerializeObject(new { success, reason, checks, metrics, elapsed = EditorApplication.timeSinceStartup - began, frame = Time.frameCount }, Newtonsoft.Json.Formatting.Indented));
}
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false, "검증 중 재생이 종료되었습니다."); return; }
        if (EditorApplication.timeSinceStartup - began > 30d) { Finish(false, "Timeout at phase " + phase); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (phase == 0)
        {
            if (Time.time - phaseTime < 0.25f || Time.frameCount < phaseFrame + 5) return;
            metrics["walkDistance"] = Distance(phasePosition, movement.transform.position);
            metrics["walkPlaybackSpeed"] = animator.GetFloat("LocomotionSpeed");
            Check(movement.IsMoving && metrics["walkDistance"] > 0.3f, "일반 WASD 지형 이동");
            Check(Mathf.Approximately(animator.GetFloat("LocomotionSpeed"), control.run2Speed * 4f / 6f), "일반 이동 재생 속도 반영");
            held = new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift }; Next(1);
        }
        else if (phase == 1)
        {
            if (Time.time - phaseTime < 0.25f || Time.frameCount < phaseFrame + 5) return;
            metrics["sprintDistance"] = Distance(phasePosition, movement.transform.position);
            Check(movement.IsMoving && metrics["sprintDistance"] > 0.3f && Mathf.Approximately(animator.GetFloat("LocomotionSpeed"), control.run2Speed), "Shift 달리기 및 저장된 속도 배율");
            Check(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Run2" && c.weight > 0.8f), "달리기2만 정식 이동 클립으로 재생");
            held = new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift, UnityEngine.InputSystem.Key.F }; Next(2);
        }
        else if (phase == 2)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            Check(control.SelectedState == "DRAW" && movement.IsMoving && control.CanMove, "이동 중 상체 발도 유지");
            held = new[] { UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift }; Next(3);
        }
        else if (phase == 3)
        {
            if (control.SelectedState != "DRAWN_IDLE" || animator.IsInTransition(0)) return;
            held = Array.Empty<UnityEngine.InputSystem.Key>(); movement.ResetToSpawn(); Next(4);
        }
        else if (phase == 4)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            held = new[] { UnityEngine.InputSystem.Key.Space }; Next(5);
        }
        else if (phase == 5)
        {
            if (!control.IsDodging) return;
            Check(Mathf.Approximately(animator.GetFloat("RollSpeed"), 1.25f), "Space가 1.25배 구르기를 시작");
            held = Array.Empty<UnityEngine.InputSystem.Key>(); Next(6);
        }
        else if (phase == 6)
        {
            if (control.IsDodging) return;
            if (animator.IsInTransition(0)) return;
            metrics["dodgeDistance"] = Distance(phasePosition, movement.transform.position);
            Check(metrics["dodgeDistance"] > 0.2f && metrics["dodgeDistance"] < 3.4f && control.SelectedState == "DRAWN_IDLE", "회피 중 실제 충돌 이동 후 대기 복귀");
            Check(control.GetComponent<Cheon.Testing.PlayerFan.Test_PlayerFanAttachment>().IsInHand, "회피 후 부채 손 장착 유지");
            movement.ResetToSpawn(); control.TryPlayAttack(1); SnapAttack(0.35f);
            held = new[] { UnityEngine.InputSystem.Key.W }; Next(7);
        }
        else if (phase == 7)
        {
            if (Time.frameCount < phaseFrame + 4) return;
            Check(control.CanDodge && !control.CanMove && !movement.IsMoving && Distance(phasePosition, movement.transform.position) < 0.03f, "회피 허용 25%·일반 이동 잠금 85% 독립 판정");
            held = new[] { UnityEngine.InputSystem.Key.Space }; Next(8);
        }
        else if (phase == 8)
        {
            if (!control.IsDodging) return;
            Check(true, "일반 이동 잠금 중 Space 회피 캔슬 허용");
            held = Array.Empty<UnityEngine.InputSystem.Key>(); movement.ResetToSpawn();
            profile.Get("ATTACK1").moveCancel = 0.25f; profile.Get("ATTACK1").dodgeCancel = 0.85f;
            control.TryPlayAttack(1); SnapAttack(0.35f); Next(9);
        }
        else if (phase == 9)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            Check(control.CanMove && !control.CanDodge && !control.TryPlayDodge(), "일반 이동 허용 25%·회피 잠금 85% 독립 판정");
            held = new[] { UnityEngine.InputSystem.Key.Space }; Next(10);
        }
        else if (phase == 10)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            Check(control.SelectedState == "ATTACK1", "회피 캔슬 시점 전 Space 차단");
            held = Array.Empty<UnityEngine.InputSystem.Key>(); movement.ResetToSpawn(); control.TryPlayAttack(1); SnapAttack(0.35f);
            held = new[] { UnityEngine.InputSystem.Key.W }; Next(11);
        }
        else if (phase == 11)
        {
            if (Time.frameCount < phaseFrame + 4) return;
            Check(movement.IsMoving && control.SelectedState == "DRAWN_IDLE", "회피 잠금과 별개로 일반 이동 캔슬 허용");
            held = Array.Empty<UnityEngine.InputSystem.Key>(); animator.speed = 1f; movement.ResetToSpawn();
            focused = GameObject.Find("Test_TimingInput_0").GetComponent<TMPro.TMP_InputField>();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(focused.gameObject); focused.ActivateInputField(); Next(12);
        }
        else if (phase == 12)
        {
            if (!focused.isFocused || Time.frameCount < phaseFrame + 3) return;
            held = new[] { UnityEngine.InputSystem.Key.Space }; Next(13);
        }
        else if (phase == 13)
        {
            if (Time.frameCount < phaseFrame + 3) return;
            Check(!control.IsDodging && control.SelectedState == "DRAWN_IDLE", "숫자 입력 중 회피 단축키 차단");
            held = Array.Empty<UnityEngine.InputSystem.Key>(); focused.DeactivateInputField(); UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Click("Test_Select_ROLL");
            Check(GameObject.Find("Test_TimingInput_1").GetComponent<TMPro.TMP_InputField>().text == "100.0", "구르기 출발 액션의 회피 캔슬 항목 표시");
            Finish(true, "마운틴 이동·회피·캔슬 분리 검증 통과");
        }
    }
    catch (Exception error) { Finish(false, error.ToString()); }
};
movement.ResetToSpawn();
UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
held = new[] { UnityEngine.InputSystem.Key.W };
EditorApplication.update += tick;
return new { started = true, report };
