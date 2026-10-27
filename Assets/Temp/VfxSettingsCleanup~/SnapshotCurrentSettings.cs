using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class SnapshotCurrentSettings
{
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var scene=SceneManager.GetActiveScene();
        bool wasDirty=scene.isDirty;
        const string root="Assets/Temp/VfxSettingsCleanup~";
        if(!File.Exists(root+"/CurrentScene.before.unity"))EditorSceneManager.SaveScene(scene,root+"/CurrentScene.before.unity",true);
        if(wasDirty)EditorSceneManager.SaveScene(scene);
        var profiles=AssetDatabase.FindAssets("t:PassFanAttackVfxProfile").Select(AssetDatabase.GUIDToAssetPath).Select(path=>{
            var value=AssetDatabase.LoadAssetAtPath<PassFanAttackVfxProfile>(path);
            string backup=root+"/Before/"+path;Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(path,backup,true);File.Copy(path+".meta",backup+".meta",true);
            return new{path,design=value.windDesign.ToString(),sweep=value.cloudSpiral.slashMaterial!=null?AssetDatabase.GetAssetPath(value.cloudSpiral.slashMaterial):null,wind=value.cloudSpiral.windMaterial!=null?AssetDatabase.GetAssetPath(value.cloudSpiral.windMaterial):null,json=EditorJsonUtility.ToJson(value)};
        }).ToArray();
        if(profiles.Any(p=>p.design!="C"))throw new Exception("An existing profile uses another VFX design");
        foreach(string path in Directory.GetFiles("Assets/Scripts/MountainPass","*.cs",SearchOption.AllDirectories))
        {string backup=root+"/Before/"+path;Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup,true);}
        var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null&&c.GetType().Namespace==typeof(PassGameFlow).Namespace)
            .Select(c=>new{type=c.GetType().Name,owner=c.name,json=EditorJsonUtility.ToJson(c)}).ToArray();
        File.WriteAllText(root+"/CurrentSettings.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{scene=scene.path,wasDirty,profiles,components},Newtonsoft.Json.Formatting.Indented));
        return new{scene=scene.path,preservedUnsavedChanges=wasDirty,profiles=profiles.Select(p=>new{p.path,p.design,p.sweep,p.wind}).ToArray()};
    }
}
