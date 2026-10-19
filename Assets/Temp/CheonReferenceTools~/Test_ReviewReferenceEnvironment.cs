using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Test_ReviewReferenceEnvironment
{
    private const string Root = "Assets/Test/Cheon/Arts/ReferenceEnvironment/";
    private const string Preview = "Assets/Temp/CheonReferencePreviews";

    public static object BuildAndVerify()
    {
        Scene original = SceneManager.GetActiveScene(); bool originalDirty = original.isDirty;
        string originalHash = Hash(Test_CheonSceneBuilder.ScenePath);
        var map = EditorSceneManager.OpenScene(Test_CheonReferenceEnvironment.ScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(map);
            var originalObjects = All(map).ToDictionary(t => t, t => t.localToWorldMatrix);
            int previousModels = All(map).Count(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            Test_CheonReferenceEnvironment.Apply();
            foreach (var pair in originalObjects)
            {
                if (pair.Key == null) throw new InvalidOperationException("기존 오브젝트가 제거되었습니다.");
                for (int index=0;index<16;index++) if (Mathf.Abs(pair.Key.localToWorldMatrix[index]-pair.Value[index])>.0001f)
                    throw new InvalidOperationException("기존 배치가 변경되었습니다: "+pair.Key.name);
            }
            var added = map.GetRootGameObjects().First(g=>g.name==Test_CheonReferenceEnvironment.MarkerName);
            var newInstances = added.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();
            var renderers = All(map).SelectMany(t=>t.GetComponents<MeshRenderer>()).ToArray();
            int invalidMaterials = renderers.SelectMany(r=>r.sharedMaterials).Count(m=>m==null||m.shader==null||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader));
            int scripts = All(map).Sum(t=>t.GetComponents<MonoBehaviour>().Length+GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(invalidMaterials!=0||scripts!=0||Hash(Test_CheonSceneBuilder.ScenePath)!=originalHash||original.isDirty!=originalDirty)
                throw new InvalidOperationException("환경 씬 검증 실패");
            var result = new {
                scene=map.path, originalSceneUnchanged=true, previousObjectsPreserved=originalObjects.Count, previousModelInstances=previousModels,
                addedModelInstances=newInstances.Length, addedPrefabTypes=newInstances.Select(t=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)).Distinct().Count(),
                prefabLibrary=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"Prefabs"}).Length, invalidMaterials, scripts,
                cameras=All(map).Sum(t=>t.GetComponents<Camera>().Length), lights=All(map).Sum(t=>t.GetComponents<Light>().Length),
                types=newInstances.GroupBy(t=>t.name).Select(g=>new {name=g.Key,count=g.Count()}).ToArray()
            };
            File.WriteAllText("Assets/Temp/Test_CheonReferenceVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(map,true); }
    }

    public static object CaptureScene()
    {
        Scene original=SceneManager.GetActiveScene();
        Scene map=EditorSceneManager.OpenScene(Test_CheonReferenceEnvironment.ScenePath,OpenSceneMode.Additive);
        Directory.CreateDirectory(Preview);
        try
        {
            SceneManager.SetActiveScene(map);
            Draw(map,new Vector3(95,135,-150),new Vector3(0,-4,3),67,1800,1400,"Overview");
            Draw(map,new Vector3(35,32,-58),new Vector3(3,0,-26),24,1600,1100,"Courtyard");
            Draw(map,new Vector3(48,30,-5),new Vector3(17,-2,-24),18,1600,1100,"Waterfall");
            Draw(map,new Vector3(52,40,-13),new Vector3(0,0,22),37,1600,1100,"SpiritGarden");
            return Preview;
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(map,true); }
    }

    public static object CaptureCatalog()
    {
        Scene original=SceneManager.GetActiveScene();
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        Directory.CreateDirectory(Preview);
        var catalog=JObject.Parse(File.ReadAllText(Root+"Test_ReferenceModelCatalog.json"));
        try
        {
            SceneManager.SetActiveScene(scene);
            foreach(var entry in catalog["models"])
            {
                string key=(string)entry["key"];
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Test_CheonRef_"+key+".prefab");
                var instance=Object.Instantiate(prefab);
                try
                {
                    instance.transform.rotation=Quaternion.Euler(0,180,0);
                    var renderers=instance.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    float size=Mathf.Max(bounds.size.y,bounds.size.x*.83f,bounds.size.z*.75f)*.72f;
                    var direction=new Vector3(.9f,.7f,-1.2f).normalized;
                    Draw(scene,bounds.center+direction*Mathf.Max(10,size*4),bounds.center,Mathf.Max(.25f,size),640,640,"Catalog_"+key);
                }
                finally {Object.DestroyImmediate(instance);}
            }
            return new{previews=catalog["models"].Count()};
        }
        finally {SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
    }

    private static void Draw(Scene scene,Vector3 position,Vector3 target,float size,int width,int height,string name)
    {
        // 함께 열려 있는 원본 씬이 미리보기에 겹쳐 그려지지 않도록 잠시 렌더링만 숨긴다.
        var otherRenderers=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=scene)
            .SelectMany(s=>s.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
        var originalVisibility=otherRenderers.Select(r=>r.forceRenderingOff).ToArray();
        for(int index=0;index<otherRenderers.Length;index++)otherRenderers[index].forceRenderingOff=true;
        var cameraObject=new GameObject("Test_TemporaryReferencePreviewCamera");var camera=cameraObject.AddComponent<Camera>();
        SceneManager.MoveGameObjectToScene(cameraObject,scene);
        camera.enabled=false;camera.scene=scene;camera.orthographic=true;camera.orthographicSize=size;
        camera.nearClipPlane=.05f;camera.farClipPlane=1600;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.89f,.91f,.88f);
        camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));camera.aspect=width/(float)height;
        var sunObject=new GameObject("Test_TemporaryReferencePreviewSun");SceneManager.MoveGameObjectToScene(sunObject,scene);
        var sun=sunObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.94f,.82f);sun.intensity=1.05f;
        sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;sun.transform.rotation=Quaternion.Euler(48,-28,0);
        var previousSun=RenderSettings.sun;var previousAmbient=RenderSettings.ambientLight;var previousMode=RenderSettings.ambientMode;bool previousFog=RenderSettings.fog;
        var texture=new RenderTexture(width,height,24);var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.64f,.68f,.67f);RenderSettings.fog=false;
            camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
            image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            File.WriteAllBytes(Preview+"/Test_T_Reference_"+name+".png",image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;RenderSettings.sun=previousSun;RenderSettings.ambientMode=previousMode;RenderSettings.ambientLight=previousAmbient;RenderSettings.fog=previousFog;
            texture.Release();Object.DestroyImmediate(texture);if(image!=null)Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(sunObject);
            for(int index=0;index<otherRenderers.Length;index++)if(otherRenderers[index]!=null)otherRenderers[index].forceRenderingOff=originalVisibility[index];
        }
    }

    private static Transform[] All(Scene scene)=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
    private static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
}
