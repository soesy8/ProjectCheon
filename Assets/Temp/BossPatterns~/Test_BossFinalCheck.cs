using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public static class Test_BossFinalCheck
{
    public static object Main()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity") throw new Exception("테스트 씬 Edit Mode 전용");
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        var boss = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternController>();
        var combat = UnityEngine.Object.FindFirstObjectByType<Test_PlayerAreaCombat>();
        int missingScripts = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)));
        if (missingScripts != 0 || settings == null || boss == null || combat == null) throw new Exception("씬 연결 오류");
        var inspector = UnityEditor.Editor.CreateEditor(boss);
        string inspectorType = inspector.GetType().Name;
        bool inspectorCreated = inspector.CreateInspectorGUI() != null;
        UnityEngine.Object.DestroyImmediate(inspector);
        var texture = (TextureImporter)AssetImporter.GetAtPath("Assets/Test/BossPatterns/Textures/Test_T_BossDust.png");
        var maxSizes = new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL" }.Select(platform => new { platform, maxSize = texture.GetPlatformTextureSettings(platform).maxTextureSize }).ToArray();
        if (texture.maxTextureSize != 1024 || maxSizes.Any(item => item.maxSize != 1024)) throw new Exception("텍스처 Max Size 오류");
        EditorUtility.SetDirty(settings);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            scene = scene.path, missingScripts, inspectorType, inspectorCreated,
            settingsObject = settings.name,
            jump = new { settings.jump.maximumDistance, settings.jump.height, settings.jump.minimumFlight, settings.jump.maximumFlight, settings.jump.windup, settings.jump.recovery, settings.jump.attackRadius, settings.jump.bossCooldown, settings.jump.minionCooldown },
            fall = new { settings.fall.windup, settings.fall.forwardAngle, settings.fall.backwardAngle, settings.fall.fallDuration, settings.fall.holdDuration, settings.fall.riseDuration, settings.fall.recovery, settings.fall.bossCooldown, settings.fall.minionCooldownMinimum, settings.fall.minionCooldownMaximum },
            turn = new { settings.turn.triggerAngle, settings.turn.duration },
            summon = new { settings.summon.requiredJumps, settings.summon.spawnCount, settings.summon.maximumAlive, settings.summon.afterClearCooldown, settings.summon.sideDistance, settings.summon.minionScale, settings.summon.windup, settings.summon.recovery },
            maxSizes
        });
    }
}
