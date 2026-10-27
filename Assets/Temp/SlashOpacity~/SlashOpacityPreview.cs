using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Cheon.Tests.FanAttackVfx;

public static class SlashOpacityPreview
{
    private const string Root = "Assets/Test/FanAttackVfx/";
    private const string Folder = "Assets/Temp/SlashOpacity~/";

    public static object Main()
    {
        string shaderPath = Root + "Shaders/Test_S_CloudSpiral_C.shader";
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new Exception("Slash shader import failed: " + string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(message => message.message)));

        var slash = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Test_M_CloudSpiralSlash_C.mat");
        var wind = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Test_M_CloudSpiralWind_C.mat");
        Undo.RecordObject(slash, "공격 궤적 안팎 투명도와 러프 경계");
        var zoneMap = GenerateZoneMap();
        if (zoneMap == null) throw new Exception("Opacity zone map did not import");
        slash.SetTexture("_OpacityZoneMap", zoneMap);
        slash.SetFloat("_UseRadialOpacity", 1);
        slash.SetFloat("_BaseTransparency", 0);
        slash.SetFloat("_InnerTransparency", .25f);
        slash.SetFloat("_OuterTransparency", .25f);
        slash.SetFloat("_InnerZoneRatio", .5f);
        slash.SetFloat("_OuterEdgePixels", 8);
        slash.SetFloat("_InnerBlendWidth", .18f);
        slash.SetFloat("_EdgeBlendPixels", 3);
        slash.SetFloat("_InnerRoughness", .06f);
        slash.SetFloat("_EdgeRoughPixels", 2);
        var serialized = new SerializedObject(slash);
        var floats = serialized.FindProperty("m_SavedProperties.m_Floats");
        for (int index = floats.arraySize - 1; index >= 0; index--)
        {
            string property = floats.GetArrayElementAtIndex(index).FindPropertyRelative("first").stringValue;
            if (property == "_OpacityBoundary" || property == "_OpacityBlendWidth" || property == "_OpacityRoughness")
                floats.DeleteArrayElementAtIndex(index);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(slash);
        AssetDatabase.SaveAssetIfDirty(slash);
        AssetDatabase.ImportAsset(Root + "Materials/Test_M_CloudSpiralSlash_C.mat", ImportAssetOptions.ForceSynchronousImport);
        if (slash.GetFloat("_UseRadialOpacity") != 1) throw new Exception("Slash opacity setting did not persist");
        if (slash.GetTexture("_OpacityZoneMap") == null) throw new Exception("Opacity zone map reference did not persist");
        if (wind.GetFloat("_UseRadialOpacity") != 0) throw new Exception("Wind should keep its original opacity");
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(Root + "Settings/Test_FanAttackVfxProfile.asset");

        var previewScene = EditorSceneManager.NewPreviewScene();
        Material preview = null;
        Mesh mesh = null;
        Texture2D zoneProbe = null;
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            var cameraObject = new GameObject("SlashOpacityCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = previewScene;
            camera.transform.position = new Vector3(0, 0, -2);
            camera.orthographic = true;
            camera.orthographicSize = .51f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.13f, .13f, .13f, 1);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var item = new GameObject("SlashOpacityQuad", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(item, previewScene);
            mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0),
                new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            preview = new Material(slash);
            preview.SetFloat("_UseSweepMask", 0);
            preview.SetColor("_BaseColor", Color.white);
            item.GetComponent<MeshRenderer>().sharedMaterial = preview;
            preview.SetFloat("_UseRadialOpacity", 0);
            Capture(camera, "Slash_Before.png");
            preview.SetFloat("_UseRadialOpacity", 1);
            Capture(camera, "Slash_After.png");
            preview.SetFloat("_FitSweepArc", 1);
            preview.SetFloat("_SweepArcAngle", 175);
            Capture(camera, "Slash_Arc175.png");
            preview.SetFloat("_SweepArcAngle", 210);
            Capture(camera, "Slash_Arc210.png");
            preview.SetFloat("_FitSweepArc", 0);
            camera.backgroundColor = new Color(.65f, .65f, .65f, 1);
            Capture(camera, "Slash_After_Light.png");
            camera.backgroundColor = Color.black;
            preview.SetTexture("_BaseMap", Texture2D.whiteTexture);
            preview.SetFloat("_UseRadialOpacity", 0);
            preview.SetFloat("_FitSweepArc", 1);
            foreach (float angle in new[] { 175f, 210f })
            {
                preview.SetFloat("_SweepArcAngle", angle);
                Capture(camera, "Test_ArcMask" + angle + ".png", angle);
            }
            // 실제 GPU 출력으로 기본 0%, 안쪽/테두리 25%, 중첩 25%를 각각 확인한다.
            zoneProbe = new Texture2D(4, 1, TextureFormat.RGBA32, false, true);
            zoneProbe.filterMode = FilterMode.Point;
            zoneProbe.wrapMode = TextureWrapMode.Clamp;
            zoneProbe.SetPixels(new[] { new Color(0, 1, 0), new Color(1, 1, 0),
                new Color(1, 0, 0), new Color(0, 0, 0) });
            zoneProbe.Apply();
            preview.SetTexture("_OpacityZoneMap", zoneProbe);
            preview.SetFloat("_FitSweepArc", 0);
            preview.SetFloat("_UseRadialOpacity", 1);
            preview.SetFloat("_InnerRoughness", 0);
            preview.SetFloat("_EdgeRoughPixels", 0);
            Capture(camera, "Test_ZoneOpacity.png", -1, true);

            var report = new { shaderErrors = ShaderUtil.ShaderHasError(shader), slashOpacityMask = slash.GetFloat("_UseRadialOpacity"), innerTransparency = slash.GetFloat("_InnerTransparency"),
                baseTransparency = slash.GetFloat("_BaseTransparency"), outerTransparency = slash.GetFloat("_OuterTransparency"),
                innerRatio = slash.GetFloat("_InnerZoneRatio"), edgePixels = slash.GetFloat("_OuterEdgePixels"),
                mask = AssetDatabase.GetAssetPath(slash.GetTexture("_OpacityZoneMap")), windOpacityMask = wind.GetFloat("_UseRadialOpacity"),
                scene = SceneManager.GetActiveScene().path, sceneDirty = SceneManager.GetActiveScene().isDirty,
                attack2Angle = profile.attack2.angle, gpuArcMasksValidated = new[] { 175, 210 },
                gpuTransparencyProbes = "inner=.75, base=1, edge=.75, overlap=.75" };
            File.WriteAllText(Folder + "Validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = previousAsync;
            EditorSceneManager.ClosePreviewScene(previewScene);
            if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (zoneProbe != null) UnityEngine.Object.DestroyImmediate(zoneProbe);
        }
    }

