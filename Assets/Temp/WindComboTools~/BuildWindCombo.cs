using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Cheon.Combat;
using Object = UnityEngine.Object;

public static class BuildWindCombo
{
    private const string Art = "Assets/Arts/WindCombo";
    private static void Set(Object obj, string name, Object value)
    {
        var so = new SerializedObject(obj);
        so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Transform Node(string name, Transform parent, Vector3 position)
    {
        var item = new GameObject(name).transform;
        item.SetParent(parent, false);
        item.localPosition = position;
        return item;
    }
    private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var item = GameObject.CreatePrimitive(type);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = scale;
        item.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(item.GetComponent<Collider>());
        return item;
    }
    private static Material Solid(string name, Color color)
    {
        string path = Art + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .08f);
        return material;
    }
    private static Material Effect(string name, string texture)
    {
        string path = Art + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Cheon/WindCloud"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/" + texture + ".png"));
        material.SetFloat("_Cutoff", texture.Contains("Impact") ? .72f : .18f);
        return material;
    }
    private static void Curve(AnimationClip clip, string path, string property, float[] times, float[] values)
    {
        var curve = new AnimationCurve(times.Select((t,i) => new Keyframe(t, values[i])).ToArray());
        for (int i = 0; i < times.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }
    private static void Position(AnimationClip clip, string path, float[] times, Vector3[] values)
    {
        Curve(clip,path,"m_LocalPosition.x",times,values.Select(v=>v.x).ToArray());
        Curve(clip,path,"m_LocalPosition.y",times,values.Select(v=>v.y).ToArray());
        Curve(clip,path,"m_LocalPosition.z",times,values.Select(v=>v.z).ToArray());
    }
    private static void Rotation(AnimationClip clip, string path, float[] times, Vector3[] values)
    {
        Curve(clip,path,"localEulerAnglesRaw.x",times,values.Select(v=>v.x).ToArray());
        Curve(clip,path,"localEulerAnglesRaw.y",times,values.Select(v=>v.y).ToArray());
        Curve(clip,path,"localEulerAnglesRaw.z",times,values.Select(v=>v.z).ToArray());
    }
    private static AnimationClip MakeClip(int stage)
    {
        string[] names = { "Wind01_Brush", "Wind02_Return", "Wind03_Release" };
        string path = Art + "/Animations/" + names[stage-1] + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = names[stage-1], frameRate = 60 }; AssetDatabase.CreateAsset(clip,path); }
        clip.ClearCurves();
        float[] times;
        Vector3[] hand, left, body, torso, fan;
        Vector3 idle = new Vector3(.62f,.03f,.28f);
        Vector3 leftIdle = new Vector3(-.55f,.03f,.15f);
        if (stage == 1)
        {
            times = new[] {0f,.06f,.09f,.14f,.28f};
            hand = new[] {idle,new Vector3(.72f,-.03f,.20f),new Vector3(.15f,-.04f,.80f),new Vector3(-.55f,-.10f,.48f),new Vector3(-.52f,-.08f,.38f)};
            left = new[] {leftIdle,new Vector3(-.55f,.03f,.12f),new Vector3(-.50f,.12f,.18f),new Vector3(-.35f,.15f,.12f),new Vector3(-.4f,.1f,.1f)};
            torso = new[] {Vector3.zero,new Vector3(0,-8,0),new Vector3(2,3,0),new Vector3(2,12,-2),new Vector3(0,8,0)};
            body = times.Select(t=>new Vector3(0,1.05f,0)).ToArray();
            fan = new[] {new Vector3(-12,-60,-25),new Vector3(-20,-80,-40),new Vector3(-55,10,-70),new Vector3(-25,75,-25),new Vector3(-20,60,-15)};
        }
        else if (stage == 2)
        {
            times = new[] {0f,.07f,.11f,.16f,.24f,.32f};
            hand = new[] {new Vector3(-.52f,-.08f,.38f),new Vector3(-.6f,-.16f,.4f),new Vector3(.05f,.13f,.85f),new Vector3(.65f,.36f,.4f),new Vector3(.45f,.20f,.38f),new Vector3(.3f,.10f,.32f)};
            left = new[] {new Vector3(-.4f,.1f,.1f),leftIdle,leftIdle,new Vector3(-.4f,.06f,.10f),new Vector3(-.05f,.03f,.25f),new Vector3(.12f,.04f,.28f)};
            torso = new[] {new Vector3(0,8,0),new Vector3(2,12,0),new Vector3(2,0,0),new Vector3(0,-10,0),new Vector3(-2,-6,0),new Vector3(-2,0,0)};
            body = times.Select(t=>new Vector3(0,1.05f,0)).ToArray();
            fan = new[] {new Vector3(-20,60,-15),new Vector3(-25,70,-30),new Vector3(-35,0,30),new Vector3(15,-60,45),new Vector3(5,-25,10),new Vector3(0,0,0)};
        }
        else
        {
            times = new[] {0f,.20f,.38f,.40f,.47f,.52f,.68f,1f};
            hand = new[] {new Vector3(.3f,.1f,.32f),new Vector3(.2f,.05f,.28f),new Vector3(.16f,.02f,.20f),new Vector3(.1f,.1f,.55f),new Vector3(.06f,.12f,.93f),new Vector3(.05f,.1f,.9f),new Vector3(.1f,.08f,.8f),idle};
            left = new[] {new Vector3(.12f,.04f,.28f),new Vector3(.08f,-.02f,.26f),new Vector3(.04f,-.05f,.18f),new Vector3(-.02f,.03f,.50f),new Vector3(-.06f,.04f,.85f),new Vector3(-.07f,.03f,.82f),new Vector3(-.06f,.02f,.7f),leftIdle};
            torso = new[] {new Vector3(-2,0,0),new Vector3(-7,0,0),new Vector3(-10,0,0),new Vector3(0,0,0),new Vector3(9,0,0),new Vector3(8,0,0),new Vector3(5,0,0),Vector3.zero};
            body = new[] {new Vector3(0,1.05f,0),new Vector3(0,.99f,-.10f),new Vector3(0,.94f,-.15f),new Vector3(0,.98f,-.02f),new Vector3(0,1.06f,.08f),new Vector3(0,1.05f,.07f),new Vector3(0,1.02f,.04f),new Vector3(0,1.05f,0)};
            fan = new[] {Vector3.zero,new Vector3(0,-10,0),new Vector3(0,-15,0),new Vector3(0,0,0),new Vector3(12,0,0),new Vector3(12,0,0),new Vector3(5,-10,-5),new Vector3(-12,-60,-25)};
        }
        Position(clip,"Body/RightHand",times,hand);
        Position(clip,"Body/LeftHand",times,left);
        Position(clip,"Body/RightElbow",times,hand.Select(v=>new Vector3(Mathf.Max(.25f,v.x*.6f),v.y-.12f,v.z*.4f)).ToArray());
        Position(clip,"Body/LeftElbow",times,left.Select(v=>new Vector3(Mathf.Min(-.25f,v.x*.6f),v.y-.12f,v.z*.4f)).ToArray());
        Position(clip,"Body",times,body);
        Rotation(clip,"Body",times,torso);
        Rotation(clip,"Body/RightHand/FanHand",times,fan);
        Position(clip,"LowerHanbok",times,body.Select(v=>new Vector3(0,.72f+(v.y-1.05f)*.6f,v.z*.45f)).ToArray());
        Position(clip,"LeftBoot",times,times.Select(t=>new Vector3(-.21f,.12f,stage==3 ? .16f*Mathf.Sin(t*Mathf.PI) : .09f)).ToArray());
        Position(clip,"RightBoot",times,times.Select(t=>new Vector3(.21f,.12f,.09f)).ToArray());
        EditorUtility.SetDirty(clip);
        return clip;
    }
    public static object Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit mode required.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Test/Cheon/Scenes/Test_CheonEnvironmentMap.unity" || scene.isDirty)
            throw new InvalidOperationException("Expected saved environment map.");
        if (GameObject.Find("WindComboPlayer") != null) throw new InvalidOperationException("Already installed.");
        foreach (string path in AssetDatabase.FindAssets("t:Texture2D",new[]{Art+"/Textures"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        var ribbon = Effect("M_WindRibbon","T_WindRibbon");
        var crescent = Effect("M_WindCrescent","T_WindCrescent");
        var impact = Effect("M_WindImpact","T_WindImpact");
        var wood = Solid("M_PracticeWood",new Color(.38f,.27f,.18f));
        var ink = Solid("M_WindInk",new Color(.07f,.16f,.17f));
        var jade = Solid("M_WindJade",new Color(.28f,.46f,.42f));
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/Meshes/WindQuad.asset");
        if (mesh == null)
        {
            mesh = new Mesh {name="WindQuad"};
            mesh.vertices = new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)};
            mesh.uv = new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
            mesh.triangles = new[]{0,2,1,2,3,1}; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Art+"/Meshes/WindQuad.asset");
        }
        var sourceScene = EditorSceneManager.OpenScene("Assets/Test/Cheon/Scenes/Test_CheonBoundary.unity",OpenSceneMode.Additive);
        var sourcePlayer = sourceScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Cheon.Testing.Test_CheonPlayer>(true)).First();
        var sourceData = new SerializedObject(sourcePlayer);
        Transform sourceModel = (Transform)sourceData.FindProperty("model").objectReferenceValue;
        var player = new GameObject("WindComboPlayer");
        SceneManager.MoveGameObjectToScene(player,scene);
        player.tag = "Player";
        var model = Object.Instantiate(sourceModel.gameObject,player.transform).transform;
        model.name = "Model"; model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
        EditorSceneManager.CloseScene(sourceScene,true);
        SceneManager.SetActiveScene(scene);
        var body = Node("Body",model,new Vector3(0,1.05f,0));
        foreach (Transform child in model.Cast<Transform>().ToArray())
        {
            if (child == body || child.name == "LowerHanbok" || child.name == "Boot" || child.name == "Sash") continue;
            child.SetParent(body,true);
        }
        var boots = model.Cast<Transform>().Where(t=>t.name=="Boot").OrderBy(t=>t.localPosition.x).ToArray();
        boots[0].name = "LeftBoot"; boots[1].name = "RightBoot";
        var sleeves = body.Cast<Transform>().Where(t=>t.name=="Sleeve").OrderBy(t=>t.localPosition.x).ToArray();
        var cream = sleeves[0].GetComponent<Renderer>().sharedMaterial;
        var skin = body.Find("Head").GetComponent<Renderer>().sharedMaterial;
        var rightShoulder = Node("RightShoulder",body,new Vector3(.28f,.32f,0));
        var leftShoulder = Node("LeftShoulder",body,new Vector3(-.28f,.32f,0));
        var rightElbow = Node("RightElbow",body,new Vector3(.42f,-.1f,.10f));
        var leftElbow = Node("LeftElbow",body,new Vector3(-.4f,-.1f,.08f));
        var rightHand = Node("RightHand",body,new Vector3(.62f,.03f,.28f));
        var leftHand = Node("LeftHand",body,new Vector3(-.55f,.03f,.15f));
        Shape("Palm",PrimitiveType.Sphere,rightHand,Vector3.zero,Vector3.one*.16f,skin);
        Shape("Palm",PrimitiveType.Sphere,leftHand,Vector3.zero,Vector3.one*.16f,skin);
        var rightLower = Shape("RightForearm",PrimitiveType.Capsule,body,Vector3.zero,Vector3.one,cream).transform;
        var leftLower = Shape("LeftForearm",PrimitiveType.Capsule,body,Vector3.zero,Vector3.one,cream).transform;
        var fan = body.Find("FanHand");
        fan.SetParent(rightHand,false); fan.localPosition = Vector3.zero;
        fan.localRotation = Quaternion.Euler(-12,-60,-25); fan.localScale = Vector3.one*.68f;
        var arms = model.gameObject.AddComponent<WindComboArms>();
        Set(arms,"rightShoulder",rightShoulder); Set(arms,"rightElbow",rightElbow); Set(arms,"rightHand",rightHand);
        Set(arms,"rightUpper",sleeves[1]); Set(arms,"rightLower",rightLower);
        Set(arms,"leftShoulder",leftShoulder); Set(arms,"leftElbow",leftElbow); Set(arms,"leftHand",leftHand);
        Set(arms,"leftUpper",sleeves[0]); Set(arms,"leftLower",leftLower);
        arms.AlignArms();
        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f; controller.center = Vector3.up*.9f; controller.radius = .30f; controller.stepOffset=.3f;
        var motion = player.AddComponent<WindComboMotion>(); Set(motion,"animationRoot",model.gameObject);
        var motionData = new SerializedObject(motion);
        var clips = motionData.FindProperty("attackClips"); clips.arraySize=3;
        for (int i=0;i<3;i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = MakeClip(i+1);
        motionData.ApplyModifiedPropertiesWithoutUndo();
        var effects = player.AddComponent<WindComboEffects>();
        Set(effects,"fan",fan); Set(effects,"ribbonMaterial",ribbon); Set(effects,"crescentMaterial",crescent); Set(effects,"impactMaterial",impact); Set(effects,"effectMesh",mesh);
        var combat = player.AddComponent<WindComboAttack>(); Set(combat,"motion",motion); Set(combat,"effects",effects);
        var input = player.AddComponent<WindComboPlayer>();
        Set(input,"guideFont",AssetDatabase.LoadAssetAtPath<Font>("Assets/Test/Cheon/Arts/Fonts/Test_CheonKorean.ttf"));
        // 기존 환경은 유지하고, 수련지 중앙에서 시작한다.
        Vector3 spawn = new Vector3(0,2,-18);
        Physics.SyncTransforms();
        if (Physics.Raycast(spawn+Vector3.up*30,Vector3.down,out var floor,70)) spawn.y=floor.point.y+.05f;
        player.transform.position=spawn;
        player.transform.rotation=Quaternion.Euler(0,90,0);
        var cameraObject = new GameObject("WindComboCamera",typeof(Camera),typeof(AudioListener));
        var camera = cameraObject.GetComponent<Camera>(); cameraObject.tag="MainCamera";
        camera.orthographic=true; camera.orthographicSize=5.2f; camera.farClipPlane=180;
        camera.backgroundColor=new Color(.14f,.22f,.23f);
        camera.clearFlags=CameraClearFlags.SolidColor;
        Vector3 cameraOffset = new Vector3(7,9,-10);
        camera.transform.position=spawn+cameraOffset;
        camera.transform.rotation=Quaternion.LookRotation(-cameraOffset+Vector3.up*.8f);
        Set(input,"viewCamera",camera);
        var targets = new GameObject("WindComboPracticeTargets");
        for (int i=0;i<3;i++)
        {
            var dummy=Node("WindPracticeDummy_"+(i+1),targets.transform,spawn+Vector3.right*(1.8f+i*1.1f)+Vector3.forward*(i==1?.45f:0));
            dummy.position=new Vector3(dummy.position.x,spawn.y,dummy.position.z);
            var reaction=Node("Reaction",dummy,Vector3.up*.8f);
            Shape("Body",PrimitiveType.Cube,reaction,new Vector3(0,.15f,0),new Vector3(.36f,1.1f,.36f),wood);
            Shape("Head",PrimitiveType.Cube,reaction,new Vector3(0,.95f,0),new Vector3(.43f,.4f,.4f),wood);
            Shape("Crossbar",PrimitiveType.Cube,reaction,new Vector3(0,.42f,0),new Vector3(.95f,.18f,.18f),wood);
            Shape("Belt",PrimitiveType.Cylinder,reaction,new Vector3(0,.05f,0),new Vector3(.47f,.06f,.47f),jade);
            Shape("Base",PrimitiveType.Cylinder,dummy,new Vector3(0,.06f,0),new Vector3(.7f,.06f,.7f),ink);
            var collider=dummy.gameObject.AddComponent<BoxCollider>(); collider.center=Vector3.up*.9f; collider.size=new Vector3(.6f,1.8f,.6f);
            var target=dummy.gameObject.AddComponent<WindComboTarget>(); Set(target,"reactionRoot",reaction);
        }
        PrefabUtility.SaveAsPrefabAsset(player,"Assets/Prefabs/WindCombo/WindComboPlayer.prefab");
        PrefabUtility.SaveAsPrefabAsset(targets.transform.GetChild(0).gameObject,"Assets/Prefabs/WindCombo/WindPracticeDummy.prefab");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject=player;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(spawn+Vector3.up, camera.transform.rotation,7f);
        return new {scene=scene.path,player=player.name,spawn=player.transform.position,clips=3,textures=3,targets=3};
    }
}
