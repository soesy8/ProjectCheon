if (!Application.isPlaying) throw new Exception("Play Mode가 필요합니다.");
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
var animator = control.CharacterAnimator;
var fan = control.GetComponent<Cheon.Testing.PlayerFan.Test_PlayerFanAttachment>();
var checks = new List<string>();
var reportPath = "Assets/Temp/AddAnimationClips~/Test_PlaybackReport.json";
int phase = 0, lastFrame = -1;
bool observedRoll = false, observedRun = false;
float previousProgress = 0f, pausedTime = 0f;
double began = EditorApplication.timeSinceStartup, phaseBegan = began;
var initialRoot = animator.transform.position;
var initialFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
EditorApplication.CallbackFunction tick = null;
void Finish(bool success, string reason)
{
    EditorApplication.update -= tick;
    if (control.IsPaused) control.TogglePause();
    control.RepeatSelectedAnimation = false;
    System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new { success, reason, checks, frameCount = Time.frameCount, elapsed = EditorApplication.timeSinceStartup - began }, Newtonsoft.Json.Formatting.Indented));
}
void Next(int next) { phase = next; phaseBegan = EditorApplication.timeSinceStartup; }
void Click(string name) { GameObject.Find("Test_Button_" + name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); }
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false, "再生終了"); return; }
        if (EditorApplication.timeSinceStartup - began > 30d) { Finish(false, "Timeout at phase " + phase); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (Vector3.Distance(initialRoot, animator.transform.position) > 0.001f) throw new Exception("キャラクターのルート位置が変わりました。");
        if (phase == 0)
        {
            observedRoll |= control.SelectedState == "ROLL" && control.PlaybackProgress > 0.25f;
            if (!observedRoll || control.SelectedState != "SHEATHED_IDLE" || animator.IsInTransition(0)) return;
            checks.Add("Roll button advanced and returned to sheathed idle");
            Click("달리기2 [7]");
            Next(1);
        }
        else if (phase == 1)
        {
            if (control.SelectedState != "RUN2") throw new Exception("달리기2 상태가 유지되지 않았습니다.");
            observedRun |= Vector3.Distance(initialFoot, animator.GetBoneTransform(HumanBodyBones.RightFoot).position) > 0.1f;
            if (control.NormalizedTime < 2.2f) return;
            if (!observedRun) throw new Exception("달리기2 본이 움직이지 않았습니다.");
            checks.Add("Run2 button advanced through more than two loops with visible bone motion");
            control.TogglePause();
            pausedTime = control.NormalizedTime;
            Next(2);
        }
        else if (phase == 2)
        {
            if (Mathf.Abs(control.NormalizedTime - pausedTime) > 0.001f) throw new Exception("일시정지 중 시간이 진행되었습니다.");
            if (EditorApplication.timeSinceStartup - phaseBegan < 0.25d) return;
            checks.Add("Pause held Run2 playback time constant");
            control.TogglePause();
            control.RepeatSelectedAnimation = true;
            Click("구르기 [6]");
            previousProgress = 0f;
            Next(3);
        }
        else if (phase == 3)
        {
            if (control.SelectedState != "ROLL") throw new Exception("반복 구르기 상태가 유지되지 않았습니다.");
            float progress = control.PlaybackProgress;
            bool repeated = previousProgress > 0.65f && progress < 0.3f;
            previousProgress = progress;
            if (!repeated) return;
            checks.Add("Repeat option restarted Roll");
            control.RepeatSelectedAnimation = false;
            Next(4);
        }
        else if (phase == 4)
        {
            if (control.SelectedState != "SHEATHED_IDLE" || animator.IsInTransition(0)) return;
            checks.Add("Disabling repeat returned Roll to idle");
            control.EnterCombat();
            Next(5);
        }
        else if (phase == 5)
        {
            if (control.SelectedState != "DRAWN_IDLE" || animator.IsInTransition(0)) return;
            observedRoll = false;
            Click("구르기 [6]");
            Next(6);
        }
        else if (phase == 6)
        {
            observedRoll |= control.SelectedState == "ROLL" && control.PlaybackProgress > 0.25f;
            if (!observedRoll || control.SelectedState != "DRAWN_IDLE" || animator.IsInTransition(0)) return;
            if (!fan.IsInHand) throw new Exception("전투 구르기 후 부채가 손에서 분리되었습니다.");
            checks.Add("Combat Roll returned to drawn idle and kept the fan in hand");
            Finish(true, "All animation playback checks passed");
        }
    }
    catch (Exception error) { Finish(false, error.ToString()); }
};
control.RepeatSelectedAnimation = false;
Click("구르기 [6]");
EditorApplication.update += tick;
return new { started = true, reportPath, frame = Time.frameCount };
