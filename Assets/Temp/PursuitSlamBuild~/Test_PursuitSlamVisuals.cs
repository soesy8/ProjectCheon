using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;

public static class Test_PursuitSlamVisuals
{
    public static async Task<object> Run()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        var boss = settings.MainBoss;
        var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var player = UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
        flow.BeginJourney(); typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
        player.InputEnabled = false; player.GetComponent<PassHealth>().Invulnerable = true;
        boss.WakeUp();
        typeof(PassBossPatternController).GetField("openingReadyAt", flags).SetValue(boss, float.PositiveInfinity);
        player.Teleport(boss.transform.position + boss.transform.forward * 7f, Quaternion.identity);
        var cameraObject = new GameObject("PursuitSlamPreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 45f; camera.nearClipPlane = .1f; camera.farClipPlane = 300f;
        camera.enabled = false;
        try
        {
            typeof(PassBossPatternController).GetMethod("BeginPursuitSlam", flags).Invoke(boss, null);
            await Task.Delay(160);
            Capture(camera, boss.transform.position + boss.transform.forward * 10f + boss.transform.right * 4f + Vector3.up * 8.9f,
                boss.transform.position + Vector3.up * 8.4f, "Test_NoseSteam.png");
            await Wait(() => boss.PursuitSlam.Stage == PassBossPursuitSlam.SlamStage.TrackingRecovery);
            await Task.Delay(100);
            camera.fieldOfView = 55f;
            Capture(camera, boss.transform.position + boss.transform.right * 12f + boss.transform.forward * 12f + Vector3.up * 10f,
                boss.transform.position + Vector3.up * 3f, "Test_TrackingLandingDust.png");
            await Wait(() => boss.PursuitSlam.Stage == PassBossPursuitSlam.SlamStage.Recovery);
            await Task.Delay(100);
            Capture(camera, boss.transform.position + boss.transform.right * 12f + boss.transform.forward * 12f + Vector3.up * 12f,
                boss.transform.position + Vector3.up, "Test_BackSlamDust.png");
            return new { nose = "Test_NoseSteam.png", tracking = "Test_TrackingLandingDust.png", slam = "Test_BackSlamDust.png" };
        }
        finally { UnityEngine.Object.Destroy(cameraObject); boss.ResetEncounter(); }
    }

    private static void Capture(Camera camera, Vector3 position, Vector3 target, string filename)
    {
        camera.transform.position = position; camera.transform.LookAt(target);
        var render = RenderTexture.GetTemporary(960, 720, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "Temp/PursuitSlamBuild~/" + filename), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(render); UnityEngine.Object.Destroy(texture);
        }
    }

    private static async Task Wait(Func<bool> condition)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed.TotalSeconds > 15) throw new InvalidOperationException("Visual capture phase timeout.");
            await Task.Delay(5);
        }
    }
}
