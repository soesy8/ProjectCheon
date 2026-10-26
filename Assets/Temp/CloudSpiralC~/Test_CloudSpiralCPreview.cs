using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_CloudSpiralCPreview
{
    private const string Folder = "Assets/Temp/CloudSpiralC~/";

    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode required");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effects = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (game == null || effects == null) throw new Exception("Wrong test scene");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        game.View.enabled = false;
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        int initialFrame = Time.frameCount;
        var reports = new System.Collections.Generic.List<string>();
        try
        {
            foreach (int attack in new[] { 1, 2, 3 })
            {
                effects.Profile.windDesign = Test_FanAttackVfxProfile.WindDesign.C;
                effects.Profile.NotifyChanged();
                await Hold(effects, attack, attack == 1 ? 24 : attack == 2 ? 30 : 29);
                if (!effects.IsSweepVisible) throw new Exception("C sweep missing: " + attack);
                if (attack == 3 && effects.CloudWindBranchCount != 5) throw new Exception("C third wind branch count");
                var camera = SetCamera(game, attack);
                Capture(camera, "Test_C_Attack" + attack + ".png");
                reports.Add("C attack " + attack + ": sweep visible, wind branches=" + effects.CloudWindBranchCount);
            }
            await Hold(effects, 1, 34);
            if (effects.CloudWindBranchCount != 3) throw new Exception("C first wind branch count");
            Capture(SetCamera(game, 1), "Test_C_Attack1Wind.png");
            reports.Add("C first wind: three visible branches and frame pause respected");

            foreach (var design in new[] { Test_FanAttackVfxProfile.WindDesign.A, Test_FanAttackVfxProfile.WindDesign.B })
            {
                effects.Profile.windDesign = design;
                effects.Profile.NotifyChanged();
                await Hold(effects, 3, 29);
                var wind = effects.transform.Find("Test_Attack3_WindSector").GetComponent<Test_FanSweepMesh>();
                if (!wind.IsVisible || effects.CloudWindBranchCount != 0)
                    throw new Exception(design + " comparison has missing wind or leftover C effects");
                if (wind.GetComponent<Renderer>().sharedMaterial != effects.Profile.GetWindMaterial())
                    throw new Exception(design + " material mismatch");
                reports.Add(design + ": original wind material visible; C hidden");
            }
            effects.Profile.windDesign = Test_FanAttackVfxProfile.WindDesign.C;
            effects.Profile.NotifyChanged();
            effects.ResumePreview();
            UnityEngine.Object.FindFirstObjectByType<PassAnimationController>().PlayIdle();
            await Task.Delay(180);
            if (effects.CloudWindBranchCount != 0 || effects.IsSweepVisible) throw new Exception("C idle cleanup failed");
            if (Time.frameCount <= initialFrame) throw new Exception("Game frames did not advance");
            reports.Add("Idle cleanup: all C renderers hidden");
            reports.Add("Frames advanced: " + (Time.frameCount - initialFrame));
            File.WriteAllLines(Folder + "Test_PlaybackReport.txt", reports);
            return string.Join("; ", reports);
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            if (effects != null) effects.ResumePreview();
        }
    }

    private static async Task Hold(Test_FanAttackVfx effects, int attack, float frame)
    {
        effects.PlayPreview(attack, frame);
        double deadline = EditorApplication.timeSinceStartup + 15;
        do
        {
            await Task.Delay(30);
            if (!EditorApplication.isPlaying || effects == null) throw new Exception("Preview interrupted");
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Attack frame timeout: " + attack);
        } while (!effects.PreviewPaused);
        if (effects.CurrentAttack != attack) throw new Exception("Wrong attack at held frame");
    }

    private static Camera SetCamera(PassGameFlow game, int attack)
    {
        var camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = attack == 3 ? 47 : 44;
        Vector3 position = attack == 1 ? new Vector3(-6f, 6.5f, -5f)
            : attack == 2 ? new Vector3(-5.5f, 6.2f, -5) : new Vector3(-7.2f, 8.3f, -6.5f);
        Vector3 focus = attack == 1 ? new Vector3(0, .8f, 1.5f)
            : attack == 2 ? new Vector3(0, .8f, 1) : new Vector3(0, .5f, 3.5f);
        camera.transform.position = game.Player.TransformPoint(position);
        camera.transform.LookAt(game.Player.TransformPoint(focus));
        return camera;
    }

    private static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1024, 768, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        float aspect = camera.aspect;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target;
            camera.aspect = 4f / 3f;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image = new Texture2D(1024, 768, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
            image.Apply();
            File.WriteAllBytes(Folder + name, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            camera.aspect = aspect;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            if (image != null) UnityEngine.Object.Destroy(image);
        }
    }
}
