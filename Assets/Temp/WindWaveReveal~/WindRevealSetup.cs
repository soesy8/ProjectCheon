using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.FanAttackVfx;

public static class WindRevealSetup
{
    private const string TexturePath = "Assets/Arts/Effects/CloudSpiral/Textures/T_CloudSpiralWindWave_AStraight.png";
    private const string Folder = "Assets/Temp/WindWaveReveal~/";

    public static object Inspect()
    {
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (effect == null || effect.Profile.windDesign != Test_FanAttackVfxProfile.WindDesign.C)
            throw new Exception("Current cloud wind scene is required.");
        SessionState.SetBool("WindReveal.RunInBackground", Application.runInBackground);
        var report = new { playing = EditorApplication.isPlaying, scene = SceneManager.GetActiveScene().path,
            sceneDirty = SceneManager.GetActiveScene().isDirty, runInBackground = Application.runInBackground,
            texture = AssetDatabase.GetAssetPath(effect.Profile.cloudSpiral.windMaterial.mainTexture),
            wind1Radius = effect.Profile.attack1Wind.radius, wind1Stretch = effect.Profile.attack1Wind.forwardScale,
            wind3Radius = effect.Profile.wind.radius, angle = effect.Profile.wind.angle };
        File.WriteAllText(Folder + "Before.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit mode is required.");
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (effect == null || texture == null || texture.width > 1024 || texture.height > 1024)
            throw new Exception("Effect or valid straight texture is missing.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        if (!importer.DoesSourceTextureHaveAlpha() || !importer.alphaIsTransparency || importer.maxTextureSize != 1024)
            throw new Exception("Transparency and the 1024 texture limit must be preserved.");
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Vector4 rectangle;
        try
        {
            source.LoadImage(File.ReadAllBytes(TexturePath));
            var pixels = source.GetPixels32();
            int left = source.width, right = -1, bottom = source.height, top = -1;
            for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                    if (pixels[y * source.width + x].a >= 24)
                    {
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    }
            if (right <= left || top <= bottom) throw new Exception("Texture has no visible wind.");
            // 이미지 여백 때문에 8m 판정 안에 작은 그림만 들어가지 않도록 실제 원화 영역을 사용한다.
            rectangle = new Vector4(Mathf.Max(0, left - 1) / (float)source.width,
                Mathf.Max(0, bottom - 1) / (float)source.height,
                Mathf.Min(source.width, right + 2) / (float)source.width,
                Mathf.Min(source.height, top + 2) / (float)source.height);
        }
        finally { UnityEngine.Object.DestroyImmediate(source); }
        var material = effect.Profile.cloudSpiral.windMaterial;
        Undo.RecordObject(material, "곧은 바람 원화와 실제 영역 적용");
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetVector("_WindTextureRect", rectangle);
        material.SetFloat("_WindRevealSoftness", .025f);
        material.SetFloat("_UseWindMask", 0);
        material.SetFloat("_UseSweepMask", 0);
        material.SetFloat("_FitSweepArc", 0);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return new { texture = TexturePath, width = texture.width, height = texture.height,
            textureRect = new[] { rectangle.x, rectangle.y, rectangle.z, rectangle.w },
            shaderErrors = ShaderUtil.ShaderHasError(material.shader), sceneDirty = SceneManager.GetActiveScene().isDirty };
    }

    public static object PrepareEditor()
    {
        Application.runInBackground = true;
        return new { runInBackground = Application.runInBackground };
    }

    public static object RestoreEditor()
    {
        Application.runInBackground = SessionState.GetBool("WindReveal.RunInBackground", true);
        return new { playing = EditorApplication.isPlaying, scene = SceneManager.GetActiveScene().path,
            sceneDirty = SceneManager.GetActiveScene().isDirty, runInBackground = Application.runInBackground };
    }
}
