using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Prototype.MountainPass.Editor;
public static class FinalizeBossBalance
{
    static int compared;
    static readonly List<string> changes=new List<string>();
    static void Compare(JToken before,JToken after,string path)
    {
        if(after is JObject obj)
        {
            if(obj["instanceID"]!=null||obj["fileID"]!=null)return;
            foreach(var property in obj.Properties())if(before[property.Name]!=null)Compare(before[property.Name],property.Value,path+"."+property.Name);
        }
        else if(!JToken.DeepEquals(before,after))changes.Add(path);else compared++;
    }
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var scene=SceneManager.GetActiveScene();var settings=UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        var before=JObject.Parse(File.ReadAllText("Assets/Temp/BossPatternAudit~/CurrentSceneSettings.json"))["settings"];
        Compare(before,JObject.Parse(JsonUtility.ToJson(settings)),"settings");
        if(changes.Count>0)throw new Exception("Unrelated tuning changed: "+string.Join(",",changes));
        if(settings.summon.requiredNormalAttacks!=3||settings.summon.replenishCooldown!=15||settings.linkedAttack.preparationTimeout!=10)throw new Exception("Saved tuning mismatch");
        var issues=PassSceneValidation.CollectIssues(scene);if(issues.Count>0)throw new Exception(string.Join(",",issues));
        foreach(string path in new[]{"Assets/Scripts/MountainPass/PassBossPatternController.cs","Assets/Scripts/MountainPass/PassBossPatternSettings.cs","Assets/Scripts/MountainPass/PassBossLinkedAttack.cs","Assets/Scripts/MountainPass/PassBossTeleportVfx.cs","Assets/Scenes/MountainPass/MountainPass.unity","Assets/Scenes/MountainPass/MountainPass_Optimized.unity"})
            if(File.ReadAllText(path+".meta")!=File.ReadAllText("Assets/Temp/BossPatternBalance~/Before/"+path+".meta"))throw new Exception("Meta changed: "+path);
        var window=EditorWindow.GetWindow<PassTuningWindow>();const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(PassTuningWindow).GetMethod("RefreshTargets",flags).Invoke(window,null);typeof(PassTuningWindow).GetField("category",flags).SetValue(window,4);window.Show();window.Repaint();
        var sections=(IEnumerable)typeof(PassTuningWindow).GetField("sections",flags).GetValue(window);var paths=new List<string>();
        foreach(var section in sections)
        {
            if((int)section.GetType().GetField("category").GetValue(section)!=4)continue;
            foreach(var field in (IEnumerable)section.GetType().GetField("fields").GetValue(section))paths.Add((string)field.GetType().GetField("path").GetValue(field));
        }
        if(!paths.Contains("selection")||!paths.Contains("linkedAttack")||!paths.Contains("summon"))throw new Exception("Central tuning controls missing");
        if(scene.isDirty)EditorSceneManager.SaveScene(scene);
        var report=new{scene=scene.path,scene.isDirty,playing=EditorApplication.isPlaying,unrelatedNumericValuesCompared=compared,changedValues=changes,sceneIssues=issues,metaPreserved=true,centralBossControls=true,testFiles=Directory.GetFileSystemEntries("Assets/Test").Length};
        File.WriteAllText("Assets/Temp/BossPatternBalance~/FinalReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
    }
}
