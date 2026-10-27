using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Prototype.MountainPass.Editor;

public static class Test_SettingsPage
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Read(object value,string name)=>value.GetType().GetField(name,Flags).GetValue(value);
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static async Task<object> Main()
    {
        Check(!EditorApplication.isPlaying,"Edit mode required");
        var window=EditorWindow.GetWindow<PassTuningWindow>();
        typeof(PassTuningWindow).GetMethod("RefreshTargets",Flags).Invoke(window,null);
        typeof(PassTuningWindow).GetField("category",Flags).SetValue(window,2);
        window.position=new Rect(100,100,900,700);window.Show();window.Repaint();
        await Task.Delay(300);
        var sections=(IEnumerable)Read(window,"sections");
        var fields=new List<object>();var categories=new HashSet<int>();int count=0;
        foreach(var section in sections)
        {
            count++;categories.Add((int)Read(section,"category"));
            var sectionFields=((IEnumerable)Read(section,"fields")).Cast<object>().ToArray();fields.AddRange(sectionFields);
            if(((string)Read(section,"title")).StartsWith("1타 ·"))
                Check(sectionFields.Select(f=>((SerializedObject)Read(Read(f,"source"),"serialized")).targetObject).Distinct().Count()==4,"Attack page must join animation, damage, visual, cancellation targets");
        }
        var keys=fields.Select(field=>{
            var serialized=(SerializedObject)Read(Read(field,"source"),"serialized");string path=(string)Read(field,"path");
            Check(serialized.targetObject!=null&&serialized.FindProperty(path)!=null,"Missing editable target "+path);
            return serialized.targetObject.GetInstanceID()+":"+path;
        }).ToArray();
        Check(keys.Distinct().Count()==keys.Length,"Duplicate tuning controls");
        Check(categories.Count==7,"All seven tuning categories must be present");
        var sources=((IDictionary)Read(window,"sources")).Values.Cast<object>().Select(source=>(SerializedObject)Read(source,"serialized")).ToArray();
        var drink=UnityEngine.Object.FindFirstObjectByType<PassGourdDrinking>();
        var profile=UnityEngine.Object.FindFirstObjectByType<PassFanAttackVfx>().Profile;
        var drinkSource=sources.Single(source=>source.targetObject==drink);var vfxSource=sources.Single(source=>source.targetObject==profile);
        float originalHeal=drink.HealAmount,originalRadius=profile.attack1.radius;
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("통합 설정 연결 검증");
        try
        {
            drinkSource.FindProperty("healAmount").floatValue=originalHeal+1;
            vfxSource.FindProperty("attack1.radius").floatValue=originalRadius+.125f;
            typeof(PassTuningWindow).GetMethod("ApplyChanges",Flags).Invoke(window,null);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            Check(drink.HealAmount==originalHeal+1&&profile.attack1.radius==originalRadius+.125f,"Window must edit actual scene and asset values");
            Undo.PerformUndo();Check(drink.HealAmount==originalHeal&&profile.attack1.radius==originalRadius,"Undo must restore both scene and asset");
            Undo.PerformRedo();Check(drink.HealAmount==originalHeal+1&&profile.attack1.radius==originalRadius+.125f,"Redo must restore the edit");
        }
        finally{Undo.RevertAllDownToGroup(group);}
        Check(drink.HealAmount==originalHeal&&profile.attack1.radius==originalRadius,"Verification must preserve original values");
        foreach(var source in sources)source.Update();
        typeof(PassTuningWindow).GetMethod("SaveSettings",Flags).Invoke(window,null);
        Check(EditorUtility.IsDirty(profile)==false&&!SceneManager.GetActiveScene().isDirty,"Save must persist source values");
        var optimized=SceneManager.GetActiveScene();var issues=PassSceneValidation.CollectIssues(optimized);Check(issues.Count==0,string.Join(",",issues));
        var original=EditorSceneManager.OpenScene("Assets/Scenes/MountainPass/MountainPass.unity",OpenSceneMode.Additive);
        try{var originalIssues=PassSceneValidation.CollectIssues(original);Check(originalIssues.Count==0,string.Join(",",originalIssues));EditorSceneManager.MarkSceneDirty(original);EditorSceneManager.SaveScene(original);}
        finally{EditorSceneManager.CloseScene(original,true);}
        var damage=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<PassFanAttackDamageSettings>());
        Check(damage.FindProperty("attack2.windWeight")==null,"Second attack must not expose an unused wind option");
        Check(new SerializedObject(profile).FindProperty("windDesign")==null,"A/B/C selector must be removed");
        var report=new{sections=count,editableFields=fields.Count,uniqueSources=sources.Length,categories=categories.Count,duplicateControls=0,sceneAndAssetUndoRedo=true,saved=true,sceneIssues=issues,heal=drink.HealAmount,oneWindlessSecondAttack=true};
        File.WriteAllText("Assets/Temp/VfxSettingsCleanup~/SettingsPageReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
    }
}
