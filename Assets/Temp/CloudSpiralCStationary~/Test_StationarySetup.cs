using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_StationarySetup
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit mode required");
        string root = "Assets/Test/FanAttackVfx/";
        string backup = "Assets/Temp/CloudSpiralCStationary~/Test_ProfileBefore.json";
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(root + "Settings/Test_FanAttackVfxProfile.asset");
        if (!File.Exists(backup)) File.WriteAllText(backup, JsonUtility.ToJson(profile, true));
        var original = ScriptableObject.CreateInstance<Test_FanAttackVfxProfile>();
        JsonUtility.FromJsonOverwrite(File.ReadAllText(backup), original);
        try
        {
            float before = original.attack1Wind.radius * original.attack1Wind.forwardScale;
            profile.attack1Wind.forwardScale = (before + .5f) / profile.attack1Wind.radius;
            profile.NotifyChanged();
            float after = profile.attack1Wind.radius * profile.attack1Wind.forwardScale;
            if (!Mathf.Approximately(after - before, .5f)) throw new Exception("Incorrect forward extension");
            if (profile.attack1Wind.radius != original.attack1Wind.radius || profile.attack1Wind.angle != original.attack1Wind.angle)
                throw new Exception("Wind width changed");
            if (JsonUtility.ToJson(profile.wind) != JsonUtility.ToJson(original.wind) ||
                JsonUtility.ToJson(profile.dust) != JsonUtility.ToJson(original.dust)) throw new Exception("Unrelated wind or dust changed");
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            string path = root + "Shaders/Test_S_CloudSpiral_C.shader";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Stationary reveal shader failed");
            File.WriteAllText("Assets/Temp/CloudSpiralCStationary~/Test_SetupReport.txt",
                "PASS\nFirst wind forward extent: " + before + "m -> " + after + "m\n" +
                "Radius, angle, third wind and dust preserved\nStationary reveal shader: no errors\n");
            return "First wind " + before + "m -> " + after + "m; width unchanged; shader errors=false";
        }
        finally { UnityEngine.Object.DestroyImmediate(original); }
    }
}
