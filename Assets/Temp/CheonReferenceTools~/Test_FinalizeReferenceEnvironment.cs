using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Test_FinalizeReferenceEnvironment
{
    private const string Root="Assets/Test/Cheon/Arts/ReferenceEnvironment/";
    public static object Verify()
    {
        var original=SceneManager.GetActiveScene();bool dirty=original.isDirty;
        var scene=EditorSceneManager.OpenScene(Test_CheonReferenceEnvironment.ScenePath,OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var added=scene.GetRootGameObjects().First(g=>g.name==Test_CheonReferenceEnvironment.MarkerName);
            var instances=added.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();
            int previous=all.Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("/EnvironmentModels/"));
            var materials=all.SelectMany(t=>t.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            int badMaterials=materials.Count(m=>m==null||m.shader==null||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader));
            var meshes=AssetDatabase.FindAssets("t:Mesh",new[]{Root+"Meshes"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Mesh>).ToArray();
            int invalidMeshes=meshes.Count(m=>m==null||m.vertexCount==0||m.triangles.Length==0||m.triangles.Any(i=>i<0||i>=m.vertexCount)||m.vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)));
            int scripts=all.Sum(t=>t.GetComponents<MonoBehaviour>().Length+GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            int cameras=all.Sum(t=>t.GetComponents<Camera>().Length),lights=all.Sum(t=>t.GetComponents<Light>().Length);
            Test_CheonReferenceEnvironment.Apply();
            int after=added.GetComponentsInChildren<Transform>(true).Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            if(previous!=463||instances.Length!=206||instances.Length!=after||badMaterials!=0||invalidMeshes!=0||scripts!=0||cameras!=0||lights!=0||original.isDirty!=dirty)
                throw new InvalidOperationException("최종 환경 씬 검증 실패");
            var catalog=JObject.Parse(File.ReadAllText(Root+"Test_ReferenceModelCatalog.json"));
            var result=new{scene=scene.path,environmentElements=20,prefabs=catalog["models"].Count(),newInstances=instances.Length,preservedOriginalModelInstances=previous,
                meshes=meshes.Length,badMaterials,invalidMeshes,scripts,cameras,lights,repeatedApplyStable=true};
            File.WriteAllText("Assets/Temp/Test_CheonReferenceFinalVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally {SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
    }

    public static object OpenResult()
    {
        var current=SceneManager.GetActiveScene();
        if(current.path==Test_CheonReferenceEnvironment.ScenePath)return new{opened=true,unsavedChangesPreserved=true};
        if(current.isDirty)
        {
            const string snapshot="Assets/Temp/Test_CheonBoundaryAfterReferenceImport.unity";
            if(!EditorSceneManager.SaveScene(current,snapshot,true))throw new IOException("현재 씬 상태 보존 실패");
            // 도구의 임시 오브젝트로 생긴 dirty 상태만 제거한다. 실제 변경이 있으면 원본을 계속 열어 둔다.
            string serialized=File.ReadAllText(snapshot).Replace("\r\n","\n");
            string saved=File.ReadAllText(current.path).Replace("\r\n","\n");
            if(serialized!=saved)
                return new{opened=false,unsavedChangesPreserved=true};
            EditorSceneManager.CloseScene(current,true);
        }
        var result=EditorSceneManager.OpenScene(Test_CheonReferenceEnvironment.ScenePath,OpenSceneMode.Single);
        var root=result.GetRootGameObjects().First(g=>g.name==Test_CheonReferenceEnvironment.MarkerName);
        Selection.activeObject=root;
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,-10),Quaternion.LookRotation(new Vector3(-.9f,-1.2f,1.5f)),50);
        return new{opened=true,unsavedChangesPreserved=true};
    }
}
