using System.Collections.Generic;
using System.Linq;
using Cheon.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 기존 씬의 프랍을 보존하면서 위치 간격과 지형만 확장하는 편집 도구다.
public static class Test_CheonMapExpansion
{
    private const float MapScale = 2.5f;
    private const string Root = "Assets/Test/Cheon/Arts/";
    private static Transform scenery, additions;
    private static Mesh cone, rock, roof;
    private static Material stone, bark, leaves, tiles, wood, cream, gold, jade, dark, red, water, spirit;
    private static readonly List<Vector3> occupied = new List<Vector3>();
    private static readonly List<Transform> newMotes = new List<Transform>();
    private static readonly Vector3[] landmarks =
    {
        new Vector3(-18, 0, -10), new Vector3(17, 0, -28), new Vector3(-19, 0, 16),
        new Vector3(22, 0, 24), new Vector3(-18, 0, 33), new Vector3(18, 0, -8),
        new Vector3(-18, 0, -32), new Vector3(23, 0, 7), new Vector3(-20, 0, 2),
        new Vector3(10, 0, -18)
    };
    private static readonly Vector3[] pathStarts =
    {
        new Vector3(0,0,-29), new Vector3(0,0,-10), new Vector3(0,0,-18),
        new Vector3(0,0,-32), new Vector3(0,0,16), new Vector3(0,0,33)
    };
    private static readonly Vector3[] pathEnds =
    {
        new Vector3(-15,0,-29), new Vector3(-18,0,-10), new Vector3(10,0,-18),
        new Vector3(-18,0,-32), new Vector3(-19,0,16), new Vector3(-18,0,33)
    };

