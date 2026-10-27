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

public static class Test_CameraDeadZone
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static PassCamera view;
    private static PassBossPatternController boss;
    private static PassPlayerMovement player;
    private static Camera camera;

    public static async Task<object> Run()
    {
        var oldKeyboard = Keyboard.current;
        var oldMouse = Mouse.current;
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        var mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
        try
        {
            Load();
            var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
            flow.BeginJourney();
            typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
            flow.CompleteBossPresentation(); boss.enabled = false;
            player.InputEnabled = false; player.GetComponent<PassHealth>().Invulnerable = true;
            Vector3 bossOrigin = boss.transform.position;
            Vector3 desired = bossOrigin - boss.transform.forward * 12f;
            Vector3 ground = Physics.RaycastAll(desired + Vector3.up * 3f, Vector3.down, 8f)
                .Where(hit => hit.normal.y > .65f && hit.collider.GetComponentInParent<PassHealth>() == null)
                .OrderBy(hit => hit.distance).First().point;
            player.Teleport(ground, Quaternion.LookRotation(Vector3.ProjectOnPlane(bossOrigin - ground, Vector3.up)));
            typeof(PassCamera).GetField("applicationFocused", Flags).SetValue(view, true);
            view.LockBoss(false); view.SnapBehind();
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            await Frames(5);
            Require(!view.BossLocked, "Unexpected auto lock.");
            await Tab(keyboard); Require(view.BossLocked, "Tab did not enable lock.");
            await Frames(90);
            var samples = new List<object>();
            samples.Add(Sample("normal")); Capture("Test_Normal.png");

            // 화면 안에서 작은 움직임을 줬을 때 시점이 따라 돌지 않는지 실제 회전을 비교한다.
            float depth = camera.WorldToViewportPoint(boss.Body.bounds.center).z;
            boss.transform.position += camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth)) - boss.Body.bounds.center;
            Physics.SyncTransforms(); await Frames(100);
            Quaternion before = view.transform.rotation;
            depth = camera.WorldToViewportPoint(boss.Body.bounds.center).z;
            boss.transform.position += camera.ViewportToWorldPoint(new Vector3(.52f, .52f, depth)) - boss.Body.bounds.center;
            Physics.SyncTransforms(); await Frames(16);
            float deadZoneAngle = Quaternion.Angle(before, view.transform.rotation);
            samples.Add(new { stage = "deadZone", deadZoneAngle, viewport = camera.WorldToViewportPoint(boss.Body.bounds.center).ToString("F3") });
            Require(deadZoneAngle < .15f, "Camera rotated inside dead zone: " + deadZoneAngle);
            Vector3 centerBefore = camera.WorldToViewportPoint(boss.Body.bounds.center);
            boss.transform.position += camera.transform.right * 10f;
            Physics.SyncTransforms(); Quaternion startRotation = view.transform.rotation;
            await Frames(1); float firstAngle = Quaternion.Angle(startRotation, view.transform.rotation);
            await Frames(65); float settledAngle = Quaternion.Angle(startRotation, view.transform.rotation);
            Require(settledAngle > 1f && firstAngle < settledAngle, "Outside tracking was not gradual.");
            samples.Add(Sample("outsideDeadZone"));
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(150f, 100f)); await Frames(3);
            Require(view.BossLocked, "Mouse released lock.");

            boss.transform.position = bossOrigin; Physics.SyncTransforms(); await Frames(90);
            float maxRetreat = 0f, maxHeight = view.transform.position.y;
            int clippedFrames = 0;
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                boss.transform.position = bossOrigin + boss.transform.right * Mathf.Sin(t * Mathf.PI * 2f) * 8f +
                    Vector3.up * Mathf.Sin(t * Mathf.PI) * 12f;
                player.Teleport(ground + boss.transform.right * Mathf.Sin(t * Mathf.PI * 2f) * -5f, player.transform.rotation);
                Physics.SyncTransforms(); await Frames(2);
                if (!BothVisible()) clippedFrames++;
                maxRetreat = Mathf.Max(maxRetreat, (float)typeof(PassCamera).GetField("extraDistance", Flags).GetValue(view));
                maxHeight = Mathf.Max(maxHeight, view.transform.position.y);
                if (i == 50) { samples.Add(Sample("highJump")); Capture("Test_HighJump.png"); }
            }
            samples.Add(new { stage = "motion", clippedFrames, maxRetreat, maxHeight });
            Require(clippedFrames == 0, "Bodies clipped during motion: " + clippedFrames);

            // 원근이 달라도 전신이 화면 안에 남는지 검사한다.
            foreach (float separation in new [] { 3f, 30f })
            {
                player.Teleport(bossOrigin - boss.transform.forward * separation, player.transform.rotation);
                boss.transform.position = bossOrigin; Physics.SyncTransforms(); await Frames(90);
                Require(BothVisible(), "Bodies clipped at separation " + separation);
                samples.Add(Sample("distance" + separation)); Capture("Test_Distance" + separation + ".png");
            }
            float originalAspect = camera.aspect;
            camera.aspect = 4f / 3f; await Frames(80);
            Require(BothVisible(), "4:3 framing clipped.");
            samples.Add(Sample("aspect4x3")); camera.aspect = originalAspect;
            await Tab(keyboard); Require(!view.BossLocked, "Tab did not release lock.");
            await Frames(45);
            float initialYaw = (float)typeof(PassCamera).GetField("yaw", Flags).GetValue(view);
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(80f, 0)); await Frames(3);
            float newYaw = (float)typeof(PassCamera).GetField("yaw", Flags).GetValue(view);
            Require(Mathf.Abs(Mathf.DeltaAngle(initialYaw, newYaw)) > 1f, "Free mouse movement failed.");
            return new { passed = true, samples };
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent();
        }
    }

    public static async Task<object> RunPatterns()
    {
        Load();
        var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        flow.BeginJourney();
        typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
        boss.enabled = true; flow.CompleteBossPresentation();
        typeof(PassBossPatternController).GetField("openingReadyAt", Flags).SetValue(boss, float.PositiveInfinity);
        player.InputEnabled = false; player.GetComponent<PassHealth>().Invulnerable = true;
        Vector3 origin = boss.transform.position;
        player.Teleport(origin - boss.transform.forward * 10f + boss.transform.right * 3f,
            Quaternion.LookRotation(boss.transform.forward));
        view.LockBoss(false); view.SnapBehind(); view.LockBoss(true); await Frames(90);
        var samples = new List<object>(); int clippedFrames = 0, observed = 0;
        var stageSet = new HashSet<string>();
        typeof(PassBossPatternController).GetMethod("BeginPursuitSlam", Flags).Invoke(boss, null);
        float start = Time.time;
        while (boss.PursuitSlam.IsActive && Time.time - start < 18f)
        {
            await Frames(1); observed++;
            if (!BothVisible()) clippedFrames++;
            string stage = boss.PursuitSlam.Stage.ToString();
            if (stageSet.Add(stage)) samples.Add(Sample(stage));
            if (stage == "HeavyRise" && boss.PursuitSlam.StageElapsed > 1.35f && stageSet.Add("Peak"))
            { samples.Add(Sample("Peak")); Capture("Test_ActualJump.png"); }
            if (stage == "HeavyFall" && boss.PursuitSlam.StageElapsed > .5f && stageSet.Add("Rotate"))
            { samples.Add(Sample("Rotate")); Capture("Test_ActualRotation.png"); }
            if (stage == "Recovery" && boss.PursuitSlam.StageElapsed > 1f && stageSet.Add("BackLanding"))
            { samples.Add(Sample("BackLanding")); Capture("Test_ActualLanding.png"); }
        }
        Require(!boss.PursuitSlam.IsActive, "Pattern timeout.");
        Require(clippedFrames == 0, "Actual jump/rotation clipped " + clippedFrames + " frames.");
        samples.Add(Sample("recovered"));
        return new { passed = true, observedFrames = observed, clippedFrames, samples };
    }
    public static async Task<object> RunPauseAndAngles()
    {
        Load();
        var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        flow.BeginJourney();
        typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
        flow.CompleteBossPresentation(); boss.enabled = false; player.InputEnabled = false;
        player.GetComponent<PassHealth>().Invulnerable = true;
        player.Teleport(boss.transform.position - boss.transform.forward * 12f, Quaternion.LookRotation(boss.transform.forward));
        var samples = new List<object>();
        foreach (float angle in new [] { 28f, -65f, 85f })
        {
            view.LockBoss(false);
            typeof(PassCamera).GetField("pitch", Flags).SetValue(view, angle);
            player.transform.rotation = Quaternion.LookRotation(angle == 28f ? -boss.transform.forward : boss.transform.forward);
            view.SnapBehind(); view.LockBoss(true); await Frames(180);
            samples.Add(Sample("angle" + angle));
            Require(BothVisible(), "Lock framing failed at starting angle " + angle);
        }
        view.AddShake(.8f); flow.TogglePause();
        Vector3 position = view.transform.position; Quaternion rotation = view.transform.rotation;
        await Frames(30);
        Require(Vector3.Distance(position, view.transform.position) < .0001f && Quaternion.Angle(rotation, view.transform.rotation) < .001f,
            "Paused camera drifted.");
        flow.TogglePause();
        view.LockBoss(false); await Frames(60);
        return new { passed = true, pausedCameraStable = true, lockReleased = !view.BossLocked, samples };
    }
    public static object Inspect() { Load(); return Sample("inspect"); }
    private static void Load()
    {
        view = UnityEngine.Object.FindFirstObjectByType<PassCamera>();
        boss = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>().MainBoss;
        player = UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>(); camera = view.GetComponent<Camera>();
    }
    private static object Sample(string stage) => new { stage, locked = view.BossLocked,
        bossCenter = camera.WorldToViewportPoint(boss.Body.bounds.center).ToString("F3"),
        position = camera.transform.position.ToString("F3"), rotation = camera.transform.eulerAngles.ToString("F3"),
        retreat = typeof(PassCamera).GetField("extraDistance", Flags).GetValue(view),
        player = ProjectBounds(PlayerBounds()), boss = ProjectBounds(BossBounds()), visible = BothVisible() };
    private static Bounds PlayerBounds() => RenderBounds(player.transform, player.Body.bounds);
    private static Bounds BossBounds() => RenderBounds(boss.transform, boss.Body.bounds);
    private static Bounds RenderBounds(Transform root, Bounds initial)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)) initial.Encapsulate(r.bounds);
        return initial;
    }
    private static Vector3[] Corners(Bounds b) => Enumerable.Range(0, 8).Select(i => b.center + Vector3.Scale(b.extents,
        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))).ToArray();
    private static object ProjectBounds(Bounds b)
    {
        var points = Corners(b).Select(p => camera.WorldToViewportPoint(p)).ToArray();
        return new { minX = points.Min(p => p.x), maxX = points.Max(p => p.x), minY = points.Min(p => p.y), maxY = points.Max(p => p.y), minZ = points.Min(p => p.z) };
    }
    private static bool BothVisible() => Corners(PlayerBounds()).Concat(Corners(BossBounds())).All(p =>
    { Vector3 v = camera.WorldToViewportPoint(p); return v.z > camera.nearClipPlane && v.x > .005f && v.x < .995f && v.y > .005f && v.y < .995f; });
    private static async Task Tab(Keyboard k)
    {
        InputSystem.QueueStateEvent(k, new KeyboardState(Key.Tab)); await Frames(3);
        InputSystem.QueueStateEvent(k, new KeyboardState()); await Frames(3);
    }
    private static async Task Frames(int count)
    {
        int target = Time.frameCount + count; var watch = System.Diagnostics.Stopwatch.StartNew();
        while (Time.frameCount < target)
        { if (watch.Elapsed.TotalSeconds > 10) throw new Exception("Player loop stopped."); await Task.Delay(1); }
    }
    private static void Capture(string name)
    {
        var original = camera.targetTexture; var previous = RenderTexture.active;
        var render = RenderTexture.GetTemporary(960, 540, 24);
        var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "Temp/CameraDeadZone~/" + name), texture.EncodeToPNG());
        }
        finally { camera.targetTexture = original; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); UnityEngine.Object.Destroy(texture); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
