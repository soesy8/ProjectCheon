using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_CloudComboSetup
{
    private const string Root = "Assets/Test/FanAttackVfx/";
    private const string Folder = "Assets/Temp/CloudComboPreview~/";

    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(Root + "Settings/Test_FanAttackVfxProfile.asset");
        string before = JsonUtility.ToJson(profile, true);
        if (!File.Exists(Folder + "Test_ProfileBefore.json")) File.WriteAllText(Folder + "Test_ProfileBefore.json", before);
        string first = JsonUtility.ToJson(profile.attack1);
        string dust = JsonUtility.ToJson(profile.dust);
        string approvedMaterial = AssetDatabase.GetAssetPath(profile.attack1SweepMaterial);
        byte[] firstMaterial = File.ReadAllBytes(approvedMaterial);

        var sweepShader = RequireShader("CloudWindSweep", "Test_S_CloudWindSweep.shader");
        var gustShader = RequireShader("CloudWindGust", "Test_S_CloudWindGust.shader");
        var particleShader = RequireShader("CloudWindParticle", "Test_S_CloudWindParticle.shader");
        profile.attack2SweepMaterial = CreateMaterial("Test_M_CloudWindSweep2", sweepShader);
        profile.attack3SweepMaterial = CreateMaterial("Test_M_CloudWindSweep3", sweepShader);
        ConfigureSweepMaterial(profile.attack2SweepMaterial, .85f, 1.04f, .010f);
        ConfigureSweepMaterial(profile.attack3SweepMaterial, 1.12f, 1.14f, .012f);
        profile.attack2.color = new Color(.13f, .77f, .72f, .95f);
        profile.attack2.intensity = 1.2f;
        profile.attack2.tailLength = .86f;
        profile.attack2.fadeSeconds = .22f;
        profile.attack3.color = new Color(.17f, .82f, .75f, .95f);
        profile.attack3.intensity = 1.2f;
        profile.attack3.tailLength = .94f;
        profile.attack3.fadeSeconds = .28f;

        profile.windMaterial = CreateMaterial("Test_M_CloudWindGust", gustShader);
        profile.windMaterial.SetColor("_EdgeColor", new Color(.68f, 1, .92f, 1));
        profile.windMaterial.SetColor("_ShadowColor", new Color(.025f, .36f, .42f, 1));
        profile.windMaterial.SetFloat("_CloudScale", 1);
        profile.windMaterial.SetFloat("_OutlineWidth", .012f);
        profile.windParticleMaterial = CreateMaterial("Test_M_CloudWindParticle", particleShader);
        profile.windParticleMaterial.SetColor("_EdgeColor", new Color(.68f, 1, .92f, 1));
        profile.windParticleMaterial.SetFloat("_Intensity", 1.15f);
        ConfigureWind(profile.attack1Wind, 20, new Vector2(.4f, .75f), .24f);
        ConfigureWind(profile.wind, 32, new Vector2(.55f, 1.0f), .32f);
        profile.NotifyChanged();

        if (first != JsonUtility.ToJson(profile.attack1) || dust != JsonUtility.ToJson(profile.dust)
            || !firstMaterial.SequenceEqual(File.ReadAllBytes(approvedMaterial))) throw new Exception("Approved first attack or ground dust changed");
        VerifyCombatSettings(before, profile);
        foreach (var material in new[] { profile.attack2SweepMaterial, profile.attack3SweepMaterial, profile.windMaterial, profile.windParticleMaterial })
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        var report = new Report { firstAttackUnchanged = true, combatSettingsUnchanged = true,
            groundDustUnchanged = true, shaderErrors = false };
        File.WriteAllText(Folder + "Test_SetupReport.json", JsonUtility.ToJson(report, true));
        return new { profile = AssetDatabase.GetAssetPath(profile), report,
            materials = new[] { profile.attack2SweepMaterial, profile.attack3SweepMaterial, profile.windMaterial, profile.windParticleMaterial }.Select(AssetDatabase.GetAssetPath).ToArray() };
    }

    private static void ConfigureWind(Test_FanAttackVfxProfile.Wind wind, float rate, Vector2 size, float fade)
    {
        wind.color = new Color(.16f, .76f, .70f, .88f);
        wind.intensity = 1.2f;
        wind.particlesPerSecond = rate;
        wind.size = size;
        wind.fadeSeconds = fade;
    }

    private static void ConfigureSweepMaterial(Material material, float width, float cloud, float outline)
    {
        material.SetColor("_EdgeColor", new Color(.68f, 1, .92f, 1));
        material.SetColor("_ShadowColor", new Color(.025f, .36f, .42f, 1));
        material.SetFloat("_RibbonWidth", width);
        material.SetFloat("_CloudScale", cloud);
        material.SetFloat("_OutlineWidth", outline);
    }

    private static Material CreateMaterial(string name, Shader shader)
    {
        string path = Root + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        return material;
    }

    private static Shader RequireShader(string name, string file)
    {
        AssetDatabase.ImportAsset(Root + "Shaders/" + file, ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Cheon/Tests/FanAttack/" + name);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader errors: " + name);
        return shader;
    }

    private static void VerifyCombatSettings(string json, Test_FanAttackVfxProfile profile)
    {
        var snapshot = ScriptableObject.CreateInstance<Test_FanAttackVfxProfile>();
        try
        {
            JsonUtility.FromJsonOverwrite(json, snapshot);
            for (int attack = 1; attack <= 3; attack++)
            {
                var original = snapshot.GetSweep(attack);
                var revised = profile.GetSweep(attack);
                original.color = revised.color;
                original.intensity = revised.intensity;
                original.tailLength = revised.tailLength;
                original.fadeSeconds = revised.fadeSeconds;
                if (JsonUtility.ToJson(original) != JsonUtility.ToJson(revised)) throw new Exception("Attack geometry or timing changed: " + attack);
            }
            foreach (int attack in new[] { 1, 3 })
            {
                var original = snapshot.GetWind(attack);
                var revised = profile.GetWind(attack);
                original.color = revised.color;
                original.intensity = revised.intensity;
                original.particlesPerSecond = revised.particlesPerSecond;
                original.size = revised.size;
                original.fadeSeconds = revised.fadeSeconds;
                if (JsonUtility.ToJson(original) != JsonUtility.ToJson(revised)) throw new Exception("Wind hit range or travel changed: " + attack);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(snapshot); }
    }

    [Serializable]
    private class Report
    {
        public bool firstAttackUnchanged, combatSettingsUnchanged, groundDustUnchanged, shaderErrors;
    }
}
