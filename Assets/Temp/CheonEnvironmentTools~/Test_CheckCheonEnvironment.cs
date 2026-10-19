using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class Test_CheckCheonEnvironment
{
    public static object Finish()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var world = Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
        int before = world.GetComponentsInChildren<Transform>(true).Count(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
        Test_CheonEnvironmentModels.Apply();
        int after = world.GetComponentsInChildren<Transform>(true).Count(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
        if (before != after) throw new InvalidOperationException("다시 적용하면서 배치가 중복되었습니다.");
        var gate = world.transform.Find("Test_경계문/Test_UpperGate").GetComponentsInChildren<Transform>(true).First(t => t.name == "Test_Cheon_Gate");
        Selection.activeGameObject = gate.gameObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(gate.position + Vector3.up * 2, Quaternion.LookRotation(new Vector3(-12, -11, 16)), 12);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(world.gameObject.scene);
        return new { instances = after, repeatedApplyStable = true, playing = EditorApplication.isPlaying, saved = !world.gameObject.scene.isDirty };
    }

    public static object Verify()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        var world = Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
        var all = world.GetComponentsInChildren<Transform>(true);
        var instances = all.Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).Select(t => new {
            path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),
            renderers = t.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled),
            active = t.gameObject.activeInHierarchy,
            position = t.position.ToString("F3"), scale = t.localScale.ToString("F3"),
            center = t.GetComponentsInChildren<MeshRenderer>(true).Aggregate(new Bounds(t.position,Vector3.zero), (bounds, renderer) => { bounds.Encapsulate(renderer.bounds); return bounds; }).center.ToString("F3")
        }).ToArray();
        var materials = world.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
        string root = "Assets/Test/Cheon/Arts/EnvironmentModels";
        var textures = AssetDatabase.FindAssets("t:Texture2D", new[] {root}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var materialAssets = AssetDatabase.FindAssets("t:Material", new[] {root}).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".mat")).Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
        var originalGroups = all.Where(t => t.name == "Test_산신당" || t.name == "Test_청동종각" || t.name == "Test_UpperGate").Select(t => new {
            name = t.name, colliders = t.GetComponentsInChildren<Collider>(true).Length
        }).ToArray();
        var result = new {
            scene = scene.path, playing = EditorApplication.isPlaying,
            prefabCounts = instances.GroupBy(i => i.path).Select(g => new {prefab = g.Key, count = g.Count()}).ToArray(),
            importedMaterials = materialAssets.Length, importedTextures = textures.Length,
            invalidMaterials = materials.Count(m => m == null || m.shader == null || !m.shader.isSupported),
            missingTextureMaterials = materialAssets.Where(m => m.GetTexture("_BaseMap") == null && !new[] {"Test_M_DarkIron", "Test_M_Pollen", "Test_M_CandleWax", "Test_M_Flame"}.Contains(m.name)).Select(m => m.name).ToArray(),
            missingScripts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
            originalGroups,
            buildings = instances.Where(i => i.path.Contains("Shrine") || i.path.Contains("Pavilion") || i.path.Contains("Gate")).ToArray(),
            player = Object.FindFirstObjectByType<Cheon.Testing.Test_CheonPlayer>().transform.position.ToString("F3"),
            gateReference = new SerializedObject(world).FindProperty("gateLintel").objectReferenceValue.name,
            importedRenderers = instances.Sum(i => i.renderers)
        };
        File.WriteAllText("Assets/Temp/Test_CheonEnvironmentVerification.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        return result;
    }

    public static object Capture()
    {
        var camera = Object.FindFirstObjectByType<Cheon.Testing.Test_CheonCamera>().GetComponent<Camera>();
        var world = Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
        var shrine = world.transform.Find("Test_산신당").position;
        var gate = world.transform.Find("Test_경계문").position;
        var bell = world.transform.Find("Test_ExpandedLandscape/Test_청동종각").position;
        Render(camera, shrine + new Vector3(10, 8, -12), shrine + Vector3.up * 1.5f, 5.2f, "Shrine");
        Render(camera, gate + new Vector3(12, 11, -16), gate + Vector3.up * 1.8f, 8.5f, "Gate");
        Render(camera, bell + new Vector3(8, 7, -10), bell + Vector3.up * 1.5f, 4.4f, "Bell");
        return new[] {"Shrine", "Gate", "Bell"};
    }

    public static object AlignExistingBuildings()
    {
        var world = Object.FindFirstObjectByType<Cheon.Testing.Test_CheonWorld>();
        var buildings = world.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
            (t.name == "Test_Cheon_MountainSpiritShrine" || t.name == "Test_Cheon_BellPavilion" || t.name == "Test_Cheon_Gate")).ToArray();
        foreach (var item in buildings)
        {
            var renderers = item.GetComponentsInChildren<MeshRenderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Undo.RecordObject(item, "건물 정면 방향 맞춤");
            item.RotateAround(new Vector3(bounds.center.x, item.position.y, bounds.center.z), Vector3.up, 180);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }
        foreach (var item in world.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(world.gameObject.scene);
        return buildings.Length;
    }

    private static void Render(Camera viewCamera, Vector3 position, Vector3 target, float size, string label)
    {
        var preview = new GameObject("Test_EnvironmentPreviewCamera");
        var camera = preview.AddComponent<Camera>(); camera.CopyFrom(viewCamera); camera.enabled = false;
        var settings = camera.GetUniversalAdditionalCameraData(); var originalSettings = viewCamera.GetUniversalAdditionalCameraData();
        settings.renderPostProcessing = originalSettings.renderPostProcessing; settings.volumeLayerMask = originalSettings.volumeLayerMask;
        Vector3 previousPosition = camera.transform.position; Quaternion previousRotation = camera.transform.rotation;
        float previousSize = camera.orthographicSize; float previousAspect = camera.aspect;
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var texture = new RenderTexture(1440, 1080, 24);
        Texture2D image = null;
        try
        {
            camera.transform.position = position; camera.transform.LookAt(target); camera.orthographicSize = size; camera.aspect = 1440f / 1080;
            camera.ResetCullingMatrix(); camera.ResetWorldToCameraMatrix(); camera.ResetProjectionMatrix();
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            image = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); image.Apply();
            File.WriteAllBytes("Assets/Temp/Test_T_CheonEnvironment_" + label + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            camera.transform.SetPositionAndRotation(previousPosition, previousRotation); camera.orthographicSize = previousSize; camera.aspect = previousAspect;
            texture.Release(); Object.DestroyImmediate(texture); if (image != null) Object.DestroyImmediate(image);
            Object.DestroyImmediate(preview);
        }
    }
}
