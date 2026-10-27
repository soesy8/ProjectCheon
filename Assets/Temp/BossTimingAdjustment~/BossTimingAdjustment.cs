public static class BossTimingAdjustment
{
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/MountainPass/MountainPass_Optimized.unity")
            throw new System.InvalidOperationException("Unexpected active scene");

        var settings = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassBossPatternSettings>();
        UnityEditor.Undo.RecordObject(settings, "Adjust boss pattern timings");
        settings.jump.recovery = 1.5f;
        settings.fall.recovery = 0f;
        settings.crack.windup = 1.25f;
        settings.crack.recoilDuration = .75f;
        settings.crack.recovery = 3f;
        UnityEditor.EditorUtility.SetDirty(settings);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
            throw new System.InvalidOperationException("Scene save failed");

        return new
        {
            scene = scene.path,
            jumpRecovery = settings.jump.recovery,
            fallRecoveryTotal = settings.fall.holdDuration + settings.fall.riseDuration + settings.fall.recovery,
            crackWindupForward = settings.crack.windup,
            crackWindupRecoil = settings.crack.recoilDuration,
            crackWindupTotal = settings.crack.windup + settings.crack.recoilDuration,
            crackRecovery = settings.crack.recovery
        };
    }
}
