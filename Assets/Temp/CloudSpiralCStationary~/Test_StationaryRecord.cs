using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_StationaryRecord
{
    public static async Task<object> Main()
    {
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        if (!EditorApplication.isPlaying || effect == null || game == null) throw new Exception("Test play mode required");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        game.View.enabled = false;
        effect.Profile.windDesign = Test_FanAttackVfxProfile.WindDesign.C;
        effect.Profile.NotifyChanged();
        var camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 44;
        camera.transform.position = game.Player.TransformPoint(new Vector3(-6, 6.5f, -5));
        camera.transform.LookAt(game.Player.TransformPoint(new Vector3(0, .8f, 1.8f)));
        float previousScale = Time.timeScale;
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            Time.timeScale = .3f;
            effect.PlayPreview(1);
            double deadline = EditorApplication.timeSinceStartup + 12;
            while (effect.CurrentAttack != 1 || effect.CurrentFrame < 17)
            {
                await Task.Delay(15);
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Record start timeout");
            }
            for (int index = 0; index < 40; index++)
            {
                Capture(camera, index);
                await Task.Delay(35);
            }
            return "Recorded 40 actual attack frames at 0.3x playback";
        }
        finally
        {
            Time.timeScale = previousScale;
            ShaderUtil.allowAsyncCompilation = previousAsync;
            effect.ResumePreview();
        }
    }

    private static void Capture(Camera camera, int index)
    {
        var target = RenderTexture.GetTemporary(768, 576, 24, RenderTextureFormat.ARGB32);
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
            image = new Texture2D(768, 576, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 768, 576), 0, 0);
            image.Apply();
            File.WriteAllBytes("Assets/Temp/CloudSpiralCStationary~/Test_Frame" + index.ToString("D2") + ".png", image.EncodeToPNG());
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
