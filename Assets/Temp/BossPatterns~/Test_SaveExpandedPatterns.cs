using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.BossPatterns;

public static class Test_SaveExpandedPatterns
{
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity") throw new Exception("테스트 씬 Edit Mode 전용");
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        var boss = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternController>();
        var combat = UnityEngine.Object.FindFirstObjectByType<Test_PlayerAreaCombat>();
        if (!boss.enabled || !combat.enabled || scene.GetRootGameObjects().Length != 25) throw new Exception("검증용 실행 상태가 남아 있습니다.");
        int missingScripts = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)));
        if (missingScripts != 0 || settings.crack.material == null) throw new Exception("씬 연결 오류");
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("씬 저장 실패");
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            saved = !scene.isDirty, missingScripts,
            flight = new[] { settings.jump.minimumFlight, settings.jump.maximumFlight },
            settings.fall.fallDuration, settings.fall.hitSidePushDistance, turnDuration = settings.turn.duration, settings.turn.cooldown, settings.turn.maximumConsecutive,
            crack = new { settings.crack.enabled, settings.crack.windup, settings.crack.recovery, settings.crack.cooldown, settings.crack.stepCount, settings.crack.stepDistance, settings.crack.stepInterval, settings.crack.radius, settings.crack.activeDuration, settings.crack.damage },
            linked = new { settings.linkedAttack.enabled, settings.linkedAttack.participants, settings.linkedAttack.windup, settings.linkedAttack.attackGap, settings.linkedAttack.cooldown }
        });
    }
}
