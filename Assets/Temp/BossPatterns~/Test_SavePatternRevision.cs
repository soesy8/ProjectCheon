using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.BossPatterns;

public static class Test_SavePatternRevision
{
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity") throw new Exception("테스트 씬 Edit Mode 전용");
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        var boss = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternController>();
        var combat = UnityEngine.Object.FindFirstObjectByType<Test_PlayerAreaCombat>();
        if (!boss.enabled || !combat.enabled) throw new Exception("검증용 비활성화 상태가 남아 있습니다.");
        if (scene.GetRootGameObjects().Length != 25) throw new Exception("검증용 오브젝트가 남아 있습니다.");
        int missingScripts = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)));
        if (missingScripts != 0) throw new Exception("누락 스크립트가 있습니다.");
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("씬 저장 실패");
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            saved = !scene.isDirty, missingScripts,
            flight = new[] { settings.jump.minimumFlight, settings.jump.maximumFlight },
            fallDuration = settings.fall.fallDuration,
            bossCooldown = new[] { settings.fall.bossCooldownMinimum, settings.fall.bossCooldownMaximum },
            miniCooldown = new[] { settings.fall.minionCooldownMinimum, settings.fall.minionCooldownMaximum },
            turnDuration = settings.turn.duration, turnHeight = settings.turn.height
        });
    }
}
