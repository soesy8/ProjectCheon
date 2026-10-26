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
        [SerializeField, Min(0.1f)] private float summitFadeHeight = 5f;
        [SerializeField, Min(0f)] private float summitClearLeadHeight = 0.5f;
        [SerializeField, Min(0.01f)] private float fogHeightSmoothTime = 0.8f;
        [Tooltip("정상보다 이 높이만큼 위까지 안개를 채워 아래쪽에서 정상 지붕이 보이지 않게 합니다.")]
        [SerializeField, Min(0f)] private float overheadHeight = 22f;
        [SerializeField, Range(0f, 0.3f)] private float climbingDensity = 0.24f;
        [SerializeField, Range(0f, 0.3f)] private float valleyDensity = 0f;

        [Header("정상에서 안개가 양옆으로 걷히는 연출")]
        [SerializeField] private Vector3 summitRevealCenter = new Vector3(4f, 27.15f, 38f);
        [Tooltip("마지막 계단에서 정상을 바라보는 월드 방향입니다.")]
        [SerializeField] private Vector3 summitRevealForward = new Vector3(0.77f, 0f, -0.64f);
        [SerializeField, Min(1f)] private float openingHalfWidth = 32f;
        [SerializeField, Min(0.1f)] private float openingFeather = 4f;
        [SerializeField, Min(0.01f)] private float openingSmoothTime = 1.4f;

        [Header("보스 전투 안개")]
        [SerializeField, Range(0f, 0.1f)] private float bossDensity = 0.022f;
        [SerializeField, Min(0.1f)] private float bossFadeSeconds = 9f;
        public float SummitClarity { get; private set; }
        public float BossMistAmount { get; private set; }
        public PassHeightFog ApproachFog => approachFog;
        public PassHeightFog RearFog => rearFog;
        public PassHeightFog BossFog => bossFog;
        private float fogHeightVelocity, displayedFogHeight, previousPlayerHeight;
        private float openingVelocity;
        private bool hasFogHeight;

        public void Configure(PassGameFlow flow, PassHeightFog approach, PassHeightFog rear, PassHeightFog arena)
        { game = flow; approachFog = approach; rearFog = rear; bossFog = arena; }
        private void Update()
        {
            if (game == null || game.Player == null || approachFog == null) return;
            float height = game.Player.position.y;
            float fadeStart = summitHeight - summitFadeHeight;
            float fadeEnd = Mathf.Max(fadeStart + 0.1f, summitHeight - summitClearLeadHeight);
            float targetClarity = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, fadeEnd, height));
            SummitClarity = Mathf.SmoothDamp(SummitClarity, targetClarity, ref openingVelocity, openingSmoothTime);
            // 정상 지붕보다 높은 상한을 유지해 초반에 위쪽으로 시야가 새지 않게 한다.
            Vector3 position = approachFog.transform.position;
            float targetHeight = Mathf.Max(height, summitHeight) + overheadHeight;
            if (!hasFogHeight || Mathf.Abs(height - previousPlayerHeight) > 5f)
            { displayedFogHeight = targetHeight; fogHeightVelocity = 0f; hasFogHeight = true; }
            else displayedFogHeight = Mathf.SmoothDamp(displayedFogHeight, targetHeight, ref fogHeightVelocity, fogHeightSmoothTime);
            previousPlayerHeight = height;
            // 높이 변화와 좌우 이동을 별도로 보간해 계단마다 안개가 튀지 않게 한다.
            position.y = displayedFogHeight;
            approachFog.transform.position = position;
            // 정상에서는 시간 보간이 끝나기를 기다리지 않고 남은 안개까지 완전히 숨긴다.
            float densityClarity = Mathf.Max(SummitClarity, targetClarity);
            float density = Mathf.Lerp(climbingDensity, valleyDensity, densityClarity);
            approachFog.SetDensity(height >= fadeEnd ? 0f : density);
            Vector3 forward = Vector3.ProjectOnPlane(summitRevealForward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.back;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            approachFog.SetSidewaysOpening(summitRevealCenter, right, openingHalfWidth * SummitClarity, openingFeather);
            approachFog.ApplySettings();
            if (bossFog == null) return;
            if (game.State == PassGameFlow.RunState.Cinematic || !game.InEncounter) BossMistAmount = 0f;
            else BossMistAmount = Mathf.MoveTowards(BossMistAmount, 1f, Time.deltaTime / bossFadeSeconds);
            bossFog.SetDensity(bossDensity * Mathf.SmoothStep(0f, 1f, BossMistAmount));
            bossFog.ApplySettings();
        }

        private void OnDisable()
        {
            if (approachFog == null) return;
            approachFog.SetSidewaysOpening(Vector3.zero, Vector3.right, 0f, openingFeather);
            approachFog.ApplySettings();
            SummitClarity = 0f;
            openingVelocity = 0f;
            hasFogHeight = false;
        }
    }
}
