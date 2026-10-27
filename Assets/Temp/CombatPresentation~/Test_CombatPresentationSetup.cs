using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class Test_CombatPresentationSetup
{
    const string Root="Assets/Test/CombatPresentation/";
    const string ScenePath=Root+"Scenes/Test_MountainPass_ArtFeedback.unity";
    static readonly List<Vector3> vertices=new List<Vector3>();
    static readonly List<int> triangles=new List<int>();
    static readonly List<Vector2> uvs=new List<Vector2>();
    public static object FinalizeScene()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("테스트 씬 Edit Mode에서 실행하세요.");
        Transform root=GameObject.Find("Test_CombatPresentation").transform;
        foreach(Transform item in root)
        {
            if(!item.name.StartsWith("Test_RockCluster"))continue;
            float bottom=item.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y);
            item.position+=Vector3.up*(root.position.y-.08f-bottom);
        }
        Transform apron=root.Find("Test_OuterApron");if(apron!=null)Object.DestroyImmediate(apron.gameObject);
        AssetDatabase.DeleteAsset(Root+"Meshes/Test_OuterApron.asset");
        AssetDatabase.DeleteAsset(Root+"Materials/Test_M_OuterEarth.mat");
        var flow=Object.FindFirstObjectByType<PassGameFlow>();
        var hud=Object.FindFirstObjectByType<PassHud>();
        var pause=hud.GetComponent<Test_PausePresentation>();
        if(pause==null) pause=hud.gameObject.AddComponent<Test_PausePresentation>();
        pause.Configure(flow,hud);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        return new {scene=ScenePath,groundedRocks=6};
    }
    public static object FinishGround()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("테스트 씬 Edit Mode에서 실행하세요.");
        AssetDatabase.Refresh();
        Transform root=GameObject.Find("Test_CombatPresentation").transform;
        root.Find("Test_OuterApron").gameObject.SetActive(false);
        Material earth=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Test_M_ArenaEarth.mat");
        earth.shader=Shader.Find("Cheon/Tests/ArenaToon");earth.SetFloat("_TextureSaturation",.22f);
        earth.SetVector("_ArenaCenter",root.position);earth.SetFloat("_ArenaRadius",23.7f);
        earth.SetColor("_EdgeTint",new Color(.72f,.77f,.84f));earth.SetColor("_BaseColor",new Color(.94f,.97f,1f));
        EditorUtility.SetDirty(earth);
        foreach(string name in new[]{"Test_M_Granite","Test_M_GraniteLight","Test_M_CloudEngraving"})
        {
            Material material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/"+name+".mat");
            material.SetColor("_BaseColor",name.Contains("Cloud")?new Color(.33f,.32f,.30f):name.EndsWith("Light")?new Color(.51f,.51f,.48f):new Color(.44f,.44f,.42f));
            EditorUtility.SetDirty(material);
        }
        var rockPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/CheonNoiseMap/GraniteBoulder.prefab");
        if(rockPrefab!=null && root.Find("Test_RockCluster_0")==null)
        {
            for(int i=0;i<6;i++)
            {
                float angle=(35+i*60)*Mathf.Deg2Rad;
                var item=(GameObject)PrefabUtility.InstantiatePrefab(rockPrefab,SceneManager.GetActiveScene());
                item.name="Test_RockCluster_"+i;item.transform.SetParent(root,false);
                item.transform.localPosition=new Vector3(Mathf.Sin(angle)*(33+i%2*2),-.25f,Mathf.Cos(angle)*(33+i%2*2));
                item.transform.localRotation=Quaternion.Euler(0,i*71,0);
                var renderer=item.GetComponentInChildren<Renderer>();
                float width=Mathf.Max(renderer.bounds.size.x,renderer.bounds.size.z);
                item.transform.localScale*= (i%2==0?4.5f:3.2f)/Mathf.Max(.1f,width);
                foreach(var collider in item.GetComponentsInChildren<Collider>())collider.enabled=false;
            }
        }
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return new {shader=earth.shader.name,groundSaturation=.22f,originalTexturesUnchanged=true};
    }
    public static object Refine()
    {
        if(EditorApplication.isPlaying) throw new Exception("Edit Mode에서 실행하세요.");
        if(SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("테스트 씬에서만 실행합니다.");
        var root=GameObject.Find("Test_CombatPresentation").transform;
        foreach(Transform child in root)
        {
            if(child.name.StartsWith("Test_ArenaLantern"))
            {Vector3 p=child.localPosition;child.localPosition=new Vector3(p.x*.82f,p.y,p.z*.82f);}
            else child.localScale=new Vector3(.77f,1,.77f);
        }
        var earth=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Test_M_ArenaEarth.mat");
        earth.SetColor("_BaseColor",new Color(.94f,.95f,1f));
        earth.SetColor("_ShadowColor",new Color(.5f,.57f,.68f));earth.SetFloat("_AmbientStrength",.4f);
        EditorUtility.SetDirty(earth);
        var environment=Object.FindFirstObjectByType<PassWorldTransition>();var data=new SerializedObject(environment);
        data.FindProperty("spiritSun").colorValue=new Color(.84f,.9f,1f);
        data.FindProperty("spiritSunIntensity").floatValue=1.12f;
        data.ApplyModifiedPropertiesWithoutUndo();
        if(RenderSettings.sun!=null) {RenderSettings.sun.shadowStrength=.72f;EditorUtility.SetDirty(RenderSettings.sun);}
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return new {innerRadius=23.7f,outerRadius=28.2f,lanternRadius=31.16f};
    }
    public static object IsolateFont()
    {
        if(EditorApplication.isPlaying) throw new Exception("Edit Mode에서 실행하세요.");
        if(!AssetDatabase.IsValidFolder(Root+"Fonts")) AssetDatabase.CreateFolder(Root.TrimEnd('/'),"Fonts");
        var hud=Object.FindFirstObjectByType<PassHud>();var data=new SerializedObject(hud);
        var source=(TMPro.TMP_FontAsset)data.FindProperty("font").objectReferenceValue;
        string fontPath=Root+"Fonts/Test_MountainKorean.asset";
        if(!File.Exists(fontPath)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),fontPath);
        var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
        var settings=data.FindProperty("textSettings").objectReferenceValue;
        string settingsPath=Root+"Fonts/Test_TextSettings.asset";
        if(!File.Exists(settingsPath)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(settings),settingsPath);
        var clonedSettings=AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>(settingsPath);
        var configuration=new SerializedObject(clonedSettings);var iterator=configuration.GetIterator();
        while(iterator.Next(true))
            if(iterator.propertyType==SerializedPropertyType.ObjectReference && iterator.objectReferenceValue==source) iterator.objectReferenceValue=font;
        configuration.ApplyModifiedPropertiesWithoutUndo();
        data.FindProperty("font").objectReferenceValue=font;data.FindProperty("textSettings").objectReferenceValue=clonedSettings;data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        return new {fontPath,settingsPath,atlasWidth=font.atlasWidth,atlasHeight=font.atlasHeight};
    }
    public static object Create()
    {
        if(EditorApplication.isPlaying) throw new Exception("Play Mode를 종료한 뒤 실행하세요.");
        if(File.Exists(ScenePath)) throw new Exception("기존 테스트 씬을 덮어쓰지 않습니다.");
        var active=SceneManager.GetActiveScene();
        if(active.isDirty) throw new Exception("원본 씬에 미저장 변경이 있어 복사를 중지합니다.");
        if(!AssetDatabase.CopyAsset("Assets/Scenes/MountainPass/MountainPass_Optimized.unity",ScenePath)) throw new Exception("씬 복사 실패");
        Scene scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var flow=Object.FindFirstObjectByType<PassGameFlow>();
        var sourceHud=Object.FindFirstObjectByType<PassHud>();
        var floor=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).First(r=>r.name=="Arena_Floor_Flat_UV");
        Vector3 center=new Vector3(floor.bounds.center.x,floor.bounds.max.y+.025f,floor.bounds.center.z);
        var root=new GameObject("Test_CombatPresentation"); root.transform.position=center;
        var earth=CopyMaterial(floor.sharedMaterial,"Test_M_ArenaEarth");
        earth.SetColor("_BaseColor",new Color(.69f,.73f,.8f,1));
        earth.SetColor("_ShadowColor",new Color(.42f,.48f,.61f,1));
        earth.SetFloat("_AmbientStrength",.34f);
        floor.sharedMaterial=earth;
        PrefabUtility.RecordPrefabInstancePropertyModifications(floor);
        var slate=Solid("Test_M_Granite",new Color(.42f,.48f,.56f));
        var slateLight=Solid("Test_M_GraniteLight",new Color(.48f,.53f,.59f));
        var outer=Solid("Test_M_OuterEarth",new Color(.26f,.29f,.32f));
        var engraving=Solid("Test_M_CloudEngraving",new Color(.34f,.36f,.39f));
        BeginMesh(); AddAnnulus(35.2f,52,0,360,128,.005f); MeshObject(root.transform,"Test_OuterApron",outer);
        BeginMesh();
        for(int i=0;i<48;i++) AddAnnulus(30.8f+(i%3)*.1f,35.1f-(i%4)*.12f,i*7.5f+.28f,(i+1)*7.5f-.28f,2,.065f);
        MeshObject(root.transform,"Test_StoneBoundary",slate);
        BeginMesh();
        for(int i=0;i<24;i++) AddAnnulus(35.25f,36.6f,i*15f+.4f,(i+1)*15f-.4f,3,.045f);
        MeshObject(root.transform,"Test_OuterStoneEdge",slateLight);
        BeginMesh(); AddAnnulus(28.6f,28.72f,0,360,180,.014f); AddAnnulus(29.08f,29.18f,0,360,180,.014f);
        // 낮은 대비의 넓은 구름 선만 넣고 중앙의 공격 판정 영역은 비워 둔다.
        for(int motif=0;motif<8;motif++)
        {
            float angle=motif*45*Mathf.Deg2Rad;
            Vector3 origin=new Vector3(Mathf.Sin(angle)*26.6f,0,Mathf.Cos(angle)*26.6f);
            Quaternion rotation=Quaternion.Euler(0,motif*45,0);
            Vector3 previous=Vector3.zero;
            for(int i=0;i<=48;i++)
            {
                float t=i/48f, a=t*Mathf.PI*3.5f, radius=Mathf.Lerp(1.8f,.12f,t);
                Vector3 p=origin+rotation*new Vector3(Mathf.Cos(a)*radius,.018f,Mathf.Sin(a)*radius*.72f);
                if(i>0) AddStrip(previous,p,.12f);
                previous=p;
            }
        }
        MeshObject(root.transform,"Test_CloudRing",engraving);

        var lanternPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/SelfModeling/Lantern.prefab");
        if(lanternPrefab!=null)
        {
            for(int i=0;i<6;i++)
            {
                float angle=(i*60+20)*Mathf.Deg2Rad;
                var lantern=(GameObject)PrefabUtility.InstantiatePrefab(lanternPrefab,scene);
                lantern.name="Test_ArenaLantern_"+(i+1); lantern.transform.SetParent(root.transform,true);
                lantern.transform.position=center+new Vector3(Mathf.Sin(angle)*38,0,Mathf.Cos(angle)*38);
                foreach(var collider in lantern.GetComponentsInChildren<Collider>()) collider.enabled=false;
            }
        }
        var camera=flow.View;
        var framing=camera.gameObject.AddComponent<Test_BossFraming>();
        framing.Configure(flow,camera,flow.Boss.GetComponentsInChildren<Renderer>().First(r=>r.name=="Head"));
        var style=sourceHud.gameObject.AddComponent<Test_HudStyle>(); style.Configure(flow,sourceHud);
        var environment=Object.FindFirstObjectByType<PassWorldTransition>();
        var data=new SerializedObject(environment);
        data.FindProperty("spiritAmbient").colorValue=new Color(.42f,.5f,.67f);
        data.FindProperty("spiritSun").colorValue=new Color(.73f,.83f,1f);
        data.FindProperty("spiritSunIntensity").floatValue=1.02f;
        data.FindProperty("spiritBackground").colorValue=new Color(.14f,.19f,.29f);
        data.ApplyModifiedPropertiesWithoutUndo();
        var hit=Object.FindFirstObjectByType<PassHitEffects>();
        var impact=new SerializedObject(hit); impact.FindProperty("particleCount").intValue=12; impact.FindProperty("effectScale").floatValue=1.15f; impact.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        return new {scene=ScenePath,center=center.ToString(),stones=48,lanterns=6,originalSceneUntouched=true};
    }
    static Material CopyMaterial(Material source,string name)
    { var material=new Material(source){name=name}; AssetDatabase.CreateAsset(material,Root+"Materials/"+name+".mat"); return material; }
    static Material Solid(string name,Color color)
    {
        var material=new Material(Shader.Find("Cheon/SimpleToon")){name=name};
        material.SetColor("_BaseColor",color); material.SetColor("_ShadowColor",new Color(.39f,.45f,.58f));
        material.SetFloat("_MidTone",.85f); material.SetFloat("_ShadowThreshold",.18f); material.SetFloat("_HighlightThreshold",.7f);
        material.SetFloat("_AmbientStrength",.3f); material.SetFloat("_ReceiveShadows",1);
        AssetDatabase.CreateAsset(material,Root+"Materials/"+name+".mat"); return material;
    }
    static void BeginMesh() { vertices.Clear();triangles.Clear();uvs.Clear(); }
    static Vector3 Polar(float radius,float degrees,float height) { float a=degrees*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*radius,height,Mathf.Cos(a)*radius); }
    static void AddAnnulus(float inner,float outer,float start,float end,int segments,float height)
    {
        for(int i=0;i<segments;i++)
        {
            float a=Mathf.Lerp(start,end,i/(float)segments),b=Mathf.Lerp(start,end,(i+1f)/segments);
            AddQuad(Polar(inner,a,height),Polar(inner,b,height),Polar(outer,b,height),Polar(outer,a,height));
        }
    }
    static void AddStrip(Vector3 a,Vector3 b,float width)
    { Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized)*width*.5f;AddQuad(a-side,b-side,b+side,a+side); }
    static void AddQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        int i=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
        uvs.Add(Vector2.zero);uvs.Add(Vector2.up);uvs.Add(Vector2.one);uvs.Add(Vector2.right);
        if(Vector3.Cross(b-a,c-a).y>=0) { triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3}); }
        else { triangles.AddRange(new[]{i,i+2,i+1,i,i+3,i+2}); }
    }
    static void MeshObject(Transform parent,string name,Material material)
    {
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uvs);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Root+"Meshes/"+name+".asset");
        var item=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); item.transform.SetParent(parent,false);
        item.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=item.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
}
