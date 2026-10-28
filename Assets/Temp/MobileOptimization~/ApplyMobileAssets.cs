using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Newtonsoft.Json;

public static class ApplyMobileAssets
{
    public sealed class Geometry
    {
        public string key, name, source;
        public int level;
        public float[] positions, normals, uv;
        public int[][] submeshes;
    }

    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying assets.");
        var records = JsonConvert.DeserializeObject<List<Geometry>>(File.ReadAllText("Assets/Temp/MobileOptimization~/GeometryOutput.json"));
        var lodMeshes = new Dictionary<string, Mesh[]>();
        Directory.CreateDirectory("Assets/Arts/Environment/Mobile/Meshes");
        AssetDatabase.Refresh();
        foreach (var record in records)
        {
            if (!lodMeshes.TryGetValue(record.key, out var levels)) lodMeshes.Add(record.key, levels = new Mesh[2]);
            string filename = Path.GetFileNameWithoutExtension(record.source) + "_" + record.name + "_LOD" + record.level;
            foreach (char invalid in Path.GetInvalidFileNameChars()) filename = filename.Replace(invalid, '_');
            string assetPath = "Assets/Arts/Environment/Mobile/Meshes/" + filename + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, assetPath); }
            mesh.Clear(); mesh.name = filename;
            int count = record.positions.Length / 3;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(record.positions[i * 3], record.positions[i * 3 + 1], record.positions[i * 3 + 2]);
                normals[i] = new Vector3(record.normals[i * 3], record.normals[i * 3 + 1], record.normals[i * 3 + 2]);
                uv[i] = new Vector2(record.uv[i * 2], record.uv[i * 2 + 1]);
            }
            mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv;
            mesh.subMeshCount = record.submeshes.Length;
            for (int i = 0; i < record.submeshes.Length; i++) mesh.SetTriangles(record.submeshes[i], i);
            mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            EditorUtility.SetDirty(mesh); levels[record.level - 1] = mesh;
        }

        int groups = 0, walls = 0;
        long originalTriangles = 0, mobileTriangles = 0;
        foreach (var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var source = filter.sharedMesh;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (source == null || renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy) continue;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id) || !lodMeshes.TryGetValue(guid + "_" + id, out var levels)) continue;
            if (filter.GetComponent<LODGroup>() != null) continue;
            var lodRenderers = new Renderer[2];
            for (int level = 0; level < 2; level++)
            {
                var child = new GameObject("MobileLOD" + (level + 1), typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(filter.transform, false);
                child.layer = filter.gameObject.layer;
                child.GetComponent<MeshFilter>().sharedMesh = levels[level];
                var copy = child.GetComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
                copy.lightProbeUsage = renderer.lightProbeUsage;
                copy.reflectionProbeUsage = renderer.reflectionProbeUsage;
                copy.motionVectorGenerationMode = renderer.motionVectorGenerationMode;
                copy.renderingLayerMask = renderer.renderingLayerMask;
                lodRenderers[level] = copy;
            }
            var group = filter.gameObject.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[] { new LOD(.25f, new Renderer[] { renderer }), new LOD(.07f, new[] { lodRenderers[0] }), new LOD(.001f, new[] { lodRenderers[1] }) });
            group.RecalculateBounds(); groups++;
            originalTriangles += (long)source.GetIndexCount(0) / 3;
            mobileTriangles += levels[0].subMeshCount > 0 ? (long)levels[0].GetIndexCount(0) / 3 : 0;
        }

        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        int environmentLayer = LayerMask.NameToLayer("Environment");
        if (environmentLayer < 0)
        {
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                { environmentLayer = i; layers.GetArrayElementAtIndex(i).stringValue = "Environment"; break; }
            if (environmentLayer < 0) throw new InvalidOperationException("No free Environment layer.");
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (collider.GetComponentInParent<Cheon.Prototype.MountainPass.PassPlayerMovement>() != null || collider.GetComponentInParent<Cheon.Prototype.MountainPass.PassBossController>() != null || collider.attachedRigidbody != null) continue;
            if (collider.gameObject.layer == 2) continue;
            collider.gameObject.layer = environmentLayer;
            if (!(collider is MeshCollider detailed) || detailed.sharedMesh == null) continue;
            string meshPath = AssetDatabase.GetAssetPath(detailed.sharedMesh);
            if (!meshPath.Contains("/SelfModeling/Models/Wall_")) continue;
            Bounds bounds = detailed.sharedMesh.bounds;
            var box = detailed.gameObject.AddComponent<BoxCollider>();
            box.center = bounds.center; box.size = bounds.size;
            box.sharedMaterial = detailed.sharedMaterial; box.isTrigger = detailed.isTrigger; box.enabled = detailed.enabled;
            UnityEngine.Object.DestroyImmediate(detailed); walls++;
        }
        int groundMask = 1 << environmentLayer;
        foreach (var settings in UnityEngine.Object.FindObjectsByType<Cheon.Prototype.MountainPass.PassBossPatternSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            settings.collision.groundMask = groundMask;
        foreach (string guid in AssetDatabase.FindAssets("t:PassFanAttackVfxProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<Cheon.Prototype.MountainPass.PassFanAttackVfxProfile>(AssetDatabase.GUIDToAssetPath(guid));
            profile.dust.groundMask = groundMask; EditorUtility.SetDirty(profile);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:PassRollDustProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<Cheon.Prototype.MountainPass.PassRollDustProfile>(AssetDatabase.GUIDToAssetPath(guid));
            profile.groundMask = groundMask; EditorUtility.SetDirty(profile);
        }

        int textures = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Arts" }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) continue;
            importer.maxTextureSize = 1024;
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "iOS", "WebGL" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.maxTextureSize = 1024;
                if (platform == "Android" || platform == "iPhone")
                {
                    settings.overridden = true;
                    settings.format = assetPath.Contains("/UI/") || assetPath.Contains("/Effects/") ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
                    settings.textureCompression = TextureImporterCompression.CompressedHQ;
                    settings.compressionQuality = 100;
                }
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport(); textures++;
        }

        var pipeline = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/Mobile_RPAsset.asset"));
        pipeline.FindProperty("m_SupportsHDR").boolValue = false;
        pipeline.FindProperty("m_ShadowDistance").floatValue = 35f;
        pipeline.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 2;
        pipeline.ApplyModifiedPropertiesWithoutUndo();
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        quality.FindProperty("m_QualitySettings").GetArrayElementAtIndex(0).FindPropertyRelative("maximumLODLevel").intValue = 1;
        quality.ApplyModifiedPropertiesWithoutUndo();

        var body = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Arts/Fonts/Cheon/Resources/CheonFonts/CheonYeonSung.asset");
        var title = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Arts/Fonts/Cheon/Resources/CheonFonts/CheonTitle.asset");
        foreach (var hud in UnityEngine.Object.FindObjectsByType<Cheon.Prototype.MountainPass.PassHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("font").objectReferenceValue = body;
            serialized.FindProperty("bodyFont").objectReferenceValue = body;
            serialized.FindProperty("titleFont").objectReferenceValue = title;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        var textSettings = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Arts/Fonts/MountainPass/Presentation/TextSettings.asset"));
        textSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = body;
        textSettings.ApplyModifiedPropertiesWithoutUndo();
        string unusedFont = "Assets/Arts/Fonts/Cheon/Resources/CheonFonts/CheonBody.asset";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(unusedFont) != null)
        {
            string error = AssetDatabase.MoveAsset(unusedFont, "Assets/Arts/Fonts/Cheon/CheonBody.asset");
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        var report = new { generatedMeshes = records.Count, lodGroups = groups, simplifiedWalls = walls, textures, originalTriangles, mobileTriangles, environmentLayer };
        File.WriteAllText("Assets/Temp/MobileOptimization~/AssetsApplied.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return report;
    }
}
