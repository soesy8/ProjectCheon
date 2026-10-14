using System.Collections.Generic;
using System.Linq;
using Cheon.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 현실의 폭을 유지하면서 영적 영역의 지형과 배치 간격만 넓힌다.
public static class Test_CheonSpiritWidth
{
    public const string MarkerName = "Test_SpiritWidth_130";
    public const float WidthMultiplier = 1.3f;
    private const float RealityWidth = 48;
    public const float SpiritWidth = RealityWidth * WidthMultiplier;
    private const string MeshFolder = "Assets/Test/Cheon/Arts/Meshes/";

    [MenuItem("Tools/Cheon Test/Apply Wider Spirit Realm")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || (!string.IsNullOrEmpty(scene.path) && scene.path != Test_CheonSceneBuilder.ScenePath))
            throw new System.InvalidOperationException("천지인 테스트 씬의 Edit Mode에서 실행하세요.");
        var world = Object.FindFirstObjectByType<Test_CheonWorld>();
        if (world == null || world.transform.Find(Test_CheonMapLayout.MarkerName) == null)
            throw new System.InvalidOperationException("맵 형태와 영역 비율 조정을 먼저 적용하세요.");
        Transform scenery = world.transform;
        var marker = scenery.Find(MarkerName);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("영적 영역 가로 30% 확장");
        try
        {
            if (marker == null)
            {
                Undo.RecordObjects(scenery.GetComponentsInChildren<Transform>(true).Cast<Object>().ToArray(), "영적 영역 프랍 배치 간격 확장");
                MoveSpiritPlacements(scenery);
                ResizeTerrain(scenery);
                marker = new GameObject(MarkerName).transform;
                marker.SetParent(scenery, false);
                AddShoulderWalls(marker);
                var ground = new GameObject("Test_SpiritWalkableGround");
                ground.transform.SetParent(marker, false); ground.AddComponent<BoxCollider>();
                Undo.RegisterCreatedObjectUndo(marker.gameObject, "영적 영역 확장 기록");
            }
            ConfigureGround(scenery, marker);
            ConfigureGameplay();
            FitSpiritFloor(scenery);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private static void MoveSpiritPlacements(Transform scenery)
    {
        Transform expansion = scenery.Find("Test_ExpandedLandscape");
        var roots = scenery.Cast<Transform>().Where(t => t != expansion).Concat(expansion.Cast<Transform>()).ToArray();
        foreach (var item in roots)
        {
            if (item.name == "Test_MossTerrace" || item.name == "Test_WalkableGround" || item.name == "Test_CliffSkirt" || item.name == "Test_SpiritTerrace" || item.name == "Test_OuterSeal" || item.name == "Test_InnerSeal" || item.name == Test_CheonMapLayout.MarkerName) continue;
            if (item.localPosition.z <= Test_CheonMapLayout.BoundaryZ + .001f) continue;
            // 배치 루트만 움직여 프랍 내부의 형태와 크기를 그대로 보존한다.
            Vector3 position = item.localPosition; position.x *= WidthMultiplier; item.localPosition = position;
        }
    }

    private static void FitSpiritFloor(Transform scenery)
    {
        // 바닥에도 영적 영역과 같은 가로·세로 비율을 적용해 긴 타원으로 늘어나지 않게 한다.
        float width = 54;
        float depth = width * (Test_CheonMapLayout.MaximumZ - Test_CheonMapLayout.BoundaryZ) / SpiritWidth;
        float centerZ = (Test_CheonMapLayout.BoundaryZ + Test_CheonMapLayout.MaximumZ) / 2;
        var floor = scenery.Find("Test_SpiritTerrace"); Undo.RecordObject(floor,"영적 바닥 비율 조정");
        floor.localPosition = new Vector3(0,floor.localPosition.y,centerZ);
        floor.localScale = new Vector3(width,floor.localScale.y,depth);
        string[] names = { "Test_OuterSeal", "Test_InnerSeal" };
        float[] fractions = { 7.8f / 16.5f, 4.9f / 16.5f };
        for (int ring = 0; ring < names.Length; ring++)
        {
            var line = scenery.Find(names[ring]).GetComponent<LineRenderer>();
            Undo.RecordObjects(new Object[] { line,line.transform },"바닥 문양 비율 조정");
            line.transform.localPosition = new Vector3(0,line.transform.localPosition.y,centerZ);
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(i,new Vector3(Mathf.Sin(angle)*width*fractions[ring],0,Mathf.Cos(angle)*depth*fractions[ring]));
            }
        }
        var marks = scenery.Cast<Transform>().Where(item => item.name == "Test_SealMark").ToArray();
        Undo.RecordObjects(marks.Cast<Object>().ToArray(),"바닥 부호 배치 조정");
        for (int i = 0; i < marks.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / marks.Length;
            marks[i].localPosition = new Vector3(Mathf.Cos(angle)*width*7.4f/16.5f,marks[i].localPosition.y,centerZ+Mathf.Sin(angle)*depth*7.4f/16.5f);
        }
    }

