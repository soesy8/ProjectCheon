public static class BossTimingRefinement
{
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (UnityEditor.EditorApplication.isPlaying || scene.path != "Assets/Scenes/MountainPass/MountainPass_Optimized.unity")
            throw new System.InvalidOperationException("The saved gameplay scene must be open in Edit mode.");

        var settings = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassBossPatternSettings>();
        UnityEditor.Undo.RecordObject(settings, "Refine boss pattern timings");
        settings.fall.windup = 1f;
        settings.linkedAttack.windup = .5f;
        settings.crack.windup = 1.25f;
        settings.crack.recoilDuration = .25f;
        settings.crack.holdDuration = .75f;
        settings.crack.returnDuration = .35f;
        settings.crack.recovery = .45f;
        UnityEditor.EditorUtility.SetDirty(settings);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
            throw new System.InvalidOperationException("Scene save failed.");

        return new
        {
            scene = scene.path,
            fallWindup = settings.fall.windup,
            linkedLeadIn = settings.linkedAttack.windup,
            crackForward = settings.crack.windup,
            crackAttackStartsAt = settings.crack.windup,
            crackRecoil = settings.crack.recoilDuration,
            crackHold = settings.crack.holdDuration,
            crackReturn = settings.crack.returnDuration,
            crackRecovery = settings.crack.recovery
        };
    }
}
