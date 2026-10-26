using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.BossPatterns;

public static class Test_ApplyPatternRevision
{
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity" || scene.isDirty)
            throw new InvalidOperationException("저장된 테스트 씬의 Edit Mode에서만 실행합니다.");
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        Undo.RecordObject(settings, "보스 패턴 속도 및 추적 변경");
        settings.jump.minimumFlight = .3f;
        settings.jump.maximumFlight = .6f;
        settings.fall.bossCooldownMinimum = settings.fall.minionCooldownMinimum = 4f;
        settings.fall.bossCooldownMaximum = settings.fall.minionCooldownMaximum = 6f;
        settings.fall.fallDuration = .1f;
        settings.turn.duration = .2f;
        settings.turn.height = 1f;
        EditorUtility.SetDirty(settings);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            flight = new[] { settings.jump.minimumFlight, settings.jump.maximumFlight },
            bossFallCooldown = new[] { settings.fall.bossCooldownMinimum, settings.fall.bossCooldownMaximum },
            minionFallCooldown = new[] { settings.fall.minionCooldownMinimum, settings.fall.minionCooldownMaximum },
            settings.fall.fallDuration, turnFlight = settings.turn.duration, turnHeight = settings.turn.height
        });
    }
}
