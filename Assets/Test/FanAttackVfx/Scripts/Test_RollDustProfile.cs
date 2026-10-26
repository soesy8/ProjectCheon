using UnityEngine;

namespace Cheon.Tests.FanAttackVfx
{
    [CreateAssetMenu(menuName = "Cheon/Test/Roll Dust", fileName = "Test_RollDustProfile")]
    public class Test_RollDustProfile : ScriptableObject
    {
        [Header("원본 구르기 FBX 프레임 — 배속과 무관")]
        [Tooltip("손과 몸이 땅에 닿는 첫 프레임입니다.")]
        [Min(0)] public float landingFrame = 12;
        [Tooltip("발을 딛고 일어나기 시작하면 먼지 발생을 멈춥니다.")]
        [Min(1)] public float endFrame = 23;
        [Header("착지 때 한 번 발생")]
        [Min(0)] public int landingCount = 18;
        [Min(0)] public float landingRadius = .4f;
        [Min(0)] public float landingOutwardSpeed = 1.2f;
        [Header("땅 위에서 구르는 동안")]
        [Min(0)] public float particlesPerSecond = 40;
        [Min(0)] public float particlesPerMeter = 9;
        [Min(0)] public float trailWidth = .22f;
        [Min(0)] public float trailDrift = .35f;
        [Header("공통 모양")]
        public Vector2 size = new Vector2(.23f, .55f);
        public Vector2 lifetime = new Vector2(.3f, .62f);
        [Range(0, 1)] public float opacity = .42f;
        public Color color = new Color(.62f, .5f, .34f, 1);
        [Min(0)] public float upwardSpeed = .45f;
        [Header("지면과 위치")]
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        [Min(0)] public float groundOffset = .04f;
        [Tooltip("캐릭터 루트 아래에 이 거리 이내로 지면이 있어야 발생합니다.")]
        [Min(.02f)] public float groundDistance = .5f;
        [Tooltip("골반 아래 접점의 캐릭터 기준 앞뒤 보정입니다.")]
        public float contactForwardOffset = .15f;
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
