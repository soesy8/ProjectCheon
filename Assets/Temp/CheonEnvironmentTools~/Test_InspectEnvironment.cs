using System.Linq;
using UnityEngine;
using UnityEditor;

public static class Test_InspectEnvironment
{
    public static object Inspect()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var world = Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
        var objects = world.GetComponentsInChildren<Transform>(true).Where(t =>
            t.name.Contains("석등") || t.name.Contains("산신") || t.name.Contains("종각") ||
            t.name.Contains("Boundary") || t.name.Contains("WallCap") || t.name.Contains("UpperGate") ||
            t.name.Contains("ShoulderWall") || t.name.Contains("Grass")).Select(t => new {
                name = t.name, parent = t.parent.name, position = t.position.ToString("F3"),
                scale = t.lossyScale.ToString("F3"), rotation = t.eulerAngles.ToString("F2"),
                materials = t.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToArray(),
                bounds = GetBounds(t).size.ToString("F3"),
                colliders = t.GetComponentsInChildren<Collider>(true).Length
            }).ToArray();
        var paths = AssetDatabase.FindAssets("t:Model", new[] {"Assets/Test/Cheon/Arts/EnvironmentModels"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var imports = paths.Select(path => new {
            path, renderers = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshRenderer>(true).Length,
            materials = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => new {
                name = m.name, shader = m.shader.name,
                properties = Enumerable.Range(0, ShaderUtil.GetPropertyCount(m.shader)).Where(i => ShaderUtil.GetPropertyType(m.shader,i) == ShaderUtil.ShaderPropertyType.TexEnv).Select(i => new {
                    property = ShaderUtil.GetPropertyName(m.shader,i), texture = m.GetTexture(ShaderUtil.GetPropertyName(m.shader,i)) == null ? null : m.GetTexture(ShaderUtil.GetPropertyName(m.shader,i)).name
                }).ToArray(),
                color = m.HasProperty("_Color") ? m.GetColor("_Color").ToString() : "none"
            }).ToArray(),
            bounds = GetBounds(AssetDatabase.LoadAssetAtPath<GameObject>(path).transform).size.ToString("F3")
        }).ToArray();
        var result = new { objects, imports };
        System.IO.File.WriteAllText("Assets/Temp/Test_CheonEnvironmentBefore.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        return new { models = imports, targetObjects = objects.Where(o => !o.name.Contains("Grass")).ToArray(), vegetation = objects.Count(o => o.name.Contains("Grass")) };
    }

    private static Bounds GetBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position,Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
}
