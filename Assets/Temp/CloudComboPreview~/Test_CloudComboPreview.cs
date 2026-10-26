using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_CloudComboPreview
{
    private const string Folder = "Assets/Temp/CloudComboPreview~/";

    public static object Open()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new Exception("Unsaved scene: " + scene.path);
        if (scene.path != "Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity")
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity");
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
    }

    private static Test_FanAttackVfx Prepare()
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
        SetCamera(false);
        return effect;
    }

    private static Camera SetCamera(bool wind)
    {
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = wind ? 47 : 46;
        Vector3 position = wind ? new Vector3(-7.2f, 8.3f, -6.5f) : new Vector3(-5.8f, 5.5f, -4.5f);
        Vector3 focus = wind ? new Vector3(0, .5f, 3.5f) : new Vector3(0, 1.05f, .55f);
        camera.transform.position = game.Player.TransformPoint(position);
        camera.transform.LookAt(game.Player.TransformPoint(focus));
        return camera;
    }

    public static async Task<object> CaptureAll()
    {
        var effect = Prepare();
        int initialFrame = Time.frameCount;
        await Hold(effect, 2, 34);
        string second = SaveCamera(SetCamera(false), "Test_CloudCombo_Attack2.png");
        await Hold(effect, 3, 40);
        string third = SaveCamera(SetCamera(false), "Test_CloudCombo_Attack3.png");
        await Hold(effect, 1, 34);
        effect.transform.Find("Test_Attack1_Sweep").GetComponent<Test_FanSweepMesh>().Hide();
        string firstWind = SaveCamera(SetCamera(false), "Test_CloudCombo_Wind1.png");
        await Hold(effect, 3, 36);
        effect.transform.Find("Test_Attack3_Sweep").GetComponent<Test_FanSweepMesh>().Hide();
        string thirdWind = SaveCamera(SetCamera(true), "Test_CloudCombo_Wind3.png");
        var shaders = new[] {effect.Profile.attack1SweepMaterial, effect.Profile.attack2SweepMaterial,
            effect.Profile.attack3SweepMaterial, effect.Profile.windMaterial, effect.Profile.windParticleMaterial};
        if (shaders.Any(material => ShaderUtil.ShaderHasError(material.shader))) throw new Exception("VFX shader errors");
        await Hold(effect, 3, 40);
        SetCamera(false);
        return new { second, third, firstWind, thirdWind, framesAdvanced = Time.frameCount - initialFrame,
            attack3Material = effect.transform.Find("Test_Attack3_Sweep").GetComponent<Renderer>().sharedMaterial.name,
            windMaterial = effect.transform.Find("Test_Attack3_WindSector").GetComponent<Renderer>().sharedMaterial.name,
            particles = effect.WindParticleCount, shaderErrors = false };
    }

    private static async Task Hold(Test_FanAttackVfx effect, int attack, float frame)
    {
        effect.PlayPreview(attack, frame);
        double deadline = EditorApplication.timeSinceStartup + 12;
        do
        {
            await Task.Delay(35);
            if (!EditorApplication.isPlaying || effect == null) throw new Exception("Preview interrupted");
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Attack did not reach preview frame: " + attack);
        } while (!effect.PreviewPaused);
        if (effect.CurrentAttack != attack) throw new Exception("Wrong preview attack");
    }

    public static async Task<object> Record()
    {
        var effect = Prepare();
        var records = new System.Collections.Generic.List<PlaybackReport>();
        foreach (int attack in new[] {2, 3})
        {
            SetCamera(attack == 3);
            effect.PlayPreview(attack);
            int initialFrame = Time.frameCount;
            int visible = 0;
            int windVisible = 0;
            int particlePeak = 0;
            float start = Time.realtimeSinceStartup;
            for (int index = 0; index < 32; index++)
            {
                await Task.Delay(30);
                if (!EditorApplication.isPlaying || effect == null) throw new Exception("Preview interrupted");
                if (effect.IsSweepVisible) visible++;
                var gust = effect.transform.Find("Test_Attack3_WindSector").GetComponent<Test_FanSweepMesh>();
                if (attack == 3 && gust.IsVisible) windVisible++;
                particlePeak = Mathf.Max(particlePeak, effect.WindParticleCount);
                SaveCamera(SetCamera(attack == 3), "Test_Attack" + attack + "_Frame" + index.ToString("D3") + ".png", 768, 576);
            }
            records.Add(new PlaybackReport { attack = attack, count = 32, visible = visible, windVisible = windVisible,
                particlePeak = particlePeak, elapsed = Time.realtimeSinceStartup - start, framesAdvanced = Time.frameCount - initialFrame });
        }
        if (records.Any(record => record.visible == 0 || record.framesAdvanced == 0)
            || records[1].windVisible == 0) throw new Exception("Missing live attack or wind frames");
        File.WriteAllText(Folder + "Test_PlaybackReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(records, Newtonsoft.Json.Formatting.Indented));
        await Hold(effect, 3, 40);
        SetCamera(true);
        return records;
    }

    private static string SaveCamera(Camera camera, string name, int width = 1024, int height = 768)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        float aspect = camera.aspect;
        Texture2D pixels = null;
        string path = Folder + name;
        try
        {
            camera.targetTexture = target; camera.aspect = (float)width / height;
            camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) UnityEngine.Object.Destroy(pixels);
        }
        return path;
    }

    [Serializable]
    private class PlaybackReport
    {
        public int attack, count, visible, windVisible, framesAdvanced, particlePeak;
        public float elapsed;
    }
}
