using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SummitMistSetup
{
    private const string Work = "Assets/Temp/SummitMistReveal~/";

    public static object Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 설정합니다.");
        var mist = UnityEngine.Object.FindFirstObjectByType<PassMountainMist>();
        var fog = mist.ApproachFog;
        var scene = SceneManager.GetActiveScene();
        var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        var landing = transforms.Where(t => t.name.StartsWith("ApproachStairLandingRow_")).OrderBy(t => t.name).ToArray();
        Vector3 center = landing.Aggregate(Vector3.zero, (sum, t) => sum + t.position) / landing.Length;
        Vector3 forward = (landing.Last().position - landing.First().position).normalized;
        Undo.RegisterFullObjectHierarchyUndo(fog.gameObject, "계단 안개 정상 가림 설정");
        Undo.RecordObject(mist, "정상 안개 걷힘 연출 설정");
        var settings = new SerializedObject(mist);
        settings.FindProperty("summitFadeHeight").floatValue = 5f;
        settings.FindProperty("summitClearLeadHeight").floatValue = 0.5f;
        settings.FindProperty("overheadHeight").floatValue = 22f;
        settings.FindProperty("climbingDensity").floatValue = 0.24f;
        settings.FindProperty("summitRevealCenter").vector3Value = center;
        settings.FindProperty("summitRevealForward").vector3Value = forward;
        settings.FindProperty("openingHalfWidth").floatValue = 32f;
        settings.FindProperty("openingFeather").floatValue = 4f;
        settings.FindProperty("openingSmoothTime").floatValue = 1.4f;
        settings.ApplyModifiedProperties();
        var volume = new SerializedObject(fog);
        volume.FindProperty("areaSize").vector2Value = new Vector2(110f, 160f);
        volume.FindProperty("maxOpacity").floatValue = 0.999f;
        volume.FindProperty("heightFade").floatValue = 5f;
        volume.FindProperty("edgeFade").floatValue = 12f;
        volume.ApplyModifiedProperties();
        fog.transform.position = new Vector3(0f, 49.15f, 55f);
        fog.SetDensity(0.24f);
        fog.SetSidewaysOpening(center, Vector3.Cross(Vector3.up, forward), 0f, 4f);
        fog.ApplySettings();
        EditorUtility.SetDirty(fog);
        EditorUtility.SetDirty(mist);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬 저장 실패");
        Selection.activeGameObject = fog.gameObject;
        return new { scene.path, fog.name, topHeight = fog.transform.position.y, center = V(center), forward = V(forward), fogCount = UnityEngine.Object.FindObjectsByType<PassHeightFog>(FindObjectsSortMode.None).Length, shaderMessages = ShaderUtil.GetShaderMessages(fog.GetComponentInChildren<MeshRenderer>().sharedMaterial.shader).Select(message => new { message.message, severity = message.severity.ToString() }).ToArray() };
    }

    public static object FindPath()
    {
        var scene = SceneManager.GetActiveScene();
        var stairs = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshFilter>(true))
            .Single(filter => filter.name == "Stairway_Winding_Approach");
        var mesh = stairs.sharedMesh;
        var vertices = mesh.vertices;
        var indices = mesh.triangles;
        var faces = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 a = stairs.transform.TransformPoint(vertices[indices[i]]);
            Vector3 b = stairs.transform.TransformPoint(vertices[indices[i + 1]]);
            Vector3 c = stairs.transform.TransformPoint(vertices[indices[i + 2]]);
            if (Vector3.Cross(b - a, c - a).normalized.y < 0.98f) continue;
            faces.Add((a + b + c) / 3f);
        }
        var steps = faces.GroupBy(point => Mathf.RoundToInt(point.y * 100f))
            .Select(group => group.Aggregate(Vector3.zero, (sum, point) => sum + point) / group.Count()).OrderBy(point => point.y).ToArray();
        float[] heights = { 3.7f, 14f, 21f, 23.5f, 25f, 27.14f };
        var samples = heights.Select(height => steps.OrderBy(point => Mathf.Abs(point.y - height)).First()).ToArray();
        File.WriteAllText(Work + "RouteSamples.json", JsonConvert.SerializeObject(samples.Select(V).ToArray(), Formatting.Indented));
        return samples.Select(V).ToArray();
    }

    public static object Stage(int index)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("플레이 모드에서 확인합니다.");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        if (game.State == PassGameFlow.RunState.Intro) game.BeginJourney();
        var player = game.Player.GetComponent<PassPlayerMovement>();
        player.InputEnabled = false;
        var samples = JArray.Parse(File.ReadAllText(Work + "RouteSamples.json"));
        Vector3 position = ReadVector(samples[index]) + Vector3.up * 0.08f;
        Vector3 direction = index + 1 < samples.Count ? ReadVector(samples[index + 1]) - position : new Vector3(0.77f, 0f, -0.64f);
        direction.y = 0f;
        player.Teleport(position, Quaternion.LookRotation(direction));
        var view = new SerializedObject(game.View);
        view.FindProperty("pitch").floatValue = 16f;
        view.ApplyModifiedPropertiesWithoutUndo();
        game.View.SnapBehind();
        return RuntimeState();
    }

    public static object RuntimeState()
    {
        var mist = UnityEngine.Object.FindFirstObjectByType<PassMountainMist>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var block = new MaterialPropertyBlock();
        mist.ApproachFog.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
        return new { frame = Time.frameCount, player = V(game.Player.position), mist.SummitClarity, openingDistance = block.GetFloat("_OpeningDistance"), density = mist.ApproachFog.Density, fogTop = mist.ApproachFog.transform.position.y, fogCount = UnityEngine.Object.FindObjectsByType<PassHeightFog>(FindObjectsSortMode.None).Length, compilationFailed = EditorUtility.scriptCompilationFailed };
    }

    private static Vector3 ReadVector(JToken token) => new Vector3((float)token[0], (float)token[1], (float)token[2]);

    public static object Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        if (!File.Exists(Work + "BeforeScene.unity"))
        {
            if (!EditorSceneManager.SaveScene(scene, Work + "BeforeScene.unity", true))
                throw new InvalidOperationException("현재 씬의 백업을 저장하지 못했습니다.");
            Directory.CreateDirectory(Work + "SourcesBefore");
            foreach (string source in new[] { "Assets/Scripts/MountainPass/PassMountainMist.cs", "Assets/Scripts/MountainPass/PassHeightFog.cs", "Assets/Arts/Shaders/MountainPass/S_LocalMountainFog.shader" })
                File.Copy(source, Work + "SourcesBefore/" + Path.GetFileName(source), false);
        }
        var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
        return new
        {
            playing = Application.isPlaying,
            scene.path,
            scene.isDirty,
            steps = transforms.Where(t => t.name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponent<Renderer>() != null)
                .Select(t => new { t.name, position = V(t.position), bounds = B(t.GetComponent<Renderer>().bounds) }).ToArray(),
            shrine = transforms.Where(t => t.name.IndexOf("shrine", StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponent<Renderer>() != null)
                .Select(t => new { t.name, bounds = B(t.GetComponent<Renderer>().bounds) }).ToArray(),
            fogs = UnityEngine.Object.FindObjectsByType<PassHeightFog>(FindObjectsSortMode.None).Select(fog =>
            {
                var serialized = new SerializedObject(fog);
                return new { fog.name, position = V(fog.transform.position), bounds = B(fog.GetComponentInChildren<MeshRenderer>().bounds), density = fog.Density, area = new[] { serialized.FindProperty("areaSize").vector2Value.x, serialized.FindProperty("areaSize").vector2Value.y } };
            }).ToArray()
        };
    }

    private static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    private static object B(Bounds b) => new { center = V(b.center), min = V(b.min), max = V(b.max) };
}
