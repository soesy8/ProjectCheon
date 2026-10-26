using System;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Test_BossPlaybackScene
{
    public static object OpenVfx()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        SessionState.SetString("BossIntegration_PreviousStartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        const string path = "Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity";
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        return path;
    }

    public static object Restore()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        string path = SessionState.GetString("BossIntegration_PreviousStartScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorSceneManager.OpenScene("Assets/Scenes/MountainPass/MountainPass.unity", OpenSceneMode.Single);
        return new { startScene = path, scene = "Assets/Scenes/MountainPass/MountainPass.unity" };
    }
}
