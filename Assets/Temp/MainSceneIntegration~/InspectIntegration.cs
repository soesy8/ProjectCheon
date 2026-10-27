using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;
using Cheon.Tests.PlayerReactions;
using Cheon.Tests.BossPatterns;

public static class InspectIntegration
{
    public static object Main()
    {
        var paths = new[] { "Assets/Scenes/MountainPass/MountainPass.unity", "Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity", "Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity", "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity" };
        return paths.Select(path => {
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            return new {
                path, dirty=scene.isDirty,
                components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(c=>c!=null && (c is PassAnimationController || c is Test_FanAttackVfx || c is Test_RollDust || c is Test_FanAttackHitDetection || c is Test_FanAttackDamageSettings || c is Test_GourdDrinking || c is Test_GourdAttachment || c is Test_BossPatternSettings || c is PassBossPatternSettings))
                    .Select(c=>new { type=c.GetType().Name, owner=c.gameObject.name, json=EditorJsonUtility.ToJson(c) }).ToArray()
            };
        }).ToArray();
    }
}