    private static void ResizeTerrain(Transform scenery)
    {
        float low = Test_CheonMapLayout.MinimumZ, high = Test_CheonMapLayout.MaximumZ, boundary = Test_CheonMapLayout.BoundaryZ;
        Vector3[] real = { Point(-20,low), Point(20,low), Point(24,low+6.5f), Point(24,boundary), Point(-24,boundary), Point(-24,low+6.5f) };
        Vector3[] spirit = { Point(-31.2f,boundary), Point(31.2f,boundary), Point(31.2f,high-6.5f), Point(26,high), Point(-26,high), Point(-31.2f,high-6.5f) };
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        // 영역별 볼록 다각형으로 나누면 폭이 달라지는 경계에도 빈 삼각형이 생기지 않는다.
        foreach (var polygon in new[] { real, spirit })
        {
            int start = vertices.Count;
            Vector3 center = polygon.Aggregate(Vector3.zero, (sum, p) => sum + p) / polygon.Length;
            vertices.Add(center); vertices.AddRange(polygon);
            for (int i = 0; i < polygon.Length; i++) triangles.AddRange(new[] { start, start + (i + 1) % polygon.Length + 1, start + i + 1 });
        }
        var terrain = new Mesh { name = "Test_RealmSplitTerrain", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        terrain.RecalculateNormals(); terrain.RecalculateBounds();
        AssignMesh(scenery.Find("Test_MossTerrace"), terrain, MeshFolder + "Test_RealmSplitTerrain.asset", new Vector3(0,-.08f,0));

        Vector3[] outline = { real[0],real[1],real[2],real[3],spirit[1],spirit[2],spirit[3],spirit[4],spirit[5],spirit[0],real[4],real[5] };
        vertices.Clear();
        for (int i = 0; i < outline.Length; i++)
        {
            Vector3 a = outline[i], b = outline[(i + 1) % outline.Length]; a.y = b.y = -.08f;
            Vector3 lowerA = new Vector3(a.x * .88f,-7,2.5f+(a.z-2.5f)*.88f);
            Vector3 lowerB = new Vector3(b.x * .88f,-7,2.5f+(b.z-2.5f)*.88f);
            vertices.AddRange(new[] { a,b,lowerB,a,lowerB,lowerA });
        }
        var skirt = new Mesh { name = "Test_RealmSplitCliffSkirt", vertices = vertices.ToArray(), triangles = Enumerable.Range(0,vertices.Count).ToArray() };
        skirt.RecalculateNormals(); skirt.RecalculateBounds();
        AssignMesh(scenery.Find("Test_ExpandedLandscape/Test_CliffSkirt"), skirt, MeshFolder + "Test_RealmSplitCliffSkirt.asset", Vector3.zero);
    }

    private static Vector3 Point(float x, float z) => new Vector3(x,0,z);

    private static void AssignMesh(Transform target, Mesh mesh, string path, Vector3 position)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh,path); existing = mesh; }
        else { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); }
        var filter = target.GetComponent<MeshFilter>(); Undo.RecordObject(filter,"영역별 지형 메시 적용"); filter.sharedMesh = existing;
        target.localPosition = position; target.localScale = Vector3.one;
    }

    private static void ConfigureGround(Transform scenery, Transform marker)
    {
        float boundary = Test_CheonMapLayout.BoundaryZ;
        var reality = scenery.Find("Test_WalkableGround").GetComponent<BoxCollider>();
        Undo.RecordObjects(new Object[] { reality,reality.transform },"현실 보행 지형");
        reality.transform.localPosition = new Vector3(0,-.18f,(Test_CheonMapLayout.MinimumZ+boundary)/2);
        reality.size = new Vector3(RealityWidth,.35f,boundary-Test_CheonMapLayout.MinimumZ);
        var spirit = marker.Find("Test_SpiritWalkableGround").GetComponent<BoxCollider>();
        Undo.RecordObjects(new Object[] { spirit,spirit.transform },"영적 영역 보행 지형");
        spirit.transform.localPosition = new Vector3(0,-.18f,(boundary+Test_CheonMapLayout.MaximumZ)/2);
        spirit.size = new Vector3(SpiritWidth,.35f,Test_CheonMapLayout.MaximumZ-boundary);
    }

    private static void ConfigureGameplay()
    {
        var player = new SerializedObject(Object.FindFirstObjectByType<Test_CheonPlayer>());
        player.FindProperty("spiritWidthMultiplier").floatValue = WidthMultiplier; player.ApplyModifiedProperties();
        var boss = new SerializedObject(Object.FindFirstObjectByType<Test_CheonBoss>(FindObjectsInactive.Include));
        boss.FindProperty("horizontalBounds").vector2Value = new Vector2(-19,19) * WidthMultiplier; boss.ApplyModifiedProperties();
    }

    private static void AddShoulderWalls(Transform marker)
    {
        var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/Cheon/Arts/Materials/Test_M_MountainStone.mat");
        var tiles = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/Cheon/Arts/Materials/Test_M_BlueTiles.mat");
        foreach (int side in new[] { -1,1 })
        {
            // 기존 담장 폭은 유지하고 영적 영역에 추가된 양옆만 연결한다.
            for (int segment = 0; segment < 3; segment++)
            {
                float x = side * (24 + (segment + .5f) * 2.4f);
                foreach (bool cap in new[] { false,true })
                {
                    var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = cap ? "Test_SpiritShoulderWallCap" : "Test_SpiritShoulderWall";
                    part.transform.SetParent(marker,false); part.transform.localPosition = new Vector3(x,cap ? 1.35f : .65f,Test_CheonMapLayout.BoundaryZ);
                    part.transform.localScale = cap ? new Vector3(2.48f,.22f,1) : new Vector3(2.4f,1.3f,.65f);
                    part.GetComponent<Renderer>().sharedMaterial = cap ? tiles : stone;
                    if (cap) Object.DestroyImmediate(part.GetComponent<Collider>());
                }
            }
        }
    }
}
