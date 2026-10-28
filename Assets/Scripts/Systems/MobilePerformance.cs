using UnityEngine;

namespace Cheon.Systems
{
    // 에디터에서도 Mobile 품질을 선택하면 실제 모바일과 같은 경량 설정을 검증할 수 있다.
    public static class MobilePerformance
    {
        private static int previousQuality = -1;
        private static bool mobileQuality;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQualityCache()
        {
            previousQuality = -1;
            mobileQuality = false;
        }

        public static bool UsesMobileQuality
        {
            get
            {
                if (Application.isMobilePlatform) return true;
                int quality = QualitySettings.GetQualityLevel();
                if (previousQuality != quality)
                {
                    string[] names = QualitySettings.names;
                    if (names.Length == 0) return false;
                    previousQuality = quality;
                    mobileQuality = quality >= 0 && quality < names.Length && names[quality] == "Mobile";
                }
                return mobileQuality;
            }
        }
    }
}