    [MenuItem("Tools/Cheon Test/Apply Expanded Map And Camera")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || (scene.path != Test_CheonSceneBuilder.ScenePath && !string.IsNullOrEmpty(scene.path)))
            throw new System.InvalidOperationException("천지인 테스트 씬의 Edit Mode에서 실행하세요.");
        var world = Object.FindFirstObjectByType<Test_CheonWorld>();
        if (world == null) throw new System.InvalidOperationException("테스트 수련지 컴포넌트가 없습니다.");
        scenery = world.transform;
        Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("천지인 맵 2.5배 확장과 카메라 조정");
        var randomState = Random.state;
        bool alreadyExpanded = scenery.Find("Test_ExpandedLandscape") != null;
        try
        {
            if (!alreadyExpanded)
            {
                LoadAssets(); Random.InitState(25102026); occupied.Clear(); newMotes.Clear();
                GroupWaterFeature();
                Undo.RecordObjects(scenery.GetComponentsInChildren<Transform>(true).Cast<Object>().ToArray(), "지형과 프랍 위치 확장");
                // 프랍의 localScale과 자식 구조를 건드리지 않고 배치 위치의 X/Z 간격만 넓힌다.
                foreach (Transform child in scenery)
                {
                    Vector3 position = child.localPosition; position.x *= MapScale; position.z *= MapScale; child.localPosition = position;
                }
                foreach (string name in new[] { "Test_MossTerrace", "Test_SpiritTerrace" })
                {
                    Transform floor = scenery.Find(name); Vector3 size = floor.localScale;
                    floor.localScale = new Vector3(size.x * MapScale, size.y, size.z * MapScale);
                }
                var ground = scenery.Find("Test_WalkableGround").GetComponent<BoxCollider>();
                Undo.RecordObject(ground, "확장된 보행 지형"); ground.size = new Vector3(60, ground.size.y, 80);
                foreach (string name in new[] { "Test_OuterSeal", "Test_InnerSeal" })
                {
                    var line = scenery.Find(name).GetComponent<LineRenderer>(); Undo.RecordObject(line, "바닥 문양 확장");
                    Vector3[] points = new Vector3[line.positionCount]; line.GetPositions(points);
                    for (int i = 0; i < points.Length; i++) points[i] *= MapScale;
                    line.SetPositions(points);
                }
                additions = Group("Test_ExpandedLandscape", scenery, Vector3.zero);
                BuildCliffsAndClouds(); FillBoundaryWalls(); BuildPaths(); BuildLandmarks(); ScatterScenery();
                var serializedWorld = new SerializedObject(world); var motes = serializedWorld.FindProperty("motes");
                int previousCount = motes.arraySize; motes.arraySize += newMotes.Count;
                for (int i = 0; i < newMotes.Count; i++) motes.GetArrayElementAtIndex(previousCount + i).objectReferenceValue = newMotes[i];
                serializedWorld.ApplyModifiedProperties();
                Undo.RegisterCreatedObjectUndo(additions.gameObject, "확장 공간 프랍 배치");
            }
            ConfigureGameplayAndCamera(!alreadyExpanded);
            if (scenery.Find(Test_CheonMapLayout.MarkerName) != null) Test_CheonMapLayout.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            var finalTerrain = scenery.Find("Test_MossTerrace").GetComponent<Renderer>().bounds;
            Debug.Log($"천지인: 지형 최대 폭 {finalTerrain.size.x:0.##}, 세로 {finalTerrain.size.z:0.##}, 기본 시야 8.4, 전투 시야 10.08 적용 완료");
        }
        finally { Random.state = randomState; Undo.CollapseUndoOperations(undoGroup); }
    }

    private static void LoadAssets()
    {
        cone = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Meshes/Test_PineCone.asset");
        rock = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Meshes/Test_Rock.asset");
        roof = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Meshes/Test_HanokRoof.asset");
        stone = Mat("MountainStone"); bark = Mat("PineBark"); leaves = Mat("PineNeedles"); tiles = Mat("BlueTiles");
        wood = Mat("RedPillars"); cream = Mat("Hanji"); gold = Mat("Brass"); jade = Mat("Jade");
        dark = Mat("Ink"); red = Mat("Vermilion"); water = Mat("Water"); spirit = Mat("SpiritWood");
    }
    private static Material Mat(string name) { return AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Test_M_" + name + ".mat"); }
    private static Transform Group(string name, Transform parent, Vector3 position)
    { var item = new GameObject(name).transform; item.SetParent(parent, false); item.localPosition = position; return item; }
    private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false)
    {
        var item = GameObject.CreatePrimitive(type); item.name = name; item.transform.SetParent(parent, false);
        item.transform.localPosition = position; item.transform.localScale = scale; item.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(item.GetComponent<Collider>()); return item;
    }
    private static GameObject MeshShape(string name, Mesh mesh, Transform parent, Vector3 position, Vector3 scale, Material material)
    { var item = Group(name, parent, position).gameObject; item.transform.localScale = scale; item.AddComponent<MeshFilter>().sharedMesh = mesh; item.AddComponent<MeshRenderer>().sharedMaterial = material; return item; }
    private static void Beam(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
    { var item = Shape(name, PrimitiveType.Cylinder, parent, (from + to) / 2, new Vector3(width, Vector3.Distance(from, to) / 2, width), material); item.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from); }
    private static void Ring(string name, Transform parent, Vector3 position, float radius, Material material, bool vertical = false)
    {
        var line = Group(name, parent, position).gameObject.AddComponent<LineRenderer>(); line.sharedMaterial = material;
        line.useWorldSpace = false; line.positionCount = 64; line.loop = true; line.widthMultiplier = .04f;
        for (int i = 0; i < 64; i++) { float angle = i * Mathf.PI / 32; line.SetPosition(i, vertical ? new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0) : new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius)); }
    }

    private static void GroupWaterFeature()
    {
        // 낙수·물결·연못은 한 프랍으로 묶어 서로 붙어 있는 간격을 유지한다.
        Transform[] waterParts = scenery.Cast<Transform>().Where(t => t.name == "Test_Pond" || t.name == "Test_Waterfall" || t.name == "Test_WaterRibbon").ToArray();
        var waterGroup = Group("Test_WaterGarden", scenery, new Vector3(11.8f, 0, -7));
        Undo.RegisterCreatedObjectUndo(waterGroup.gameObject, "물 프랍 묶음 유지");
        foreach (var part in waterParts) Undo.SetTransformParent(part, waterGroup, "물 프랍 묶음 유지");
    }

    private static void ConfigureGameplayAndCamera(bool moveStartPositions)
    {
        var player = Object.FindFirstObjectByType<Test_CheonPlayer>();
        var boss = Object.FindFirstObjectByType<Test_CheonBoss>(FindObjectsInactive.Include);
        var session = Object.FindFirstObjectByType<Test_CheonSession>();
        var rig = Object.FindFirstObjectByType<Test_CheonCamera>(); var camera = rig.GetComponent<Camera>();
        if (moveStartPositions)
        {
            Undo.RecordObject(player.transform, "도사 시작 위치 확장"); player.transform.position = ScalePosition(player.transform.position);
            Undo.RecordObject(boss.transform, "장승 시작 위치 확장"); boss.transform.position = ScalePosition(boss.transform.position);
        }
        SetVector2(player, "horizontalBounds", new Vector2(-28.8f, 28.8f)); SetVector2(player, "depthBounds", new Vector2(-36.8f, 41.9f)); SetFloat(player, "cornerInset", 5);
        SetVector2(boss, "horizontalBounds", new Vector2(-23.75f, 23.75f)); SetVector2(boss, "depthBounds", new Vector2(7.5f, 37.5f));
        SetVector(session, "shrinePosition", scenery.Find("RecoveryStone").position);
        Vector3 offset = new Vector3(10.5f, 16.1f, -16.1f);
        SetVector(rig, "offset", offset); SetFloat(rig, "explorationSize", 8.4f); SetFloat(rig, "battleDistanceMultiplier", 1.2f);
        Undo.RecordObject(camera, "카메라 기본 시야"); camera.orthographicSize = 8.4f; camera.farClipPlane = 320;
        Undo.RecordObject(camera.transform, "카메라 기본 거리"); camera.transform.position = player.transform.position + Vector3.forward * 3 + offset; camera.transform.rotation = Quaternion.LookRotation(-offset);
        RenderSettings.fogStartDistance = 70; RenderSettings.fogEndDistance = 240;
    }
    private static Vector3 ScalePosition(Vector3 position) { return new Vector3(position.x * MapScale, position.y, position.z * MapScale); }
    private static void SetVector(Object item, string name, Vector3 value) { var data = new SerializedObject(item); data.FindProperty(name).vector3Value = value; data.ApplyModifiedProperties(); }
    private static void SetVector2(Object item, string name, Vector2 value) { var data = new SerializedObject(item); data.FindProperty(name).vector2Value = value; data.ApplyModifiedProperties(); }
    private static void SetFloat(Object item, string name, float value) { var data = new SerializedObject(item); data.FindProperty(name).floatValue = value; data.ApplyModifiedProperties(); }

    private static void BuildCliffsAndClouds()
    {
        // 넓어진 바닥의 옆면은 지형 메시로 막고, 원래 크기의 절벽 돌을 더해 실루엣을 만든다.
        const string meshPath = Root + "Meshes/Test_ExpandedCliffSkirt.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            Vector3[] edge = { new Vector3(-25,-.1f,-37.5f), new Vector3(25,-.1f,-37.5f), new Vector3(30,-.1f,-32.5f), new Vector3(30,-.1f,37.5f), new Vector3(25,-.1f,42.5f), new Vector3(-25,-.1f,42.5f), new Vector3(-30,-.1f,37.5f), new Vector3(-30,-.1f,-32.5f) };
            var vertices = new List<Vector3>();
            for (int i = 0; i < edge.Length; i++)
            {
                Vector3 a = edge[i], b = edge[(i + 1) % edge.Length];
                Vector3 lowerA = new Vector3(a.x * .88f, -7, 2.5f + (a.z - 2.5f) * .88f), lowerB = new Vector3(b.x * .88f, -7, 2.5f + (b.z - 2.5f) * .88f);
                vertices.AddRange(new[] { a, b, lowerB, a, lowerB, lowerA });
            }
            mesh = new Mesh { name = "Test_ExpandedCliffSkirt", vertices = vertices.ToArray(), triangles = Enumerable.Range(0, vertices.Count).ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, meshPath);
        }
        MeshShape("Test_CliffSkirt", mesh, additions, Vector3.zero, Vector3.one, Mat("CliffShadow"));
        for (int i = 0; i < 90; i++)
        {
            if (i % 3 == 0) continue;
            float angle = i * Mathf.PI * 2 / 90, height = Random.Range(4f, 7);
            var cliff = MeshShape("Test_ExtraCliff", rock, additions, new Vector3(Mathf.Cos(angle) * 26.75f, -height - .2f, 2.5f + Mathf.Sin(angle) * 37), new Vector3(Random.Range(2.3f, 4.1f), height, Random.Range(2.5f, 4.1f)), stone);
            cliff.transform.localRotation = Quaternion.Euler(0, i * 42, 0);
        }
        for (int i = 0; i < 40; i++)
        {
            float angle = i * Mathf.PI / 20, distance = Random.Range(73f, 122);
            var peak = MeshShape("Test_ExtraDistantPeak", rock, additions, new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) > .2f ? -17 : -30, 5 + Mathf.Sin(angle) * distance), new Vector3(Random.Range(4f, 7), Random.Range(12f, 25), Random.Range(4f, 7)), stone);
            peak.transform.localRotation = Quaternion.Euler(0, i * 31, 0);
            Shape("Test_ExtraCloudBank", PrimitiveType.Sphere, additions, new Vector3(Mathf.Cos(angle) * distance * .85f, -6 - i % 3, Mathf.Sin(angle) * distance * .85f), new Vector3(16, 1.2f, 6), Mat("Clouds"));
        }
    }

    private static void FillBoundaryWalls()
    {
        foreach (int side in new[] { -1, 1 })
        {
            foreach (var gap in new[] { new Vector2(3.3f, 13.5f), new Vector2(21.5f, 29.8f) })
            {
                int count = Mathf.CeilToInt((gap.y - gap.x) / 4);
                float length = (gap.y - gap.x) / count;
                for (int i = 0; i < count; i++)
                {
                    float x = side * (gap.x + (i + .5f) * length);
                    Shape("Test_ExtraBoundaryWall", PrimitiveType.Cube, additions, new Vector3(x, .65f, 0), new Vector3(length, 1.3f, .65f), stone, true);
                    Shape("Test_ExtraWallCap", PrimitiveType.Cube, additions, new Vector3(x, 1.35f, 0), new Vector3(length + .08f, .22f, 1), tiles);
                }
            }
        }
    }
    private static void BuildPaths()
    {
        Vector3[] original = scenery.Cast<Transform>().Where(t => t.name == "Test_SteppingStone").Select(t => t.position).ToArray();
        for (float z = -35; z < 38; z += 1.45f)
        {
            Vector3 position = new Vector3(Mathf.Sin(z * .19f) * .45f, .01f, z);
            if (!original.Any(p => Vector3.Distance(p, position) < 1)) Step(position);
        }
        for (int path = 0; path < pathStarts.Length; path++)
        {
            int count = Mathf.CeilToInt(Vector3.Distance(pathStarts[path], pathEnds[path]) / 1.5f);
            for (int i = 1; i <= count; i++) Step(Vector3.Lerp(pathStarts[path], pathEnds[path], (float)i / count));
        }
        for (float z = -33; z < 38; z += 6)
        {
            float x = z < 4 ? 3.7f : 20.5f;
            Lantern(new Vector3(-x, 0, z)); Lantern(new Vector3(x, 0, z));
        }
    }
    private static void Step(Vector3 position)
    { position.y = .01f; var item = Shape("Test_ExtraSteppingStone", PrimitiveType.Cube, additions, position, new Vector3(Random.Range(1.4f, 2.1f), .12f, 1.1f), Mat("Path")); item.transform.localRotation = Quaternion.Euler(0, Random.Range(-16, 16), 0); }
    private static void Lantern(Vector3 position)
    {
        var item = Group("Test_추가석등", additions, position);
        Shape("Foot", PrimitiveType.Cube, item, new Vector3(0,.12f,0), new Vector3(.7f,.24f,.7f), stone);
        Shape("Stem", PrimitiveType.Cylinder, item, new Vector3(0,.65f,0), new Vector3(.22f,.55f,.22f), stone);
        Shape("Light", PrimitiveType.Cube, item, new Vector3(0,1.35f,0), new Vector3(.42f,.47f,.42f), gold);
        MeshShape("Roof", roof, item, new Vector3(0,1.58f,0), new Vector3(.5f,.4f,.5f), tiles);
    }

    private static void BuildLandmarks()
    {
        BellPavilion(landmarks[0]); BirdPoles(landmarks[1]); FloatingRings(landmarks[2]); ConstellationGarden(landmarks[3]); WishRibbons(landmarks[4]);
        Cairns(landmarks[5]); MirrorPond(landmarks[6]); SpiritAltar(landmarks[7]); RuinedPavilion(landmarks[8]); LanternCourt(landmarks[9]);
    }
    private static void BellPavilion(Vector3 position)
    {
        var item = Group("Test_청동종각", additions, position);
        Shape("Foundation", PrimitiveType.Cube, item, new Vector3(0,.12f,0), new Vector3(4.3f,.24f,3.8f), stone);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) Shape("Pillar", PrimitiveType.Cylinder, item, new Vector3(x*1.5f,1.5f,z*1.25f), new Vector3(.24f,1.5f,.24f), wood, true);
        Shape("Beam", PrimitiveType.Cube, item, new Vector3(0,2.9f,0), new Vector3(3.6f,.25f,.3f), bark);
        MeshShape("Roof", roof, item, new Vector3(0,3,0), new Vector3(2,1.15f,1.8f), tiles);
        Shape("Bell", PrimitiveType.Cylinder, item, new Vector3(0,1.9f,0), new Vector3(1, .65f,1), gold);
        Shape("BellRim", PrimitiveType.Cylinder, item, new Vector3(0,1.25f,0), new Vector3(1.3f,.08f,1.3f), gold);
        Shape("BellMouth", PrimitiveType.Cylinder, item, new Vector3(0,1.16f,0), new Vector3(1.05f,.015f,1.05f), dark);
        Beam("Striker", item, new Vector3(-1.5f,1.6f,-.2f), new Vector3(-.4f,1.6f,-.2f), .2f, bark);
        Lantern(position + new Vector3(-2.6f,0,-2.4f)); Lantern(position + new Vector3(2.6f,0,-2.4f));
    }
    private static void BirdPoles(Vector3 position)
    {
        var item = Group("Test_솟대숲", additions, position);
        for (int i = 0; i < 7; i++)
        {
            Vector3 point = new Vector3((i%3-1)*1.3f,0,(i/3-1)*1.4f); float height = 2.6f + i%3*.65f;
            Shape("Pole", PrimitiveType.Cylinder, item, point + Vector3.up*height/2, new Vector3(.14f,height/2,.14f), bark, true);
            Shape("BirdBody", PrimitiveType.Sphere, item, point + Vector3.up*(height+.1f), new Vector3(.58f,.24f,.25f), dark);
            Shape("BirdHead", PrimitiveType.Sphere, item, point + new Vector3(.25f,height+.27f,0), Vector3.one*.2f, dark);
            Shape("BirdBeak", PrimitiveType.Cube, item, point + new Vector3(.39f,height+.25f,0), new Vector3(.2f,.06f,.08f), gold);
            Shape("Wings", PrimitiveType.Cube, item, point + Vector3.up*(height+.13f), new Vector3(.4f,.04f,.75f), dark).transform.localRotation = Quaternion.Euler(0, i*19, 0);
        }
    }
    private static void FloatingRings(Vector3 position)
    {
        var item = Group("Test_부유환구", additions, position);
        Shape("Altar", PrimitiveType.Cylinder, item, new Vector3(0,.15f,0), new Vector3(4.8f,.15f,4.8f), stone);
        Ring("Seal", item, new Vector3(0,.32f,0),2.1f,gold);
        for (int i = 0; i < 3; i++) { var orbit = Group("Orbit", item, new Vector3(0,2.4f,0)); orbit.localRotation = Quaternion.Euler(i*43, i*57, i*36); Ring("JadeRing", orbit, Vector3.zero,1.8f,jade,true); }
        var core = Shape("Test_FloatingCore", PrimitiveType.Cube, item, new Vector3(0,2.4f,0), Vector3.one*.65f,gold); core.transform.localRotation = Quaternion.Euler(25,45,25); newMotes.Add(core.transform);
    }
    private static void ConstellationGarden(Vector3 position)
    {
        var item = Group("Test_별자리석정원", additions, position); Ring("Constellation",item,new Vector3(0,.09f,0),2.4f,gold);
        for (int i = 0; i < 5; i++)
        {
            float angle = i*Mathf.PI*2/5; Vector3 point = new Vector3(Mathf.Cos(angle)*2.4f,0,Mathf.Sin(angle)*2.4f);
            MeshShape("StandingStone",rock,item,point,new Vector3(.55f,1.4f+i%3*.5f,.55f),spirit);
            var light = Shape("Test_Star",PrimitiveType.Cube,item,point+Vector3.up*(2.5f+i%3*.2f),Vector3.one*.2f,jade); newMotes.Add(light.transform);
        }
    }
    private static void WishRibbons(Vector3 position)
    {
        var item = Group("Test_소원비단숲",additions,position);
        for (int row = 0; row < 2; row++)
        {
            float z = row*1.1f;
            for (int side=-1;side<=1;side+=2) Shape("Pole",PrimitiveType.Cylinder,item,new Vector3(side*2.2f,1.5f,z),new Vector3(.18f,1.5f,.18f),wood,true);
            Beam("Rope",item,new Vector3(-2.2f,2.8f,z),new Vector3(2.2f,2.8f,z),.045f,gold);
            for (int i=0;i<8;i++)
            {
                float x=-1.9f+i*.54f; float length=.55f+(i%3)*.18f;
                Shape("Wish",PrimitiveType.Cube,item,new Vector3(x,2.65f-length/2,z),new Vector3(.24f,length,.025f),i%2==0?cream:red).transform.localRotation=Quaternion.Euler(0,0,(i%3-1)*9);
            }
        }
    }
    private static void Cairns(Vector3 position)
    {
        var item=Group("Test_청운돌탑",additions,position);
        for(int stack=0;stack<3;stack++) for(int layer=0;layer<6;layer++)
        {
            float radius=.85f-layer*.11f;
            var stoneItem=MeshShape("Stone",rock,item,new Vector3((stack-1)*1.7f,layer*.32f,stack%2),new Vector3(radius,.36f,radius*.85f),layer%2==0?stone:Mat("Moss")); stoneItem.transform.localRotation=Quaternion.Euler(0,layer*41+stack*29,0);
        }
    }
    private static void MirrorPond(Vector3 position)
    {
        var item=Group("Test_거울연못",additions,position);
        Shape("Mirror",PrimitiveType.Cylinder,item,new Vector3(0,.02f,0),new Vector3(5,.035f,4),water);
        Ring("MoonGate",item,new Vector3(0,1.8f,1.1f),1.7f,gold,true);
        for(int i=0;i<7;i++) { float angle=i*Mathf.PI*2/7; MeshShape("PondRock",rock,item,new Vector3(Mathf.Cos(angle)*2.7f,0,Mathf.Sin(angle)*2.1f),new Vector3(.65f,.5f,.65f),stone); }
    }
    private static void SpiritAltar(Vector3 position)
    {
        var item=Group("Test_서낭제단",additions,position);
        Shape("Table",PrimitiveType.Cube,item,new Vector3(0,.4f,0),new Vector3(2.6f,.8f,1.8f),stone,true);
        Shape("Offering",PrimitiveType.Sphere,item,new Vector3(0,1.1f,0),Vector3.one*.6f,jade);
        for(int side=-1;side<=1;side+=2) { Shape("Pillar",PrimitiveType.Cylinder,item,new Vector3(side*1.8f,1.35f,.4f),new Vector3(.16f,1.35f,.16f),wood); Shape("Banner",PrimitiveType.Cube,item,new Vector3(side*1.5f,2.1f,.4f),new Vector3(.65f,.8f,.03f),red); }
    }
    private static void RuinedPavilion(Vector3 position)
    {
        var item=Group("Test_폐허누각",additions,position);
        Shape("Footing",PrimitiveType.Cube,item,new Vector3(0,.1f,0),new Vector3(3.6f,.2f,3),stone);
        for(int i=0;i<3;i++) Shape("BrokenPillar",PrimitiveType.Cylinder,item,new Vector3(i==1?1.3f:-1.3f,i==2?.65f:1.2f,i==0?-1:1),new Vector3(.25f,i==2?.65f:1.2f,.25f),wood,true);
        var fragment=MeshShape("BrokenRoof",roof,item,new Vector3(0,2.4f,0),new Vector3(1.7f,.7f,1.5f),tiles); fragment.transform.localRotation=Quaternion.Euler(0,15,-14);
        MeshShape("FallenStone",rock,item,new Vector3(1.8f,0,-1),new Vector3(.8f,1.1f,.7f),stone);
    }
    private static void LanternCourt(Vector3 position)
    {
        var item=Group("Test_석등마당",additions,position);
        Shape("Floor",PrimitiveType.Cylinder,item,new Vector3(0,.02f,0),new Vector3(5.5f,.035f,5.5f),Mat("Path"));
        for(int i=0;i<5;i++) { float angle=i*Mathf.PI*2/5; Lantern(position+new Vector3(Mathf.Cos(angle)*2.7f,0,Mathf.Sin(angle)*2.7f)); }
        Ring("CourtRing",item,new Vector3(0,.08f,0),2.3f,gold);
    }

    private static void ScatterScenery()
    {
        occupied.AddRange(scenery.Cast<Transform>().Where(t=>t.name=="Test_소나무").Select(t=>t.position));
        for(int placed=0, attempts=0;placed<90 && attempts<3000;attempts++)
        {
            Vector3 point=RandomPoint();
            if(!CanPlace(point,2.3f) || occupied.Any(p=>Vector3.Distance(p,point)<3.3f)) continue;
            Pine(point,Random.Range(.85f,1.3f),Random.Range(0,360)); occupied.Add(point); placed++;
        }
        for(int i=0;i<170;i++)
        {
            Vector3 point=RandomPoint(); if(!CanPlace(point,.7f)) continue;
            float size=Random.Range(.25f,.9f); var item=MeshShape("Test_ExtraMossRock",rock,additions,point,new Vector3(size,size*.65f,size*.75f),i%3==0?Mat("Moss"):stone); item.transform.localRotation=Quaternion.Euler(0,i*137.5f,0);
        }
        for(int i=0;i<700;i++)
        {
            Vector3 point=RandomPoint(); if(!CanPlace(point,.2f)) continue;
            point.y=.03f; var item=MeshShape("Test_ExtraGrass",cone,additions,point,new Vector3(.14f,Random.Range(.2f,.45f),.14f),i%6==0?Mat("Petals"):leaves); item.transform.localRotation=Quaternion.Euler(0,i*137.5f,i%2==0?14:-14);
        }
        for(int i=0;i<120;i++)
        {
            Vector3 point=RandomPoint(); point.y=Random.Range(.5f,4);
            var mote=Shape("Test_ExtraFirefly",PrimitiveType.Sphere,additions,point,Vector3.one*Random.Range(.045f,.12f),i%3==0?gold:jade); newMotes.Add(mote.transform);
        }
    }
    private static Vector3 RandomPoint() { return new Vector3(Random.Range(-27f,27),0,Random.Range(-34f,39)); }
    private static bool CanPlace(Vector3 point,float margin)
    {
        if(Mathf.Abs(point.x)<3.7f+margin || Mathf.Abs(point.z)<2.1f+margin) return false;
        Vector2 arena=new Vector2(point.x/19.5f,(point.z-22.5f)/19.5f); if(arena.sqrMagnitude<.93f) return false;
        if(Vector3.Distance(point,new Vector3(-15,0,-25))<5+margin || Vector3.Distance(point,new Vector3(27,0,-17.5f))<4.5f+margin) return false;
        if(landmarks.Any(p=>Vector3.Distance(p,point)<3.8f+margin)) return false;
        for(int i=0;i<pathStarts.Length;i++)
        {
            Vector3 delta=pathEnds[i]-pathStarts[i]; float progress=Mathf.Clamp01(Vector3.Dot(point-pathStarts[i],delta)/delta.sqrMagnitude);
            if(Vector3.Distance(point,pathStarts[i]+delta*progress)<1.2f+margin) return false;
        }
        return true;
    }
    private static void Pine(Vector3 position,float size,float rotation)
    {
        var item=Group("Test_추가소나무",additions,position); item.localScale=Vector3.one*size; item.localRotation=Quaternion.Euler(0,rotation,0);
        Shape("Trunk",PrimitiveType.Cylinder,item,new Vector3(0,1.25f,0),new Vector3(.26f,1.25f,.26f),bark,true);
        Beam("Branch",item,new Vector3(0,1.4f,0),new Vector3(1,2.2f,0),.15f,bark);
        for(int i=0;i<3;i++) MeshShape("Needles",cone,item,new Vector3(i==1?.35f:0,1.5f+i*.7f,0),new Vector3(1.8f-i*.4f,1,1.6f-i*.3f),leaves);
    }
}
