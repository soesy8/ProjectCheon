using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    public class PassStairAtmosphere : MonoBehaviour
    {
        [InspectorName("플레이어")]
        [SerializeField] private Transform player;
        [InspectorName("현세·영계 환경 제어기")]
        [SerializeField] private PassWorldTransition environment;
        [InspectorName("등불 조명 목록")]
        [SerializeField] private Light[] lanterns;
        [InspectorName("계단 시작 높이 (m)")]
        [SerializeField] private float startHeight = 3.48f;
        [InspectorName("계단 정상 높이 (m)")]
        [SerializeField] private float topHeight = 27.145f;
        [InspectorName("계단 아래쪽 밝기 (0~1)")]
        [SerializeField, Range(0.4f, 1f)] private float lowerBrightness = 0.68f;
        [InspectorName("계단 밝아짐 시작 시점 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float brighteningStart = 0.86f;
        public float Brightness { get; private set; } = 1f;
        private float previousProgress = -1f;
        public void Configure(Transform target, PassWorldTransition transition, Light[] lights, float bottom, float top)
        { player = target; environment = transition; lanterns = lights; startHeight = bottom; topHeight = top; }
        private void Update()
        {
            if (player == null || environment == null) return;
            float progress = Mathf.InverseLerp(startHeight, topHeight, player.position.y);
            if (Mathf.Approximately(previousProgress, progress)) return;
            previousProgress = progress;
            // 대부분의 계단에서는 등불이 보이는 밝기를 유지하고, 마지막 구간에서 햇빛으로 풀어 준다.
            float ending = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(brighteningStart, 1f, progress));
            Brightness = Mathf.Lerp(lowerBrightness + 0.05f * progress, 1f, ending);
            environment.SetApproachBrightness(Brightness);
            foreach (var light in lanterns) if (light != null) light.intensity = Mathf.Lerp(2.6f, 1.2f, progress);
        }
        private void OnDisable() { if (environment != null) environment.SetApproachBrightness(1f); }
    }
}
