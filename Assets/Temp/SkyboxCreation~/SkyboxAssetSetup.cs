using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class SkyboxAssetSetup
{
    public const string AssetFolder = "Assets/Arts/Environment/Cheon/Skyboxes";
    public const string TexturePath = AssetFolder + "/T_CheonMountainClouds_Panorama.png";
    public const string MaterialPath = AssetFolder + "/M_CheonMountainClouds_Skybox.mat";
    private const string PreviewFolder = "Assets/Temp/SkyboxCreation~";

    public static object InspectAndPrepare()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i))
            .Select(s => new { s.path, s.isLoaded, s.isDirty }).ToArray();
        var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(c => new { c.name, scene = c.gameObject.scene.path, c.enabled,
                position = Vector(c.transform.position), rotation = Vector(c.transform.eulerAngles),
                c.fieldOfView, clearFlags = c.clearFlags.ToString(), c.farClipPlane }).ToArray();
        var shader = Shader.Find("Skybox/Panoramic");
        if (shader == null) throw new InvalidOperationException("Skybox/Panoramic shader is unavailable.");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Arts/Environment/Cheon", "Skyboxes");
        var report = new { playing = EditorApplication.isPlaying, scenes, cameras,
            currentSkybox = RenderSettings.skybox != null ? AssetDatabase.GetAssetPath(RenderSettings.skybox) : null,
            fog = new[] { RenderSettings.fogColor.r, RenderSettings.fogColor.g, RenderSettings.fogColor.b },
            ambient = RenderSettings.ambientMode.ToString(),
            shader = shader.name, shaderProperties = Enumerable.Range(0, shader.GetPropertyCount())
                .Select(i => new { name = shader.GetPropertyName(i), type = shader.GetPropertyType(i).ToString() }).ToArray() };
        File.WriteAllText(PreviewFolder + "/SceneInspection.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return JObject.FromObject(report);
    }

    public static object ImportAndPreview()
    {
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 1;
        // 파노라마의 좌우는 반복하고 극점 방향은 고정해 구면 경계의 번짐을 줄인다.
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = TextureWrapMode.Clamp;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            var setting = importer.GetPlatformTextureSettings(platform);
            setting.name = platform;
            setting.maxTextureSize = 1024;
            setting.overridden = true;
            setting.format = TextureImporterFormat.RGB24;
            importer.SetPlatformTextureSettings(setting);
        }
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture.width != 1024 || texture.height != 512)
            throw new InvalidOperationException("The final texture must be 1024 x 512.");
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            throw new InvalidOperationException("Refusing to overwrite an existing material.");
        var material = new Material(Shader.Find("Skybox/Panoramic"));
        material.name = "M_CheonMountainClouds_Skybox";
        material.SetTexture("_MainTex", texture);
        material.SetColor("_Tint", Color.gray);
        material.SetFloat("_Exposure", 1f);
        material.SetFloat("_Rotation", 0f);
        material.SetFloat("_Mapping", 1f);
        material.SetFloat("_ImageType", 0f);
        material.SetFloat("_Layout", 0f);
        material.SetFloat("_MirrorOnBack", 0f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssetIfDirty(material);
        var report = new { texture = TexturePath, texture.width, texture.height,
            material = MaterialPath, shader = material.shader.name,
            textureGuid = AssetDatabase.AssetPathToGUID(TexturePath),
            materialGuid = AssetDatabase.AssetPathToGUID(MaterialPath),
            preview = RenderPreview(material) };
        File.WriteAllText(PreviewFolder + "/ImportVerification.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return JObject.FromObject(report);
    }

    private static object RenderPreview(Material material)
    {
        var previewScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("SkyboxPreviewCamera");
        SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 75f;
        camera.aspect = 2f;
        camera.cullingMask = 0;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.scene = previewScene;
        cameraObject.AddComponent<Skybox>().material = material;
        var target = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32);
        var previousTarget = RenderTexture.active;
        var frame = new Texture2D(1024, 512, TextureFormat.RGB24, false);
        var paths = new System.Collections.Generic.List<string>();
        try
        {
            camera.targetTexture = target;
            foreach (var view in new[] {
                new { name = "T_Preview_Forward", pitch = 0f, yaw = 0f },
                new { name = "T_Preview_Back", pitch = 0f, yaw = 180f },
                new { name = "T_Preview_Seam", pitch = 0f, yaw = 270f },
                new { name = "T_Preview_Up", pitch = -65f, yaw = 0f },
                new { name = "T_Preview_Down", pitch = 65f, yaw = 0f } })
            {
                camera.transform.rotation = Quaternion.Euler(view.pitch, view.yaw, 0f);
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, 1024, 512), 0, 0);
                frame.Apply();
                string path = PreviewFolder + "/" + view.name + ".png";
                File.WriteAllBytes(path, frame.EncodeToPNG());
                paths.Add(path);
            }
        }
        finally
        {
            RenderTexture.active = previousTarget;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(frame);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
        }
        return paths.ToArray();
    }

    private static float[] Vector(Vector3 value) => new[] { value.x, value.y, value.z };

    public static object FinalizeAndVerify()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        var serializedImporter = new SerializedObject(importer);
        var legacySize = serializedImporter.FindProperty("m_MaxTextureSize");
        if (legacySize != null)
        {
            legacySize.intValue = 1024;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var serializedMaterial = new SerializedObject(material);
        var invalidKeywords = serializedMaterial.FindProperty("m_InvalidKeywords");
        if (invalidKeywords != null)
        {
            invalidKeywords.arraySize = 0;
            serializedMaterial.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(material);
        }
        var report = new {
            importer.maxTextureSize,
            legacyMaxTextureSize = legacySize != null ? legacySize.intValue : -1,
            platforms = new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL" }
                .Select(name => new { name, maxSize = importer.GetPlatformTextureSettings(name).maxTextureSize }).ToArray(),
            previews = RenderPreview(material),
            sceneDirty = SceneManager.GetActiveScene().isDirty,
            shaderMessages = ShaderUtil.GetShaderMessages(material.shader).Select(message => message.message).ToArray()
        };
        File.WriteAllText(PreviewFolder + "/FinalVerification.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return JObject.FromObject(report);
    }
}
