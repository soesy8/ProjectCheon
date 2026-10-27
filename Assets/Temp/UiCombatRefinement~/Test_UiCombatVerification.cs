using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class Test_UiCombatVerification
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static PassGameFlow flow;
    static PassPlayerMovement player;
    static PassBossPatternController boss;
    static PassPlayerEnergy energy;
    static PassAnimationController animation;
    static PassHealth health;
    static readonly List<object> checks = new List<object>();
    static void Load()
    {
        flow = Object.FindFirstObjectByType<PassGameFlow>(); player = flow.Player.GetComponent<PassPlayerMovement>();
        boss = (PassBossPatternController)flow.Boss; energy = flow.Energy; animation = player.AnimationControl; health = flow.PlayerHealth;
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        typeof(PassCamera).GetField("applicationFocused", Flags).SetValue(flow.View, true);
    }
    public static async Task<object> Preview()
    {
        Load(); int before = Time.frameCount; await Frames(5);
        Require(Time.frameCount > before, "player loop advances");
        await Encounter();
        return new {frame = Time.frameCount, state = flow.State.ToString(), energy = energy.Current, bossHealth = boss.Health.Current};
    }
    static async Task Encounter()
    {
        await Until(() => !flow.IsTransitioning);
        flow.BeginBossChallenge(); await Until(() => !flow.IsTransitioning);
        if (flow.Entrance != null) flow.Entrance.SkipPresentation();
        await Until(() => !flow.IsTransitioning && flow.State == PassGameFlow.RunState.Playing);
        await Frames(5); boss.enabled = false; health.Invulnerable = true;
        PlacePlayer(9f); await Frames(5);
    }
    static void PlacePlayer(float distance)
    {
        Vector3 desired = boss.transform.position - boss.transform.forward * distance;
        var hits = Physics.RaycastAll(desired + Vector3.up * 8f, Vector3.down, 30f)
            .Where(x => x.normal.y > .5f && x.collider.GetComponentInParent<PassHealth>() == null).OrderBy(x => x.distance).ToArray();
        if (hits.Length > 0) desired = hits[0].point + Vector3.up * .05f;
        player.Teleport(desired, Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position - desired, Vector3.up)));
        flow.View.LockBoss(false); flow.View.SnapBehind();
    }
    public static async Task<object> Resources()
    {
        Load(); checks.Clear();
        var prior = Keyboard.current;
        var background = InputSystem.settings.backgroundBehavior;
        var inputMode = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        try
        {
            await Encounter();
            Require(energy.Maximum == 100f && boss.Health.Maximum == 1000f, "maximum energy 100 / boss 1000");
            energy.Restore(); await Keys(keyboard, Key.W, Key.LeftShift); await Frames(3);
            float before = energy.Current, start = Time.time; await Frames(50);
            float seconds = Time.time - start, spent = before - energy.Current;
            Require(Mathf.Abs(spent - 5 * seconds) < .35f, "forward sprint drains 5/s", new {seconds, spent});
            await Keys(keyboard); PlacePlayer(9f); energy.Restore();
            await Keys(keyboard, Key.S, Key.LeftShift); await Frames(3);
            before = energy.Current; start = Time.time; await Frames(50);
            seconds = Time.time - start; spent = before - energy.Current;
            Require(Mathf.Abs(spent - 10 * seconds) < .45f, "retreat sprint drains 10/s", new {seconds, spent});
            await Keys(keyboard); PlacePlayer(9f); energy.Restore();
            await Keys(keyboard, Key.Space); await Frames(3);
            Require(animation.IsDodging && Mathf.Abs(energy.Current - 75f) < .01f, "actual dodge spends 25 once", energy.Current);
            await Keys(keyboard); await Until(() => !animation.IsDodging); await Frames(2);
            energy.Restore(); energy.SpendDodge(); energy.SpendDodge();
            before = energy.Current; start = Time.time; await Frames(50);
            seconds = Time.time - start;
            Require(Mathf.Abs(energy.Current - before - 10f * seconds) < .35f, "idle regenerates 10/s", new {seconds, gained = energy.Current - before});
            flow.TogglePause(); await Until(() => !flow.IsTransitioning);
            before = energy.Current; await Frames(20);
            Require(Time.timeScale == 0f && energy.Current == before, "pause freezes energy");
            flow.TogglePause(); await Until(() => !flow.IsTransitioning);
            energy.Restore(); for (int i = 0; i < 4; i++) energy.SpendDodge();
            energy.enabled = false;
            await Keys(keyboard, Key.Space); await Frames(3);
            Require(!animation.IsDodging && energy.Current == 0f, "empty energy blocks dodge");
            await Keys(keyboard, Key.W, Key.LeftShift); await Frames(5);
            Require(!player.IsSprinting, "empty energy blocks sprint");
            await Keys(keyboard); energy.enabled = true; energy.Restore();
            return Save("Resources", checks);
        }
        finally
        {
            energy.enabled = true; Time.timeScale = 1f; InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = inputMode;
            if (prior != null && prior.added) prior.MakeCurrent();
        }
    }
    public static async Task<object> Healing()
    {
        Load(); checks.Clear(); await Encounter();
        var gourd = flow.Gourd;
        health.Restore(); health.Invulnerable = false; health.TakeDamage(70, ignoreHitRecovery:true);
        animation.PlayIdle(); await Frames(3); player.InputEnabled = true;
        gourd.Refill(); Require(gourd.TryDrink(), "drink starts");
        float before = health.Current;
        await Until(() => gourd.IsSipping && health.Current > before + 2);
        float partial = health.Current - before;
        Require(partial > 0 && partial < 40, "healing progresses instead of instant 40", partial);
        await Until(() => !gourd.IsPlaying);
        Require(Mathf.Abs(health.Current - before - 40) < .02f && gourd.RemainingUses == 4, "full sip heals exactly 40 and uses one charge", health.Current);
        health.TakeDamage(45, ignoreHitRecovery:true); animation.PlayIdle(); await Frames(3);
        before = health.Current; Require(gourd.TryDrink(), "interrupted drink starts");
        await Until(() => gourd.IsSipping && health.Current > before + 5);
        float earned = health.Current - before;
        health.TakeDamage(1, ignoreHitRecovery:true); float interrupted = health.Current;
        await Frames(65);
        Require(earned < 40 && !gourd.IsPlaying && Mathf.Abs(health.Current - interrupted) < .02f,
            "damage cancels remaining healing", new {earned, interrupted, after = health.Current});
        animation.PlayIdle(); await Frames(3);
        int uses = gourd.RemainingUses;
        Require(gourd.TryDrink(), "pre-sip drink starts"); gourd.CancelDrinking(); await Frames(4);
        Require(gourd.RemainingUses == uses, "cancel before sipping preserves charge");
        health.Restore(); health.Invulnerable = true;
        return Save("Healing", checks);
    }
    public static async Task<object> DamageAndGround()
    {
        Load(); checks.Clear(); await Encounter();
        var combat = player.GetComponent<PassFanAttackCombat>();
        var apply = typeof(PassFanAttackCombat).GetMethod("ApplyDamage", Flags);
        float[] direct = {10, 15, 40};
        boss.Health.Invulnerable = false;
        for (int attack = 1; attack <= 3; attack++)
        {
            float before = boss.Health.Current;
            apply.Invoke(combat, new object[] {boss.Health, boss.Body.ClosestPoint(player.transform.position + Vector3.up), attack, 20f, false});
            Require(Mathf.Abs(before - boss.Health.Current - direct[attack - 1]) < .001f, "direct attack " + attack, before - boss.Health.Current);
        }
        foreach (int attack in new[] {1, 3})
        {
            float before = boss.Health.Current;
            apply.Invoke(combat, new object[] {boss.Health, boss.Body.ClosestPoint(player.transform.position + Vector3.up), attack, 20f, true});
            Require(Mathf.Abs(before - boss.Health.Current - (attack == 1 ? 5 : 10)) < .001f, "wind attack " + attack, before - boss.Health.Current);
        }
        var hit = Object.FindFirstObjectByType<PassHitEffects>();
        Require(hit.ActiveParticles > 0, "contact hit particles emitted", hit.ActiveParticles);
        var meshObject = new GameObject("Test_WindProjection");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Test_Ground";
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Cube); enemy.name = "Test_Enemy";
        try
        {
            Vector3 position = new Vector3(900, 20, 900);
            floor.transform.position = position - Vector3.up * .5f; floor.transform.localScale = new Vector3(30, 1, 30);
            enemy.transform.position = position + new Vector3(0, 2, 4); enemy.transform.localScale = new Vector3(5, 4, 5); enemy.AddComponent<PassHealth>();
            meshObject.transform.position = position;
            var mesh = meshObject.AddComponent<PassFanSweepMesh>();
            var profile = Object.FindFirstObjectByType<PassFanAttackVfx>().Profile;
            mesh.Rebuild(profile.attack1Wind); Physics.SyncTransforms();
            mesh.ConformToGround(player.transform, ~0, .1f);
            float max = mesh.Mesh.vertices.Max(v => v.y);
            Require(max < .12f, "wind projection ignores enemy body", max);
        }
        finally { Object.Destroy(meshObject); Object.Destroy(floor); Object.Destroy(enemy); }
        var labels = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None);
        Require(!labels.Any(x => x.gameObject.activeInHierarchy && (x.text == "생명력" || x.text == "도력" || x.text == "스테미너")), "resource labels hidden");
        Require(labels.Any(x => x.name == "EnergyValue" && x.gameObject.activeInHierarchy), "energy number visible");
        boss.Health.Restore(); health.Restore(); health.Invulnerable = true; energy.Restore();
        await Frames(10);
        return Save("DamageAndGround", checks);
    }
    public static async Task<object> TransitionsAndAudio()
    {
        Load(); checks.Clear();
        await Until(() => !flow.IsTransitioning);
        flow.BeginJourney();
        float maximumOpacity = 0f;
        var veil = GameObject.Find("ScreenFade").GetComponent<UnityEngine.UI.Image>();
        while (flow.IsTransitioning) { maximumOpacity = Mathf.Max(maximumOpacity, veil.color.a); await Frames(1); }
        Require(maximumOpacity > .95f && veil.color.a < .01f && Time.timeScale == 1f && flow.IsRunning,
            "journey fade reaches black then clears", maximumOpacity);
        flow.BeginBossChallenge(); await Until(() => !flow.IsTransitioning);
        Require(flow.State == PassGameFlow.RunState.Cinematic && Time.timeScale == 1f, "boss introduction advances after fade");
        flow.Entrance.SkipPresentation(); await Until(() => !flow.IsTransitioning && flow.State == PassGameFlow.RunState.Playing);
        Require(Time.timeScale == 1f && Camera.main != null && flow.View.enabled, "cinematic exit restores gameplay camera and time");
        boss.enabled = false; health.Invulnerable = true; PlacePlayer(12f);
        flow.TogglePause(); await Until(() => !flow.IsTransitioning);
        Require(Time.timeScale == 0f && flow.State == PassGameFlow.RunState.Paused && !veil.raycastTarget, "pause fade restores paused time");
        flow.TogglePause(); await Until(() => !flow.IsTransitioning);
        Require(Time.timeScale == 1f && flow.IsRunning, "resume fade restores normal time");
        string[] clipFields = {"fanWhoosh", "strike", "windHit", "hurt", "slam", "bell", "wind", "footstep", "dodge", "drink", "confirm", "spirit", "victory", "defeat", "equipment"};
        foreach (var name in clipFields)
        {
            var clip = (AudioClip)typeof(PassAudio).GetField(name, Flags).GetValue(flow.Audio);
            Require(clip != null && clip.samples > 0 && clip.loadState == AudioDataLoadState.Loaded, "audio loaded: " + name, clip == null ? 0 : clip.length);
        }
        var samples = new float[1024];
        flow.Audio.Swing(1); flow.Audio.Strike(false, 3);
        var sources = flow.Audio.GetComponents<AudioSource>();
        float peak = 0f;
        for (int frame = 0; frame < 20; frame++)
        {
            await Frames(1);
            foreach (var source in sources) { source.GetOutputData(samples, 0); peak = Mathf.Max(peak, samples.Max(x => Mathf.Abs(x))); }
            AudioListener.GetOutputData(samples, 0); peak = Mathf.Max(peak, samples.Max(x => Mathf.Abs(x)));
        }
        Require(sources.Any(x => x.isPlaying) && peak > 0.00001f, "audio produces output", peak);
        var result = Save("TransitionsAndAudio", checks);
        return result;
    }
    public static async Task<object> HealingActions()
    {
        Load(); checks.Clear(); await Encounter();
        var gourd = flow.Gourd;
        foreach (string action in new[] {"dodge", "attack"})
        {
            health.Restore(); health.Invulnerable = false; health.TakeDamage(70, ignoreHitRecovery:true);
            animation.PlayIdle(); await Frames(3); health.Invulnerable = true;
            if (action == "attack") { animation.TryAttackInput(1, false, false); await Until(() => !animation.IsActionPlaying); }
            gourd.Refill(); Require(gourd.TryDrink(), action + " drink starts");
            float before = health.Current;
            await Until(() => gourd.IsSipping && health.Current > before + 4);
            bool accepted = action == "dodge" ? animation.TryPlayDodge() : animation.TryAttackInput(1, false, false);
            float stopped = health.Current;
            await Frames(65);
            Require(accepted && !gourd.IsPlaying && stopped - before < 40 && Mathf.Abs(health.Current - stopped) < .02f,
                action + " cancels remaining healing", new {healed = stopped - before, health = health.Current});
            animation.PlayIdle(); await Frames(5);
        }
        health.Restore(); energy.Restore(); gourd.Refill();
        return Save("HealingActions", checks);
    }
    static async Task Keys(Keyboard keyboard, params Key[] keys)
    { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); await Frames(1); }
    public static async Task<object> ActualCombat()
    {
        Load(); checks.Clear(); await Encounter();
        var combat = player.GetComponent<PassFanAttackCombat>();
        PlacePlayer(boss.Body.radius + 1.2f); await Frames(3);
        health.Invulnerable = true; boss.Health.Invulnerable = false;
        if (!animation.IsCombatRequested)
        { animation.TryAttackInput(1, false, false); await Until(() => !animation.IsEquipmentTransition); }
        await Until(() => !animation.IsActionPlaying);
        combat.ResetHits(); boss.Health.Restore(); boss.Health.Invulnerable = false;
        Require(animation.TryAttackInput(1, false, false), "real attack accepted");
        await Until(() => combat.DirectHitsLanded > 0);
        ScreenCapture.CaptureScreenshot("Assets/Temp/UiCombatRefinement~/T_HitImpact.png");
        await Until(() => !animation.IsActionPlaying); await Frames(3);
        Require(combat.DirectHitsLanded == 1 && combat.WindHitsLanded == 1 && Mathf.Abs(boss.Health.Current - 985) < .001f,
            "real animation hits once for 10 + 5", new {combat.DirectHitsLanded, combat.WindHitsLanded, boss.Health.Current});
        PlacePlayer(12f); health.Invulnerable = false; health.TakeDamage(16, ignoreHitRecovery:true); animation.PlayIdle();
        energy.Restore(); energy.SpendDodge(); boss.Health.TakeDamage(265, ignoreHitRecovery:true); health.Invulnerable = true;
        await Frames(3);
        return Save("ActualCombat", checks);
    }
    public static object InspectAudio()
    {
        Load();
        var source = flow.Audio.GetComponents<AudioSource>();
        var clip = (AudioClip)typeof(PassAudio).GetField("strike", Flags).GetValue(flow.Audio);
        float[] samples = new float[clip.samples]; bool data = clip.GetData(samples, 0);
        return new {listenerVolume = AudioListener.volume, listenerPause = AudioListener.pause, muted = UnityEditor.EditorUtility.audioMasterMute,
            config = AudioSettings.GetConfiguration(), dspTime = AudioSettings.dspTime, clipData = data, assetPeak = samples.Max(x => Mathf.Abs(x)),
            listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Select(x => new {x.name, x.enabled, active = x.gameObject.activeInHierarchy}).ToArray(),
            sources = source.Select(x => new {x.isPlaying, x.volume, x.mute, x.enabled, x.spatialBlend, x.time}).ToArray()};
    }
    public static async Task<object> ResultsTransitions()
    {
        Load(); checks.Clear(); await Until(() => !flow.IsTransitioning); await Frames(3);
        if (flow.State == PassGameFlow.RunState.Intro)
        { ScreenCapture.CaptureScreenshot("Assets/Temp/UiCombatRefinement~/T_Title_Final.png"); await Frames(3); }
        await Encounter(); energy.SpendDodge(); health.Invulnerable = false; health.Kill();
        await Until(() => !flow.IsTransitioning && flow.State == PassGameFlow.RunState.Defeat);
        Require(!player.InputEnabled && Time.timeScale == 1f, "defeat fades into result screen");
        flow.RetryCheckpoint(); await Until(() => !flow.IsTransitioning);
        flow.Entrance.SkipPresentation(); await Until(() => !flow.IsTransitioning && flow.IsRunning);
        Require(energy.Current == 100f && health.Current == health.Maximum && flow.Gourd.RemainingUses == flow.Gourd.MaximumUses,
            "retry restores health energy and checkpoint gourd");
        boss.enabled = false; health.Invulnerable = true; boss.Health.Kill();
        await Until(() => !flow.IsTransitioning && flow.State == PassGameFlow.RunState.Victory);
        Require(!player.InputEnabled && Time.timeScale == 1f, "victory fades into result screen");
        return Save("ResultsTransitions", checks);
    }
    static async Task Frames(int count)
    {
        int target = Time.frameCount + count; var watch = System.Diagnostics.Stopwatch.StartNew();
        while (Time.frameCount < target) { if (watch.Elapsed.TotalSeconds > 15) throw new Exception("Player loop stopped."); await Task.Delay(1); }
    }
    static async Task Until(Func<bool> condition)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed.TotalSeconds > 12) throw new Exception("Condition timed out."); await Task.Delay(1); }
    }
    static void Require(bool condition, string name, object details = null)
    {
        checks.Add(new {name, passed = condition, details});
        if (!condition) { Save("Failure", checks); throw new Exception(name + " : " + Newtonsoft.Json.JsonConvert.SerializeObject(details)); }
    }
    static object Save(string group, object result)
    {
        var report = new {group, passed = group != "Failure", frame = Time.frameCount, checks = result};
        File.WriteAllText("Assets/Temp/UiCombatRefinement~/Test_" + group + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
