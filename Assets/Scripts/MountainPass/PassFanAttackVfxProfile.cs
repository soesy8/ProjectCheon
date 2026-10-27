using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [CreateAssetMenu(menuName = "천/산길/공격 시각 효과 설정", fileName = "PassFanAttackVfxProfile")]
    public class PassFanAttackVfxProfile : ScriptableObject
    {
        public enum SweepPlane { [InspectorName("가로 베기")] Horizontal, [InspectorName("세로 베기")] Vertical }

        [Serializable]
        public class CloudSpiral
        {
            [InspectorName("현재 구름 베기 머티리얼")] public Material slashMaterial;
            [InspectorName("현재 구름 바람 머티리얼")] public Material windMaterial;
            [InspectorName("가로 베기 원화 메시")] public Mesh slashMesh;
            [InspectorName("구름 베기 크기 배율"), Range(.5f, 1.5f)] public float sweepScale = 1f;
            [InspectorName("구름 베기 불투명도"), Range(0f, 1f)] public float sweepOpacity = 1f;
            [InspectorName("구름 바람 불투명도"), Range(0f, 1f)] public float windOpacity = .95f;
        }

        [Serializable]
        public class Sweep
        {
            [Header("표시·판정 프레임")]
            [InspectorName("궤적 표시 시작 프레임"), Min(0f)] public float startFrame;
            [InspectorName("궤적 표시 종료 프레임"), Min(1f)] public float endFrame;
            [InspectorName("타격 시작 프레임 (-1: 표시 구간 사용)")] public float hitStartFrame = -1f;
            [InspectorName("타격 종료 프레임 (-1: 표시 구간 사용)")] public float hitEndFrame = -1f;
            [Header("공격 범위·방향")]
            [InspectorName("공격 반경 (m)"), Min(.1f)] public float radius = 3f;
            [InspectorName("전체 범위 각도 (도)"), Range(1f, 360f)] public float angle = 35f;
            [InspectorName("공격 안쪽 반경 (m)"), Min(0f)] public float innerRadius = .45f;
            [InspectorName("좌우 회전 보정 (도)")] public float yawOffset;
            [InspectorName("베기 방향")] public SweepPlane plane;
            [InspectorName("궤적 진행 방향 반전")] public bool reverse;
            [Header("세로 베기")]
            [InspectorName("궤적 중심 높이 (m)")] public float pivotHeight = 1.45f;
            [InspectorName("세로 타격 시작 각도 (도)")] public float startPitch = 65f;
            [InspectorName("세로 베기 옆 기울기 (도)")] public float roll = -18f;
            [InspectorName("세로 타격 반쪽 너비 (m)"), Min(.05f)] public float verticalHitHalfWidth = .6f;
            [InspectorName("원화 아래 곡선 높이 배율"), Range(.1f, 1f)] public float lowerArcHeightScale = 1f;
            [Header("높이·잔상")]
            [InspectorName("가로 베기 높이 곡선 (m)")] public AnimationCurve height = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            [InspectorName("가로 베기 높이 배율"), Range(0f, 2f)] public float heightMultiplier = 1f;
            [InspectorName("궤적 추가 높이 (m)")] public float heightOffset;
            [InspectorName("궤적 진행 곡선")] public AnimationCurve motionProgress = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            [InspectorName("잔상 소멸 시간 (초)"), Min(.01f)] public float fadeSeconds = .14f;
        }

        [Serializable]
        public class Wind
        {
            [InspectorName("바람 시작 프레임")] public float startFrame = 20f;
            [InspectorName("바람 종료 프레임")] public float endFrame = 40f;
            [InspectorName("바람 도달 반경 (m)"), Min(.1f)] public float radius = 8f;
            [InspectorName("바람 전체 각도 (도)"), Range(1f, 90f)] public float angle = 35f;
            [InspectorName("바람 시작 반경 (m)"), Min(0f)] public float startRadius = .5f;
            [InspectorName("바람 전방 길이 배율"), Min(.1f)] public float forwardScale = 1f;
            [InspectorName("바람 전진 진행 곡선")] public AnimationCurve travelProgress = CreateWindTravelCurve();
            [InspectorName("바람 표시 높이 (m)"), Min(0f)] public float height = .65f;
            [InspectorName("지면 높이를 따라 표시")] public bool conformToGround = true;
            [InspectorName("바람 잔상 소멸 시간 (초)"), Min(.05f)] public float fadeSeconds = .28f;
            [Tooltip("원거리 피해를 판정하는 띠의 길이입니다. 원화 잔상은 소멸 시간으로 조절합니다.")]
            [InspectorName("바람 타격 띠 길이 (0~1)"), Range(.1f, 1f)] public float waveLength = .48f;
            public float EvaluateTravel(float time) => Mathf.Clamp01(travelProgress.Evaluate(Mathf.Clamp01(time)));
            public Vector3 StretchDirection(Vector3 direction) => new Vector3(direction.x, direction.y, direction.z * forwardScale);
        }

        [Serializable]
        public class Dust
        {
            [InspectorName("초당 먼지 입자 수"), Min(0f)] public float particlesPerSecond = 95f;
            [InspectorName("입자 크기 최소·최대 (m)")] public Vector2 size = new Vector2(.38f, .95f);
            [InspectorName("입자 수명 최소·최대 (초)")] public Vector2 lifetime = new Vector2(.35f, .75f);
            [InspectorName("먼지 바깥쪽 속도 (m/초)"), Min(0f)] public float outwardSpeed = 1.1f;
            [InspectorName("먼지 상승 속도 (m/초)"), Min(0f)] public float upwardSpeed = .65f;
            [InspectorName("먼지 불투명도"), Range(0f, 1f)] public float opacity = .46f;
            [InspectorName("먼지 색상")] public Color color = new Color(.62f, .5f, .34f, 1f);
            [InspectorName("지면 위 먼지 높이 (m)"), Min(0f)] public float groundOffset = .06f;
            [InspectorName("지면 판정 레이어")] public LayerMask groundMask = Physics.DefaultRaycastLayers;
            [InspectorName("전방 먼지 입자 비율"), Range(0f, 1f)] public float frontShare = .5f;
            [InspectorName("바람 끝 추가 먼지 수"), Min(0)] public int endBurstCount = 20;
            [InspectorName("전방 먼지 분출 속도 (m/초)"), Min(0f)] public float frontSpeed = 1.8f;
        }

        [InspectorName("1타 범위·궤적")]
        public Sweep attack1 = new Sweep { startFrame = 19f, endFrame = 28f, radius = 2.45f, angle = 145f, startPitch = 110f, plane = SweepPlane.Vertical };
        [InspectorName("2타 범위·궤적")]
        public Sweep attack2 = new Sweep { startFrame = 28f, endFrame = 34f, angle = 155f };
        [InspectorName("3타 범위·궤적")]
        public Sweep attack3 = new Sweep { startFrame = 20f, endFrame = 40f, angle = 210f };
        [InspectorName("1타 바람")]
        public Wind attack1Wind = new Wind { radius = 3f, angle = 35f, startFrame = 25f, endFrame = 38f, height = .3f, conformToGround = false, forwardScale = 1.3f, fadeSeconds = .18f };
        [InspectorName("3타 바람")] public Wind wind = new Wind();
        [InspectorName("공격 흙먼지")] public Dust dust = new Dust();
        [InspectorName("흙먼지 머티리얼")] public Material dustMaterial;
        [InspectorName("구름 원화 표현")] public CloudSpiral cloudSpiral = new CloudSpiral();
        public int Revision { get; private set; }

        public static AnimationCurve CreateWindTravelCurve() => new AnimationCurve(new Keyframe(0f, 0f, 2.6f, 2.6f), new Keyframe(1f, 1f, .12f, .12f));
        public Sweep GetSweep(int attack) => attack == 1 ? attack1 : attack == 2 ? attack2 : attack3;
        public Wind GetWind(int attack) => attack == 1 ? attack1Wind : attack == 3 ? wind : null;
        public void NotifyChanged() => OnValidate();
        private void OnValidate()
        {
            Revision++;
            ValidateSweep(attack1); ValidateSweep(attack2); ValidateSweep(attack3);
            ValidateWind(attack1Wind); ValidateWind(wind);
            dust.size.y = Mathf.Max(dust.size.x, dust.size.y);
            dust.lifetime.x = Mathf.Max(.05f, dust.lifetime.x);
            dust.lifetime.y = Mathf.Max(dust.lifetime.x, dust.lifetime.y);
        }
        private static void ValidateSweep(Sweep sweep)
        {
            sweep.radius = Mathf.Max(.1f, sweep.radius);
            sweep.innerRadius = Mathf.Clamp(sweep.innerRadius, 0f, sweep.radius - .01f);
            sweep.endFrame = Mathf.Max(sweep.startFrame + 1f, sweep.endFrame);
        }
        private static void ValidateWind(Wind gust)
        {
            gust.radius = Mathf.Max(gust.startRadius + .1f, gust.radius);
            gust.forwardScale = Mathf.Max(.1f, gust.forwardScale);
            if (gust.travelProgress == null || gust.travelProgress.length < 2) gust.travelProgress = CreateWindTravelCurve();
            gust.endFrame = Mathf.Max(gust.startFrame + 1f, gust.endFrame);
        }
    }
}
