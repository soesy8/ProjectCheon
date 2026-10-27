using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class ApplyVfxCleanup
{
    const string Root="Assets/Temp/VfxSettingsCleanup~";
    static readonly List<string> removedObjects=new List<string>();
    static readonly List<string> retainedValuesChanged=new List<string>();
    static int compared;
    static void Compare(JToken before,JToken after,string path)
    {
        if(after is JObject obj) { foreach(var property in obj.Properties()) if(before[property.Name]!=null) Compare(before[property.Name],property.Value,path+"."+property.Name); return; }
        if(after is JArray array) { if(!(before is JArray original)||original.Count!=array.Count){retainedValuesChanged.Add(path);return;} for(int i=0;i<array.Count;i++)Compare(original[i],array[i],path+"["+i+"]");return; }
        compared++;if(!JToken.DeepEquals(before,after))retainedValuesChanged.Add(path);
    }
    static void Backup(string path)
    {
        string backup=Root+"/Before/"+path;Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if(!File.Exists(backup))File.Copy(path,backup);
        if(File.Exists(path+".meta")&&!File.Exists(backup+".meta"))File.Copy(path+".meta",backup+".meta");
    }
    static void Clean(GameObject root)
    {
        foreach(var vfx in root.GetComponentsInChildren<PassFanAttackVfx>(true))
        {
            using(var serialized=new SerializedObject(vfx))
            {
                var first=serialized.FindProperty("attack1WindSector").objectReferenceValue as PassFanSweepMesh;
                var final=serialized.FindProperty("windSector").objectReferenceValue as PassFanSweepMesh;
                if(first==null||final==null)throw new Exception("Missing active wind geometry");
                foreach(var range in vfx.GetComponentsInChildren<PassFanSweepMesh>(true))
                {
                    if(range!=first&&range!=final){removedObjects.Add(range.gameObject.name);UnityEngine.Object.DestroyImmediate(range.gameObject);continue;}
                    var renderer=range.GetComponent<MeshRenderer>();if(renderer!=null)UnityEngine.Object.DestroyImmediate(renderer);
                    var filter=range.GetComponent<MeshFilter>();if(filter!=null)UnityEngine.Object.DestroyImmediate(filter);
                    EditorUtility.SetDirty(range);
                }
                var dustControl=serialized.FindProperty("particles").objectReferenceValue as PassFanWindParticles;
                using(var dustSerialized=new SerializedObject(dustControl))
                {
                    var activeDust=dustSerialized.FindProperty("groundDust").objectReferenceValue as ParticleSystem;
                    if(activeDust==null)throw new Exception("Missing active dust");
                    foreach(var system in dustControl.GetComponentsInChildren<ParticleSystem>(true))if(system!=activeDust)
                    {removedObjects.Add(system.gameObject.name);UnityEngine.Object.DestroyImmediate(system.gameObject);}
                }
                EditorUtility.SetDirty(dustControl);EditorUtility.SetDirty(vfx);
            }
        }
    }
    static void CleanScene(Scene scene)
    {
        foreach(var root in scene.GetRootGameObjects())Clean(root);
        var prefabRoots=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PassFanAttackVfx>(true))
            .Select(vfx=>PrefabUtility.GetOutermostPrefabInstanceRoot(vfx.gameObject)).Where(root=>root!=null).Distinct().ToArray();
        if(prefabRoots.Length>0)PrefabUtility.RemoveUnusedOverrides(prefabRoots,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var snapshot=JObject.Parse(File.ReadAllText(Root+"/CurrentSettings.json"));
        foreach(var entry in snapshot["profiles"])
        {
            var path=(string)entry["path"];var profile=AssetDatabase.LoadAssetAtPath<PassFanAttackVfxProfile>(path);
            Compare(JObject.Parse((string)entry["json"]),JObject.Parse(EditorJsonUtility.ToJson(profile)),path);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
        if(retainedValuesChanged.Count>0)throw new Exception("Retained values changed: "+string.Join(",",retainedValuesChanged));
        const string prefabPath="Assets/Prefabs/MountainPass/FanAttackVfx.prefab";
        Backup(prefabPath);
        var contents=PrefabUtility.LoadPrefabContents(prefabPath);
        try{Clean(contents);PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(contents);}
        var active=SceneManager.GetActiveScene();CleanScene(active);
        const string originalPath="Assets/Scenes/MountainPass/MountainPass.unity";
        if(active.path!=originalPath)
        {
            Backup(originalPath);var original=EditorSceneManager.OpenScene(originalPath,OpenSceneMode.Additive);
            try{CleanScene(original);}finally{EditorSceneManager.CloseScene(original,true);}
        }
        AssetDatabase.ForceReserializeAssets(AssetDatabase.FindAssets("t:PassFanAttackVfxProfile").Select(AssetDatabase.GUIDToAssetPath));
        // 테스트·작업 백업은 사용 중인 에셋으로 세지 않는다. 다른 실제 에셋이 참조하는 항목은 보존한다.
        const string artRoot="Assets/Arts/Effects/FanAttack/";
        var paths=AssetDatabase.GetAllAssetPaths().Where(path=>path.StartsWith("Assets/")&&!path.StartsWith("Assets/Temp/")&&!path.StartsWith("Assets/Test/")&&!AssetDatabase.IsValidFolder(path)).ToArray();
        var candidates=paths.Where(path=>path.StartsWith(artRoot)).ToArray();
        var used=new HashSet<string>(AssetDatabase.GetDependencies(paths.Where(path=>!path.StartsWith(artRoot)).ToArray(),true));
        var unused=candidates.Where(path=>!used.Contains(path)).ToArray();
        foreach(string path in unused){Backup(path);if(!AssetDatabase.DeleteAsset(path))throw new Exception("Could not remove "+path);}
        var report=new{comparedRetainedValues=compared,changedRetainedValues=retainedValuesChanged,removedObjects,removedUnusedArt=unused,activeScene=active.path};
        File.WriteAllText(Root+"/CleanupReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
