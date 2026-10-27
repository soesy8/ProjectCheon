using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneOptimizationAudit
{
    public static object Main()
    {
        var scene=SceneManager.GetActiveScene();
        var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
        var components=objects.SelectMany(o=>o.GetComponents<Component>()).Where(c=>c!=null).ToArray();
        var renderers=components.OfType<Renderer>().ToArray();
        var lights=components.OfType<Light>().ToArray();
        var behaviors=components.OfType<MonoBehaviour>().ToArray();
        var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
        var nullReferences=new List<object>();
        foreach(var behavior in behaviors)
        {
            var serialized=new SerializedObject(behavior);var property=serialized.GetIterator();
            while(property.Next(true))
                if(property.propertyType==SerializedPropertyType.ObjectReference && property.name!="m_Script" && property.objectReferenceValue==null)
                    nullReferences.Add(new{owner=PathOf(behavior.transform),type=behavior.GetType().Name,field=property.propertyPath,missing=property.objectReferenceInstanceIDValue!=0});
        }
        var outsideTests=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/") && !p.StartsWith("Assets/Test/") && !p.StartsWith("Assets/Temp/") && !AssetDatabase.IsValidFolder(p)
            && new[]{".unity",".prefab",".asset",".controller",".overrideController",".mat",".fbx"}.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray();
        var testReferences=outsideTests.Select(p=>new{path=p,dependencies=AssetDatabase.GetDependencies(p,false).Where(d=>d.StartsWith("Assets/Test/")).ToArray()}).Where(r=>r.dependencies.Length>0).ToArray();
        var result=new{
            scene=scene.path,
            counts=new{objects=objects.Length,activeObjects=objects.Count(o=>o.activeInHierarchy),renderers=renderers.Length,activeRenderers=renderers.Count(r=>r.enabled&&r.gameObject.activeInHierarchy),materials=materials.Length,
                staticRenderers=renderers.Count(r=>GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.BatchingStatic)),lights=lights.Length,realtimeLights=lights.Count(l=>l.lightmapBakeType!=LightmapBakeType.Baked),shadowLights=lights.Count(l=>l.shadows!=LightShadows.None),colliders=components.OfType<Collider>().Count(),
                meshColliders=components.OfType<MeshCollider>().Count(),rigidbodies=components.OfType<Rigidbody>().Count(),particleSystems=components.OfType<ParticleSystem>().Count(),missingScripts=objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount)},
            roots=scene.GetRootGameObjects().Select(r=>new{name=r.name,children=r.GetComponentsInChildren<Transform>(true).Length,renderers=r.GetComponentsInChildren<Renderer>(true).Length,position=new{x=r.transform.position.x,y=r.transform.position.y,z=r.transform.position.z}}).ToArray(),
            behaviors=behaviors.GroupBy(c=>c.GetType().Name).Select(g=>new{type=g.Key,count=g.Count(),enabled=g.Count(c=>c.enabled)}).OrderByDescending(c=>c.count).ToArray(),
            lights=lights.Select(l=>new{path=PathOf(l.transform),type=l.type.ToString(),l.enabled,range=l.range,shadows=l.shadows.ToString(),l.intensity,renderMode=l.renderMode.ToString()}).ToArray(),
            repeatedMeshes=components.OfType<MeshFilter>().Where(f=>f.sharedMesh!=null).GroupBy(f=>f.sharedMesh).Select(g=>new{mesh=g.Key.name,count=g.Count(),vertices=g.Key.vertexCount,path=AssetDatabase.GetAssetPath(g.Key)}).OrderByDescending(g=>g.count).Take(20).ToArray(),
            nullReferences,testReferences,
            mainDependencies=AssetDatabase.GetDependencies(scene.path,true),
            buildScenes=EditorBuildSettings.scenes.Select(s=>new{s.path,s.enabled,exists=File.Exists(s.path)}).ToArray()
        };
        string report="Assets/Temp/SceneOptimization~/"+(scene.path.Contains("_Optimized")?"AuditAfter":"AuditBefore")+".json";
        File.WriteAllText(report,Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return new{result.scene,result.counts,result.roots,result.behaviors,nullReferences=nullReferences.Count,testReferences,report};
    }
    private static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
}
