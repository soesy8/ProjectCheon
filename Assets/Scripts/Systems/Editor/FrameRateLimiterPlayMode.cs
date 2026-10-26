using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Cheon.Systems.Editor
{
    [InitializeOnLoad]
    public static class FrameRateLimiterPlayMode
    {
        private const string RestoreSettingsKey = "Cheon.FrameRateLimiter.RestoreSettings";
        private const string PreviousFrameRateKey = "Cheon.FrameRateLimiter.PreviousFrameRate";
        private const string PreviousVSyncKey = "Cheon.FrameRateLimiter.PreviousVSync";

        static FrameRateLimiterPlayMode()
        {
            EditorApplication.playModeStateChanged += RestoreEditorFrameRate;
        }

        private static void RestoreEditorFrameRate(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                bool hasActiveLimiter = Object.FindObjectsByType<FrameRateLimiter>(FindObjectsSortMode.None)
                    .Any(limiter => limiter.isActiveAndEnabled);
                SessionState.SetBool(RestoreSettingsKey, hasActiveLimiter);
                if (!hasActiveLimiter) return;

                // 도메인 리로드를 거쳐도 플레이 시작 전 설정을 보존한다.
                SessionState.SetInt(PreviousFrameRateKey, Application.targetFrameRate);
                SessionState.SetInt(PreviousVSyncKey, QualitySettings.vSyncCount);
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RestoreSettingsKey, false))
            {
                // Unity의 플레이 종료 처리가 끝난 뒤 에디터 설정을 복원한다.
                Application.targetFrameRate = SessionState.GetInt(PreviousFrameRateKey, -1);
                QualitySettings.vSyncCount = SessionState.GetInt(PreviousVSyncKey, 0);
                SessionState.EraseBool(RestoreSettingsKey);
                SessionState.EraseInt(PreviousFrameRateKey);
                SessionState.EraseInt(PreviousVSyncKey);
            }
        }
    }
}
