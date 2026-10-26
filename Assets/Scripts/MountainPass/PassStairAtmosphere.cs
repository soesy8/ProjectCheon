using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    public class PassStairAtmosphere : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private PassWorldTransition environment;
        [SerializeField] private Light[] lanterns;
        [SerializeField] private float startHeight = 3.48f;
        [SerializeField] private float topHeight = 27.145f;
        [SerializeField, Range(0.4f, 1f)] private float lowerBrightness = 0.68f;
        [SerializeField, Range(0f, 1f)] private float brighteningStart = 0.86f;
        public float Brightness { get; private set; } = 1f;
        public void Configure(Transform target, PassWorldTransition transition, Light[] lights, float bottom, float top)
        { player = target; environment = transition; lanterns = lights; startHeight = bottom; topHeight = top; }
        private void Update()
        {
            if (player == null || environment == null) return;
            float progress = Mathf.InverseLerp(startHeight, topHeight, player.position.y);
            // 대부분의 계단에서는 등불이 보이는 밝기를 유지하고, 마지막 구간에서 햇빛으로 풀어 준다.
            float ending = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(brighteningStart, 1f, progress));
            Brightness = Mathf.Lerp(lowerBrightness + 0.05f * progress, 1f, ending);
            environment.SetApproachBrightness(Brightness);
            foreach (var light in lanterns) if (light != null) light.intensity = Mathf.Lerp(2.6f, 1.2f, progress);
        }
        private void OnDisable() { if (environment != null) environment.SetApproachBrightness(1f); }
    }
}
