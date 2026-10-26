using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_CloudWindPreview
{
    private const string Folder = "Assets/Temp/CloudWindPreview~/";

    public static object Open()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new Exception("Unsaved scene: " + scene.path);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity");
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
    }

    public static object Begin()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (game == null || effect == null) throw new Exception("Fan VFX test scene required");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        game.View.enabled = false;
        Camera camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 43;
        camera.transform.position = game.Player.TransformPoint(new Vector3(-5.8f, 3.6f, -3.4f));
        camera.transform.LookAt(game.Player.TransformPoint(new Vector3(0, 1.65f, 0.65f)));
        effect.PlayPreview(1, 28);
        return new { scene = effect.gameObject.scene.path, player = game.Player.position };
    }

    public static object Before() => Capture("Test_CloudWind_Before.png");
    public static object After() => Capture("Test_CloudWind_After.png");

    public static object Comparison()
    {
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (!effect.PreviewPaused) throw new Exception("Held frame required");
        var original = ScriptableObject.CreateInstance<Test_FanAttackVfxProfile>();
        JsonUtility.FromJsonOverwrite(File.ReadAllText(Folder + "Test_ProfileBefore.json"), original);
        var sweep = effect.transform.Find("Test_Attack1_Sweep").GetComponent<Test_FanSweepMesh>();
        sweep.Rebuild(original.attack1, effect.Profile.sweepMaterial);
        sweep.Show(1, original.attack1.tailLength, original.attack1.color, 1, original.attack1.intensity);
        Before();
        sweep.Rebuild(effect.Profile.attack1, effect.Profile.attack1SweepMaterial);
        sweep.Show(1, effect.Profile.attack1.tailLength, effect.Profile.attack1.color, 1, effect.Profile.attack1.intensity);
        After();
        original.attack1.color = effect.Profile.attack1.color;
        original.attack1.intensity = effect.Profile.attack1.intensity;
        original.attack1.fadeSeconds = effect.Profile.attack1.fadeSeconds;
        bool shapeAndTimingUnchanged = JsonUtility.ToJson(original.attack1) == JsonUtility.ToJson(effect.Profile.attack1);
        bool otherAttacksUnchanged = JsonUtility.ToJson(original.attack2) == JsonUtility.ToJson(effect.Profile.attack2)
            && JsonUtility.ToJson(original.attack3) == JsonUtility.ToJson(effect.Profile.attack3);
        UnityEngine.Object.Destroy(original);
        if (!shapeAndTimingUnchanged || !otherAttacksUnchanged) throw new Exception("Unrelated attack settings changed");
        return new { shapeAndTimingUnchanged, otherAttacksUnchanged, effect.CurrentFrame,
            shaderErrors = ShaderUtil.ShaderHasError(effect.Profile.attack1SweepMaterial.shader) };
    }

    public static async Task<object> Record()
    {
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        effect.PlayPreview(1);
        float start = Time.realtimeSinceStartup;
        int initialFrame = Time.frameCount;
        int count = 0;
        int visible = 0;
        float previous = -1;
        for (int index = 0; index < 45; index++)
        {
            await Task.Delay(45);
            if (!EditorApplication.isPlaying || effect == null) throw new Exception("Preview scene changed during capture");
            if (effect.IsSweepVisible) visible++;
            previous = effect.CurrentFrame;
            SaveCamera(game.View.GetComponent<Camera>(), "Test_CloudWind_Frame" + count.ToString("D3") + ".png");
            count++;
        }
        var result = new { count, visible, elapsed = Time.realtimeSinceStartup - start,
            framesAdvanced = Time.frameCount - initialFrame, finalAttackFrame = previous };
        File.WriteAllText(Folder + "Test_PlaybackReport.json", JsonUtility.ToJson(new PlaybackReport
            { count = count, visible = visible, framesAdvanced = Time.frameCount - initialFrame }, true));
        effect.PlayPreview(1, 28);
        return result;
    }

    [Serializable]
    private class PlaybackReport
    {
        public int count, visible, framesAdvanced;
    }

    public static object Capture(string name)
    {
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (!effect.PreviewPaused) throw new Exception("Wait for the held attack frame");
        var path = SaveCamera(game.View.GetComponent<Camera>(), name);
        return new { path, effect.CurrentFrame, effect.PreviewPaused, effect.IsSweepVisible };
    }

    public static string SaveCamera(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1024, 768, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        float aspect = camera.aspect;
        Texture2D pixels = null;
        string path = Folder + name;
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1024f / 768;
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(1024, 768, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) UnityEngine.Object.Destroy(pixels);
        }
        return path;
    }
}
