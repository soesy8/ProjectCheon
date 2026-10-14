using System.Linq;
using Cheon.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 지형 크기와 두 영역의 길이를 조절하되, 개별 프랍의 모양과 크기는 보존한다.
public static class Test_CheonMapLayout
{
    public const string MarkerName = "Test_MapLayout_48x104_35_65";
    public const float MinimumZ = -49.5f;
    public const float MaximumZ = 54.5f;
    public const float RealityFraction = .35f;
    public const float BoundaryZ = MinimumZ + (MaximumZ - MinimumZ) * RealityFraction;
    private const float WidthScale = .8f;
    private const float DepthScale = 1.3f;
    private const float PreviousMinimumZ = -37.5f;
    private const float PreviousMaximumZ = 42.5f;
    private const float PreviousCenterZ = 2.5f;
    private const float SpiritStretch = (MaximumZ - BoundaryZ) / PreviousMaximumZ;
    private static Transform scenery;

    [MenuItem("Tools/Cheon Test/Apply Map Shape And Realm Ratio")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || (!string.IsNullOrEmpty(scene.path) && scene.path != Test_CheonSceneBuilder.ScenePath))
            throw new System.InvalidOperationException("천지인 테스트 씬의 Edit Mode에서 실행하세요.");
        var world = Object.FindFirstObjectByType<Test_CheonWorld>();
        if (world == null || world.transform.Find("Test_ExpandedLandscape") == null)
            throw new System.InvalidOperationException("맵 확장 도구를 먼저 적용하세요.");
        scenery = world.transform;
        bool alreadyApplied = scenery.Find(MarkerName) != null;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("천지인 맵 형태와 현실·영계 비율 조정");
        try
        {
            if (!alreadyApplied)
            {
                Undo.RecordObjects(scenery.GetComponentsInChildren<Transform>(true).Cast<Object>().ToArray(), "프랍 배치 간격 조정");
                MovePlacements(); ResizeTerrain(); ResizeArena();
                var marker = new GameObject(MarkerName).transform;
                marker.SetParent(scenery, false); Undo.RegisterCreatedObjectUndo(marker.gameObject, "맵 배치 적용 기록");
                CompleteBoundaryWalls(marker);
            }
            ConfigureGameplay(!alreadyApplied);
            if (scenery.Find(Test_CheonSpiritWidth.MarkerName) != null) Test_CheonSpiritWidth.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static float MapRegionZ(float value)
    {
        // 경계 양쪽을 따로 배치해 현실 36.4, 영계 67.6의 길이를 만든다.
        if (value <= 0)
            return Mathf.LerpUnclamped(MinimumZ, BoundaryZ, (value - PreviousMinimumZ) / -PreviousMinimumZ);
        return BoundaryZ + value * SpiritStretch;
    }

    private static Vector3 MapPosition(Vector3 position, bool regionPlacement)
    {
        position.x *= WidthScale;
        position.z = regionPlacement ? MapRegionZ(position.z) : PreviousCenterZ + (position.z - PreviousCenterZ) * DepthScale;
        return position;
    }

    private static void MoveTransform(Transform item, bool regionPlacement)
    {
        Vector3 localPosition = scenery.InverseTransformPoint(item.position);
        item.position = scenery.TransformPoint(MapPosition(localPosition, regionPlacement));
    }

    private static void MovePlacements()
    {
        var expansion = scenery.Find("Test_ExpandedLandscape");
        var roots = scenery.Cast<Transform>().Where(t => t != expansion).Concat(expansion.Cast<Transform>()).ToArray();
        foreach (var item in roots)
        {
            if (item.name == "Test_MossTerrace" || item.name == "Test_WalkableGround" || item.name == "Test_CliffSkirt" || item.name == "Test_SpiritTerrace") continue;
            bool landscapeEdge = item.name.Contains("Cliff") || item.name.Contains("DistantPeak") || item.name.Contains("CloudBank");
            // 종각·소나무·물 프랍은 배치 루트만 옮겨 자식들의 크기와 간격을 유지한다.
            MoveTransform(item, !landscapeEdge);
        }
    }

    private static void ResizeTerrain()
    {
        foreach (var floor in new[] { scenery.Find("Test_MossTerrace"), scenery.Find("Test_ExpandedLandscape/Test_CliffSkirt") })
        {
            MoveTransform(floor, false);
            Vector3 scale = floor.localScale;
            floor.localScale = new Vector3(scale.x * WidthScale, scale.y, scale.z * DepthScale);
        }
        var ground = scenery.Find("Test_WalkableGround"); MoveTransform(ground, false);
        var collider = ground.GetComponent<BoxCollider>(); Undo.RecordObject(collider, "보행 지형 크기 조정");
        collider.size = new Vector3(collider.size.x * WidthScale, collider.size.y, collider.size.z * DepthScale);
    }

    private static void ResizeArena()
    {
        var floor = scenery.Find("Test_SpiritTerrace"); MoveTransform(floor, true);
        Vector3 scale = floor.localScale;
        floor.localScale = new Vector3(scale.x * WidthScale, scale.y, scale.z * SpiritStretch);
        foreach (string name in new[] { "Test_OuterSeal", "Test_InnerSeal" })
        {
            var line = scenery.Find(name).GetComponent<LineRenderer>(); Undo.RecordObject(line, "영계 바닥 문양 조정");
            Vector3[] points = new Vector3[line.positionCount]; line.GetPositions(points);
            for (int i = 0; i < points.Length; i++) points[i] = new Vector3(points[i].x * WidthScale, points[i].y, points[i].z * SpiritStretch);
            line.SetPositions(points);
        }
    }

    private static void ConfigureGameplay(bool moveActors)
    {
        var player = Object.FindFirstObjectByType<Test_CheonPlayer>();
        var boss = Object.FindFirstObjectByType<Test_CheonBoss>(FindObjectsInactive.Include);
        var session = Object.FindFirstObjectByType<Test_CheonSession>();
        if (moveActors)
        {
            Undo.RecordObject(player.transform, "도사 시작 위치 조정"); MoveTransform(player.transform, true);
            Undo.RecordObject(boss.transform, "장승 시작 위치 조정"); MoveTransform(boss.transform, true);
        }
        var playerData = new SerializedObject(player);
        playerData.FindProperty("horizontalBounds").vector2Value = new Vector2(-23.04f, 23.04f);
        playerData.FindProperty("depthBounds").vector2Value = new Vector2(MinimumZ + .7f, MaximumZ - .6f);
        playerData.FindProperty("cornerInset").floatValue = 4;
        playerData.FindProperty("cornerDepthInset").floatValue = 6.5f;
        playerData.ApplyModifiedProperties();
        var bossData = new SerializedObject(boss);
        bossData.FindProperty("horizontalBounds").vector2Value = new Vector2(-19, 19);
        bossData.FindProperty("depthBounds").vector2Value = new Vector2(MapRegionZ(7.5f), MapRegionZ(37.5f));
        bossData.ApplyModifiedProperties();
        var sessionData = new SerializedObject(session);
        sessionData.FindProperty("shrinePosition").vector3Value = scenery.Find("RecoveryStone").position;
        sessionData.ApplyModifiedProperties();
        var rig = Object.FindFirstObjectByType<Test_CheonCamera>();
        Vector3 offset = new SerializedObject(rig).FindProperty("offset").vector3Value;
        Undo.RecordObject(rig.transform, "카메라 시작 위치 조정");
        rig.transform.position = player.transform.position + Vector3.forward * 3 + offset;
    }

    private static void CompleteBoundaryWalls(Transform parent)
    {
        // 프랍 크기를 유지하면서 X 간격을 좁힌 뒤 생기는 가장자리 틈만 막는다.
        string materials = "Assets/Test/Cheon/Arts/Materials/";
        var stone = AssetDatabase.LoadAssetAtPath<Material>(materials + "Test_M_MountainStone.mat");
        var tiles = AssetDatabase.LoadAssetAtPath<Material>(materials + "Test_M_BlueTiles.mat");
        foreach (int side in new[] { -1, 1 })
        {
            AddWallPart(parent, "Test_RealmBoundaryExtension", new Vector3(side * 23.3f, .65f, BoundaryZ), new Vector3(1.4f, 1.3f, .65f), stone, true);
            AddWallPart(parent, "Test_RealmBoundaryCap", new Vector3(side * 23.3f, 1.35f, BoundaryZ), new Vector3(1.48f, .22f, 1), tiles, false);
        }
    }

    private static void AddWallPart(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collision)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = name; item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(item.GetComponent<Collider>());
    }
}
