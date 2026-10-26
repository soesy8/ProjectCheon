using System;
using System.Linq;
using Cheon.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FrameRateLimiterSetup
{
    public static object Inspect()
    {
        var limiter = UnityEngine.Object.FindFirstObjectByType<FrameRateLimiter>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        return new
        {
            isPlaying = Application.isPlaying,
            isPlayingObject = Application.IsPlaying(limiter.gameObject),
            willPlay = EditorApplication.isPlayingOrWillChangePlaymode,
            applied = typeof(FrameRateLimiter).GetField("isLimitApplied", flags).GetValue(limiter),
            previous = typeof(FrameRateLimiter).GetField("previousTargetFrameRate", flags).GetValue(limiter),
            target = Application.targetFrameRate
        };
    }

    public static object Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서만 생성합니다.");
        var scene = SceneManager.GetActiveScene();
        var limiter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FrameRateLimiter>(true)).SingleOrDefault();
        if (limiter == null)
        {
            var gameObject = new GameObject("FrameRateLimiter");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            Undo.RegisterCreatedObjectUndo(gameObject, "프레임 제한 오브젝트 생성");
            limiter = Undo.AddComponent<FrameRateLimiter>(gameObject);
        }
        Undo.RecordObject(limiter, "프레임 제한 활성화");
        limiter.enabled = true;
        Selection.activeGameObject = limiter.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("씬을 저장하지 못했습니다.");
        return new { scene.path, limiter.gameObject.name, components = limiter.GetComponents<Component>().Select(component => component.GetType().Name).ToArray(), targetFrameRate = new SerializedObject(limiter).FindProperty("targetFrameRate").intValue };
    }

    public static object VerifyRuntime()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("플레이 모드가 아닙니다.");
        var limiter = UnityEngine.Object.FindFirstObjectByType<FrameRateLimiter>();
        if (limiter == null) throw new InvalidOperationException("프레임 제한 컴포넌트가 없습니다.");
        int enabledRate = Application.targetFrameRate;
        int enabledVSync = QualitySettings.vSyncCount;
        if (enabledRate != 60 || enabledVSync != 0) throw new InvalidOperationException("60 FPS 제한이 적용되지 않았습니다.");
        limiter.enabled = false;
        int disabledRate = Application.targetFrameRate;
        int disabledVSync = QualitySettings.vSyncCount;
        limiter.enabled = true;
        if (Application.targetFrameRate != 60 || QualitySettings.vSyncCount != 0) throw new InvalidOperationException("재활성화 후 제한이 적용되지 않았습니다.");
        return new { enabledRate, enabledVSync, disabledRate, disabledVSync, reenabledRate = Application.targetFrameRate, frame = Time.frameCount, realtime = Time.realtimeSinceStartupAsDouble };
    }
}
