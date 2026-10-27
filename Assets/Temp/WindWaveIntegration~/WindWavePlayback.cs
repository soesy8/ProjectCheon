using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class WindWavePlayback
{
    private const string Folder = "Assets/Temp/WindWaveIntegration~/";
    private const string TexturePath = "Assets/Arts/Effects/CloudSpiral/Textures/T_CloudSpiralWindWave_A.png";

    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode is required.");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var control = UnityEngine.Object.FindFirstObjectByType<PassAnimationController>();
        if (game == null || effect == null || control == null) throw new Exception("Current fan VFX scene is required.");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        game.View.enabled = false;
        var camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 44;
        camera.transform.position = game.Player.TransformPoint(new Vector3(-7.5f, 8f, -6f));
        camera.transform.LookAt(game.Player.TransformPoint(new Vector3(0, .7f, 2.2f)));
        int initialFrame = Time.frameCount;
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var records = new List<object>();
        try
        {
            foreach (int attack in new[] { 1, 2, 3 })
            {
                var wind = effect.Profile.GetWind(attack);
                var sweep = effect.Profile.GetSweep(attack);
                float holdAt = wind != null ? Mathf.Lerp(wind.startFrame, wind.endFrame, .5f)
                    : Mathf.Lerp(sweep.startFrame, sweep.endFrame, .7f);
                effect.PlayPreview(attack, holdAt);
                double deadline = EditorApplication.timeSinceStartup + 12;
                while (!effect.PreviewPaused)
                {
                    await Task.Delay(20);
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Preview did not advance for attack " + attack);
                }
                var renderers = effect.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(item => item.name.StartsWith("Test_C_Wind")).ToArray();
                int visible = renderers.Count(item => item.enabled && item.gameObject.activeInHierarchy);
                int expected = wind != null ? 1 : 0;
                if (visible != expected || effect.CloudWindBranchCount != expected)
                    throw new Exception("Attack " + attack + " should show " + expected + " wind wave, got " + visible);
                if (renderers.Length != 1) throw new Exception("Wind renderer was duplicated: " + renderers.Length);
                if (effect.WindParticleCount != 0) throw new Exception("Legacy wind particles are still visible.");
                var renderer = renderers[0];
                if (AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture) != TexturePath)
                    throw new Exception("The approved A texture was not used.");
                if (ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)) throw new Exception("Wind shader failed.");
                if (wind != null)
                {
                    Vector3 origin; Quaternion rotation;
                    if (!effect.TryGetWindPose(out origin, out rotation)) throw new Exception("Missing wind emission pose.");
                    if (Vector3.Dot(renderer.transform.forward, rotation * Vector3.forward) < .999f)
                        throw new Exception("The single wind is not aimed forward.");
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    if (mesh.uv[1].x != 0 || mesh.vertices[1].z <= 0)
                        throw new Exception("The left-leading texture is facing backward.");
                }
                Capture(camera, "WindWave_Attack" + attack + ".png");
                records.Add(new { attack, frame = effect.CurrentFrame, visibleWind = visible,
                    windRenderers = renderers.Length, legacyWindParticles = effect.WindParticleCount,
                    texture = AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture),
                    worldPosition = renderer.transform.position, size = renderer.transform.localScale,
                    screenshot = Folder + "WindWave_Attack" + attack + ".png" });
            }

            // 사라지는 구간까지 실제 재생해 중복 생성과 잔류 원화가 없는지 확인한다.
            effect.ResumePreview();
            effect.PlayPreview(3);
            double fullDeadline = EditorApplication.timeSinceStartup + 12;
            bool seenWind = false;
            int peakVisible = 0;
            Vector3 firstPosition = Vector3.zero, lastPosition = Vector3.zero;
            while (true)
            {
                await Task.Delay(12);
                int visible = effect.CloudWindBranchCount;
                peakVisible = Mathf.Max(peakVisible, visible);
                if (visible > 1) throw new Exception("Multiple wind waves appeared during full playback.");
                if (visible == 1)
                {
                    var wave = effect.transform.Find("Test_C_WindWave");
                    if (!seenWind) firstPosition = wave.position;
                    lastPosition = wave.position;
                    seenWind = true;
                }
                if (seenWind && visible == 0) break;
                if (EditorApplication.timeSinceStartup > fullDeadline) throw new Exception("Wind did not appear and finish.");
            }
            float moved = Vector3.Distance(firstPosition, lastPosition);
            if (moved < .5f) throw new Exception("The wave did not travel forward.");
            if (Time.frameCount <= initialFrame) throw new Exception("Game frames did not advance.");
            var report = new { status = "passed", framesAdvanced = Time.frameCount - initialFrame,
                attacks = records, fullPlayback = new { peakVisible, finishedVisible = effect.CloudWindBranchCount,
                    forwardTravel = moved, windRenderers = effect.GetComponentsInChildren<MeshRenderer>(true)
                        .Count(item => item.name.StartsWith("Test_C_Wind")) } };
            File.WriteAllText(Folder + "PlaybackValidation.json", Newtonsoft.Json.JsonConvert.SerializeObject(report,
                Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore }));
            return report;
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = previousAsync;
            effect.ResumePreview();
            control.PlayIdle();
        }
    }

    private static void Capture(Camera camera, string file)
    {
        var target = RenderTexture.GetTemporary(1024, 768, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        float previousAspect = camera.aspect;
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
            File.WriteAllBytes(Folder + file, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
