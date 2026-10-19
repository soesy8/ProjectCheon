using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class Test_ImportCheonEnvironmentModels
{
    private const string Root = "Assets/Test/Cheon/Arts/EnvironmentModels";

    public static object Import()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        var manifest = JObject.Parse(File.ReadAllText("Assets/Temp/Test_CheonEnvironmentMaterials.json"));
        var reports = new List<object>();
        foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
        {
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            string assetName = Path.GetFileNameWithoutExtension(path).Substring("Test_".Length);
            EnsureFolder(folder + "/Materials"); EnsureFolder(folder + "/Meshes");
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true; importer.importCameras = false; importer.importLights = false;
            importer.importAnimation = false;
            var sourceMaterials = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>(true)
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).GroupBy(m => m.name).Select(g => g.First()).ToArray();
            var materials = new Dictionary<string, Material>();
            foreach (var source in sourceMaterials)
            {
                var data = manifest[assetName].First(m => (string)m["name"] == source.name);
                string materialPath = folder + "/Materials/Test_" + source.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
                material.name = "Test_" + source.name; material.enableInstancing = true;
                string imageName = data["images"].Select(v => (string)v).FirstOrDefault();
                Texture2D texture = imageName == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/Test_" + Path.GetFileNameWithoutExtension(imageName) + ".png");
                if (imageName != null && texture == null) throw new InvalidOperationException("텍스처 누락: " + imageName);
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", texture != null ? Color.white : ColorValue(data["color"]).gamma);
                material.SetFloat("_Metallic", (float)data["metallic"]);
                material.SetFloat("_Smoothness", 1 - (float)data["roughness"]);
                float strength = (float)data["emissionStrength"];
                if (strength > 0)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", ColorValue(data["emission"]) * strength);
                }
                EditorUtility.SetDirty(material); materials[source.name] = material;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            }
            importer.SaveAndReimport();
            var sourceObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var prefabObject = new GameObject("Test_" + assetName);
            try
            {
                var pivots = new Dictionary<Transform, Transform>();
                var groups = new Dictionary<(Transform parent, Material material), List<CombineInstance>>();
                foreach (var filter in sourceObject.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || filter.sharedMesh == null) continue;
                    Transform sourcePivot = filter.transform.parent;
                    while (sourcePivot != null && sourcePivot != sourceObject.transform && !sourcePivot.name.EndsWith("_Pivot")) sourcePivot = sourcePivot.parent;
                    Transform parent = prefabObject.transform;
                    if (sourcePivot != null && sourcePivot != sourceObject.transform)
                    {
                        if (!pivots.TryGetValue(sourcePivot, out parent))
                        {
                            parent = new GameObject(sourcePivot.name).transform; parent.SetParent(prefabObject.transform, false);
                            parent.position = sourcePivot.position; parent.rotation = sourcePivot.rotation; parent.localScale = sourcePivot.lossyScale;
                            pivots[sourcePivot] = parent;
                        }
                    }
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    {
                        var material = renderer.sharedMaterials[submesh];
                        if (material == null) throw new InvalidOperationException("재질 누락: " + filter.name);
                        var key = (parent, material);
                        if (!groups.TryGetValue(key, out var instances)) { instances = new List<CombineInstance>(); groups[key] = instances; }
                        instances.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = submesh, transform = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                }
                // 반복 배치 시 수백 개 부품을 따로 그리지 않도록 같은 재질끼리 결합한다.
                // 종과 산신각 문은 피벗별로 결합해 원본의 조절 가능한 축을 보존한다.
                foreach (var pair in groups)
                {
                    string name = "Test_" + assetName + "_" + pair.Key.parent.name + "_" + pair.Key.material.name;
                    var combined = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                    combined.CombineMeshes(pair.Value.ToArray(), true, true); combined.RecalculateBounds();
                    string meshPath = folder + "/Meshes/" + name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null) AssetDatabase.CreateAsset(combined, meshPath);
                    else { EditorUtility.CopySerialized(combined, existing); Object.DestroyImmediate(combined); combined = existing; EditorUtility.SetDirty(existing); }
                    var part = new GameObject(pair.Key.material.name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(pair.Key.parent, false);
                    part.GetComponent<MeshFilter>().sharedMesh = combined;
                    part.GetComponent<MeshRenderer>().sharedMaterial = pair.Key.material;
                }
                string prefabPath = folder + "/Test_" + assetName + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(prefabObject, prefabPath);
                reports.Add(new { model = assetName, sourceRenderers = sourceObject.GetComponentsInChildren<MeshRenderer>(true).Length, prefabRenderers = groups.Count, pivots = pivots.Count, prefab = prefabPath });
            }
            finally { Object.DestroyImmediate(sourceObject); Object.DestroyImmediate(prefabObject); }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/Temp/Test_CheonEnvironmentImport.json", Newtonsoft.Json.JsonConvert.SerializeObject(reports, Newtonsoft.Json.Formatting.Indented));
        return reports;
    }

    private static Color ColorValue(JToken values) => new Color((float)values[0], (float)values[1], (float)values[2], (float)values[3]);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
