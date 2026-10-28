using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

public static class Test_MobileAssets
{
    public static object Main()
    {
        var errors = new List<string>();
        int groups = 0, missing = 0, pngs = 0;
        long highTriangles = 0, mobileTriangles = 0;
        foreach (var group in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var levels = group.GetLODs(); groups++;
            if (levels.Length != 3) errors.Add("Invalid LOD count: " + group.name);
            foreach (var level in levels)
            foreach (var renderer in level.renderers)
                if (renderer == null || renderer.GetComponent<MeshFilter>().sharedMesh == null || renderer.sharedMaterials.Any(m => m == null)) errors.Add("Missing LOD reference: " + group.name);
            if (levels.Length == 3)
            {
                foreach (var renderer in levels[0].renderers) highTriangles += Count(renderer.GetComponent<MeshFilter>().sharedMesh);
                foreach (var renderer in levels[1].renderers) mobileTriangles += Count(renderer.GetComponent<MeshFilter>().sharedMesh);
                Bounds original = levels[0].renderers[0].bounds, reduced = levels[1].renderers[0].bounds;
                if ((original.center - reduced.center).magnitude > Mathf.Max(0.05f, original.size.magnitude * .1f) && original.size.sqrMagnitude > 0.01f) errors.Add("LOD moved: " + group.name);
            }
        }
        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        if (missing != 0) errors.Add("Missing scripts: " + missing);
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Arts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height); pngs++;
            if (width > 1024 || height > 1024 || importer.maxTextureSize != 1024) errors.Add("Oversized texture: " + path);
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "iOS", "WebGL" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (settings.maxTextureSize != 1024) errors.Add("Oversized platform: " + path + " " + platform);
                if ((platform == "Android" || platform == "iPhone") && (!settings.overridden || (settings.format != TextureImporterFormat.ASTC_4x4 && settings.format != TextureImporterFormat.ASTC_6x6))) errors.Add("Missing compression: " + path + " " + platform);
            }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Arts" }))
        foreach (var message in ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid))))
            if (message.severity.ToString() == "Error") errors.Add("Shader: " + message.message);
        string[] dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
        if (dependencies.Any(p => p.EndsWith("MountainKorean.ttf"))) errors.Add("Legacy font is still a scene dependency.");
        if (QualitySettings.maximumLODLevel != 1) errors.Add("Mobile LOD limit not applied.");
        var result = new { success = errors.Count == 0, errors, groups, pngs, missing, highTriangles, mobileTriangles, triangleReduction = 1d - mobileTriangles / (double)highTriangles };
        File.WriteAllText("Assets/Temp/MobileOptimization~/AssetVerification.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }

    private static long Count(Mesh mesh)
    {
        long indices = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
        return indices / 3;
    }
}
