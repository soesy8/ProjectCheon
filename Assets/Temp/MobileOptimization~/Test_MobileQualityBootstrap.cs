using UnityEngine;

public static class Test_MobileQualityBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SelectQuality()
    {
        // 검증 세션에서만 Standalone 기본 품질을 모바일 품질로 전환한다.
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool("Cheon.VerifyMobileQuality", false))
            QualitySettings.SetQualityLevel(0, true);
#endif
    }
}
