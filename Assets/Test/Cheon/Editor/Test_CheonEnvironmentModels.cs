using System;
using System.Collections.Generic;
using System.Linq;
using Cheon.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 가져온 환경 프리팹을 기존 테스트 맵의 배치와 연결한다.
public static class Test_CheonEnvironmentModels
{
    public const string MarkerName = "Test_BlenderEnvironmentModels";
    private const string Root = "Assets/Test/Cheon/Arts/EnvironmentModels/";
    private static readonly string[] ModelPaths = {
        "01_Lantern/Test_Cheon_Lantern.prefab", "02_Grass/Test_Cheon_Grass.prefab",
        "03_Flowers/Test_Cheon_Flowers.prefab", "04_Gate/Test_Cheon_Gate.prefab",
        "05_Wall/Test_Cheon_Wall.prefab", "06_Bell_Pavilion/Test_Cheon_BellPavilion.prefab",
        "07_Mountain_Spirit_Shrine/Test_Cheon_MountainSpiritShrine.prefab"
    };

    [MenuItem("Tools/Cheon Test/Apply Blender Environment Models")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || (!string.IsNullOrEmpty(scene.path) && scene.path != Test_CheonSceneBuilder.ScenePath))
            throw new InvalidOperationException("천지인 테스트 씬의 Edit Mode에서 실행하세요.");
        var world = Object.FindFirstObjectByType<Test_CheonWorld>();
        if (world == null) throw new InvalidOperationException("천지인 테스트 맵이 없습니다.");
        if (world.transform.Find(MarkerName) != null) return;
        var prefabs = ModelPaths.Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(Root + path)).ToArray();
        if (prefabs.Any(prefab => prefab == null)) throw new InvalidOperationException("환경 모델 7종의 프리팹을 먼저 가져오세요.");

        var originals = world.GetComponentsInChildren<Transform>(true);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("블렌더 환경 모델 적용");
        try
        {
            var marker = new GameObject(MarkerName).transform;
            marker.SetParent(world.transform, false); Undo.RegisterCreatedObjectUndo(marker.gameObject, "환경 모델 배치");
            var lanterns = NewGroup(marker, "Test_ImportedLanterns");
            var plants = NewGroup(marker, "Test_ImportedPlants");
            var buildings = NewGroup(marker, "Test_ImportedBuildings");
            var walls = NewGroup(marker, "Test_ImportedWalls");

            foreach (var item in originals.Where(t => t.name == "Test_석등" || t.name == "Test_추가석등"))
            {
                Bounds target = LocalBounds(item);
                float scale = target.size.y / LocalBounds(prefabs[0].transform).size.y;
                Place(prefabs[0], lanterns, item.position, item.rotation, Vector3.one * scale);
                HideOriginalRenderers(item);
            }
            int plantIndex = 0;
            foreach (var item in originals.Where(t => t.name == "Test_Grass" || t.name == "Test_ExtraGrass"))
            {
                // 꽃을 일정 간격으로 섞고, 기존 줄기 스케일 대신 군락 전체의 높이를 맞춘다.
                var prefab = prefabs[plantIndex++ % 6 == 0 ? 2 : 1];
                float height = item.GetComponent<MeshRenderer>().bounds.size.y;
                float scale = Mathf.Clamp(height / LocalBounds(prefab.transform).size.y, .3f, .85f);
                Place(prefab, plants, item.position, Quaternion.Euler(0, item.eulerAngles.y, 0), Vector3.one * scale);
                HideOriginalRenderers(item);
            }
            ReplaceBuilding(originals.First(t => t.name == "Test_산신당"), prefabs[6], buildings);
            ReplaceBuilding(originals.First(t => t.name == "Test_청동종각"), prefabs[5], buildings);
            // 경계문 상단의 기존 활성화 전환을 그대로 따르게 같은 부모 아래 배치한다.
            var gate = originals.First(t => t.name == "Test_UpperGate");
            ReplaceBuilding(gate, prefabs[3], gate);

            string[] wallNames = { "Test_BoundaryWall", "Test_ExtraBoundaryWall", "Test_RealmBoundaryExtension", "Test_SpiritShoulderWall" };
            var wallParts = originals.Where(t => wallNames.Contains(t.name)).ToArray();
            foreach (int side in new[] { -1, 1 })
            {
                var bounds = wallParts.Where(t => Mathf.Sign(t.position.x) == side).Select(t => t.GetComponent<MeshRenderer>().bounds).ToArray();
                float minimum = bounds.Min(b => b.min.x), maximum = bounds.Max(b => b.max.x);
                int count = Mathf.CeilToInt((maximum - minimum) / 4.4f);
                float width = (maximum - minimum) / count;
                float scale = width / LocalBounds(prefabs[4].transform).size.x;
                for (int index = 0; index < count; index++)
                    Place(prefabs[4], walls, new Vector3(minimum + (index + .5f) * width, 0, bounds[0].center.z), Quaternion.identity, Vector3.one * scale);
            }
            foreach (var item in originals.Where(t => wallNames.Contains(t.name) || t.name == "Test_WallCap" || t.name == "Test_ExtraWallCap" || t.name == "Test_RealmBoundaryCap" || t.name == "Test_SpiritShoulderWallCap"))
                HideOriginalRenderers(item);
            // 원본 오브젝트는 충돌체와 게임플레이 참조를 보존하고 표시만 교체한다.
            RegisterPalette(world, prefabs);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static Transform NewGroup(Transform parent, string name)
    {
        var group = new GameObject(name).transform; group.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(group.gameObject, "모델 분류"); return group;
    }

    private static GameObject Place(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "환경 프리팹 배치");
        instance.transform.SetPositionAndRotation(position, rotation); instance.transform.localScale = scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance;
    }

    private static void ReplaceBuilding(Transform original, GameObject prefab, Transform parent)
    {
        Bounds target = LocalBounds(original), source = LocalBounds(prefab.transform);
        Vector3 scale = new Vector3(target.size.x / source.size.x, target.size.y / source.size.y, target.size.z / source.size.z);
        // 표시 크기는 기존 건물 영역에 맞추되, 기존 충돌체와 회복 위치 참조는 유지한다.
        HideOriginalRenderers(original);
        // FBX의 정면(+Z)을 기존 테스트 건물의 정면(-Z)에 맞춘다.
        Quaternion facing = Quaternion.Euler(0, 180, 0);
        Vector3 sourceBase = facing * Vector3.Scale(new Vector3(source.center.x, source.min.y, source.center.z), scale);
        Vector3 offset = new Vector3(target.center.x, target.min.y, target.center.z) - sourceBase;
        Place(prefab, parent, original.TransformPoint(offset), original.rotation * facing, scale);
    }

    private static void HideOriginalRenderers(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            Undo.RecordObject(renderer, "임시 모델 표시 교체"); renderer.enabled = false;
        }
    }

    private static Bounds LocalBounds(Transform root)
    {
        var points = new List<Vector3>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds bounds = filter.sharedMesh.bounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                points.Add(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
        }
        if (points.Count == 0) throw new InvalidOperationException("모델 메시가 없습니다: " + root.name);
        var result = new Bounds(points[0], Vector3.zero); foreach (var point in points) result.Encapsulate(point); return result;
    }

    private static void RegisterPalette(Test_CheonWorld world, GameObject[] prefabs)
    {
        var materials = prefabs.SelectMany(p => p.GetComponentsInChildren<MeshRenderer>(true)).SelectMany(r => r.sharedMaterials).Distinct();
        var data = new SerializedObject(world); var palettes = data.FindProperty("palettes");
        var existing = new HashSet<Object>();
        for (int index = 0; index < palettes.arraySize; index++) existing.Add(palettes.GetArrayElementAtIndex(index).FindPropertyRelative("material").objectReferenceValue);
        foreach (var material in materials)
        {
            if (existing.Contains(material)) continue;
            int index = palettes.arraySize; palettes.InsertArrayElementAtIndex(index);
            var entry = palettes.GetArrayElementAtIndex(index);
            Color reality = material.GetColor("_BaseColor");
            entry.FindPropertyRelative("material").objectReferenceValue = material;
            entry.FindPropertyRelative("reality").colorValue = reality;
            entry.FindPropertyRelative("spirit").colorValue = reality * new Color(.72f, .85f, .97f, 1);
        }
        data.ApplyModifiedProperties();
    }
}
