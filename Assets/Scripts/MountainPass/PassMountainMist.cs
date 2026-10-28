using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(30)]
    public class PassMountainMist : MonoBehaviour
    {
        [InspectorName("게임 진행 제어기")]
        [SerializeField] private PassGameFlow game;
        [InspectorName("등산로 안개")]
        [SerializeField] private PassHeightFog approachFog;
        [InspectorName("후면 안개")]
        [SerializeField] private PassHeightFog rearFog;
        [InspectorName("보스 전투 안개")]
        [SerializeField] private PassHeightFog bossFog;
        [InspectorName("정상 기준 높이 (m)")]
        [SerializeField] private float summitHeight = 27.15f;
        [InspectorName("정상 안개 해제 시작 높이 차이 (m)")]
        [SerializeField, Min(0.1f)] private float summitFadeHeight = 5f;
        [InspectorName("정상 도착 전 안개 해제 높이 여유 (m)")]
        [SerializeField, Min(0f)] private float summitClearLeadHeight = 0.5f;
        [InspectorName("안개 높이 보간 시간 (초)")]
        [SerializeField, Min(0.01f)] private float fogHeightSmoothTime = 0.8f;
        [Tooltip("정상보다 이 높이만큼 위까지 안개를 채워 아래쪽에서 정상 지붕이 보이지 않게 합니다.")]
        [InspectorName("정상 위 안개 추가 높이 (m)")]
        [SerializeField, Min(0f)] private float overheadHeight = 22f;
        [InspectorName("등산 중 안개 농도")]
        [SerializeField, Range(0f, 0.3f)] private float climbingDensity = 0.24f;
        [InspectorName("정상 도착 후 안개 농도")]
        [SerializeField, Range(0f, 0.3f)] private float valleyDensity = 0f;

        [Header("등 위치 기준 안개 해제 구간")]
        [InspectorName("안개가 걷히기 시작하는 위치")]
        [SerializeField] private Transform fadeStartPoint;
        [InspectorName("안개가 완전히 걷히는 위치")]
        [SerializeField] private Transform fadeEndPoint;

        [Header("보스 전투 안개")]
        [InspectorName("보스 전투 안개 농도")]
        [SerializeField, Range(0f, 0.1f)] private float bossDensity = 0.022f;
        [InspectorName("보스 안개 생성 시간 (초)")]
        [SerializeField, Min(0.1f)] private float bossFadeSeconds = 9f;
        public float SummitClarity { get; private set; }
        public bool IsAtSummit { get; private set; }
        public float BossMistAmount { get; private set; }
        public PassHeightFog ApproachFog => approachFog;
        public PassHeightFog RearFog => rearFog;
        public PassHeightFog BossFog => bossFog;
        // 공간 안개의 남은 농도를 0~1 범위로 조회한다.
        public float ClimbingMistAmount => isActiveAndEnabled && approachFog != null && approachFog.isActiveAndEnabled
            ? Mathf.InverseLerp(valleyDensity, climbingDensity, approachFog.Density) : 0f;
        private float fogHeightVelocity, displayedFogHeight, previousPlayerHeight;
        private bool hasFogHeight;

        public void Configure(PassGameFlow flow, PassHeightFog approach, PassHeightFog rear, PassHeightFog arena)
        { game = flow; approachFog = approach; rearFog = rear; bossFog = arena; }
        private void Update()
        {
            if (game == null || game.Player == null || approachFog == null) return;
            float height = game.Player.position.y;
            SummitClarity = CalculateClarity(game.Player.position);
            IsAtSummit = SummitClarity >= 1f;
            // 정상 지붕보다 높은 상한을 유지해 초반에 위쪽으로 시야가 새지 않게 한다.
            Vector3 position = approachFog.transform.position;
            float targetHeight = Mathf.Max(height, summitHeight) + overheadHeight;
            if (!hasFogHeight || Mathf.Abs(height - previousPlayerHeight) > 5f)
            { displayedFogHeight = targetHeight; fogHeightVelocity = 0f; hasFogHeight = true; }
            else displayedFogHeight = Mathf.SmoothDamp(displayedFogHeight, targetHeight, ref fogHeightVelocity, fogHeightSmoothTime);
            previousPlayerHeight = height;
            position.y = displayedFogHeight;
            approachFog.transform.position = position;
            // 같은 위치에서는 같은 농도를 유지하고, 내려오면 그 위치의 농도로 다시 채운다.
            float density = Mathf.Lerp(climbingDensity, valleyDensity, SummitClarity);
            approachFog.SetDensity(IsAtSummit ? 0f : density);
            // 기존 공간 안개는 갈라지지 않고 농도만 옅어진다. 남아 있는 열림 값도 해제한다.
            approachFog.SetSidewaysOpening(Vector3.zero, Vector3.right, 0f, 4f);
            if (bossFog == null) return;
            if (game.State == PassGameFlow.RunState.Cinematic || !game.InEncounter) BossMistAmount = 0f;
            else BossMistAmount = Mathf.MoveTowards(BossMistAmount, 1f, Time.deltaTime / bossFadeSeconds);
            bossFog.SetDensity(bossDensity * Mathf.SmoothStep(0f, 1f, BossMistAmount));
        }

        private float CalculateClarity(Vector3 playerPosition)
        {
            if (fadeStartPoint != null && fadeEndPoint != null)
            {
                Vector3 direction = fadeEndPoint.position - fadeStartPoint.position;
                // 등 밑동의 높이나 점프 높이가 아니라 계단을 따라 이동한 위치를 기준으로 한다.
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    float progress = Vector3.Dot(playerPosition - fadeStartPoint.position, direction) / direction.sqrMagnitude;
                    return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
                }
            }

            // 다른 씬에서 위치 기준을 지정하지 않았을 때는 기존 높이 설정을 사용한다.
            float fadeStart = summitHeight - summitFadeHeight;
            float fadeEnd = Mathf.Max(fadeStart + 0.1f, summitHeight - summitClearLeadHeight);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, fadeEnd, playerPosition.y));
        }

        private void OnDisable()
        {
            if (approachFog == null) return;
            approachFog.SetSidewaysOpening(Vector3.zero, Vector3.right, 0f, 4f);
            approachFog.ApplySettings();
            SummitClarity = 0f;
            IsAtSummit = false;
            hasFogHeight = false;
        }
    }
}
