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

public static class WindRevealPlayback
{
    private const string Folder = "Assets/Temp/WindWaveReveal~/";
    private const string TexturePath = "Assets/Arts/Effects/CloudSpiral/Textures/T_CloudSpiralWindWave_AStraight.png";

    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode required.");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var control = UnityEngine.Object.FindFirstObjectByType<PassAnimationController>();
        if (game == null || effect == null || control == null) throw new Exception("Fan VFX scene required.");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        game.View.enabled = false;
        var camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 44;
        camera.transform.position = game.Player.TransformPoint(new Vector3(-8, 10, -7));
        camera.transform.LookAt(game.Player.TransformPoint(new Vector3(0, .5f, 3)));
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        int initialFrame = Time.frameCount;
        var stages = new List<object>();
        try
        {
            foreach (int attack in new[] { 1, 3 })
            {
                var wind = effect.Profile.GetWind(attack);
                float previousHead = -1;
                foreach (float fraction in new[] { .10f, .35f, .75f })
                {
                    effect.PlayPreview(attack, Mathf.Lerp(wind.startFrame, wind.endFrame, fraction));
                    await WaitForPause(effect);
                    var renderers = effect.GetComponentsInChildren<MeshRenderer>(true)
                        .Where(item => item.name.StartsWith("Test_C_Wind")).ToArray();
                    if (renderers.Length != 1 || effect.CloudWindBranchCount != 1 || effect.WindParticleCount != 0)
                        throw new Exception("Wind is missing or duplicated.");
                    var renderer = renderers[0];
                    if (AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture) != TexturePath)
                        throw new Exception("Straight texture was not applied.");
                    if (ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)) throw new Exception("Shader error.");
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    float head = block.GetFloat("_WindHead");
                    if (block.GetFloat("_UseWindMask") != 1 || head <= previousHead)
                        throw new Exception("Wind is not being revealed progressively.");
                    previousHead = head;
                    Vector3 origin; Quaternion facing;
                    if (!effect.TryGetWindPose(out origin, out facing)) throw new Exception("Missing anchored origin.");
                    if (Vector3.Distance(renderer.transform.position, origin) > .001f
                        || Quaternion.Angle(renderer.transform.rotation, facing) > .01f
                        || Vector3.Distance(renderer.transform.localScale, Vector3.one) > .001f)
                        throw new Exception("Wind geometry is moving or resizing instead of revealing.");
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    float maximumRadius = 0, maximumAngle = 0, nearestRadius = float.PositiveInfinity;
                    foreach (var vertex in mesh.vertices)
                    {
                        var local = new Vector2(vertex.x, vertex.z / wind.forwardScale);
                        maximumRadius = Mathf.Max(maximumRadius, local.magnitude);
                        nearestRadius = Mathf.Min(nearestRadius, local.magnitude);
                        maximumAngle = Mathf.Max(maximumAngle, Mathf.Abs(Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg));
                    }
                    if (Mathf.Abs(maximumRadius - wind.radius) > .002f
                        || Mathf.Abs(nearestRadius - wind.startRadius) > .002f
                        || Mathf.Abs(maximumAngle - wind.angle * .5f) > .002f)
                        throw new Exception("The image footprint does not fit the attack sector.");
                    int percent = Mathf.RoundToInt(fraction * 100);
                    var probe = ProbeWind(renderer, wind, origin, facing, head, "Top_Attack" + attack + "_" + percent + ".png");
                    var image = Capture(camera, 1024, 768);
                    try { File.WriteAllBytes(Folder + "Game_Attack" + attack + "_" + percent + ".png", image.EncodeToPNG()); }
                    finally { UnityEngine.Object.DestroyImmediate(image); }
                    stages.Add(new { attack, fraction, frame = effect.CurrentFrame, head,
                        radius = maximumRadius, angle = maximumAngle * 2,
                        forwardReach = wind.radius * wind.forwardScale,
                        fullWidth = wind.radius * Mathf.Sin(wind.angle * .5f * Mathf.Deg2Rad) * 2,
                        visibleWind = effect.CloudWindBranchCount, probe });
                }
            }

            // 한 번의 전체 재생에서 플레이어가 움직여도 표시 면의 월드 위치가 고정되는지 확인한다.
            effect.PlayPreview(3);
            await Task.Delay(100);
            bool seen = false;
            Vector3 anchor = Vector3.zero;
            float maximumMovement = 0;
            double deadline = EditorApplication.timeSinceStartup + 12;
            while (true)
            {
                await Task.Delay(12);
                if (effect.CloudWindBranchCount > 1) throw new Exception("Duplicate wind during playback.");
                if (effect.CloudWindBranchCount == 1)
                {
                    var position = effect.transform.Find("Test_C_WindWave").position;
                    if (!seen) anchor = position;
                    maximumMovement = Mathf.Max(maximumMovement, Vector3.Distance(anchor, position));
                    seen = true;
                }
                if (seen && effect.CloudWindBranchCount == 0) break;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Wind did not complete.");
            }
            if (maximumMovement > .001f) throw new Exception("The wave still flies forward: " + maximumMovement);
            effect.PlayPreview(3, 30);
            await WaitForPause(effect);
            effect.ResumePreview();
            control.PlayIdle();
            await Task.Delay(60);
            if (effect.CloudWindBranchCount != 0) throw new Exception("Wind remains after interruption.");
            if (Time.frameCount <= initialFrame) throw new Exception("Game did not advance.");
            var report = new { status = "passed", framesAdvanced = Time.frameCount - initialFrame,
                maximumWorldMovement = maximumMovement, visibleAfterFinishAndInterrupt = effect.CloudWindBranchCount,
                stages };
            File.WriteAllText(Folder + "PlaybackValidation.json", Newtonsoft.Json.JsonConvert.SerializeObject(report,
                Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = previousAsync;
            effect.ResumePreview();
            control.PlayIdle();
        }
    }

    private static async Task WaitForPause(Test_FanAttackVfx effect)
    {
        double deadline = EditorApplication.timeSinceStartup + 12;
        while (!effect.PreviewPaused)
        {
            await Task.Delay(12);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Preview did not advance.");
        }
    }

    private static object ProbeWind(MeshRenderer renderer, Test_FanAttackVfxProfile.Wind wind,
        Vector3 origin, Quaternion facing, float head, string filename)
    {
        var cameraObject = new GameObject("WindRevealProbe") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        int previousLayer = renderer.gameObject.layer;
        Texture2D image = null;
        try
        {
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 1;
            camera.orthographicSize = wind.radius * wind.forwardScale * .55f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            camera.transform.SetPositionAndRotation(origin + facing * new Vector3(0, 20, wind.radius * wind.forwardScale * .5f),
                facing * Quaternion.Euler(90, 0, 0));
            renderer.gameObject.layer = 30;
            image = Capture(camera, 512, 512);
            var pixels = image.GetPixels32();
            int visiblePixels = 0;
            float maxVisibleRadius = 0, maxVisibleAngle = 0;
            for (int y = 1; y < image.height; y += 2)
                for (int x = 1; x < image.width; x += 2)
                {
                    var pixel = pixels[y * image.width + x];
                    if (pixel.r + pixel.g + pixel.b < 40) continue;
                    Vector3 world = camera.ViewportToWorldPoint(new Vector3((x + .5f) / image.width, (y + .5f) / image.height, 20));
                    Vector3 local = Quaternion.Inverse(facing) * (world - origin);
                    local.z /= wind.forwardScale;
                    float radius = new Vector2(local.x, local.z).magnitude;
                    float angle = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
                    maxVisibleRadius = Mathf.Max(maxVisibleRadius, radius);
                    maxVisibleAngle = Mathf.Max(maxVisibleAngle, angle);
                    visiblePixels++;
                }
            File.WriteAllBytes(Folder + filename, image.EncodeToPNG());
            float hitFront = Mathf.Lerp(wind.startRadius, wind.radius, head);
            if (visiblePixels < 10) throw new Exception("The progressively revealed wind is invisible.");
            if (maxVisibleRadius > hitFront + .05f || maxVisibleAngle > wind.angle * .5f + .5f)
                throw new Exception("GPU wind extends past its current attack front: " + maxVisibleRadius + " / " + hitFront);
            return new { visiblePixels, maxVisibleRadius, maxVisibleAngle, hitFront };
        }
        finally
        {
            renderer.gameObject.layer = previousLayer;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static Texture2D Capture(Camera camera, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        float aspect = camera.aspect;
        try
        {
            camera.targetTexture = target;
            camera.aspect = width / (float)height;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            return image;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = aspect;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
