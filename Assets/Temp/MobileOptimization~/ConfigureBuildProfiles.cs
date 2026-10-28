using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Profile;

public static class ConfigureBuildProfiles
{
    public static object Main()
    {
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isPlaying)
            throw new InvalidOperationException("Profiles must be configured while the editor is idle.");
        var originalTarget = EditorUserBuildSettings.activeBuildTarget;
        var originalProfile = BuildProfile.GetActiveBuildProfile();
        string folder = "Assets/Settings/BuildProfiles";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Settings", "BuildProfiles");
        var factory = typeof(BuildProfile).GetMethod("CreateInstance", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(BuildTarget), typeof(StandaloneBuildSubtarget) }, null);
        if (factory == null) throw new MissingMethodException("Unity BuildProfile platform factory is unavailable.");
        var results = new List<object>();
        foreach (var item in new[] {
            (name: "PC_Windows", target: BuildTarget.StandaloneWindows64, quality: "PC"),
            (name: "Mobile_Android", target: BuildTarget.Android, quality: "Mobile") })
        {
            string path = folder + "/" + item.name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            if (profile == null)
            {
                var subtarget = item.target == BuildTarget.StandaloneWindows64 ? StandaloneBuildSubtarget.Player : StandaloneBuildSubtarget.Default;
                profile = (BuildProfile)factory.Invoke(null, new object[] { item.target, subtarget });
                profile.name = item.name;
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.overrideGlobalScenes = true;
            profile.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            var serialized = new SerializedObject(profile);
            string target = serialized.FindProperty("m_BuildTarget").enumDisplayNames[serialized.FindProperty("m_BuildTarget").enumValueIndex];
            results.Add(new { item.name, path, target, quality = item.quality, scene = profile.scenes[0].path, overrideScenes = profile.overrideGlobalScenes });
        }
        if (BuildPipeline.isBuildingPlayer || originalTarget != EditorUserBuildSettings.activeBuildTarget || originalProfile != BuildProfile.GetActiveBuildProfile())
            throw new InvalidOperationException("Profile creation changed the active editor state.");
        return new { profiles = results, building = BuildPipeline.isBuildingPlayer, activeTarget = originalTarget.ToString() };
    }
}
