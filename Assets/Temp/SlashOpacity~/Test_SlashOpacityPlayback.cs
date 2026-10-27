using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_SlashOpacityPlayback
{
    private const string Folder = "Assets/Temp/SlashOpacity~/";

    public static object PrepareEditor()
    {
        SessionState.SetBool("SlashOpacity.PreviousRunInBackground", Application.runInBackground);
        Application.runInBackground = true;
        return new { previousRunInBackground = SessionState.GetBool("SlashOpacity.PreviousRunInBackground", false),
            autotick = SessionState.GetBool("Unity.Pipeline.AutoTick.Enabled", false),
            autotickInterval = SessionState.GetInt("Unity.Pipeline.AutoTick.IntervalMs", 16) };
    }

    public static object RestoreEditor()
    {
        Application.runInBackground = SessionState.GetBool("SlashOpacity.PreviousRunInBackground", false);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/FanAttackVfx/Materials/Test_M_CloudSpiralSlash_C.mat");
        return new { playing = EditorApplication.isPlaying, runInBackground = Application.runInBackground,
            sceneDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,
            slashOpacityMask = material.GetFloat("_UseRadialOpacity") };
    }

    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode required");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (game == null || effect == null) throw new Exception("Fan VFX test scene required");
        if (effect.Profile.windDesign != Test_FanAttackVfxProfile.WindDesign.C) throw new Exception("C design required");
        if (effect.Profile.attack2.angle != 175) throw new Exception("Attack 2 should use 175 degrees");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        game.View.enabled = false;
        var camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 44;
        camera.transform.position = game.Player.TransformPoint(new Vector3(-5.8f, 5.5f, -4.5f));
        camera.transform.LookAt(game.Player.TransformPoint(new Vector3(0, 1.05f, .55f)));
        int initialFrame = Time.frameCount;
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var records = new System.Collections.Generic.List<object>();
        float secondHeight = 0;
        try
        {
            foreach (int attack in new[] { 1, 2, 3 })
            {
                var sweep = effect.Profile.GetSweep(attack);
                float holdFrame = Mathf.Lerp(sweep.startFrame, sweep.endFrame, .72f);
                effect.PlayPreview(attack, holdFrame);
                double deadline = EditorApplication.timeSinceStartup + 12;
                do
                {
                    await Task.Delay(25);
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Attack preview timed out: " + attack);
                } while (!effect.PreviewPaused);
                if (!effect.IsSweepVisible) throw new Exception("Slash missing at attack " + attack);
                var renderer = effect.transform.Find("Test_C_Slash").GetComponent<Renderer>();
                if (renderer.sharedMaterial.GetFloat("_UseRadialOpacity") != 1) throw new Exception("Slash opacity disabled in playback");
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                float progress = Mathf.InverseLerp(sweep.startFrame, sweep.endFrame, effect.CurrentFrame);
                if (Mathf.Abs(block.GetFloat("_SweepHead") - sweep.motionProgress.Evaluate(progress)) > .002f)
                    throw new Exception("Original reveal timing was not restored: " + attack);
                float expectedFade = attack == 1 ? .26f : attack == 2 ? .22f : .28f;
                if (Mathf.Abs(sweep.fadeSeconds - expectedFade) > .001f)
                    throw new Exception("Original fade timing was not restored: " + attack);
                float relativeHeight = renderer.transform.position.y - game.Player.position.y;
                if (attack == 2) secondHeight = relativeHeight;
                if (attack == 3 && Mathf.Abs(relativeHeight - secondHeight) > .03f)
                    throw new Exception("Attack 3 does not match attack 2 height: " + relativeHeight + " vs " + secondHeight);
                bool horizontal = sweep.plane == Test_FanAttackVfxProfile.SweepPlane.Horizontal;
                if (block.GetFloat("_FitSweepArc") != (horizontal ? 1 : 0)
                    || Mathf.Abs(block.GetFloat("_SweepArcAngle") - sweep.angle) > .01f)
                    throw new Exception("Slash arc does not match hit settings for attack " + attack);
                if (Mathf.Abs(renderer.sharedMaterial.GetFloat("_InnerTransparency") - .25f) > .001f
                    || Mathf.Abs(renderer.sharedMaterial.GetFloat("_OuterTransparency") - .25f) > .001f
                    || renderer.sharedMaterial.GetFloat("_BaseTransparency") != 0)
                    throw new Exception("Slash transparency mismatch");
                if (Mathf.Abs(renderer.sharedMaterial.GetFloat("_InnerZoneRatio") - .5f) > .001f
                    || Mathf.Abs(renderer.sharedMaterial.GetFloat("_OuterEdgePixels") - 8) > .001f
                    || renderer.sharedMaterial.GetTexture("_OpacityZoneMap") == null)
                    throw new Exception("Inner half / 8px contour settings mismatch");
                SaveCamera(camera, "Slash_Attack" + attack + ".png");
                records.Add(new { attack, frame = effect.CurrentFrame, slashVisible = effect.IsSweepVisible,
                    radialOpacity = renderer.sharedMaterial.GetFloat("_UseRadialOpacity"), windMask = effect.Profile.cloudSpiral.windMaterial.GetFloat("_UseRadialOpacity"),
                    fittedArc = block.GetFloat("_FitSweepArc"), arcAngle = block.GetFloat("_SweepArcAngle"),
                    fadeSeconds = sweep.fadeSeconds, relativeHeight });
            }
            if (Time.frameCount <= initialFrame) throw new Exception("Game frames did not advance");
            var report = new { framesAdvanced = Time.frameCount - initialFrame, attacks = records };
            File.WriteAllText(Folder + "PlaybackValidation.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = previousAsync;
            effect.ResumePreview();
            UnityEngine.Object.FindFirstObjectByType<PassAnimationController>().PlayIdle();
        }
    }

    private static void SaveCamera(Camera camera, string name)
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
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
