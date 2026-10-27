using System;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SteamShapeSceneSetup
{
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MountainPass/MountainPass_Optimized.unity")
            throw new InvalidOperationException("Open the gameplay scene in Edit mode.");
        var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        Undo.RecordObject(settings, "Correct stretched nose steam particles");
        settings.pursuitSlam.steamSize = new Vector2(.45f, .65f);
        EditorUtility.SetDirty(settings); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        return new { saved = true, width = settings.pursuitSlam.steamSize.x, height = settings.pursuitSlam.steamSize.y };
    }
}
