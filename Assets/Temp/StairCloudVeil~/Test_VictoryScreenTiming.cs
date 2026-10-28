var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
if (!game.IsRunning || !game.InEncounter || game.Boss.Health.IsDead) throw new System.Exception("Start a living boss encounter first.");
var boss = (Cheon.Prototype.MountainPass.PassBossPatternController)game.Boss;
var hud = game.transform.Find("MountainPassHUD");
var result = hud.Find("Result").gameObject;
var fade = hud.Find("ScreenFade").GetComponent<UnityEngine.UI.Image>();
var rows = new System.Collections.Generic.List<string> { "seconds,state,result_visible,screen_fade_alpha,transitioning,time_scale,death_tint,death_dissolve" };
float startedAt = Time.unscaledTime;
float firstResultAt = -1f;
float maxFade = 0f;
bool transitionSeen = false;
float minimumTimeScale = 1f;
int previousFrame = -1;
Canvas.WillRenderCanvases sample = null;
sample = () =>
{
    if (!Application.isPlaying || game == null)
    {
        Canvas.willRenderCanvases -= sample;
        return;
    }
    if (Time.frameCount == previousFrame) return;
    previousFrame = Time.frameCount;
    float elapsed = Time.unscaledTime - startedAt;
    if (result.activeInHierarchy && firstResultAt < 0f) firstResultAt = elapsed;
    maxFade = Mathf.Max(maxFade, fade.color.a);
    transitionSeen |= game.IsTransitioning;
    minimumTimeScale = Mathf.Min(minimumTimeScale, Time.timeScale);
    rows.Add(elapsed.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "," + game.State + "," + result.activeInHierarchy
        + "," + fade.color.a + "," + game.IsTransitioning + "," + Time.timeScale + "," + boss.DeathVfx.TintProgress + "," + boss.DeathVfx.DissolveProgress);
    if (elapsed < 1.35f) return;
    Canvas.willRenderCanvases -= sample;
    bool passed = firstResultAt >= 1f && firstResultAt < 1.15f && maxFade < 0.001f && !transitionSeen && minimumTimeScale > 0.99f;
    string summary = (passed ? "PASS" : "FAIL") + ": result at " + firstResultAt.ToString("F4") + "s; fade alpha max=" + maxFade
        + "; transition=" + transitionSeen + "; minimum time scale=" + minimumTimeScale;
    System.IO.File.WriteAllLines("Assets/Temp/StairCloudVeil~/VictoryTiming.csv", rows);
    System.IO.File.WriteAllText("Assets/Temp/StairCloudVeil~/VictoryTiming.txt", summary);
};
Canvas.willRenderCanvases += sample;
boss.Health.Kill();
return new { stateAfterDeath = game.State.ToString(), screenTransition = game.IsTransitioning, playerInput = game.Player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerMovement>().InputEnabled };
