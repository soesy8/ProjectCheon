using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.BossPatterns;
public static class Test_SetupTeleportLink
{
    public static object Main()
    {
        if (Application.isPlaying) throw new Exception("Edit Mode 전용");
        const string path = "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity";
        if (SceneManager.GetActiveScene().path != path)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty) throw new Exception("다른 씬에 저장되지 않은 변경이 있습니다.");
            EditorSceneManager.OpenScene(path);
        }
        var shader = Resources.Load<Shader>("Test_S_WhiteSmoke");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("흰 연기 셰이더 오류");
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        Undo.RecordObject(settings, "순간이동 연계 공격 설정");
        settings.linkedAttack.jumpInterval = .5f; settings.linkedAttack.attackGap = .5f;
        settings.linkedAttack.teleportDuration = .25f;
        EditorUtility.SetDirty(settings); EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
        if (!EditorSceneManager.SaveScene(settings.gameObject.scene)) throw new Exception("테스트 씬 저장 실패");
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            scene = settings.gameObject.scene.path, settings.linkedAttack.jumpInterval, settings.linkedAttack.attackGap,
            settings.linkedAttack.teleportDuration, settings.linkedAttack.teleportSideDistance, settings.linkedAttack.teleportHopHeight,
            settings.linkedAttack.vanishFraction, settings.linkedAttack.appearFraction, shader = shader.name
        });
    }
}
