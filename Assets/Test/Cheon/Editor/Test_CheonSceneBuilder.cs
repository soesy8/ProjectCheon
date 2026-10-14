using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cheon.Testing;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

public static class Test_CheonSceneBuilder
{
    public const string ScenePath = "Assets/Test/Cheon/Scenes/Test_CheonBoundary.unity";
    private const string Root = "Assets/Test/Cheon";
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private static readonly List<Test_CheonWorld.Palette> palettes = new List<Test_CheonWorld.Palette>();
    private static Mesh cone, rock, roof, fan, island;
    private static TMP_FontAsset font;
    private static Material grass, stone, bark, leaves, roofMat, wood, cream, gold, jade, dark, red, spirit, water, glow, violet;
    private static Transform scenery;

    [MenuItem("Tools/Cheon Test/Create Boundary Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Play Mode를 종료한 뒤 생성하세요.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new System.InvalidOperationException("현재 씬에 저장하지 않은 변경이 있습니다. 먼저 저장하세요.");
        foreach (string path in new[] { Root + "/Scenes", Root + "/Arts/Materials", Root + "/Arts/Meshes", Root + "/Arts/Fonts" }) EnsureFolder(path);
        Random.InitState(3117); materials.Clear(); palettes.Clear();
        Test_CheonTextSettings.Ensure();
        SetupMaterials(); SetupMeshes(); font = SetupFont();
        if (TMP_Settings.defaultFontAsset == null) TMP_Settings.defaultFontAsset = font;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var worldObject = new GameObject("Test_청운수련지"); scenery = worldObject.transform;
        var world = worldObject.AddComponent<Test_CheonWorld>();
        BuildIsland(); BuildScenery(); BuildShrine(new Vector3(-6, 0, -10), 16); BuildGate(); BuildArena();
        GameObject sunObject = new GameObject("Test_Sun", typeof(Light));
        var sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f;
        sun.color = new Color(1, .88f, .69f); sun.shadows = LightShadows.Soft; sun.shadowStrength = .75f;
        sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.68f, .77f, .76f);
        RenderSettings.skybox = null; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 42; RenderSettings.fogEndDistance = 105; RenderSettings.fogColor = new Color(.55f, .73f, .72f);
        var sessionObject = new GameObject("Test_수련진행");
        var session = sessionObject.AddComponent<Test_CheonSession>();
        var sound = sessionObject.AddComponent<Test_CheonAudio>();
        var effects = new GameObject("Test_도술연출").AddComponent<Test_CheonEffects>();
        var player = BuildPlayer(); var boss = BuildBoss();
        var cameraObject = new GameObject("Test_QuarterView", typeof(Camera), typeof(AudioListener), typeof(Test_CheonCamera)); cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 8.4f;
        camera.nearClipPlane = .1f; camera.farClipPlane = 160; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = RenderSettings.fogColor;
        camera.transform.position = player.transform.position + new Vector3(15, 23, -20);
        camera.transform.rotation = Quaternion.LookRotation(new Vector3(-15, -23, 23));
        camera.allowMSAA = true;
        var cameraRig = cameraObject.GetComponent<Test_CheonCamera>();
        var hud = new GameObject("Test_수련화면").AddComponent<Test_CheonHud>();
        Set(session, "player", player); Set(session, "boss", boss); Set(session, "world", world); Set(session, "hud", hud); Set(session, "sound", sound);
        Set(player, "session", session); Set(player, "boss", boss); Set(player, "effects", effects); Set(player, "sound", sound); Set(player, "viewCamera", camera);
        Set(boss, "session", session); Set(boss, "player", player); Set(boss, "effects", effects); Set(boss, "sound", sound);
        Set(effects, "effectMaterial", glow); Set(effects, "font", font); Set(effects, "view", cameraRig);
        Set(cameraRig, "player", player.transform); Set(cameraRig, "boss", boss.transform); Set(cameraRig, "session", session);
        Set(hud, "font", font); Set(hud, "session", session); Set(hud, "player", player); Set(hud, "boss", boss); Set(hud, "sound", sound);
        Set(world, "viewCamera", camera); Set(world, "sun", sun);
        SetPalette(world);
        var motes = new List<Transform>();
        for (int i = 0; i < 40; i++)
        {
            var mote = Shape("Test_반딧불", PrimitiveType.Sphere, scenery, new Vector3(Random.Range(-11f, 11), Random.Range(.5f, 4), Random.Range(-14f, 17)), Vector3.one * Random.Range(.045f, .12f), i % 3 == 0 ? gold : jade);
            motes.Add(mote.transform);
        }
        SetArray(world, "motes", motes.Cast<Object>().ToArray());
        var halo = scenery.Find("Test_경계문/Test_GateHalo"); if (halo != null) Set(world, "gateHalo", halo);
        Set(world, "gateLintel", scenery.Find("Test_경계문/Test_UpperGate"));
        Test_CheonMapExpansion.Apply();
        Test_CheonMapLayout.Apply();
        Test_CheonSpiritWidth.Apply();
        Test_CheonConceptArt.Apply();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets(); Selection.activeObject = player.gameObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 0), camera.transform.rotation, 24);
        Debug.Log("천지인 테스트 씬 생성 완료: " + ScenePath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Material Mat(string name, string hex, string spiritHex = null, bool transparent = false)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        string path = Root + "/Arts/Materials/Test_M_" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(transparent ? "Cheon/Test/Spirit" : "Cheon/Test/Toon")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); materials[name] = material;
        if (spiritHex != null)
        {
            ColorUtility.TryParseHtmlString("#" + spiritHex, out Color spiritColor);
            palettes.Add(new Test_CheonWorld.Palette { material = material, reality = color, spirit = spiritColor });
        }
        return material;
    }

    private static void SetupMaterials()
    {
        grass = Mat("Moss", "789D7A", "474D69"); stone = Mat("MountainStone", "698183", "333C59");
        bark = Mat("PineBark", "695850", "4D405D"); leaves = Mat("PineNeedles", "326D65", "404663");
        roofMat = Mat("BlueTiles", "354F5D", "252941"); wood = Mat("RedPillars", "855549", "67485F");
        cream = Mat("Hanji", "E2D7B3"); gold = Mat("Brass", "E4B86D"); jade = Mat("Jade", "76E2C3");
        dark = Mat("Ink", "202F38"); red = Mat("Vermilion", "A74742"); spirit = Mat("SpiritWood", "68567C");
        water = Mat("Water", "69B8BC", "53648F"); violet = Mat("OtherSide", "827798");
        glow = Mat("SpiritLight", "FFFFFF", null, true);
        Mat("Path", "B5B7A2", "69657B"); Mat("Petals", "D6B9BB", "A485C0");
        Mat("CliffShadow", "4D666C", "292D49"); Mat("Clouds", "CBDAD5", "444764");
        Mat("SpiritFloor", "8F879B", "544C73"); Mat("WaterFoam", "D3ECE2", "8C9BC5");
        Mat("Robe", "348D89"); Mat("Skin", "E3BC99"); Mat("BossWood", "694B48");
        Mat("EyeGlow", "FF706B", null, true); Mat("PortalGlow", "68E8D5BB", null, true);
    }

    private static void SetupMeshes()
    {
        cone = SaveMesh("PineCone", ConeMesh(9, .02f)); rock = SaveMesh("Rock", ConeMesh(7, .55f));
        roof = SaveMesh("HanokRoof", RoofMesh()); fan = SaveMesh("Fan", FanMesh()); island = SaveMesh("MountainIsland", IslandMesh());
    }
    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = Root + "/Arts/Meshes/Test_" + name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { Object.DestroyImmediate(mesh); return existing; }
        mesh.name = "Test_" + name; AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static Mesh FlatMesh(List<Vector3> vertices, List<int> triangles)
    {
        // 각 삼각형의 꼭짓점을 분리해 돌과 기와의 면이 부드럽게 뭉개지지 않게 한다.
        var flat = triangles.Select(index => vertices[index]).ToArray();
        Mesh mesh = new Mesh(); mesh.vertices = flat; mesh.triangles = Enumerable.Range(0, flat.Length).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static Mesh ConeMesh(int sides, float topRadius)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2 / sides;
            vertices.Add(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)));
            vertices.Add(new Vector3(Mathf.Cos(angle + .14f) * topRadius, 1, Mathf.Sin(angle + .14f) * topRadius));
        }
        vertices.Add(new Vector3(0, 1.07f, 0));
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles.AddRange(new[] { i * 2, i * 2 + 1, next * 2 + 1, i * 2, next * 2 + 1, next * 2, i * 2 + 1, sides * 2, next * 2 + 1 });
        }
        return FlatMesh(vertices, triangles);
    }
    private static Mesh RoofMesh()
    {
        var vertices = new List<Vector3> {
            new Vector3(-1.2f,.15f,-1), new Vector3(1.2f,.15f,-1), new Vector3(1.2f,.15f,1), new Vector3(-1.2f,.15f,1),
            new Vector3(-.95f,.42f,-.6f), new Vector3(.95f,.42f,-.6f), new Vector3(.95f,.42f,.6f), new Vector3(-.95f,.42f,.6f),
            new Vector3(-.8f,.9f,0), new Vector3(.8f,.9f,0) };
        var triangles = new List<int>();
        for (int i = 0; i < 4; i++) { int n = (i + 1) % 4; triangles.AddRange(new[] { i, i + 4, n + 4, i, n + 4, n }); }
        triangles.AddRange(new[] { 4,8,9, 4,9,5, 7,6,9, 7,9,8, 4,7,8, 5,9,6 });
        return FlatMesh(vertices, triangles);
    }
    private static Mesh FanMesh()
    {
        var vertices = new List<Vector3> { Vector3.zero }; var triangles = new List<int>();
        for (int i = 0; i <= 12; i++) { float angle = (20 + i * 140f / 12) * Mathf.Deg2Rad; vertices.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .9f); }
        for (int i = 1; i <= 12; i++) triangles.AddRange(new[] { 0, i + 1, i });
        Mesh mesh = FlatMesh(vertices, triangles); // 양면을 보이게 뒷면도 추가한다.
        var all = mesh.triangles.ToList(); all.AddRange(mesh.triangles.Reverse()); mesh.triangles = all.ToArray(); return mesh;
    }
    private static Mesh IslandMesh()
    {
        Vector2[] outline = { new Vector2(-10,-15), new Vector2(10,-15), new Vector2(12,-13), new Vector2(12,15), new Vector2(10,17), new Vector2(-10,17), new Vector2(-12,15), new Vector2(-12,-13) };
        var vertices = new List<Vector3> { new Vector3(0,0,1) }; var triangles = new List<int>();
        foreach (var point in outline) vertices.Add(new Vector3(point.x, 0, point.y));
        for (int i = 0; i < 8; i++) triangles.AddRange(new[] { 0, (i + 1) % 8 + 1, i + 1 });
        return FlatMesh(vertices, triangles);
    }

    private static TMP_FontAsset SetupFont()
    {
        string path = Root + "/Arts/Fonts/Test_CheonKorean.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null) return asset;
        string fontPath = Root + "/Arts/Fonts/Test_CheonKorean.ttf";
        Font source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (source == null)
        {
            if (!File.Exists(fontPath)) File.Copy("C:/Windows/Fonts/malgun.ttf", fontPath);
            AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceSynchronousImport);
            source = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        }
        asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = "Test_CheonKorean";
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture, asset);
        string characters = string.Concat(Directory.GetFiles(Root + "/Scripts", "*.cs").Select(File.ReadAllText)) + "天地人天下大將軍地下女將軍0123456789:./- ";
        asset.TryAddCharacters(new string(characters.Distinct().ToArray()), out string missing);
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
        return asset;
    }

    private static GameObject Group(string name, Transform parent, Vector3 position)
    { var item = new GameObject(name); item.transform.SetParent(parent, false); item.transform.localPosition = position; return item; }
    private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false)
    {
        var item = GameObject.CreatePrimitive(type); item.name = name; item.transform.SetParent(parent, false); item.transform.localPosition = position; item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(item.GetComponent<Collider>());
        return item;
    }
    private static GameObject MeshShape(string name, Mesh mesh, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var item = Group(name, parent, position); item.transform.localScale = scale;
        item.AddComponent<MeshFilter>().sharedMesh = mesh; item.AddComponent<MeshRenderer>().sharedMaterial = material; return item;
    }
    private static void Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
    { var item = Shape(name, PrimitiveType.Cylinder, parent, (a + b) / 2, new Vector3(width, (b - a).magnitude / 2, width), material); item.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); }

    private static void BuildIsland()
    {
        MeshShape("Test_MossTerrace", island, scenery, new Vector3(0, -.08f, 0), Vector3.one, grass);
        var ground = Group("Test_WalkableGround", scenery, new Vector3(0, -.18f, 1)).AddComponent<BoxCollider>(); ground.size = new Vector3(24, .35f, 32);
        for (int i = 0; i < 30; i++)
        {
            float angle = i * Mathf.PI * 2 / 30;
            float height = Random.Range(4f, 7);
            Vector3 position = new Vector3(Mathf.Cos(angle) * 10.7f, -height - .2f, 1 + Mathf.Sin(angle) * 14.8f);
            var cliff = MeshShape("Test_Cliff", rock, scenery, position, new Vector3(Random.Range(2.3f, 4.1f), height, Random.Range(2.5f, 4.1f)), i % 2 == 0 ? stone : materials["CliffShadow"]);
            cliff.transform.localRotation = Quaternion.Euler(0, i * 42, 0);
        }
        for (int i = 0; i < 25; i++)
        {
            float angle = i * Mathf.PI * 2 / 25;
            float distance = Random.Range(31f, 48f);
            float bottom = Mathf.Sin(angle) > .2f ? -17 : -30;
            var peak = MeshShape("Test_DistantPeak", rock, scenery, new Vector3(Mathf.Cos(angle) * distance, bottom, Mathf.Sin(angle) * distance + 4), new Vector3(Random.Range(4f, 7f), Random.Range(12f, 25f), Random.Range(4f, 7f)), stone);
            peak.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            Shape("Test_CloudBank", PrimitiveType.Sphere, scenery, new Vector3(Mathf.Cos(angle) * distance, -6 - i % 3, Mathf.Sin(angle) * distance), new Vector3(16, 1.2f, 6), materials["Clouds"]);
        }
        // 山水의 흐름을 계곡과 낙수로 표현한다.
        Shape("Test_Pond", PrimitiveType.Cylinder, scenery, new Vector3(9, -.005f, -7), new Vector3(6, .04f, 9), water);
        Shape("Test_Waterfall", PrimitiveType.Cube, scenery, new Vector3(11.8f, -4.2f, -7), new Vector3(.5f, 8.5f, 3.7f), water);
        for (int i = 0; i < 7; i++) Shape("Test_WaterRibbon", PrimitiveType.Cube, scenery, new Vector3(12.08f, -4.2f, -8.55f + i * .5f), new Vector3(.035f, 8.3f, .08f), materials["WaterFoam"]);
        for (int i = 0; i < 15; i++)
        {
            float z = -13.5f + i * 1.6f;
            var slab = Shape("Test_SteppingStone", PrimitiveType.Cube, scenery, new Vector3(Mathf.Sin(i * .7f) * .48f, .01f, z), new Vector3(Random.Range(1.3f, 2.2f), .12f, 1.1f), materials["Path"]);
            slab.transform.localRotation = Quaternion.Euler(0, Random.Range(-16, 16), 0);
        }
    }

    private static void Pine(Vector3 position, float scale, float rotation)
    {
        var tree = Group("Test_소나무", scenery, position).transform; tree.localScale = Vector3.one * scale; tree.localRotation = Quaternion.Euler(0, rotation, 0);
        Shape("Trunk", PrimitiveType.Cylinder, tree, new Vector3(0, 1.25f, 0), new Vector3(.26f, 1.25f, .26f), bark, true);
        Beam("Branch", tree, new Vector3(0, 1.4f, 0), new Vector3(1, 2.2f, 0), .15f, bark);
        for (int i = 0; i < 3; i++) MeshShape("Needles", cone, tree, new Vector3(i == 1 ? .35f : 0, 1.5f + i * .7f, 0), new Vector3(1.8f - i * .4f, 1, 1.6f - i * .3f), leaves);
    }
    private static void BuildScenery()
    {
        for (int i = 0; i < 12; i++)
        {
            Pine(new Vector3(-10.4f + Random.Range(-.4f, .4f), 0, -13 + i * 2.6f), Random.Range(.85f, 1.4f), i * 46);
            if (i < 3 || i > 5) Pine(new Vector3(10.3f, 0, -13 + i * 2.6f), Random.Range(.8f, 1.2f), i * 76);
        }
        for (int i = 0; i < 60; i++)
        {
            Vector3 position = new Vector3(Random.Range(-11f, 11f), 0, Random.Range(-14f, 16));
            if (Mathf.Abs(position.x) < 3 || (position.z > 4 && Mathf.Abs(position.x) < 7)) continue;
            float size = Random.Range(.25f, .9f);
            MeshShape("Test_MossRock", rock, scenery, position, new Vector3(size, size * .65f, size * .75f), i % 3 == 0 ? grass : stone);
        }
        for (int i = 0; i < 150; i++)
        {
            Vector3 position = new Vector3(Random.Range(-11f, 11), .03f, Random.Range(-14f, 16));
            if (Mathf.Abs(position.x) < 2 || (position.z > 4 && Mathf.Abs(position.x) < 6)) continue;
            var tuft = MeshShape("Test_Grass", cone, scenery, position, new Vector3(.14f, Random.Range(.2f, .45f), .14f), i % 6 == 0 ? materials["Petals"] : leaves);
            tuft.transform.localRotation = Quaternion.Euler(0, i * 137.5f, i % 2 == 0 ? 14 : -14);
        }
        for (int i = 0; i < 8; i++) Lantern(new Vector3(i % 2 == 0 ? -3.7f : 3.7f, 0, -11 + i / 2 * 6));
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -7 : 7;
            Shape("Test_BoundaryWall", PrimitiveType.Cube, scenery, new Vector3(x, .65f, 0), new Vector3(8, 1.3f, .65f), stone, true);
            Shape("Test_WallCap", PrimitiveType.Cube, scenery, new Vector3(x, 1.35f, 0), new Vector3(8.3f, .22f, 1), roofMat);
        }
    }
    private static void Lantern(Vector3 position)
    {
        var lantern = Group("Test_석등", scenery, position).transform;
        Shape("Foot", PrimitiveType.Cube, lantern, new Vector3(0, .12f, 0), new Vector3(.7f, .24f, .7f), stone);
        Shape("Stem", PrimitiveType.Cylinder, lantern, new Vector3(0, .65f, 0), new Vector3(.22f, .55f, .22f), stone);
        Shape("Light", PrimitiveType.Cube, lantern, new Vector3(0, 1.35f, 0), new Vector3(.42f, .47f, .42f), gold);
        MeshShape("Roof", roof, lantern, new Vector3(0, 1.58f, 0), new Vector3(.5f, .4f, .5f), roofMat);
    }
    private static void BuildShrine(Vector3 position, float rotation)
    {
        var shrine = Group("Test_산신당", scenery, position).transform; shrine.localRotation = Quaternion.Euler(0, rotation, 0);
        Shape("Foundation", PrimitiveType.Cube, shrine, new Vector3(0, .2f, 0), new Vector3(5.5f, .4f, 4.6f), stone, true);
        Shape("WoodFloor", PrimitiveType.Cube, shrine, new Vector3(0, .46f, 0), new Vector3(4.8f, .15f, 3.8f), bark);
        Shape("BackWall", PrimitiveType.Cube, shrine, new Vector3(0, 1.55f, 1.5f), new Vector3(4.4f, 2.2f, .18f), cream);
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            Shape("RedPillar", PrimitiveType.Cylinder, shrine, new Vector3(x * 2.15f, 1.6f, z * 1.6f), new Vector3(.28f, 1.2f, .28f), wood, true);
        Shape("Crossbeam", PrimitiveType.Cube, shrine, new Vector3(0, 2.6f, -1.6f), new Vector3(4.8f, .32f, .3f), wood);
        MeshShape("GiwaRoof", roof, shrine, new Vector3(0, 2.65f, 0), new Vector3(2.65f, 1.6f, 2.15f), roofMat);
        Shape("RoofRidge", PrimitiveType.Cube, shrine, new Vector3(0, 4.12f, 0), new Vector3(4.8f, .16f, .2f), gold);
        for (int i = -6; i <= 6; i++)
            Beam("TileRib", shrine, new Vector3(i * .38f, 3.34f, -.85f), new Vector3(i * .42f, 2.93f, -2.12f), .075f, stone);
        Shape("Altar", PrimitiveType.Cube, shrine, new Vector3(0, .9f, .5f), new Vector3(1.8f, .9f, .8f), wood);
        Shape("Offering", PrimitiveType.Sphere, shrine, new Vector3(0, 1.5f, .5f), Vector3.one * .4f, jade);
        Shape("ShrineStep", PrimitiveType.Cube, shrine, new Vector3(0, .12f, -2.5f), new Vector3(2.5f, .24f, .75f), materials["Path"]);
        Shape("RecoveryStone", PrimitiveType.Cylinder, scenery, new Vector3(0, .04f, -10), new Vector3(2.6f, .055f, 2.6f), materials["Path"]);
        Circle("Test_ShrineRune", scenery, new Vector3(0, .12f, -10), 1.1f, .035f, jade, false);
    }

    private static void BuildGate()
    {
        var gate = Group("Test_경계문", scenery, Vector3.zero).transform;
        var upper = Group("Test_UpperGate", gate, Vector3.zero).transform;
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -2.7f : 2.7f;
            var post = Group("Test_수호장승", upper, new Vector3(x, 0, 0)).transform;
            Shape("Base", PrimitiveType.Cube, post, new Vector3(0, .2f, 0), new Vector3(1, .4f, 1), stone, true);
            Shape("Pole", PrimitiveType.Cube, post, new Vector3(0, 2, 0), new Vector3(.6f, 3.7f, .65f), bark, true);
            Shape("Face", PrimitiveType.Cube, post, new Vector3(0, 3.3f, -.1f), new Vector3(.9f, 1.35f, .8f), wood);
            for (int e = -1; e <= 1; e += 2)
            {
                var eye = Shape("Eye", PrimitiveType.Cube, post, new Vector3(e * .23f, 3.48f, -.515f), new Vector3(.27f, .12f, .03f), cream);
                eye.transform.localRotation = Quaternion.Euler(0, 0, e * -15);
            }
            Shape("Mouth", PrimitiveType.Cube, post, new Vector3(0, 3.02f, -.515f), new Vector3(.57f, .17f, .035f), dark);
            MeshShape("Hat", roof, post, new Vector3(0, 4, 0), new Vector3(.65f, .6f, .6f), roofMat);
        }
        Shape("Lintel", PrimitiveType.Cube, upper, new Vector3(0, 4.45f, 0), new Vector3(6.3f, .34f, .45f), wood);
        Shape("SacredRope", PrimitiveType.Cylinder, upper, new Vector3(0, 3.95f, 0), new Vector3(.04f, 2.5f, .04f), gold).transform.localRotation = Quaternion.Euler(0, 0, 90);
        for (int i = -2; i <= 2; i++)
        {
            var paper = Shape("Talisman", PrimitiveType.Cube, upper, new Vector3(i * .85f, 3.65f, -.05f), new Vector3(.19f, .45f, .025f), cream);
            paper.transform.localRotation = Quaternion.Euler(0, 0, i * 7);
        }
        Circle("Test_GateHalo", gate, new Vector3(0, 2.1f, 0), 2.15f, .055f, materials["PortalGlow"], true);
        Circle("Test_InnerHalo", gate, new Vector3(0, 2.1f, .05f), 2.02f, .018f, materials["PortalGlow"], true);
        Circle("Test_GateFloor", gate, new Vector3(0, .15f, 0), 2.1f, .05f, jade, false);
    }
    private static void Circle(string name, Transform parent, Vector3 position, float radius, float width, Material material, bool vertical)
    {
        var item = Group(name, parent, position); var line = item.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = false; line.loop = true; line.widthMultiplier = width; line.positionCount = 80;
        for (int i = 0; i < 80; i++) { float a = i * Mathf.PI * 2 / 80; line.SetPosition(i, vertical ? new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, 0) : new Vector3(Mathf.Sin(a) * radius, 0, Mathf.Cos(a) * radius)); }
    }
    private static void BuildArena()
    {
        Shape("Test_SpiritTerrace", PrimitiveType.Cylinder, scenery, new Vector3(0, .005f, 9), new Vector3(16.5f, .04f, 16.5f), materials["SpiritFloor"]);
        Circle("Test_OuterSeal", scenery, new Vector3(0, .065f, 9), 7.8f, .045f, gold, false);
        Circle("Test_InnerSeal", scenery, new Vector3(0, .075f, 9), 4.9f, .025f, violet, false);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4;
            Vector3 position = new Vector3(Mathf.Cos(a) * 7.4f, .1f, 9 + Mathf.Sin(a) * 7.4f);
            var mark = Shape("Test_SealMark", PrimitiveType.Cube, scenery, position, new Vector3(.1f, .04f, .9f), gold);
            mark.transform.localRotation = Quaternion.Euler(0, -i * 45, 0);
        }
        for (int i = 0; i < 5; i++)
        {
            MeshShape("Test_SpiritStandingStone", rock, scenery, new Vector3(-8 + i * 4, 0, 16), new Vector3(.8f, 2.4f + i % 2, 1), spirit);
            Shape("Test_FloatingStone", PrimitiveType.Cube, scenery, new Vector3(-8 + i * 4, 3.5f + i % 2, 16), new Vector3(.45f, .45f, .45f), jade).transform.localRotation = Quaternion.Euler(25, 45, 25);
        }
    }

    private static Test_CheonPlayer BuildPlayer()
    {
        var item = new GameObject("Test_청운_견습도사"); item.transform.position = new Vector3(0, 0, -10); item.transform.rotation = Quaternion.Euler(0, 0, 0);
        var controller = item.AddComponent<CharacterController>(); controller.height = 1.8f; controller.center = Vector3.up * .9f; controller.radius = .33f; controller.stepOffset = .35f;
        var player = item.AddComponent<Test_CheonPlayer>();
        var model = Group("Model", item.transform, Vector3.zero).transform;
        Shape("LowerHanbok", PrimitiveType.Capsule, model, new Vector3(0, .72f, 0), new Vector3(.8f, .56f, .68f), materials["Robe"]);
        Shape("Jacket", PrimitiveType.Capsule, model, new Vector3(0, 1.15f, 0), new Vector3(.68f, .37f, .53f), cream);
        Shape("Belt", PrimitiveType.Cylinder, model, new Vector3(0, .95f, 0), new Vector3(.73f, .07f, .57f), gold);
        Shape("Head", PrimitiveType.Sphere, model, new Vector3(0, 1.67f, 0), Vector3.one * .5f, materials["Skin"]);
        Shape("GatBrim", PrimitiveType.Cylinder, model, new Vector3(0, 1.92f, 0), new Vector3(1.1f, .035f, 1.1f), dark);
        Shape("GatTop", PrimitiveType.Cylinder, model, new Vector3(0, 2.07f, 0), new Vector3(.44f, .15f, .44f), dark);
        for (int side = -1; side <= 1; side += 2)
        {
            Shape("Sleeve", PrimitiveType.Capsule, model, new Vector3(side * .46f, 1.17f, .05f), new Vector3(.36f, .36f, .38f), cream).transform.localRotation = Quaternion.Euler(0, 0, side * 52);
            Shape("Boot", PrimitiveType.Cube, model, new Vector3(side * .21f, .12f, .09f), new Vector3(.24f, .25f, .47f), dark);
        }
        Shape("Sash", PrimitiveType.Cube, model, new Vector3(-.18f, .6f, -.37f), new Vector3(.16f, .72f, .06f), red).transform.localRotation = Quaternion.Euler(-15, 0, -14);
        var fanRoot = Group("FanHand", model, new Vector3(.68f, 1.17f, .35f)).transform;
        MeshShape("PaperFan", fan, fanRoot, Vector3.zero, Vector3.one, cream);
        for (int i = 0; i < 7; i++)
        {
            float a = (20 + i * 140f / 6) * Mathf.Deg2Rad;
            Beam("FanRib", fanRoot, new Vector3(0, 0, -.015f), new Vector3(Mathf.Cos(a) * .9f, Mathf.Sin(a) * .9f, -.015f), .025f, gold);
        }
        Set(player, "model", model); Set(player, "fan", fanRoot); return player;
    }
    private static Test_CheonBoss BuildBoss()
    {
        var item = new GameObject("Test_타락한수호장승"); item.transform.position = new Vector3(0, 0, 11); item.transform.rotation = Quaternion.Euler(0, 180, 0);
        var collider = item.AddComponent<CapsuleCollider>(); collider.center = new Vector3(0, 2, 0); collider.height = 4; collider.radius = .95f;
        var boss = item.AddComponent<Test_CheonBoss>(); var model = Group("Model", item.transform, Vector3.zero).transform;
        var body = materials["BossWood"];
        Shape("CarvedBody", PrimitiveType.Cube, model, new Vector3(0, 2.1f, 0), new Vector3(1.6f, 2.8f, 1.3f), body);
        Shape("GreatHead", PrimitiveType.Cube, model, new Vector3(0, 3.8f, .05f), new Vector3(2.1f, 1.6f, 1.6f), wood);
        for (int side = -1; side <= 1; side += 2)
        {
            Shape("Foot", PrimitiveType.Cube, model, new Vector3(side * .56f, .35f, .18f), new Vector3(.65f, .7f, 1), body);
            var eye = Shape("BurningEye", PrimitiveType.Cube, model, new Vector3(side * .5f, 3.96f, .87f), new Vector3(.55f, .18f, .07f), materials["EyeGlow"]);
            eye.transform.localRotation = Quaternion.Euler(0, 0, side * 18);
            Shape("Brow", PrimitiveType.Cube, model, new Vector3(side * .5f, 4.2f, .88f), new Vector3(.75f, .2f, .16f), dark).transform.localRotation = Quaternion.Euler(0, 0, side * 18);
            Beam("Horn", model, new Vector3(side * .75f, 4.4f, 0), new Vector3(side * 1.1f, 5.3f, -.05f), .3f, body);
            Beam("UpperArm", model, new Vector3(side * .9f, 2.7f, 0), new Vector3(side * 1.8f, 2.2f, .2f), .55f, body);
            Shape("Fist", PrimitiveType.Cube, model, new Vector3(side * 1.85f, 2.15f, .2f), new Vector3(.65f, .75f, .65f), body);
            Shape("Tassel", PrimitiveType.Cube, model, new Vector3(side * .65f, 1.3f, .71f), new Vector3(.2f, 1, .05f), red);
        }
        Shape("Nose", PrimitiveType.Cube, model, new Vector3(0, 3.76f, .95f), new Vector3(.35f, .7f, .4f), gold);
        Shape("Mouth", PrimitiveType.Cube, model, new Vector3(0, 3.36f, .88f), new Vector3(1.35f, .43f, .05f), dark);
        for (int i = -2; i <= 2; i++) Shape("Tooth", PrimitiveType.Cube, model, new Vector3(i * .24f, 3.43f, .94f), new Vector3(.17f, .26f, .08f), cream);
        for (int i = -2; i <= 2; i++) Shape("CarvedRune", PrimitiveType.Cube, model, new Vector3(i * .2f, 2.15f + Mathf.Abs(i) * .1f, .665f), new Vector3(.08f, .65f, .025f), gold);
        var clubRoot = Group("ClubHand", model, new Vector3(2.05f, 2.15f, .35f)).transform;
        Shape("ClubGrip", PrimitiveType.Cylinder, clubRoot, new Vector3(0, .3f, 0), new Vector3(.2f, 1.2f, .2f), bark);
        Shape("ClubHead", PrimitiveType.Cube, clubRoot, new Vector3(0, 1.65f, 0), new Vector3(.8f, 1.2f, .8f), dark);
        for (int i = 0; i < 3; i++) Shape("ClubBand", PrimitiveType.Cube, clubRoot, new Vector3(0, 1.25f + i * .4f, 0), new Vector3(.87f, .12f, .87f), gold);
        Set(boss, "model", model); Set(boss, "club", clubRoot); return boss;
    }

    private static void Set(Object item, string property, Object value)
    { var serialized = new SerializedObject(item); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetArray(Object item, string property, Object[] values)
    { var serialized = new SerializedObject(item); var array = serialized.FindProperty(property); array.arraySize = values.Length; for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetPalette(Test_CheonWorld world)
    {
        var serialized = new SerializedObject(world); var array = serialized.FindProperty("palettes"); array.arraySize = palettes.Count;
        for (int i = 0; i < palettes.Count; i++)
        {
            var entry = array.GetArrayElementAtIndex(i); entry.FindPropertyRelative("material").objectReferenceValue = palettes[i].material;
            entry.FindPropertyRelative("reality").colorValue = palettes[i].reality; entry.FindPropertyRelative("spirit").colorValue = palettes[i].spirit;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
