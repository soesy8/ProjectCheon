using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Prototype.MountainPass.Editor;
public static class SaveBalancedScenes
{
    static object Save(Scene scene)
    {
        var settings=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PassBossPatternSettings>(true)).First();
        if(settings.summon.requiredNormalAttacks!=3||settings.summon.replenishCooldown!=15)throw new Exception("Legacy tuning values were not retained");
        if(settings.linkedAttack.minimumNormalAttacks!=2||settings.linkedAttack.maximumNormalAttacks!=3||settings.linkedAttack.preparationTimeout!=10)throw new Exception("Missing coordination settings");
        EditorUtility.SetDirty(settings);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var issues=PassSceneValidation.CollectIssues(scene);if(issues.Count>0)throw new Exception(string.Join(",",issues));
        return new{path=scene.path,issues,settings.summon.requiredNormalAttacks,settings.summon.replenishCooldown};
    }
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var active=SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(active,"Assets/Temp/BossPatternBalance~/Before/LoadedScene.unity",true);
        var current=Save(active);
        var original=EditorSceneManager.OpenScene("Assets/Scenes/MountainPass/MountainPass.unity",OpenSceneMode.Additive);
        object other;try{other=Save(original);}finally{EditorSceneManager.CloseScene(original,true);}
        PassTuningWindow.Open();
        return new{current,other};
    }
}
