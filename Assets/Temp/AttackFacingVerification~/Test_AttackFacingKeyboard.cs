var flow = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassPlayerMovement>();
var animation = player.AnimationControl;
var view = flow.View;
var boss = flow.Boss;
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
var results = new System.Collections.Generic.List<object>();
var samples = new System.Collections.Generic.List<object>();
var savedBossPosition = boss.transform.position;
int scenario = -1, previousStage = 0, queuedStage = 0, thirdFrames = 0, lastFrame = -1;
int firstFrame = UnityEngine.Time.frameCount;
bool motionFixed = true, sawBufferedInput = false, finished = false;
double startedAt = 0;
string reportPath = "D:/project/ProjectCheon/Assets/Temp/AttackFacingVerification~/Test_AttackFacingKeyboardResults.json";
UnityEditor.EditorApplication.CallbackFunction tick = null;

void Queue(bool attack)
{
    var keys = new System.Collections.Generic.List<UnityEngine.InputSystem.Key>();
    if (previousStage > 0) keys.Add(scenario == 1 ? UnityEngine.InputSystem.Key.W : UnityEngine.InputSystem.Key.D);
    if (attack) keys.Add(UnityEngine.InputSystem.Key.J);
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
        new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
}
void StartScenario()
{
    scenario++;
    previousStage = queuedStage = thirdFrames = 0;
    motionFixed = true;
    sawBufferedInput = false;
    samples.Clear();
    flow.BeginJourney();
    flow.enabled = false;
    view.enabled = false;
    player.transform.rotation = UnityEngine.Quaternion.identity;
    view.transform.rotation = UnityEngine.Quaternion.Euler(20, scenario == 1 ? 15 : 0, 0);
    boss.transform.position = player.transform.position + UnityEngine.Vector3.forward * 20;
    view.LockBoss(scenario == 2);
    if (!animation.IsCombatRequested) animation.EnterCombat();
    animation.PlayIdle();
    animation.CharacterAnimator.Play("DRAWN_IDLE", 0, 0);
    animation.CharacterAnimator.Update(0);
    startedAt = UnityEditor.EditorApplication.timeSinceStartup;
    Queue(true);
}
void Finish()
{
    if (finished) return;
    finished = true;
    UnityEditor.EditorApplication.update -= tick;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    boss.transform.position = savedBossPosition;
    flow.enabled = true;
    view.enabled = true;
    flow.BeginJourney();
    System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(
        new { frameAdvanced = UnityEngine.Time.frameCount > firstFrame, results }, Newtonsoft.Json.Formatting.Indented));
}

tick = () =>
{
    if (lastFrame == UnityEngine.Time.frameCount) return;
    lastFrame = UnityEngine.Time.frameCount;
    try
    {
        int stage = animation.SelectedState == "ATTACK1" ? 1 : animation.SelectedState == "ATTACK2" ? 2 :
            animation.SelectedState == "ATTACK3" ? 3 : 0;
        float yaw = Mathf.DeltaAngle(0, player.transform.eulerAngles.y);
        if (stage > 0)
        {
            float expected = stage == 1 ? 0 : scenario == 1 ? 15 : (stage - 1) * 45;
            if (Mathf.Abs(Mathf.DeltaAngle(expected, yaw)) > 0.1f) motionFixed = false;
            if (stage != previousStage) samples.Add(new { frame = UnityEngine.Time.frameCount, stage, expected, yaw });
            previousStage = stage;
            if (player.BufferedAction != "없음") sawBufferedInput = true;
        }
        if (stage == 3 && ++thirdFrames >= 8)
        {
            bool passed = motionFixed && sawBufferedInput && samples.Count == 3;
            results.Add(new { scenario = scenario == 0 ? "free_D" : scenario == 1 ? "free_camera15_W" : "locked_D",
                passed, motionFixed, sawBufferedInput, samples = samples.ToArray() });
            if (scenario == 2) Finish();
            else StartScenario();
            return;
        }
        if (UnityEditor.EditorApplication.timeSinceStartup - startedAt > 12)
        {
            results.Add(new { scenario, passed = false, error = "Timed out", state = animation.SelectedState, samples = samples.ToArray() });
            Finish();
            return;
        }
        bool request = stage > 0 && stage < 3 && queuedStage != stage && animation.SecondsUntilAttackCancel(stage + 1) <= 0.12f;
        if (request) queuedStage = stage;
        Queue(stage == 0 && previousStage == 0 || request);
    }
    catch (System.Exception error)
    {
        results.Add(new { scenario, passed = false, error = error.ToString() });
        Finish();
    }
};
StartScenario();
UnityEditor.EditorApplication.update += tick;
return new { started = true, firstFrame, reportPath };
