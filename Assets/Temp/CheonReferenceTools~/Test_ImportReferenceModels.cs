using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class Test_ImportReferenceModels
{
    private const string Root = "Assets/Test/Cheon/Arts/ReferenceEnvironment/";

    public static object Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var catalog = JObject.Parse(File.ReadAllText(Root + "Test_ReferenceModelCatalog.json"));
        EnsureFolder(Root + "Materials"); EnsureFolder(Root + "Meshes"); EnsureFolder(Root + "Prefabs");
        var surface = Shader.Find("Cheon/Test/ReferenceSurface"); var effect = Shader.Find("Cheon/Test/ReferenceEffect");
        if (surface == null || effect == null || ShaderUtil.ShaderHasError(surface) || ShaderUtil.ShaderHasError(effect)) throw new InvalidOperationException("참고 모델 셰이더 컴파일 실패");
        var materials = new Dictionary<string, Material>();
        foreach (var property in ((JObject)catalog["materials"]).Properties())
        {
            var data = property.Value;
            string path = Root + "Materials/" + property.Name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material((string)data["kind"] == "effect" ? effect : surface); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = (string)data["kind"] == "effect" ? effect : surface;
            ColorUtility.TryParseHtmlString("#" + (string)data["color"], out var color);
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Mode", (float)data["mode"]); mat.enableInstancing = true;
            if ((string)data["kind"] == "effect") { mat.SetFloat("_Opacity", (float)data["opacity"]); mat.SetFloat("_Speed", .25f); }
            else { mat.SetFloat("_Emission", (float)data["emission"]); mat.SetFloat("_FlowSpeed", .18f); mat.SetFloat("_Cull", 0); }
            if (data["texture"].Type != JTokenType.Null)
            {
                string texturePath = Root + "Textures/" + (string)data["texture"];
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null) throw new InvalidOperationException("참고 모델 텍스처 누락: " + texturePath);
                mat.SetTexture("_BaseMap", texture);
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                textureImporter.wrapMode = TextureWrapMode.Clamp; textureImporter.alphaIsTransparency = (string)data["kind"] == "effect";
                textureImporter.SaveAndReimport();
            }
            EditorUtility.SetDirty(mat); materials[property.Name] = mat;
        }
        var reports = new List<object>();
        foreach (var entry in catalog["models"])
        {
            string key = (string)entry["key"], path = Root + (string)entry["path"];
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("FBX 임포트 실패: " + path);
            importer.isReadable = true; importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            foreach (string name in entry["materials"].Select(v => (string)v)) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), materials[name]);
            importer.SaveAndReimport();
            var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var output = new GameObject("Test_CheonRef_" + key);
            try
            {
                var pivots = new Dictionary<string, Transform>();
                var groups = new Dictionary<(Transform, Material), List<CombineInstance>>();
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || filter.sharedMesh == null) continue;
                    if (filter.sharedMesh.colors.Length != filter.sharedMesh.vertexCount) throw new InvalidOperationException("버텍스 색 누락: " + key);
                    Transform parent = output.transform;
                    if (key == "JadeRings" && filter.name.StartsWith("IndependentJadeRing_"))
                    {
                        if (!pivots.TryGetValue(filter.name, out parent))
                        {
                            parent = new GameObject(filter.name + "_Pivot").transform; parent.SetParent(output.transform, false);
                            parent.position = filter.transform.position; parent.rotation = filter.transform.rotation; parent.localScale = filter.transform.lossyScale;
                            pivots[filter.name] = parent;
                        }
                    }
                    for (int index = 0; index < filter.sharedMesh.subMeshCount; index++)
                    {
                        var mat = renderer.sharedMaterials[index];
                        if (mat == null || !mat.name.StartsWith("Test_M_Ref")) throw new InvalidOperationException("재질 연결 실패: " + key + "/" + filter.name);
                        var group = (parent, mat);
                        if (!groups.TryGetValue(group, out var combines)) { combines = new List<CombineInstance>(); groups[group] = combines; }
                        combines.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = index, transform = parent.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                }
                foreach (var group in groups)
                {
                    var mesh = new Mesh { name = "Test_Ref_" + key + "_" + group.Key.Item1.name + "_" + group.Key.Item2.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds();
                    string meshPath = Root + "Meshes/" + mesh.name + ".asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
                    else
                    {
                        // 버퍼를 명시적으로 갱신해 에디터가 이전 메시의 GPU 데이터를 재사용하지 않게 한다.
                        existing.Clear(); existing.indexFormat = mesh.indexFormat;
                        existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv;
                        existing.colors = mesh.colors; existing.tangents = mesh.tangents;
                        existing.triangles = mesh.triangles; existing.bounds = mesh.bounds;
                        existing.UploadMeshData(false); EditorUtility.SetDirty(existing);
                        Object.DestroyImmediate(mesh); mesh = existing;
                    }
                    var part = new GameObject(group.Key.Item2.name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(group.Key.Item1, false); part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key.Item2;
                    if (group.Key.Item2.shader == effect) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
                }
                PrefabUtility.SaveAsPrefabAsset(output, Root + "Prefabs/Test_CheonRef_" + key + ".prefab");
                reports.Add(new { key, originalParts = (int)entry["parts"], renderers = groups.Count, triangles = (int)entry["triangles"] });
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(output); }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Assets/Temp/Test_CheonReferenceImport.json", Newtonsoft.Json.JsonConvert.SerializeObject(reports, Newtonsoft.Json.Formatting.Indented));
        return new { importedPrefabs = reports.Count, materials = materials.Count, shaderErrors = false };
    }

    private static void EnsureFolder(string path)
    {
        path = path.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
