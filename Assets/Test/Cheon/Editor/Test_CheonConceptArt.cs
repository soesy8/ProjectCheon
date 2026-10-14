using System.Collections.Generic;
using System.Linq;
using Cheon.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 컨셉 아트의 색·표면·형태를 적용한다. 기존 Transform과 충돌체는 수정하지 않는다.
public static class Test_CheonConceptArt
{
    private const string Root = "Assets/Test/Cheon/Arts/";
    private const string MarkerName = "Test_ConceptArt";
    private static readonly Dictionary<string,Mesh> meshes = new Dictionary<string,Mesh>();
    private static readonly Dictionary<Material,Color[]> palette = new Dictionary<Material,Color[]>();
    private static Shader surface;
    private static Material botanicals, masonry, lanternGlow, sealStone, karst, flame, dark;

    [MenuItem("Tools/Cheon Test/Apply Concept Art Design")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || (!string.IsNullOrEmpty(scene.path) && scene.path != Test_CheonSceneBuilder.ScenePath))
            throw new System.InvalidOperationException("천지인 테스트 씬의 Edit Mode에서 실행하세요.");
        var world = Object.FindFirstObjectByType<Test_CheonWorld>();
        if (world == null) throw new System.InvalidOperationException("천지인 수련지 씬이 없습니다.");
        if (UniversalRenderPipeline.asset == null) throw new System.InvalidOperationException("URP가 필요합니다.");
        surface = Shader.Find("Cheon/Test/ConceptSurface");
        if (surface == null) throw new System.InvalidOperationException("컨셉 표면 셰이더의 임포트를 완료하세요.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("천지인 컨셉 아트 적용");
        var randomState = Random.state; Random.InitState(14102026);
        try
        {
            meshes.Clear(); palette.Clear(); SetupMaterials();
            UpgradeMeshes(world.transform);
            SetupPalette(world);
            Transform marker = world.transform.Find(MarkerName);
            if (marker == null)
            {
                marker = new GameObject(MarkerName).transform; marker.SetParent(world.transform,false);
                Undo.RegisterCreatedObjectUndo(marker.gameObject,"컨셉 아트 적용 기록");
            }
            SetupAtmosphere(world,marker);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("천지인 컨셉 아트 적용 완료: 배치·크기·충돌 경계 유지");
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
        finally { Random.state = randomState; Undo.CollapseUndoOperations(group); }
    }

    private static Color Hex(string text)
    { ColorUtility.TryParseHtmlString("#"+text,out Color color); return color; }

    private static Material Material(string name, string reality, string spirit, int type = 0, string accent = "648445", bool vertex = false, float glow = 0, float decor = 0)
    {
        string path = Root+"Materials/Test_M_"+name+".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(surface) { name = "Test_M_"+name }; AssetDatabase.CreateAsset(material,path);
        }
        Undo.RecordObject(material,"컨셉 아트 재질"); material.shader = surface;
        material.SetColor("_BaseColor",Hex(reality)); material.SetColor("_AccentColor",Hex(accent));
        material.SetFloat("_SurfaceType",type);material.SetFloat("_DetailScale",1);material.SetFloat("_VertexTint",vertex ? 1 : 0);
        material.SetFloat("_Glow",glow); material.SetFloat("_Decor",decor);
        EditorUtility.SetDirty(material);
        palette[material] = new[] { Hex(reality),Hex(spirit ?? reality) }; return material;
    }

