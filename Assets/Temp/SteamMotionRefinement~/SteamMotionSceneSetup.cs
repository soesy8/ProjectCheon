using System;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SteamMotionSceneSetup
{
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MountainPass/MountainPass_Optimized.unity")
            throw new InvalidOperationException("Open the gameplay scene in Edit mode.");
        var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        Undo.RecordObject(settings, "Refine sequential nose steam motion");
        var steam = settings.pursuitSlam;
        steam.steamEmissionDuration = .42f;
        steam.steamDownSpeed = 4.5f;
        steam.steamEndSpeedRatio = .08f;
        steam.steamGrowth = 1.25f;
        EditorUtility.SetDirty(settings); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        return new { saved = true, duration = steam.steamEmissionDuration, downSpeed = steam.steamDownSpeed,
            endSpeedRatio = steam.steamEndSpeedRatio, growth = steam.steamGrowth,
            particleSize = steam.steamSize.ToString() };
    }
}
