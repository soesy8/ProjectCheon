using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LanternEmissionAdjustment
{
    private const string ToonMaterialPath = "Assets/Arts/Materials/ToonEnvironment/M_Toon_Lantern_Stone_e13481f1.mat";
    private const string LitMaterialPath = "Assets/Arts/Environment/SelfModeling/Materials/M_Lantern_Stone.mat";
    private const string OutputPath = "Assets/Temp/ToonShader~/";

    public static object Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        var renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true));
        return new
        {
            scene = scene.path,
            sceneDirty = scene.isDirty,
            materials = new[] { ToonMaterialPath, LitMaterialPath }.Select(path =>
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                return new
                {
                    path,
                    shader = material.shader.name,
                    hasShaderErrors = ShaderUtil.ShaderHasError(material.shader),
                    emission = material.GetColor("_EmissionColor").ToString(),
                    mask = AssetDatabase.GetAssetPath(material.GetTexture("_EmissionMap")),
                    sceneUsers = renderers.Count(renderer => renderer.sharedMaterials.Contains(material))
                };
            }).ToArray()
        };
    }

    public static object Apply()
    {
        var paths = new[] { ToonMaterialPath, LitMaterialPath };
        var materials = paths.Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
        var mask = materials[0].GetTexture("_EmissionMap");
        if (mask == null || materials.Any(material => material.GetTexture("_EmissionMap") != mask))
            throw new InvalidOperationException("Lantern emission masks must match.");

        foreach (var path in paths)
            File.Copy(path, OutputPath + Path.GetFileNameWithoutExtension(path) + "_BeforeEmission.txt", true);

        Render(materials[0], "T_Lantern_Emission_Before.png");

        // 기존 마스크를 유지해 노란 창 부분에만 은은한 따뜻한 빛을 더한다.
        Undo.RecordObjects(materials, "Adjust lantern emission");
        foreach (var material in materials)
        {
            material.SetColor("_EmissionColor", new Color(1.0f, 0.64f, 0.24f, 1f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        Render(materials[0], "T_Lantern_Emission_After.png");
        SceneView.RepaintAll();
        return Inspect();
    }

    private static void Render(Material material, string filename)
    {
        var preview = new PreviewRenderUtility();
        Texture2D texture = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/SelfModeling/Lantern.prefab");
            var instance = preview.InstantiatePrefabInScene(prefab);
            var renderer = instance.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterial = material;
            var bounds = renderer.bounds;
            preview.camera.transform.position = bounds.center + new Vector3(1.6f, 1.6f, 1.8f).normalized * 7f;
            preview.camera.transform.LookAt(bounds.center);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 2.4f;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.17f, 0.18f, 0.2f);
            preview.lights[0].intensity = 1.4f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            preview.lights[1].intensity = 0.6f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 170f, 0f);
            preview.ambientColor = new Color(0.45f, 0.45f, 0.45f);
            preview.BeginStaticPreview(new Rect(0f, 0f, 800f, 800f));
            preview.Render(true);
            texture = preview.EndStaticPreview();
            File.WriteAllBytes(OutputPath + filename, texture.EncodeToPNG());
        }
        finally
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            preview.Cleanup();
        }
    }
}
