using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Prototype.MountainPass.Editor;

public static class FinalizeOptimizedScene
{
    public static object Main()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||!scene.path.EndsWith("MountainPass_Optimized.unity"))throw new Exception("Optimized edit scene required");
        var names=new Dictionary<string,string>{{"MountainPassSystems","게임 진행·화면·효과음"},{"BossPatternSettings","보스 패턴 설정"},{"FanAttackDamageSettings","플레이어 공격 피해 설정"},{"FrameRateLimiter","프레임 제한"},{"MountainShrineInteraction","산신각 기도·체크포인트"}};
        foreach(var transform in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))
            if(names.TryGetValue(transform.name,out var name))transform.name=name;
        int index=0;foreach(var root in scene.GetRootGameObjects().OrderBy(r=>r.name))root.transform.SetSiblingIndex(index++);
        var issues=PassSceneValidation.CollectIssues(scene);
        if(issues.Count>0)throw new Exception(string.Join("\n",issues));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if(Directory.GetFileSystemEntries("Assets/Test").Length!=0)throw new Exception("Test folder not empty");
        return new{scene=scene.path,issues=issues.Count,roots=scene.rootCount,testFolderEmpty=true,build=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray()};
    }
}