    private static void SetupMaterials()
    {
        Material("Moss","789449","4E7271",1,"A8B35D");
        Material("MountainStone","D4C6A4","92AAB4",2,"708C43");
        Material("CliffShadow","ADA487","6E8997",2,"526E3B");
        Material("PineBark","A56134","7C6262",3,"734228");
        Material("PineNeedles","387767","3B707B",8,"9BAF55",true);
        Material("BlueTiles","294B68","34596F",4,"8AADB7",true);
        Material("RedPillars","BF5B32","A65D4F",3,"327E77",false,0,1);
        Material("Hanji","F5DEAA",null,0,"E4C377");
        Material("Brass","F3BD46","FAD17E",0,"B98B36",false,.18f);
        Material("Jade","63DCD1","7DE9E8",0,"D8FFFF",false,.7f);
        dark = Material("Ink","182D3C",null);
        Material("Vermilion","CD542C","B56A5A",3);
        Material("SpiritWood","81735E","6B98A6",6,"8BEBFF",false,.04f);
        Material("Water","249DBF","3DB1D8",7,"D2F7FF",false,.08f);
        Material("OtherSide","7ECBD4","9AE8F1",0,"BDD6E5",false,.20f);
        Material("Path","E5D5B0","ABC0C5",2,"A5AD71");
        Material("Petals","FFF2C7","D0E8E8");
        Material("Clouds","FFF3DC","8CAABD",0,"DBE3EA");
        Material("SpiritFloor","CCC8AD","7899A7",5,"7F9680");
        Material("WaterFoam","D9F9FF","C4F2FF",7,"FFFFFF",false,.45f);
        Material("BossWood","B96032",null,3,"8B4020");
        Material("Robe","319C9C",null);
        Material("Skin","EAC297",null);
        botanicals = Material("ConceptBotanicals","FFFFFF","D9F0EA",0,"77973D",true);
        masonry = Material("ConceptMasonry","D9CDAF","9BAEB7",5,"7F9277");
        lanternGlow = Material("ConceptLanternGlow","FFB338","FFD287",0,"FFD68A",false,1.15f);
        sealStone = Material("ConceptSealStone","A59C86","87A5B6",6,"94F1FF");
        karst = Material("ConceptKarst","FFFFFF","A8C5D4",2,"5D7F47",true);
        flame = Material("ConceptSpiritFlame","62E8FF","A8F8FF",0,"E4FFFF",false,1.7f);
        foreach (var pair in new[] { new KeyValuePair<string,Color>("PortalGlow",new Color(.22f,1.45f,1.8f,.68f)),new KeyValuePair<string,Color>("EyeGlow",new Color(1.8f,.35f,.08f,1)) })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Test_M_"+pair.Key+".mat");
            Undo.RecordObject(material,"경계문과 눈의 발광색");material.SetColor("_BaseColor",pair.Value);EditorUtility.SetDirty(material);
        }
    }

    private static Mesh Mesh(string name, Bounds bounds, System.Func<Bounds,Mesh> create)
    {
        if (meshes.TryGetValue(name,out Mesh cached)) return cached;
        string path = Root+"Meshes/Test_Concept"+name+".asset";
        var generated = create(bounds); generated.name = "Test_Concept"+name;
        var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset == null) { AssetDatabase.CreateAsset(generated,path); asset = generated; }
        else { EditorUtility.CopySerialized(generated,asset); Object.DestroyImmediate(generated); asset.UploadMeshData(false); EditorUtility.SetDirty(asset); }
        meshes[name] = asset; return asset;
    }

    private static void UpgradeMeshes(Transform scenery)
    {
        var filters = scenery.GetComponentsInChildren<MeshFilter>(true);
        int crownIndex = 0, grassIndex = 0, rockIndex = 0, cloudIndex = 0;
        foreach (var filter in filters)
        {
            string name = filter.name; var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null || name.StartsWith("Test_Art")) continue;
            Bounds bounds = filter.sharedMesh.bounds; Mesh replacement = null;
            Undo.RecordObjects(new Object[] { filter,renderer },"기존 프랍 아트 교체");
            if (name == "Needles")
            {
                int variant = crownIndex++%4;
                replacement = Mesh("PineCrown"+variant,bounds,b => Test_CheonConceptMeshes.PineCrown(b,variant));
            }
            else if (name == "Trunk") replacement = Mesh("CurvedPineTrunk",bounds,Test_CheonConceptMeshes.CurvedTrunk);
            else if (name.Contains("Grass"))
            {
                int variant = grassIndex++%6;
                replacement = Mesh("Botanical"+variant,bounds,b => Test_CheonConceptMeshes.Botanical(b,variant)); renderer.sharedMaterial = botanicals;
            }
            else if (name.Contains("CloudBank"))
            {
                int variant = cloudIndex++%3;
                replacement = Mesh("CloudBank"+variant,bounds,b => Test_CheonConceptMeshes.Cloud(b,variant));
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            else if (name.Contains("DistantPeak"))
            {
                int variant = rockIndex++%4;
                replacement = Mesh("KarstPeak"+variant,bounds,b => Test_CheonConceptMeshes.Karst(b,variant)); renderer.sharedMaterial = karst;
            }
            else if (name.Contains("StandingStone"))
            { replacement = Mesh("SealObelisk",bounds,Test_CheonConceptMeshes.Obelisk); renderer.sharedMaterial = sealStone; }
            else if (name == "Test_FloatingStone" || name == "Test_FloatingCore")
            { replacement = SpiritOrbMesh(); renderer.sharedMaterial = flame; }
            else if (filter.sharedMesh.name.Contains("HanokRoof") || filter.sharedMesh.name.Contains("ConceptGiwaRoof"))
                replacement = Mesh("GiwaRoof",bounds,Test_CheonConceptMeshes.Roof);
            else if (name == "Test_Cliff" || name == "Test_ExtraCliff" || name.Contains("MossRock") || name == "Stone" || name == "PondRock" || name == "FallenStone")
            {
                int variant = rockIndex++%3;
                replacement = Mesh("WeatheredRock"+variant,bounds,b => Test_CheonConceptMeshes.Rock(b,variant));
            }
            else if (filter.sharedMesh.name == "Cube" || filter.sharedMesh.name == "Test_ConceptBeveledBlock")
            {
                // 물의 윤곽과 이펙트용 메시는 유지하고 건축·석판의 모서리만 부드럽게 한다.
                if (name != "Test_Waterfall" && name != "Test_WaterRibbon" && !name.Contains("Firefly"))
                    replacement = Mesh("BeveledBlock",bounds,Test_CheonConceptMeshes.BeveledBox);
            }
            if (name.Contains("BoundaryWall") || name.Contains("ShoulderWall") && !name.Contains("Cap") || name == "Test_RealmBoundaryExtension" || name == "Foundation" || name == "Footing") renderer.sharedMaterial = masonry;
            if (name == "Light" && (filter.transform.parent.name.Contains("석등") || filter.transform.parent.name.Contains("Lantern")))
            {
                renderer.sharedMaterial = lanternGlow;
                AddDetail(filter.transform,"Test_ArtLanternFrame",Mesh("LanternFrame",bounds,Test_CheonConceptMeshes.LanternFrame),dark);
            }
            if (replacement != null) filter.sharedMesh = replacement;
        }
    }

    private static Mesh SpiritOrbMesh()
    {
        if (meshes.TryGetValue("SpiritOrb",out Mesh cached)) return cached;
        // 검증된 기본 구 메시를 사용해 에디터 미리보기의 비정상 면을 방지한다.
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var sphere = primitive.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(primitive); meshes["SpiritOrb"] = sphere; return sphere;
    }

    private static void AddDetail(Transform parent, string name, Mesh mesh, Material material)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform; child.SetParent(parent,false);
            Undo.RegisterCreatedObjectUndo(child.gameObject,"프랍 표면 디테일");
        }
        var filter = child.GetComponent<MeshFilter>(); var renderer = child.GetComponent<MeshRenderer>();
        Undo.RecordObjects(new Object[] { filter,renderer },"프랍 표면 디테일 갱신");
        filter.sharedMesh = mesh; renderer.sharedMaterial = material;
    }

    private static void SetupPalette(Test_CheonWorld world)
    {
        var data = new SerializedObject(world); var entries = data.FindProperty("palettes"); entries.arraySize = palette.Count;
        int index = 0;
        foreach (var pair in palette)
        {
            var item = entries.GetArrayElementAtIndex(index++);
            item.FindPropertyRelative("material").objectReferenceValue = pair.Key;
            item.FindPropertyRelative("reality").colorValue = pair.Value[0];
            item.FindPropertyRelative("spirit").colorValue = pair.Value[1];
        }
        data.ApplyModifiedProperties();
    }

    private static void SetupAtmosphere(Test_CheonWorld world, Transform marker)
    {
        string skyPath = Root+"Materials/Test_M_ConceptSky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Cheon/Test/ConceptSky")) { name = "Test_M_ConceptSky" };
            AssetDatabase.CreateAsset(sky,skyPath);
        }
        sky.SetColor("_ZenithColor",Hex("5DABE0"));sky.SetColor("_HorizonColor",Hex("B1D4E4"));sky.SetColor("_CloudColor",Hex("FFF1D5"));EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Hex("C2D5DB");
        RenderSettings.fog = true; RenderSettings.fogColor = Hex("ACD0E2");RenderSettings.fogStartDistance = 85;RenderSettings.fogEndDistance = 280;
        var data = new SerializedObject(world);
        data.FindProperty("skyMaterial").objectReferenceValue = sky;
        SetColor(data,"realityFog","ACD0E2");SetColor(data,"spiritFog","2E4C68");
        SetColor(data,"realityAmbient","C2D5DB");SetColor(data,"spiritAmbient","889BAF");
        SetColor(data,"realitySun","FFE8C1");SetColor(data,"spiritSun","B4DDFF");
        SetColor(data,"realitySky","5DABE0");SetColor(data,"spiritSky","183B62");
        data.FindProperty("realitySunIntensity").floatValue = 1.35f;data.FindProperty("spiritSunIntensity").floatValue = 1.1f;data.ApplyModifiedProperties();
        var sun = RenderSettings.sun;Undo.RecordObject(sun,"컨셉 아트 햇빛");
        sun.color = Hex("FFE8C1");sun.intensity = 1.35f;sun.shadows = LightShadows.Soft;sun.shadowStrength = .85f;sun.shadowBias = .035f;sun.shadowNormalBias = .22f;
        var camera = Object.FindFirstObjectByType<Test_CheonCamera>().GetComponent<Camera>();Undo.RecordObject(camera,"컨셉 아트 하늘과 HDR");
        camera.clearFlags = CameraClearFlags.Skybox;camera.allowHDR = true;
        var cameraData = camera.GetUniversalAdditionalCameraData();Undo.RecordObject(cameraData,"컨셉 아트 후처리");
        cameraData.renderPostProcessing = true;cameraData.volumeLayerMask |= 1;

        var volume = marker.GetComponent<Volume>();if (volume == null) volume = Undo.AddComponent<Volume>(marker.gameObject);
        Undo.RecordObject(volume,"컨셉 아트 볼륨");volume.isGlobal = true;volume.priority = 10;
        string profilePath = Root+"Test_ConceptAtmosphere.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Test_ConceptAtmosphere";AssetDatabase.CreateAsset(profile,profilePath); }
        var bloom = Override<Bloom>(profile);bloom.intensity.Override(.32f);bloom.threshold.Override(1.05f);bloom.scatter.Override(.58f);
        var tone = Override<Tonemapping>(profile);tone.mode.Override(TonemappingMode.Neutral);
        var color = Override<ColorAdjustments>(profile);color.contrast.Override(7);color.saturation.Override(8);color.postExposure.Override(.05f);
        foreach (var component in profile.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);volume.sharedProfile = profile;
    }
    private static void SetColor(SerializedObject data, string name, string hex) { data.FindProperty(name).colorValue = Hex(hex); }
    private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out T component)) return component;
        component = profile.Add<T>(false);component.name = "Test_"+typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component,profile);return component;
    }
}
