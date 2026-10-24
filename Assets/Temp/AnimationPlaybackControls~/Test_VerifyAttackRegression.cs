if (!Application.isPlaying) throw new Exception("Play mode is required");
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
if (control == null) throw new Exception("Test animation controller not found");
var animator = control.CharacterAnimator;
var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
var head = animator.GetBoneTransform(HumanBodyBones.Head);
var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
var samples = new List<object>();
var finishedAttacks = new List<int>();
var observedProgress = new HashSet<int>();
int activeAttack = 0, lastFrame = -1;
double began = EditorApplication.timeSinceStartup;
string reportPath = "Assets/Temp/AnimationPlaybackControls~/Test_AttackRegressionReport.json";
EditorApplication.CallbackFunction tick = null;
void Finish(bool success, string reason)
{
    EditorApplication.update -= tick;
    var result = new { success, reason, finishedAttacks, observedProgress = observedProgress.OrderBy(i=>i).ToArray(), elapsed = EditorApplication.timeSinceStartup - began, frameCount = Time.frameCount, samples };
    System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
}
tick = () =>
{
    try
    {
        if (!Application.isPlaying || control == null) { Finish(false,"Play mode ended before completion"); return; }
        if (EditorApplication.timeSinceStartup - began > 25d) { Finish(false,"Verification timed out"); return; }
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        if (activeAttack > 0)
        {
            string state = "ATTACK" + activeAttack;
            if (control.SelectedState == state)
            {
                float progress = control.PlaybackProgress;
                if (progress > 0.2f) observedProgress.Add(activeAttack);
                samples.Add(new { attack = activeAttack, frame = Time.frameCount, progress, inTransition = animator.IsInTransition(0), neckAngle = Quaternion.Angle(Quaternion.identity,neck.localRotation), headAngle = Quaternion.Angle(Quaternion.identity,head.localRotation), combinedAngle = Quaternion.Angle(Quaternion.identity,Quaternion.Inverse(chest.rotation)*head.rotation) });
                return;
            }
            if (control.SelectedState != "DRAWN_IDLE" || animator.IsInTransition(0)) return;
            if (!observedProgress.Contains(activeAttack)) { Finish(false,"Attack did not advance: " + activeAttack); return; }
            finishedAttacks.Add(activeAttack);
            if (activeAttack == 3) { Finish(true,"All three attacks advanced and returned to combat idle"); return; }
        }
        else if (control.SelectedState != "DRAWN_IDLE" || animator.IsInTransition(0)) return;
        activeAttack++;
        if (activeAttack == 1) control.PlayAttack1();
        else if (activeAttack == 2) control.PlayAttack2();
        else control.PlayAttack3();
    }
    catch (Exception exception) { Finish(false,exception.ToString()); }
};
control.EnterCombat();
EditorApplication.update += tick;
return new { started = true, reportPath, initialFrame = Time.frameCount };

