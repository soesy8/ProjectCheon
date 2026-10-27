using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [CreateAssetMenu(menuName = "천/산길/구르기 먼지 설정", fileName = "PassRollDustProfile")]
    public class PassRollDustProfile : ScriptableObject
    {
        [Header("원본 구르기 FBX 프레임 — 배속과 무관")]
        [Tooltip("손과 몸이 땅에 닿는 첫 프레임입니다.")]
        [InspectorName("최초 착지 프레임")]
        [Min(0)] public float landingFrame = 12;
        [Tooltip("발을 딛고 일어나기 시작하면 먼지 발생을 멈춥니다.")]
        [InspectorName("종료 프레임")]
        [Min(1)] public float endFrame = 23;
        [Header("착지 때 한 번 발생")]
        [InspectorName("착지 먼지 입자 수")]
        [Min(0)] public int landingCount = 18;
        [InspectorName("착지 먼지 생성 반경 (m)")]
        [Min(0)] public float landingRadius = .4f;
        [InspectorName("착지 먼지 퍼짐 속도 (m/초)")]
        [Min(0)] public float landingOutwardSpeed = 1.2f;
        [Header("땅 위에서 구르는 동안")]
        [InspectorName("초당 입자 생성 수")]
        [Min(0)] public float particlesPerSecond = 40;
        [InspectorName("이동 1m당 먼지 입자 수")]
        [Min(0)] public float particlesPerMeter = 9;
        [InspectorName("구르기 먼지 생성 폭 (m)")]
        [Min(0)] public float trailWidth = .22f;
        [InspectorName("구르기 먼지 옆 흐름 속도 (m/초)")]
        [Min(0)] public float trailDrift = .35f;
        [Header("공통 모양")]
        [InspectorName("입자 크기 최소·최대 (m)")]
        public Vector2 size = new Vector2(.23f, .55f);
        [InspectorName("입자 수명 최소·최대 (초)")]
        public Vector2 lifetime = new Vector2(.3f, .62f);
        [InspectorName("입자 불투명도 (0~1)")]
        [Range(0, 1)] public float opacity = .42f;
        [InspectorName("색상")]
        public Color color = new Color(.62f, .5f, .34f, 1);
        [InspectorName("입자 상승 속도 (m/초)")]
        [Min(0)] public float upwardSpeed = .45f;
        [Header("지면과 위치")]
        [InspectorName("지면 판정 레이어")]
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        [InspectorName("지면 위 표시 높이 (m)")]
        [Min(0)] public float groundOffset = .04f;
        [Tooltip("캐릭터 루트 아래에 이 거리 이내로 지면이 있어야 발생합니다.")]
        [InspectorName("접지 허용 거리 (m)")]
        [Min(.02f)] public float groundDistance = .5f;
        [Tooltip("골반 아래 접점의 캐릭터 기준 앞뒤 보정입니다.")]
        [InspectorName("골반 접점 앞뒤 보정 (m)")]
        public float contactForwardOffset = .15f;
        [InspectorName("흙먼지 머티리얼")]
        public Material dustMaterial;

        private void OnValidate()
        {
            endFrame = Mathf.Max(landingFrame + 1, endFrame);
            size.x = Mathf.Max(.01f, size.x);
            size.y = Mathf.Max(size.x, size.y);
            lifetime.x = Mathf.Max(.05f, lifetime.x);
            lifetime.y = Mathf.Max(lifetime.x, lifetime.y);
        }
    }
}
