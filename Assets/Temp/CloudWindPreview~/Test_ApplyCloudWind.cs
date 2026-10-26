using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_ApplyCloudWind
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        const string root = "Assets/Test/FanAttackVfx/";
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(root + "Settings/Test_FanAttackVfxProfile.asset");
        string second = JsonUtility.ToJson(profile.attack2);
        string third = JsonUtility.ToJson(profile.attack3);
        string firstWind = JsonUtility.ToJson(profile.attack1Wind);
        string thirdWind = JsonUtility.ToJson(profile.wind);
        string dust = JsonUtility.ToJson(profile.dust);
        string first = JsonUtility.ToJson(profile.attack1);
        string backup = "Assets/Temp/CloudWindPreview~/Test_ProfileBefore.json";
        if (!File.Exists(backup)) File.WriteAllText(backup, JsonUtility.ToJson(profile, true));
        AssetDatabase.ImportAsset(root + "Shaders/Test_S_CloudWindSweep.shader", ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Cheon/Tests/FanAttack/CloudWindSweep");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Cloud wind shader is not ready");
        string path = root + "Materials/Test_M_CloudWindSweep.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_EdgeColor", new Color(.68f, 1f, .92f, 1));
        material.SetColor("_ShadowColor", new Color(.025f, .36f, .42f, 1));
        material.SetFloat("_RibbonWidth", 1);
        material.SetFloat("_CloudScale", 1);
        material.SetFloat("_OutlineWidth", .012f);
        EditorUtility.SetDirty(material);
        profile.attack1SweepMaterial = material;
        profile.attack1.color = new Color(.12f, .78f, .73f, .95f);
        profile.attack1.intensity = 1.20f;
        profile.attack1.fadeSeconds = .26f;
        profile.NotifyChanged();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(material);
        AssetDatabase.SaveAssetIfDirty(profile);
        if (second != JsonUtility.ToJson(profile.attack2) || third != JsonUtility.ToJson(profile.attack3)
            || firstWind != JsonUtility.ToJson(profile.attack1Wind) || thirdWind != JsonUtility.ToJson(profile.wind)
            || dust != JsonUtility.ToJson(profile.dust)) throw new Exception("Unrelated effects changed");
        return new { path, shaderErrors = ShaderUtil.ShaderHasError(shader), otherEffectsUnchanged = true,
            originalAttack1 = first, appliedAttack1 = JsonUtility.ToJson(profile.attack1) };
    }
}
