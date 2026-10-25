using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(30)]
    public class PassMountainMist : MonoBehaviour
    {
        [SerializeField] private PassGameFlow game;
        [SerializeField] private PassHeightFog approachFog;
        [SerializeField] private PassHeightFog rearFog;
        [SerializeField] private PassHeightFog bossFog;
        [SerializeField] private float summitHeight = 27.15f;
        [SerializeField, Min(0.1f)] private float summitFadeHeight = 7f;
        [SerializeField, Min(0f)] private float summitClearLeadHeight = 1.5f;
        [SerializeField, Min(0.01f)] private float fogHeightSmoothTime = 0.8f;
        [SerializeField, Min(0f)] private float overheadHeight = 8f;
        [SerializeField] private float remainingFogHeight = 22f;
        [SerializeField, Range(0f, 0.3f)] private float climbingDensity = 0.13f;
        [SerializeField, Range(0f, 0.3f)] private float valleyDensity = 0.11f;
        [SerializeField, Range(0f, 0.1f)] private float bossDensity = 0.022f;
        [SerializeField, Min(0.1f)] private float bossFadeSeconds = 9f;
        public float SummitClarity { get; private set; }
        public float BossMistAmount { get; private set; }
        public PassHeightFog ApproachFog => approachFog;
        public PassHeightFog RearFog => rearFog;
        public PassHeightFog BossFog => bossFog;
        private float fogHeightVelocity, displayedFogHeight, previousPlayerHeight;
        private bool hasFogHeight;

        public void Configure(PassGameFlow flow, PassHeightFog approach, PassHeightFog rear, PassHeightFog arena)
        { game = flow; approachFog = approach; rearFog = rear; bossFog = arena; }
        private void Update()
        {
            if (game == null) return;
            float height = game.Player.position.y;
            float fadeStart = summitHeight - summitFadeHeight;
            float fadeEnd = Mathf.Max(fadeStart + 0.1f, summitHeight - summitClearLeadHeight);
            SummitClarity = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, fadeEnd, height));
            // 정상에서는 안개 상한을 계단 아래로 내려 마루와 보스맵의 시야를 비운다.
            Vector3 position = approachFog.transform.position;
            float targetHeight = Mathf.Lerp(height + overheadHeight, remainingFogHeight, SummitClarity);
            if (!hasFogHeight || Mathf.Abs(height - previousPlayerHeight) > 5f)
            { displayedFogHeight = targetHeight; fogHeightVelocity = 0f; hasFogHeight = true; }
            else displayedFogHeight = Mathf.SmoothDamp(displayedFogHeight, targetHeight, ref fogHeightVelocity, fogHeightSmoothTime);
            previousPlayerHeight = height;
            // 한 단씩 올라갈 때 안개 상한이 튀지 않도록 시간에 따라 부드럽게 따라간다.
            position.y = displayedFogHeight;
            approachFog.transform.position = position;
            approachFog.SetDensity(Mathf.Lerp(climbingDensity, valleyDensity, SummitClarity));
            approachFog.ApplySettings();
            if (game.State == PassGameFlow.RunState.Cinematic || !game.InEncounter) BossMistAmount = 0f;
            else BossMistAmount = Mathf.MoveTowards(BossMistAmount, 1f, Time.deltaTime / bossFadeSeconds);
            bossFog.SetDensity(bossDensity * Mathf.SmoothStep(0f, 1f, BossMistAmount));
            bossFog.ApplySettings();
        }
    }
}
