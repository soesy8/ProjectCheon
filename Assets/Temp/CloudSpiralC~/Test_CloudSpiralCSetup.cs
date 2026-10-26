using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_CloudSpiralCSetup
{
    private const string Root = "Assets/Test/FanAttackVfx/";
    private const string Temp = "Assets/Temp/CloudSpiralC~/";

    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit mode required");
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (active.isDirty) throw new InvalidOperationException("Current scene has unsaved changes: " + active.path);
        foreach (string relative in new[] { "Textures/T_CloudSpiralSlash.png", "Textures/T_CloudSpiralWind.png",
                "Materials/M_CloudSpiralSlash.mat", "Materials/M_CloudSpiralWind.mat",
                "Meshes/CloudSpiralSlashQuad.asset", "Meshes/CloudSpiralWindQuad.asset" })
            AssetDatabase.ImportAsset("Assets/Arts/Effects/CloudSpiral/" + relative,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(Root + "Settings/Test_FanAttackVfxProfile.asset");
        string before = JsonUtility.ToJson(profile, true);
        if (!File.Exists(Temp + "Test_ProfileBefore.json")) File.WriteAllText(Temp + "Test_ProfileBefore.json", before);
        string[] originals = { JsonUtility.ToJson(profile.attack1), JsonUtility.ToJson(profile.attack2),
            JsonUtility.ToJson(profile.attack3), JsonUtility.ToJson(profile.attack1Wind),
            JsonUtility.ToJson(profile.wind), JsonUtility.ToJson(profile.dust) };
        var style = profile.cloudSpiral;
        style.slashMaterial = CopyMaterial("Slash");
        style.windMaterial = CopyMaterial("Wind");
        style.slashMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Arts/Effects/CloudSpiral/Meshes/CloudSpiralSlashQuad.asset");
        style.windMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Arts/Effects/CloudSpiral/Meshes/CloudSpiralWindQuad.asset");
        if (style.slashMesh == null || style.windMesh == null) throw new InvalidOperationException("Missing source art meshes");
        profile.windDesign = Test_FanAttackVfxProfile.WindDesign.C;
        profile.NotifyChanged();
        string[] after = { JsonUtility.ToJson(profile.attack1), JsonUtility.ToJson(profile.attack2),
            JsonUtility.ToJson(profile.attack3), JsonUtility.ToJson(profile.attack1Wind),
            JsonUtility.ToJson(profile.wind), JsonUtility.ToJson(profile.dust) };
        for (int index = 0; index < originals.Length; index++)
            if (originals[index] != after[index]) throw new InvalidOperationException("Original settings changed: " + index);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);

        string prefabPath = Root + "Prefabs/Test_FanAttackVfx.prefab";
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try { Attach(contents.GetComponent<Test_FanAttackVfx>()); PrefabUtility.SaveAsPrefabAsset(contents, prefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        string scenePath = Root + "Scenes/Test_FanAttackVfx.unity";
        var scene = active.path == scenePath ? active : EditorSceneManager.OpenScene(scenePath);
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (effect == null) throw new InvalidOperationException("Test VFX controller missing");
        Attach(effect);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Temp + "Test_SetupReport.txt", "PASS\nC selected in profile\n" +
            "Source textures/materials/meshes reused\nExisting A/B materials and sweep/wind/dust settings preserved\n" +
            "C adapter attached to test prefab and scene\n");
        return "C configured in test scene and prefab; original sweep, wind travel and dust settings preserved";
    }

    private static Material CopyMaterial(string effect)
    {
        string path = Root + "Materials/Test_M_CloudSpiral" + effect + "_C.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null && !AssetDatabase.CopyAsset(
                "Assets/Arts/Effects/CloudSpiral/Materials/M_CloudSpiral" + effect + ".mat", path))
            throw new InvalidOperationException("Material copy failed: " + path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void Attach(Test_FanAttackVfx effect)
    {
        var adapter = effect.GetComponent<Test_FanCloudSpiralVfx>();
        if (adapter == null) adapter = effect.gameObject.AddComponent<Test_FanCloudSpiralVfx>();
        var serialized = new SerializedObject(effect);
        serialized.FindProperty("cloudSpiral").objectReferenceValue = adapter;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(effect);
    }
}
