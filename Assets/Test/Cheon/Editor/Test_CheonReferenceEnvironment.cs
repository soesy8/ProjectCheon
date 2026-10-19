using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 환경 전용 씬에 레퍼런스 팩의 나머지 환경 요소를 조립한다.
public static class Test_CheonReferenceEnvironment
{
    public const string ScenePath = "Assets/Test/Cheon/Scenes/Test_CheonEnvironmentMap.unity";
    public const string MarkerName = "Test_ReferenceEnvironment";
    private const string PrefabFolder = "Assets/Test/Cheon/Arts/ReferenceEnvironment/Prefabs/";
    private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
    private static Transform nature, landmarks, water, paths, magic, background;

    [MenuItem("Tools/Cheon Test/Add Reference Environment To Map")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("환경 전용 씬의 Edit Mode에서 실행하세요.");
        if (scene.GetRootGameObjects().Any(root => root.name == MarkerName)) return;
        Prefabs.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder.TrimEnd('/') }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            Prefabs[prefab.name.Substring("Test_CheonRef_".Length)] = prefab;
        }
        if (Prefabs.Count != 40) throw new InvalidOperationException("참고 모델 40종의 임포트를 완료하세요.");
        Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("레퍼런스 환경 오브젝트 추가");
        try
        {
            var root = new GameObject(MarkerName).transform; Undo.RegisterCreatedObjectUndo(root.gameObject, "레퍼런스 환경 추가");
            nature = Group(root, "Test_Nature"); landmarks = Group(root, "Test_FolkLandmarks");
            water = Group(root, "Test_WaterFeatures"); paths = Group(root, "Test_PathsAndPaving");
            magic = Group(root, "Test_RitualElements"); background = Group(root, "Test_MountainsAndMist");
            AddPaths(); AddLandmarks(); AddNature(); AddWater(); AddBackground();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        catch { Undo.RevertAllDownToGroup(undoGroup); throw; }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    private static Transform Group(Transform root, string name)
    {
        var group = new GameObject(name).transform; group.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(group.gameObject, "환경 분류"); return group;
    }

    private static Transform Place(string key, Transform parent, Vector3 position, float yaw = 0, float scale = 1)
    {
        var item = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[key], parent);
        Undo.RegisterCreatedObjectUndo(item, "참고 모델 배치");
        item.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); item.transform.localScale = Vector3.one * scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform); return item.transform;
    }

    private static Vector3 Point(float x, float z, float y = 0) => new Vector3(x, y, z);

    private static void AddPaths()
    {
        // 중앙 동선에는 낮은 포장만 두고, 부속 건물로 이어지는 길을 분리한다.
        for (int index = 0; index < 17; index++)
            Place(index % 4 == 0 ? "PathStraight" : index % 4 == 1 ? "PathBorder" : "PathPlain", paths, Point(0, -46 + index * 2));
        PathLine(Point(-.3f,-39.5f), Point(-11.5f,-41), .94f);
        PathLine(Point(-.3f,-24.5f), Point(-14.3f,-25.5f), .94f);
        PathLine(Point(.2f,-26), Point(10.8f,-26), .94f);
        PathLine(Point(0,-11), Point(-18,-3.5f), .9f);
        Place("StoneStairs", paths, Point(0,-48.3f,-.7f),180);
        Place("BoundaryStone", paths, Point(-1.2f,-46),90);
        Place("BoundaryStone", paths, Point(1.2f,-46),90);
        Place("BattleFloor", paths, Point(0,20.7f,.05f),0,3);
    }

    private static void PathLine(Vector3 start, Vector3 end, float scale)
    {
        float length = Vector3.Distance(start,end); int count = Mathf.Max(1, Mathf.CeilToInt(length / (2 * scale)));
        float yaw = Mathf.Atan2(end.x-start.x,end.z-start.z) * Mathf.Rad2Deg;
        for (int index = 0; index < count; index++)
            Place("PathPlain",paths,Vector3.Lerp(start,end,(index+.5f)/count),yaw,scale);
    }

    private static void AddLandmarks()
    {
        Place("OutdoorAltar",landmarks,Point(-20,0),180,1.1f);
        Place("WishRibbons",landmarks,Point(16.7f,-42),180,1.05f);
        Place("SotdaeGroup",landmarks,Point(-18,-44),-22);
        Place("Sotdae",landmarks,Point(25,2),42,.85f);
        Place("RuinedPavilion",landmarks,Point(23,11),180,1.12f);
        Place("CairnA",landmarks,Point(10,-44.5f),12);
        Place("CairnB",landmarks,Point(-9,-17),-18,1.1f);
        Place("CairnC",landmarks,Point(24,45),25,1.15f);
        Place("FlatRock",landmarks,Point(19.7f,-39.7f),18,.85f);
        Place("RiceBowl",landmarks,Point(19.4f,-39.7f,.39f),0,.7f);
        Place("WaterCup",landmarks,Point(19.8f,-39.5f,.4f),0,.8f);
        Place("FruitTray",landmarks,Point(19.9f,-39.9f,.39f),22,.52f);
        Place("WishRibbon",landmarks,Point(14.2f,-42.02f,1.8f),180,.7f);
        Place("TalismanBundle",landmarks,Point(-20.6f,-1.2f,.8f),180,.42f);

        Place("HealingCircle",magic,Point(-5,-36.5f,.01f),0,.95f);
        Place("SealingCircle",magic,Point(-17,5,.02f),0,1.15f);
        Place("ConstellationGarden",magic,Point(21.5f,36,.045f),0,1.05f);
        Place("JadeRings",magic,Point(-23,34,.02f),0,1.25f);
        foreach (var position in new[] {Point(-22,43),Point(-12,48),Point(0,49),Point(12,48),Point(22,43)})
        {
            Place("Sealstone",magic,position,180,1.12f);
            Place("SpiritFlame",magic,position+Vector3.up*1.74f,0,1.1f);
        }
        for (int index=0;index<6;index++)
        {
            float angle=index*Mathf.PI/3;
            Place("SpiritFlame",magic,Point(21.5f+Mathf.Cos(angle)*2.835f,36+Mathf.Sin(angle)*2.835f,1.1f),0,.75f);
        }
        Place("SpiritFlame",magic,Point(21.5f,36,1.4f),0,.85f);
        for (int index=0;index<3;index++)
        {
            float angle=index*Mathf.PI*2/3+Mathf.PI/2;
            Place("Talisman",magic,Point(-17+Mathf.Cos(angle)*2,5+Mathf.Sin(angle)*2,.13f),180,.8f);
        }
    }

    private static void AddNature()
    {
        Vector2[] clearings = {new Vector2(-12,-37.4f),new Vector2(-14.4f,-22.8f),new Vector2(16.7f,-42),new Vector2(-18,-44),new Vector2(15,-26),new Vector2(-20,0),new Vector2(23,11),new Vector2(-23,34),new Vector2(21.5f,36)};
        int counter=0;
        foreach (int side in new[] {-1,1})
            for (float z=-43;z<51;z+=7.8f)
            {
                float x=side*(z<-13.1f?20.8f:28.2f);
                var point=new Vector2(x,z);
                if (clearings.Any(center=>Vector2.Distance(center,point)<5.2f)) continue;
                Place("Pine"+(char)('A'+counter%3),nature,Point(x,z),counter*73, .85f+.08f*(counter%4)); counter++;
            }
        foreach(var point in new[] {Point(-7,-45),Point(10,-34),Point(-18,-31),Point(22,-18),Point(-27,17),Point(26,25),Point(-18,47),Point(18,48)})
        { Place("Pine"+(char)('A'+counter%3),nature,point,counter*61,.85f);counter++; }
        for(int index=0;index<30;index++)
        {
            float z=-44+index*3.1f; int side=index%2==0?-1:1;
            float x=side*(z<-13.1f?22:29);
            if(clearings.Any(center=>Vector2.Distance(center,new Vector2(x,z))<3.8f))continue;
            Place("Rock"+(char)('A'+index%3),nature,Point(x,z,-.12f),index*47,.55f+.15f*(index%3));
        }
        foreach(var point in new[] {Point(11.8f,-24),Point(18.5f,-28),Point(-16.8f,-2.7f),Point(25,13.6f),Point(-24.8f,31),Point(24,38.5f)})
            Place("FlatRock",nature,point,point.x*19,1.1f);
    }

    private static void AddWater()
    {
        Place("Pond",water,Point(15,-26,.015f),0,1.05f);
        Place("WaterfallStream",water,Point(21.8f,-26,.015f),90);
    }

    private static void AddBackground()
    {
        // 기존 정상 지형 아래에 넓어지는 암반을 보태 산줄기와 연결한다.
        Place("GroundedMountainBase",background,Vector3.zero);
        for(int index=0;index<12;index++)
        {
            float angle=index*Mathf.PI*2/12;
            float x=Mathf.Cos(angle)*91,z=7+Mathf.Sin(angle)*109;
            Place(index%2==0?"MountainNear":"MountainMid",background,Point(x,z,-42),-index*30,1.4f+.16f*(index%3));
        }
        for(int index=0;index<10;index++)
        {
            float angle=(index+.3f)*Mathf.PI*2/10;
            Place("MountainFar",background,Point(Mathf.Cos(angle)*155,Mathf.Sin(angle)*176,-50),-index*36,1.7f+(index%3)*.2f);
        }
        for(int index=0;index<22;index++)
        {
            float angle=index*Mathf.PI*2/22;
            Place("CloudLayer",background,Point(Mathf.Cos(angle)*72,5+Mathf.Sin(angle)*88,-13-(index%3)*3),index*33,1.6f+(index%4)*.2f);
        }
    }
}
