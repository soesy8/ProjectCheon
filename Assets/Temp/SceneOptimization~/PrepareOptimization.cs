using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrepareOptimization
{
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        const string original="Assets/Scenes/MountainPass/MountainPass.unity";
        const string copy="Assets/Scenes/MountainPass/MountainPass_Optimized.unity";
        var current=SceneManager.GetActiveScene();
        if(current.isDirty)throw new Exception("Unsaved scene changes");
        var audit=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Temp/SceneOptimization~/AuditBefore.json"));
        if(audit["testReferences"].Any())throw new Exception("Test dependencies must be removed before cleanup");
        string project=Directory.GetParent(Application.dataPath).FullName;
        string test=Path.GetFullPath(Path.Combine(project,"Assets/Test"));
        string expected=Path.GetFullPath(Path.Combine(Application.dataPath,"Test"));
        if(!string.Equals(test,expected,StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected cleanup target");
        string archive=Path.GetFullPath("Assets/Temp/SceneOptimization~/RemovedTests.zip");
        int files=Directory.GetFiles(test,"*",SearchOption.AllDirectories).Length;
        if(!File.Exists(archive) || new FileInfo(archive).Length==0)throw new Exception("Verified archive required");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(copy)!=null)throw new Exception("Optimized scene already exists");
        if(!AssetDatabase.CopyAsset(original,copy))throw new Exception("Scene copy failed");
        var scene=EditorSceneManager.OpenScene(copy,OpenSceneMode.Single);
        const string settingsRoot="Assets/Settings/MountainPass/Optimized";
        if(!AssetDatabase.IsValidFolder(settingsRoot))AssetDatabase.CreateFolder("Assets/Settings/MountainPass","Optimized");
        var mapping=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
        foreach(string dependency in AssetDatabase.GetDependencies(copy,true).Where(p=>p.StartsWith("Assets/Settings/MountainPass/")&&!p.Contains("/Optimized/")))
        {
            var value=AssetDatabase.LoadMainAssetAtPath(dependency);
            if(value==null || !value.GetType().Name.StartsWith("Pass"))continue;
            string destination=settingsRoot+"/"+Path.GetFileName(dependency);
            if(!AssetDatabase.CopyAsset(dependency,destination))throw new Exception("Settings copy failed: "+dependency);
            mapping[value]=AssetDatabase.LoadMainAssetAtPath(destination);
        }
        foreach(var component in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if(component==null)continue;
            var serialized=new SerializedObject(component);var field=serialized.GetIterator();
            while(field.Next(true))
                if(field.propertyType==SerializedPropertyType.ObjectReference && field.objectReferenceValue!=null && mapping.TryGetValue(field.objectReferenceValue,out var replacement))field.objectReferenceValue=replacement;
            if(serialized.ApplyModifiedPropertiesWithoutUndo())
            {EditorUtility.SetDirty(component);PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var removed=new List<string>();
        foreach(string folder in AssetDatabase.GetSubFolders("Assets/Test"))
        {
            string absolute=Path.GetFullPath(Path.Combine(project,folder));
            if(!absolute.StartsWith(test+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Outside test folder: "+absolute);
            if(!AssetDatabase.DeleteAsset(folder))throw new Exception("Cleanup failed: "+folder);
            removed.Add(folder);
        }
        foreach(string file in Directory.GetFiles(test).Where(p=>!p.EndsWith(".meta")))
        {
            string relative="Assets/Test/"+Path.GetFileName(file);
            if(!AssetDatabase.DeleteAsset(relative))throw new Exception("Cleanup failed: "+relative);
            removed.Add(relative);
        }
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(copy);
        return new{scene=copy,settings=mapping.Count,archivedFiles=files,archive="Assets/Temp/SceneOptimization~/RemovedTests.zip",removed};
    }
}
