using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_FirstSweepPreview
{
    public static object Begin()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (effect == null) throw new Exception("VFX test scene required");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        var movement = game.Player.GetComponent<PassPlayerMovement>();
        movement.InputEnabled = false;
        game.View.enabled = false;
        Camera camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 42f;
        camera.transform.position = game.Player.TransformPoint(new Vector3(5.4f, 3.5f, -3.2f));
        camera.transform.LookAt(game.Player.TransformPoint(new Vector3(0, 1.5f, .75f)));
        effect.PlayPreview(1, 28);
        return new { scene = effect.gameObject.scene.path, player = game.Player.position, camera = camera.transform.position };
    }

    public static object Before() => Capture("Test_FirstSweep_Before.png");
    public static object After() => Capture("Test_FirstSweep_After.png");

    public static object Comparison()
    {
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (!effect.PreviewPaused) throw new Exception("Paused preview required");
        Camera camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 46f;
        camera.transform.position = game.Player.TransformPoint(new Vector3(-5.8f, 5.1f, -3.4f));
        camera.transform.LookAt(game.Player.TransformPoint(new Vector3(0, 1.5f, .65f)));
        var sweep = effect.transform.Find("Test_Attack1_Sweep").GetComponent<Test_FanSweepMesh>();
        var original = JsonUtility.FromJson<Test_FanAttackVfxProfile.Sweep>(JsonUtility.ToJson(effect.Profile.attack1));
        original.shapedRibbon = false;
        original.color = new Color(.55f, 1f, .88f, .8f); original.intensity = 1.5f;
        sweep.Rebuild(original, effect.Profile.sweepMaterial);
        sweep.Show(1, original.tailLength, original.color, 1, original.intensity);
        object before = Capture("Test_FirstSweep_Before.png");
        var revised = effect.Profile.attack1;
        sweep.Rebuild(revised, effect.Profile.sweepMaterial);
        sweep.Show(1, revised.tailLength, revised.color, 1, revised.intensity);
        object after = Capture("Test_FirstSweep_After.png");
        return new { before, after };
    }

    private static object Capture(string name)
    {
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (!effect.PreviewPaused || !effect.IsSweepVisible) throw new Exception("Wait for the paused full sweep");
        Camera camera = game.View.GetComponent<Camera>();
        var target = RenderTexture.GetTemporary(1024, 768, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        float aspect = camera.aspect;
        Texture2D pixels = null;
        string path = "Assets/Temp/FirstSweepRefinement~/" + name;
        try
        {
            camera.targetTexture = target; camera.aspect = 1024f / 768f;
            camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(1024, 768, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) UnityEngine.Object.Destroy(pixels);
        }
        return new { path, frame = effect.CurrentFrame, paused = effect.PreviewPaused, visible = effect.IsSweepVisible };
    }
}
