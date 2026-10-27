using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Prototype.MountainPass.Editor;
public static class FinalizeCleanup
{
    static int compared;
    static readonly List<string> changes=new List<string>();
    static void Compare(JToken old,JToken current,string path)
    {
        if(current is JObject obj)
        {
            if(obj["fileID"]!=null||obj["instanceID"]!=null)return;
            foreach(var property in obj.Properties())if(old[property.Name]!=null)Compare(old[property.Name],property.Value,path+"."+property.Name);
            return;
        }
        if(current is JArray array){if(!(old is JArray original)||original.Count!=array.Count){changes.Add(path);return;}for(int i=0;i<array.Count;i++)Compare(original[i],array[i],path+"["+i+"]");return;}
        compared++;if(!JToken.DeepEquals(old,current))changes.Add(path);
    }
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var snapshot=JObject.Parse(File.ReadAllText("Assets/Temp/VfxSettingsCleanup~/CurrentSettings.json"));
        var scene=SceneManager.GetActiveScene();
        foreach(var component in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null))
        {
            var original=snapshot["components"].FirstOrDefault(item=>(string)item["type"]==component.GetType().Name&&(string)item["owner"]==component.name);
            if(original!=null)Compare(JObject.Parse((string)original["json"]),JObject.Parse(EditorJsonUtility.ToJson(component)),component.GetType().Name+"/"+component.name);
        }
        foreach(var entry in snapshot["profiles"])
        {
            var profile=AssetDatabase.LoadAssetAtPath<PassFanAttackVfxProfile>((string)entry["path"]);
            Compare(JObject.Parse((string)entry["json"]),JObject.Parse(EditorJsonUtility.ToJson(profile)),(string)entry["path"]);
        }
        if(changes.Count>0)throw new Exception("Retained values changed: "+string.Join(",",changes));
        var issues=PassSceneValidation.CollectIssues(scene);if(issues.Count>0)throw new Exception(string.Join(",",issues));
        var removed=JObject.Parse(File.ReadAllText("Assets/Temp/VfxSettingsCleanup~/CleanupReport.json"))["removedUnusedArt"].Values<string>().ToArray();
        var guids=removed.Select(path=>File.ReadAllText("Assets/Temp/VfxSettingsCleanup~/Before/"+path+".meta")).Select(meta=>System.Text.RegularExpressions.Regex.Match(meta,@"guid: ([a-f0-9]+)").Groups[1].Value).ToArray();
        var extensions=new HashSet<string>{".asset",".prefab",".unity",".mat",".controller",".playable",".shadergraph"};
        var stale=AssetDatabase.GetAllAssetPaths().Where(path=>path.StartsWith("Assets/")&&!path.StartsWith("Assets/Temp/")&&extensions.Contains(Path.GetExtension(path))&&File.Exists(path))
            .Where(path=>{string text=File.ReadAllText(path);return guids.Any(guid=>text.Contains(guid));}).ToArray();
        if(stale.Length>0)throw new Exception("Removed asset references: "+string.Join(",",stale));
        PassTuningWindow.Open();
        var report=new{scene=scene.path,scene.isDirty,playing=EditorApplication.isPlaying,retainedNumericValuesCompared=compared,changedValues=changes,sceneIssues=issues,removedAssetReferenceCount=stale.Length,testFiles=Directory.GetFileSystemEntries("Assets/Test").Length};
        File.WriteAllText("Assets/Temp/VfxSettingsCleanup~/FinalReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
    }
}
