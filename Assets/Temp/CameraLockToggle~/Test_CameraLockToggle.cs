using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class Test_CameraLockToggle
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode required.");
        var originalKeyboard = Keyboard.current;
        var originalMouse = Mouse.current;
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        var mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
        try
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
            var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
            var boss = settings.MainBoss;
            var player = UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
            var view = flow.View;
            flow.BeginJourney();
            typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
            flow.CompleteBossPresentation(); boss.enabled = false;
            player.InputEnabled = false; player.GetComponent<PassHealth>().Invulnerable = true;
            var desired = boss.transform.position - boss.transform.forward * 12f;
            var ground = Physics.RaycastAll(desired + Vector3.up * 3f, Vector3.down, 8f)
                .Where(hit => hit.normal.y > .65f && hit.collider.GetComponentInParent<PassHealth>() == null)
                .OrderBy(hit => hit.distance).First().point;
            player.Teleport(ground, Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position - ground, Vector3.up)));
            typeof(PassCamera).GetField("applicationFocused", PrivateInstance).SetValue(view, true);
            view.LockBoss(false); view.SnapBehind();
            var gameView = UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            gameView.Focus();
            await Frames(8);
            Require(!view.BossLocked, "Combat must start with free camera.");
            float configuredDistance = (float)typeof(PassCamera).GetField("distance", PrivateInstance).GetValue(view);
            float beforeDistance = OrbitDistance(view);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            await Frames(10);
            Require(view.BossLocked, "Tab must enable and hold boss lock.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(3);
            float lockedDistance = OrbitDistance(view);
            Near(lockedDistance, beforeDistance, .05f, "Lock changed camera distance.");
            float pitch = view.Pitch;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(120f, 80f));
            InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0, -120f));
            await Frames(4);
            Require(view.BossLocked, "Mouse movement released boss lock.");
            Near(view.Pitch, pitch, .001f, "Mouse changed locked camera pitch.");
            Require(Vector2.Distance(TargetViewport(view, boss), new Vector2(.5f, .65f)) < .01f, "Camera did not keep boss framing.");
            Capture(view.GetComponent<Camera>());

            boss.transform.position += boss.transform.right * 3f + Vector3.up * 3f;
            Physics.SyncTransforms(); await Frames(12);
            Require(Vector2.Distance(TargetViewport(view, boss), new Vector2(.5f, .65f)) < .01f, "Moving boss lost camera framing.");
            Near(OrbitDistance(view), beforeDistance, .05f, "Moving boss increased distance.");

            await PressTab(keyboard);
            Require(!view.BossLocked, "Second Tab must disable lock.");
            await Frames(10);
            float yawBefore = (float)typeof(PassCamera).GetField("yaw", PrivateInstance).GetValue(view);
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(80f, 0));
            InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0, -120f));
            await Frames(4);
            float yawAfter = (float)typeof(PassCamera).GetField("yaw", PrivateInstance).GetValue(view);
            Require(Mathf.Abs(Mathf.DeltaAngle(yawBefore, yawAfter)) > 1f, "Free camera did not accept mouse movement.");
            Near((float)typeof(PassCamera).GetField("distance", PrivateInstance).GetValue(view), configuredDistance, .001f, "Scroll changed distance.");
            Near(OrbitDistance(view), beforeDistance, .05f, "Free camera distance changed.");

            await PressTab(keyboard); Require(view.BossLocked, "Third Tab must enable lock.");
            flow.TogglePause(); await PressTab(keyboard);
            Require(view.BossLocked, "Pause must ignore Tab."); flow.TogglePause();
            typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, false); await Frames(3);
            Require(!view.BossLocked, "Leaving combat must release lock.");
            await PressTab(keyboard); Require(!view.BossLocked, "Tab outside combat must be ignored.");
            return new { passed = true, configuredDistance, orbitDistance = beforeDistance, lockedDistance,
                heldKeyTogglesOnce = true, mouseKeepsLock = true, followsMovingBoss = true,
                freeMouseRestored = true, scrollDoesNotZoom = true, pauseAndCombatExitPassed = true };
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
            if (originalMouse != null && originalMouse.added) originalMouse.MakeCurrent();
        }
    }

    private static float OrbitDistance(PassCamera view) => Vector3.Distance(view.transform.position,
        (Vector3)typeof(PassCamera).GetField("focus", PrivateInstance).GetValue(view));

    private static Vector2 TargetViewport(PassCamera view, PassBossPatternController boss)
    {
        Vector3 point = view.GetComponent<Camera>().WorldToViewportPoint(boss.Body.bounds.center);
        return new Vector2(point.x, point.y);
    }

    private static async Task PressTab(Keyboard keyboard)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); await Frames(3);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames(3);
    }

    private static async Task Frames(int count)
    {
        int target = Time.frameCount + count;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (Time.frameCount < target)
        {
            if (timeout.Elapsed.TotalSeconds > 8) throw new InvalidOperationException("Player loop stopped.");
            await Task.Delay(1);
        }
    }

    private static void Capture(Camera camera)
    {
        var original = camera.targetTexture;
        var previous = RenderTexture.active;
        var render = RenderTexture.GetTemporary(960, 540, 24);
        var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "Temp/CameraLockToggle~/Test_BossLockedView.png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = original; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(render); UnityEngine.Object.Destroy(texture);
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Near(float actual, float expected, float tolerance, string message)
    { Require(Mathf.Abs(actual - expected) <= tolerance, message + " " + actual + " != " + expected); }
}
