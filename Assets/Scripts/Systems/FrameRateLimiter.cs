using UnityEngine;

namespace Cheon.Systems
{
    [DisallowMultipleComponent]
    public sealed class FrameRateLimiter : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("플레이 중 적용할 최대 FPS입니다.")]
        [InspectorName("목표 초당 프레임 수")]
        private int targetFrameRate = 60;

        private int previousTargetFrameRate;
        private int previousVSyncCount;
        private bool isLimitApplied;

        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;

            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
            ApplyFrameRateLimit();
            isLimitApplied = true;
        }

        private void OnValidate()
        {
            targetFrameRate = Mathf.Max(1, targetFrameRate);
            if (Application.IsPlaying(gameObject) && isLimitApplied)
                ApplyFrameRateLimit();
        }

        private void OnDisable()
        {
            RestoreFrameRateSettings();
        }

        private void OnApplicationQuit()
        {
            RestoreFrameRateSettings();
        }

        private void RestoreFrameRateSettings()
        {
            if (!isLimitApplied) return;

            // 컴포넌트를 끄거나 플레이를 종료하면 기존 프레임 설정으로 복원한다.
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
            isLimitApplied = false;
        }

        private void ApplyFrameRateLimit()
        {
            // VSync가 켜져 있으면 targetFrameRate보다 우선하므로 함께 해제한다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
        }
    }
}
