using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CloudSpiralPreviewRenderer
{
    public static object Main()
    {
        Scene original = SceneManager.GetActiveScene();
        var previewRoot = new GameObject("CloudSpiralTemporaryPreview");
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        RenderTexture target = null;
        try
        {
            var cameraObject = new GameObject("CloudSpiralPreviewCamera");
            cameraObject.transform.SetParent(previewRoot.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .07f, .12f, 1);
            camera.orthographic = true;
            camera.orthographicSize = 4.25f;
            camera.cullingMask = 1;
            camera.transform.SetPositionAndRotation(new Vector3(0, 13, -5), Quaternion.Euler(70, 0, 0));
            target = new RenderTexture(1024, 768, 24);
            target.Create();
            camera.targetTexture = target;

            var slash = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Effects/CloudSpiral/CloudSpiralSlash.prefab"));
            var wind = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Effects/CloudSpiral/CloudSpiralWind.prefab"));
            slash.layer = wind.layer = 0;
            slash.transform.SetParent(previewRoot.transform);
            wind.transform.SetParent(previewRoot.transform);
            slash.transform.position = new Vector3(-2.5f, 0, 0);
            wind.transform.position = new Vector3(2.2f, 0, -1.2f);
            for (int frame = 0; frame < 25; frame++)
            {
                float time = frame / 40f;
                slash.GetComponent<ParticleSystem>().Simulate(time, true, true, false);
                wind.GetComponent<ParticleSystem>().Simulate(time, true, true, false);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(1024, 768, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                image.Apply();
                File.WriteAllBytes("Assets/Temp/CloudSpiralDesign/Preview_" + frame.ToString("D2") + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.active = previous;
            }
            return "Rendered 25 frames; temporary objects removed without saving the active scene";
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            UnityEngine.Object.DestroyImmediate(previewRoot);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
