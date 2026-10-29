using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class ApplyMobileRevision
{
    public static object Main()
    {
        if (EditorApplication.isPlaying || BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Editor must be idle.");
        var hud = UnityEngine.Object.FindFirstObjectByType<PassHud>();
        var controls = hud.GetComponent<PassMobileControls>();
        Undo.RecordObject(controls, "Apply mobile run and sheathe artwork");
        var serialized = new SerializedObject(controls);
        serialized.FindProperty("runSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/UI/MountainPass/MobileControls/T_MobileRun.png");
        serialized.FindProperty("sheatheSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/UI/MountainPass/MobileControls/T_MobileSheathe.png");
        serialized.ApplyModifiedProperties();

        const string folder = "Assets/Arts/UI/MountainPass/AppIcon/";
        string[] names = { "T_CheonAppIcon", "T_CheonAppIconForeground", "T_CheonAppIconBackground" };
        foreach (string name in names)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(folder + name + ".png");
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 512; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            foreach (string platform in new[] { "Standalone", "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = true; platformSettings.maxTextureSize = 512;
                platformSettings.format = TextureImporterFormat.RGBA32;
                importer.SetPlatformTextureSettings(platformSettings);
            }
            importer.SaveAndReimport();
        }
        var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "T_CheonAppIcon.png");
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "T_CheonAppIconForeground.png");
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "T_CheonAppIconBackground.png");
        var results = new System.Collections.Generic.List<object>();
        foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Adaptive })
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var icon in icons)
            {
                if (kind == AndroidPlatformIconKind.Adaptive) { icon.SetTexture(background, 0); icon.SetTexture(foreground, 1); }
                else icon.SetTexture(legacy, 0);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            results.Add(new { kind = kind.ToString(), slots = icons.Length });
        }
        PlayerSettings.Android.bundleVersionCode = Math.Max(2, PlayerSettings.Android.bundleVersionCode);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorSceneManager.SaveScene(hud.gameObject.scene);
        return new { icons = results, run = serialized.FindProperty("runSprite").objectReferenceValue.name, sheathe = serialized.FindProperty("sheatheSprite").objectReferenceValue.name, versionCode = PlayerSettings.Android.bundleVersionCode };
    }
}
