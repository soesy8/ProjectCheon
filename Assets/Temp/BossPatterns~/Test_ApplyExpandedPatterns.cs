using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.BossPatterns;

public static class Test_ApplyExpandedPatterns
{
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity") throw new Exception("테스트 씬 Edit Mode 전용");
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        Undo.RecordObject(settings, "지면 균열과 장승 연계 공격 설정");
        settings.jump.minimumFlight = .45f; settings.jump.maximumFlight = .9f;
        settings.fall.fallDuration = .25f;
        settings.turn.duration = .5f; settings.turn.cooldown = 1f; settings.turn.maximumConsecutive = 3;
        const string materialPath = "Assets/Test/BossPatterns/Materials/Test_M_GroundCrack.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new Exception("지면 균열 셰이더를 찾을 수 없습니다.");
            material = new Material(shader) { name = "Test_M_GroundCrack" };
            material.SetColor("_BaseColor", settings.crack.color);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        settings.crack.material = material;
        EditorUtility.SetDirty(settings); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("씬 저장 실패");
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            flight = new[] { settings.jump.minimumFlight, settings.jump.maximumFlight },
            settings.fall.fallDuration, turnDuration = settings.turn.duration, settings.turn.cooldown, settings.turn.maximumConsecutive,
            crack = new { settings.crack.windup, settings.crack.recovery, settings.crack.cooldown, settings.crack.stepCount, settings.crack.stepDistance, settings.crack.stepInterval, settings.crack.radius, settings.crack.damage },
            linked = new { settings.linkedAttack.participants, settings.linkedAttack.windup, settings.linkedAttack.attackGap, settings.linkedAttack.cooldown }
        });
    }
}