    private static Texture2D GenerateZoneMap()
    {
        const string art = "Assets/Arts/Effects/CloudSpiral/Textures/";
        const string path = art + "T_CloudSpiralSlashOpacityMask.png";
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        source.LoadImage(File.ReadAllBytes(art + "T_CloudSpiralSlash.png"));
        int width = source.width, height = source.height;
        if (width > 1024 || height > 1024) throw new Exception("Source exceeds texture size limit");
        var pixels = source.GetPixels32();
        var solid = new bool[pixels.Length];
        var inner = new float[pixels.Length];
        var outer = new float[pixels.Length];
        for (int index = 0; index < pixels.Length; index++)
        {
            solid[index] = pixels[index].a >= 128;
            inner[index] = outer[index] = width + height;
        }
        int[] offsetX = { -1, 1, 0, 0 }, offsetY = { 0, 0, -1, 1 };
        int innerSeeds = 0, outerSeeds = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (!solid[index]) continue;
                for (int side = 0; side < 4; side++)
                {
                    int adjacentX = x + offsetX[side], adjacentY = y + offsetY[side];
                    if (adjacentX >= 0 && adjacentX < width && adjacentY >= 0 && adjacentY < height
                        && solid[adjacentY * width + adjacentX]) continue;
                    float radial = (x - (width - 1) * .5f) * offsetX[side]
                        + (y - (height - 1) * .5f) * offsetY[side];
                    if (radial < 0) { inner[index] = 0; innerSeeds++; }
                    else { outer[index] = 0; outerSeeds++; }
                }
            }
        if (innerSeeds == 0 || outerSeeds == 0) throw new Exception("Missing contour seeds");
        PropagateDistance(inner, width, height);
        PropagateDistance(outer, width, height);
        var maskPixels = new Color32[pixels.Length];
        for (int index = 0; index < pixels.Length; index++)
        {
            // 안팎 두 윤곽까지의 거리 비율로 원화 두께의 절반을 구한다.
            float total = inner[index] + outer[index];
            float across = total > 0 ? inner[index] / total : .5f;
            float distance = Mathf.Min(inner[index], outer[index]) + .5f;
            maskPixels[index] = new Color32((byte)Mathf.RoundToInt(across * 255),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(distance / 32) * 255), 0, 255);
        }
        var mask = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        mask.SetPixels32(maskPixels);
        mask.Apply();
        File.WriteAllBytes(path, mask.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(mask);
        UnityEngine.Object.DestroyImmediate(source);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }
        importer.userData = "R: inner-to-outer thickness ratio; G: contour distance in source pixels / 32";
        var serializedImporter = new SerializedObject(importer);
        var legacyMax = serializedImporter.FindProperty("m_MaxTextureSize");
        if (legacyMax != null)
        {
            legacyMax.intValue = 1024;
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void PropagateDistance(float[] distance, int width, int height)
    {
        const float diagonal = 1.41421356f;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float value = distance[index];
                if (x > 0) value = Mathf.Min(value, distance[index - 1] + 1);
                if (y > 0)
                {
                    value = Mathf.Min(value, distance[index - width] + 1);
                    if (x > 0) value = Mathf.Min(value, distance[index - width - 1] + diagonal);
                    if (x + 1 < width) value = Mathf.Min(value, distance[index - width + 1] + diagonal);
                }
                distance[index] = value;
            }
        for (int y = height - 1; y >= 0; y--)
            for (int x = width - 1; x >= 0; x--)
            {
                int index = y * width + x;
                float value = distance[index];
                if (x + 1 < width) value = Mathf.Min(value, distance[index + 1] + 1);
                if (y + 1 < height)
                {
                    value = Mathf.Min(value, distance[index + width] + 1);
                    if (x > 0) value = Mathf.Min(value, distance[index + width - 1] + diagonal);
                    if (x + 1 < width) value = Mathf.Min(value, distance[index + width + 1] + diagonal);
                }
                distance[index] = value;
            }
    }

    private static void Capture(Camera camera, string name, float validateArc = -1, bool validateZones = false)
    {
        var target = RenderTexture.GetTemporary(1024, 1024, 24, RenderTextureFormat.ARGB32,
            validateZones ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            image.Apply();
            if (validateArc > 0) ValidateArcPixels(image, validateArc);
            if (validateZones)
            {
                float[] expected = { .75f, 1, .75f, .75f };
                for (int index = 0; index < expected.Length; index++)
                {
                    float worldX = (index + .5f) / 4 - .5f;
                    int x = Mathf.RoundToInt((worldX / 1.02f + .5f) * 1024);
                    float actual = image.GetPixel(x, 512).r;
                    if (Mathf.Abs(actual - expected[index]) > .015f)
                        throw new Exception("GPU transparency probe " + index + ": expected " + expected[index] + ", got " + actual);
                }
            }
            File.WriteAllBytes(Folder + name, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void ValidateArcPixels(Texture2D image, float angle)
    {
        int inside = 0, outside = 0;
        for (int y = 4; y < 1024; y += 8)
            for (int x = 4; x < 1024; x += 8)
            {
                var position = new Vector2(((x + .5f) / 1024 - .5f) * 1.02f, ((y + .5f) / 1024 - .5f) * 1.02f);
                float radius = position.magnitude;
                float degrees = Mathf.Abs(Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg);
                bool forbidden = radius > .502f || radius > .1f && degrees > angle * .5f + .5f;
                bool solid = radius > .2f && radius < .45f && degrees < angle * .5f - 5;
                float value = image.GetPixel(x, y).maxColorComponent;
                if (forbidden)
                {
                    outside++;
                    if (value > .01f) throw new Exception("GPU arc extends beyond " + angle + " degrees at " + position);
                }
                if (solid)
                {
                    inside++;
                    if (value < .98f) throw new Exception("GPU arc missing inside " + angle + " degrees at " + position);
                }
            }
        if (inside < 100 || outside < 100) throw new Exception("Insufficient GPU arc samples");
    }
}
