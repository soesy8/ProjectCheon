using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_WindDesignSetup
{
    private const string Root = "Assets/Test/FanAttackVfx/";
    private const string Folder = "Assets/Temp/WindDesignAB~/";

    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(Root + "Settings/Test_FanAttackVfxProfile.asset");
        string before = JsonUtility.ToJson(profile, true);
        if (!File.Exists(Folder + "Test_ProfileBefore.json")) File.WriteAllText(Folder + "Test_ProfileBefore.json", before);
        byte[] materialA = File.ReadAllBytes(AssetDatabase.GetAssetPath(profile.windMaterial));
        byte[] particlesA = File.ReadAllBytes(AssetDatabase.GetAssetPath(profile.windParticleMaterial));
        var gustShader = RequireShader("RollingWindGust", "Test_S_RollingWindGust.shader");
        var particleShader = RequireShader("RollingWindParticle", "Test_S_RollingWindParticle.shader");
        RequireShader("CloudWindGust", "Test_S_CloudWindGust.shader");
        profile.windMaterialB = CreateMaterial("Test_M_RollingWindGust_B", gustShader);
        profile.windMaterialB.SetColor("_EdgeColor", new Color(.76f, 1, .95f, 1));
        profile.windMaterialB.SetColor("_ShadowColor", new Color(.025f, .33f, .4f, 1));
        profile.windMaterialB.SetFloat("_RibbonWidth", 1);
        profile.windMaterialB.SetFloat("_CurlSize", 1);
        profile.windParticleMaterialB = CreateMaterial("Test_M_RollingWindParticle_B", particleShader);
        profile.windParticleMaterialB.SetColor("_EdgeColor", new Color(.70f, 1, .94f, 1));
        profile.windParticleMaterialB.SetFloat("_Intensity", 1.1f);
        profile.attack1Wind.branchCount = 3;
        profile.wind.branchCount = 5;
        profile.windDesign = Test_FanAttackVfxProfile.WindDesign.B;
        profile.NotifyChanged();

        var original = ScriptableObject.CreateInstance<Test_FanAttackVfxProfile>();
        try
        {
            JsonUtility.FromJsonOverwrite(before, original);
            foreach (int attack in new[] { 1, 2, 3 })
                if (JsonUtility.ToJson(original.GetSweep(attack)) != JsonUtility.ToJson(profile.GetSweep(attack)))
                    throw new Exception("Melee effect changed: " + attack);
            foreach (int attack in new[] { 1, 3 })
            {
                original.GetWind(attack).branchCount = profile.GetWind(attack).branchCount;
                if (JsonUtility.ToJson(original.GetWind(attack)) != JsonUtility.ToJson(profile.GetWind(attack)))
                    throw new Exception("Wind travel or hit settings changed: " + attack);
            }
            if (JsonUtility.ToJson(original.dust) != JsonUtility.ToJson(profile.dust)) throw new Exception("Dust settings changed");
        }
        finally { UnityEngine.Object.DestroyImmediate(original); }
        if (!materialA.SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(profile.windMaterial)))
            || !particlesA.SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(profile.windParticleMaterial))))
            throw new Exception("Original A materials changed");
        foreach (var material in new[] {profile.windMaterialB, profile.windParticleMaterialB})
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        var report = new { currentDesign = profile.windDesign.ToString(), firstBranches = profile.attack1Wind.branchCount,
            thirdBranches = profile.wind.branchCount, originalAMaterialsPreserved = true, meleeAndHitSettingsUnchanged = true,
            materialA = AssetDatabase.GetAssetPath(profile.windMaterial), materialB = AssetDatabase.GetAssetPath(profile.windMaterialB), shaderErrors = false };
        File.WriteAllText(Folder + "Test_SetupReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
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
}
