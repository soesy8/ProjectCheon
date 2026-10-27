using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Cheon.Prototype.MountainPass;
using Object=UnityEngine.Object;

public static class OptimizeSceneStructure
{
    private sealed class Wall
    {
        public BoxCollider source; public Vector3[] corners; public Vector3 center;
        public readonly List<Ray> probes=new List<Ray>();public float probeLength;
    }
    private static readonly int[] BoxTriangles={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
    private static readonly string[] SafetyRoots={"ApproachSafetyWalls","ApproachRearSafetyWalls","MountainOuterSafetyWalls","StairConnectionSafetyWalls"};
    private static readonly Dictionary<string,Transform> groups=new Dictionary<string,Transform>();
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var scene=SceneManager.GetActiveScene();
        if(!scene.path.EndsWith("MountainPass_Optimized.unity"))throw new Exception("Optimized scene required");
        var roots=scene.GetRootGameObjects();
        int before=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Length);
        var all=roots.SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        var references=new HashSet<Object>();
        foreach(var component in all)
        {
            if(component is Transform)continue;
            var serialized=new SerializedObject(component);var field=serialized.GetIterator();
            while(field.Next(true))if(field.propertyType==SerializedPropertyType.ObjectReference && field.objectReferenceValue!=null && field.name!="m_GameObject")references.Add(field.objectReferenceValue);
        }
        var range=all.OfType<PassAttackRangeEffects>().SingleOrDefault();
        int obsoleteRangeObjects=0;
        if(range!=null && !range.enabled)
        {obsoleteRangeObjects=range.GetComponentsInChildren<Transform>(true).Length;Object.DestroyImmediate(range.gameObject);}

