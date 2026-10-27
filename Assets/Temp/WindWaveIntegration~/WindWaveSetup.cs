using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.FanAttackVfx;

public static class WindWaveSetup
{
    private const string Art = "Assets/Arts/Effects/CloudSpiral/";
    private const string ProfilePath = "Assets/Test/FanAttackVfx/Settings/Test_FanAttackVfxProfile.asset";
    private const string Folder = "Assets/Temp/WindWaveIntegration~/";

    public static object Inspect()
    {
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(ProfilePath);
        var scene = SceneManager.GetActiveScene();
        var report = new { playing = EditorApplication.isPlaying, scene = scene.path, dirty = scene.isDirty,
            effectFound = effect != null, profile = effect != null ? AssetDatabase.GetAssetPath(effect.Profile) : "",
            windTexture = AssetDatabase.GetAssetPath(profile.cloudSpiral.windMaterial.mainTexture),
            windLength = profile.cloudSpiral.windLength, runInBackground = Application.runInBackground,
            autoTick = SessionState.GetBool("Unity.Pipeline.AutoTick.Enabled", false),
            autoTickInterval = SessionState.GetInt("Unity.Pipeline.AutoTick.IntervalMs", 16) };
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "Before.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        SessionState.SetBool("WindWave.PreviousRunInBackground", Application.runInBackground);
        SessionState.SetBool("WindWave.PreviousAutoTick", report.autoTick);
        SessionState.SetInt("WindWave.PreviousAutoTickInterval", report.autoTickInterval);
        return report;
    }

    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode before changing assets.");
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(ProfilePath);
        if (profile == null || profile.windDesign != Test_FanAttackVfxProfile.WindDesign.C)
            throw new Exception("Current cloud VFX profile is required.");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Textures/T_CloudSpiralWindWave_A.png");
        if (texture == null || texture.width > 1024 || texture.height > 1024)
            throw new Exception("A wave texture is missing or exceeds the project resolution limit.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        if (!importer.DoesSourceTextureHaveAlpha() || !importer.alphaIsTransparency || importer.maxTextureSize != 1024)
            throw new Exception("A wave texture must keep its transparency and 1024 import limit.");

        string meshPath = Art + "Meshes/CloudSpiralWindWave_AQuad.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "CloudSpiralWindWave_AQuad" };
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        Undo.RecordObject(mesh, "바람 파도 원화 비율과 진행 방향");
        // 정사각형 PNG의 비율을 보존하고, 왼쪽 구름 머리를 로컬 +Z 정면에 맞춘다.
        mesh.vertices = new[] { new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f),
            new Vector3(-.5f, 0, .5f), new Vector3(-.5f, 0, -.5f) };
        mesh.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        EditorUtility.SetDirty(mesh);

        var material = profile.cloudSpiral.windMaterial;
        Undo.RecordObject(material, "추가타 바람을 A안 원화로 교체");
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetFloat("_UseSweepMask", 0);
        material.SetFloat("_FitSweepArc", 0);
        material.SetFloat("_UseRadialOpacity", 0);
        EditorUtility.SetDirty(material);
        Undo.RecordObject(profile, "단일 바람 파도 표시 설정");
        profile.cloudSpiral.windMesh = mesh;
        profile.cloudSpiral.windLength = 4.8f;
        profile.NotifyChanged();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(material);
        AssetDatabase.SaveAssetIfDirty(profile);
        return new { texture = AssetDatabase.GetAssetPath(texture), mesh = meshPath,
            material = AssetDatabase.GetAssetPath(material), windLength = profile.cloudSpiral.windLength,
            shaderErrors = ShaderUtil.ShaderHasError(material.shader), scene = SceneManager.GetActiveScene().path,
            sceneDirty = SceneManager.GetActiveScene().isDirty };
    }

    public static object PrepareEditor()
    {
        Application.runInBackground = true;
        return new { runInBackground = Application.runInBackground };
    }

    public static object RestoreEditor()
    {
        Application.runInBackground = SessionState.GetBool("WindWave.PreviousRunInBackground", false);
        return new { playing = EditorApplication.isPlaying, runInBackground = Application.runInBackground,
            previousAutoTick = SessionState.GetBool("WindWave.PreviousAutoTick", false),
            previousAutoTickInterval = SessionState.GetInt("WindWave.PreviousAutoTickInterval", 16),
            scene = SceneManager.GetActiveScene().path, sceneDirty = SceneManager.GetActiveScene().isDirty };
    }
}
