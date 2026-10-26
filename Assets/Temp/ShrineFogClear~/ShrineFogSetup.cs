using System;
using System.Linq;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShrineFogSetup
{
    public static object Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 설정합니다.");
        var mist = UnityEngine.Object.FindFirstObjectByType<PassMountainMist>();
        Undo.RecordObject(mist, "산신각에서 계단 안개 완전히 제거");
        var settings = new SerializedObject(mist);
        settings.FindProperty("valleyDensity").floatValue = 0f;
        settings.ApplyModifiedProperties();
        EditorUtility.SetDirty(mist);
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        return new { saved = EditorSceneManager.SaveScene(scene), scene.path };
    }

    public static object State()
    {
        var mist = UnityEngine.Object.FindFirstObjectByType<PassMountainMist>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var renderer = mist.ApproachFog.GetComponentInChildren<MeshRenderer>();
        return new { frame = Time.frameCount, playerHeight = game.Player.position.y, density = mist.ApproachFog.Density, rendererEnabled = renderer.enabled, mist.SummitClarity, globalFog = RenderSettings.fog, globalFogDensity = RenderSettings.fogDensity, fogMode = RenderSettings.fogMode.ToString(), compilationFailed = EditorUtility.scriptCompilationFailed };
    }

    public static object MoveToShrine()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("플레이 모드에서 확인합니다.");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        if (game.State == PassGameFlow.RunState.Intro) game.BeginJourney();
        var movement = game.Player.GetComponent<PassPlayerMovement>();
        movement.InputEnabled = false;
        Vector3 position = game.Shrine.Position + Vector3.up * 0.1f;
        movement.Teleport(position, Quaternion.Euler(0f, 135f, 0f));
        game.View.SnapBehind();
        return State();
    }
}