        // 선택하지 않은 외형 메시만 제거하고, 본·장착 소켓·스크립트에서 참조하는 오브젝트는 보존한다.
        int unusedRenderers=0;
        var player=all.OfType<PassPlayerMovement>().Single();
        foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if(renderer==null || renderer.gameObject.activeInHierarchy || renderer.transform.childCount!=0)continue;
            if(references.Contains(renderer)||references.Contains(renderer.transform)||references.Contains(renderer.gameObject))continue;
            if(renderer.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is MeshFilter)&&!(c is Renderer)))continue;
            Object.DestroyImmediate(renderer.gameObject);unusedRenderers++;
        }

        // 안전벽의 정확한 월드 모양을 공간별 정적 충돌 메시로 합친다. 지형·계단 바닥은 유지한다.
        var walls=new List<Wall>();
        foreach(var root in roots.Where(r=>SafetyRoots.Contains(r.name)))
        {
            foreach(var box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if(!box.enabled||box.isTrigger||!box.gameObject.activeInHierarchy||box.attachedRigidbody!=null)continue;
                if(references.Contains(box)||references.Contains(box.gameObject)||references.Contains(box.transform))continue;
                var wall=new Wall{source=box,center=box.transform.TransformPoint(box.center)};
                Vector3 half=box.size*.5f;
                var corners=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
                wall.corners=corners.Select(c=>box.transform.TransformPoint(box.center+Vector3.Scale(c,half))).ToArray();
                wall.probeLength=box.bounds.size.magnitude+2f;
                foreach(Vector3 axis in new[]{box.transform.right,box.transform.up,box.transform.forward})
                    foreach(float sign in new[]{-1f,1f})
                    {Vector3 direction=axis*sign;wall.probes.Add(new Ray(wall.center+direction*wall.probeLength,-direction));}
                walls.Add(wall);
            }
        }
        var chunks=walls.GroupBy(w=>new{layer=w.source.gameObject.layer,material=w.source.sharedMaterial,x=Mathf.FloorToInt(w.center.x/16f),z=Mathf.FloorToInt(w.center.z/16f)}).ToArray();
        EnsureFolder("Assets/Arts/Environment/MountainPass/Collision");
        var collisionRoot=Group(scene,"90_충돌");
        int probeCount=0,chunkIndex=0;
        foreach(var chunk in chunks)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            foreach(var wall in chunk)
            {
                int first=vertices.Count;vertices.AddRange(wall.corners);
                bool mirrored=wall.source.transform.localToWorldMatrix.determinant<0f;
                for(int index=0;index<BoxTriangles.Length;index+=3)
                {triangles.Add(first+BoxTriangles[index]);triangles.Add(first+BoxTriangles[index+(mirrored?2:1)]);triangles.Add(first+BoxTriangles[index+(mirrored?1:2)]);}
            }
            var mesh=new Mesh{name="SafetyCollision_"+chunkIndex};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            string asset="Assets/Arts/Environment/MountainPass/Collision/SafetyCollision_"+chunkIndex.ToString("00")+".asset";
            if(File.Exists(asset))throw new Exception("Existing collision asset "+asset);
            AssetDatabase.CreateAsset(mesh,asset);
            var item=new GameObject("안전벽_구역_"+chunkIndex.ToString("00"));item.layer=chunk.Key.layer;item.transform.SetParent(collisionRoot,false);item.isStatic=true;
            var collider=item.AddComponent<MeshCollider>();collider.sharedMaterial=chunk.Key.material;collider.sharedMesh=mesh;
            Physics.SyncTransforms();
            foreach(var wall in chunk)
                foreach(var ray in wall.probes)
                {
                    if(!collider.Raycast(ray,out var hit,wall.probeLength*2f))throw new Exception("Collision probe failed in chunk "+chunkIndex);
                    probeCount++;
                }
            chunkIndex++;
        }
        foreach(var wall in walls)
        {
            var item=wall.source.gameObject;
            Object.DestroyImmediate(wall.source);
            if(item.GetComponents<Component>().All(c=>c is Transform||c is MeshFilter) && item.transform.childCount==0)Object.DestroyImmediate(item);
        }
        foreach(var root in roots.Where(r=>r!=null&&SafetyRoots.Contains(r.name)))
            if(root.GetComponentsInChildren<Collider>(true).Length==0 && root.GetComponentsInChildren<MonoBehaviour>(true).Length==0)Object.DestroyImmediate(root);

        int staticRenderers=0;
        string[] fixedRoots={"MountainPassEnvironment","Test_ShrineEntranceWalls","ApproachStoneParapets","ApproachLanterns","ShrinePorchThresholdSteps"};
        foreach(var root in roots.Where(r=>r!=null&&fixedRoots.Contains(r.name)))
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.GetComponentInParent<Animator>()!=null)continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)|StaticEditorFlags.BatchingStatic);
                staticRenderers++;
            }
        foreach(var root in roots.Where(r=>r!=null))
        {
            string group=root.name=="Player"?"20_플레이어":root.name=="JangseungBoss"||root.name=="BossGateEntrance"||root.name=="BossIntroductionRig"?"40_보스":
                root.name=="MountainShrineInteraction"?"30_산신각":root.name=="FanAttackVfx"||root.name=="RollDust"?"50_효과":
                root.GetComponent<Camera>()!=null||root.GetComponent<Light>()!=null?"60_조명·카메라":
                root.name=="MountainPassSystems"||root.name=="BossPatternSettings"||root.name=="FanAttackDamageSettings"||root.name=="FrameRateLimiter"?"00_시스템":
                root.GetComponentsInChildren<Renderer>(true).Length==0&&root.GetComponentsInChildren<Collider>(true).Length>0?"90_충돌":"10_환경";
            root.transform.SetParent(Group(scene,group),true);
            if(root.name.StartsWith("Test_"))root.name=root.name.Substring(5);
        }
        var timing=player.AnimationControl.transform;
        if(timing.name=="Test_AnimationControl")timing.name="플레이어 동작·회복";
        var build=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(scene.path,true)};
        build.AddRange(EditorBuildSettings.scenes.Where(s=>File.Exists(s.path)&&s.path!=scene.path).Select(s=>new EditorBuildSettingsScene(s.path,false)));
        EditorBuildSettings.scenes=build.ToArray();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var result=new{before,after=scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Length),obsoleteRangeObjects,unusedRenderers,wallBoxes=walls.Count,collisionChunks=chunks.Length,collisionProbes=probeCount,staticRenderers,roots=scene.rootCount};
        File.WriteAllText("Assets/Temp/SceneOptimization~/StructureResult.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
    private static Transform Group(Scene scene,string name)
    {
        if(groups.TryGetValue(name,out var existing))return existing;
        var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);groups[name]=root.transform;return root.transform;
    }
    private static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
}
