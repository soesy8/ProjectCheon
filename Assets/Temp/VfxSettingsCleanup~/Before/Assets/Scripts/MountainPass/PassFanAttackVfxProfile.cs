using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [CreateAssetMenu(menuName = "천/산길/공격 시각 효과 설정", fileName = "PassFanAttackVfxProfile")]
    public class PassFanAttackVfxProfile : ScriptableObject
    {
        public enum SweepPlane { [InspectorName("가로 베기")] Horizontal, [InspectorName("세로 베기")] Vertical }
        public enum WindDesign
        {
            [InspectorName("A안 · 구름 갈래")] A,
            [InspectorName("B안 · 말려 흐르는 바람")] B,
            [InspectorName("C안 · 청록 구름 소용돌이")] C
        }

        [Serializable]
        public class CloudSpiral
        {
            [InspectorName("구름 베기 머티리얼")]
            public Material slashMaterial;
            [InspectorName("기본 바람 머티리얼")]
            public Material windMaterial;
            [InspectorName("구름 베기 원화 메시")]
            public Mesh slashMesh;
            [InspectorName("바람 원화 메시")]
            [HideInInspector] public Mesh windMesh;
            [InspectorName("구름 베기 크기 배율")]
            [Range(.5f, 1.5f)] public float sweepScale = 1f;
            [InspectorName("구름 베기 불투명도 (0~1)")]
            [Range(0, 1)] public float sweepOpacity = 1f;
            // 바람 크기는 Wind의 판정 범위로 결정한다. 이전 에셋과의 호환을 위해 값을 보존한다.
            [InspectorName("이전 바람 길이 설정 (호환용)")]
            [HideInInspector] public float windLength = 2.7f;
            [InspectorName("구름 바람 불투명도 (0~1)")]
            [Range(0, 1)] public float windOpacity = .95f;
        }

        [Serializable]
        public class Sweep
        {
            [InspectorName("설정 표시 이름")]
            public string label;
            [Header("원본 FBX 기준 표시 프레임")]
            [InspectorName("시작 프레임")]
            [Min(0)] public float startFrame;
            [InspectorName("종료 프레임")]
            [Min(1)] public float endFrame;
            [Header("공격 범위 — 미터 / 전체 각도")]
            [InspectorName("공격 반경 (m)")]
            [Min(.1f)] public float radius = 3f;
            [InspectorName("전체 범위 각도 (도)")]
            [Range(1f, 360f)] public float angle = 35f;
            [InspectorName("공격 범위 안쪽 반경 (m)")]
            [Min(0)] public float innerRadius = .45f;
            [InspectorName("공격 범위 좌우 회전 보정 (도)")]
            public float yawOffset;
            [InspectorName("공격 궤적 방향")]
            public SweepPlane plane;
            [Header("세로 궤적 — 중심 높이 / 시작 각도 / 옆 기울기")]
            [InspectorName("공격 궤적 중심 높이 (m)")]
            public float pivotHeight = 1.45f;
            [Tooltip("90도를 넘기면 머리 위에서 캐릭터 뒤쪽까지 시작 영역이 펼쳐집니다. 뒤쪽만 늘리려면 Angle도 같은 양만큼 늘리세요.")]
            [InspectorName("세로 베기 시작 각도 (도)")]
            public float startPitch = 65;
            [InspectorName("옆 기울기 (도)")]
            public float roll = -18;
            [Header("모션에서 추출한 높이 / 진행률 — 재샘플링 메뉴로 갱신")]
            [InspectorName("모션에서 추출한 궤적 높이 곡선 (m)")]
            public AnimationCurve height = AnimationCurve.Linear(0, 1, 1, 1);
            [InspectorName("공격 궤적 진행 곡선")]
            public AnimationCurve motionProgress = AnimationCurve.Linear(0, 0, 1, 1);
            [InspectorName("공격 궤적 진행 방향 반전")]
            public bool reverse;
            [Header("모양 / 색상 — 재생 중 조절 가능")]
            [InspectorName("궤적 추가 높이 (m)")]
            public float heightOffset;
            [InspectorName("궤적 높이 배율")]
            [Range(0f, 2f)] public float heightMultiplier = 1f;
            [InspectorName("궤적 곡면 높이 (m)")]
            [Range(0f, 1f)] public float archHeight = .18f;
            [InspectorName("궤적 꼬리 길이 (0~1)")]
            [Range(.05f, 1f)] public float tailLength = .62f;
            [InspectorName("잔상 소멸 시간 (초)")]
            [Min(.01f)] public float fadeSeconds = .14f;
            [InspectorName("색상")]
            [ColorUsage(true, true)] public Color color = new Color(.55f, 1f, .88f, .8f);
            [InspectorName("밝기·강도 배율")]
            [Range(0f, 5f)] public float intensity = 1.5f;
            [Header("궤적 면의 외형 — 타격 범위에는 영향 없음")]
            [InspectorName("곡선형 베기 궤적 사용")]
            public bool shapedRibbon;
            [Tooltip("시작부터 끝까지의 폭 배율입니다. 양끝을 낮추면 중간이 넓은 초승달 형태가 됩니다.")]
            [InspectorName("공격 궤적 폭 곡선")]
            public AnimationCurve ribbonWidth = AnimationCurve.Linear(0, 1, 1, 1);
            [InspectorName("궤적 중앙 볼록함")]
            [Min(0)] public float middleBulge;
            [InspectorName("궤적 끝 연장 각도 (도)")]
            [Range(0, 90)] public float endExtension;
            [Tooltip("중심 아래쪽 곡선의 높이 배율입니다. 끝을 늘려도 바닥에 묻히지 않게 조절합니다.")]
            [InspectorName("궤적 아래 곡선 높이 배율")]
            [Range(.1f, 1f)] public float lowerArcHeightScale = 1f;
            [InspectorName("궤적 면 채움 비율 (0~1)")]
            [Range(0, 1)] public float surfaceFill;
            [InspectorName("궤적 끝 톱니 강도")]
            [Range(0, .15f)] public float tipSerration;
            [Header("타격 구간 — 음수이면 궤적 표시 구간과 동일")]
            [InspectorName("타격 판정 시작 프레임 (-1: 표시 구간 사용)")]
            public float hitStartFrame = -1;
            [InspectorName("타격 판정 종료 프레임 (-1: 표시 구간 사용)")]
            public float hitEndFrame = -1;
            [InspectorName("세로 베기 타격 반쪽 너비 (m)")]
            [Min(.05f)] public float verticalHitHalfWidth = .6f;
        }

        [Serializable]
        public class Wind
        {
            [Header("원본 프레임 / 전방 부채꼴")]
            [InspectorName("시작 프레임")]
            public float startFrame = 20;
            [InspectorName("종료 프레임")]
            public float endFrame = 40;
            [InspectorName("공격 반경 (m)")]
            [Min(.1f)] public float radius = 8f;
            [InspectorName("전체 범위 각도 (도)")]
            [Range(1f, 90f)] public float angle = 35f;
            [InspectorName("바람 시작 반경 (m)")]
            [Min(0)] public float startRadius = .5f;
            [Tooltip("폭을 유지하면서 전방 길이만 늘립니다.")]
            [InspectorName("바람 전방 길이 배율")]
            [Min(.1f)] public float forwardScale = 1f;
            [Tooltip("시간 → 이동 거리 비율. 시작 기울기가 크고 끝 기울기가 작으면 점점 느려집니다.")]
            [InspectorName("바람 전진 진행 곡선")]
            public AnimationCurve travelProgress = CreateWindTravelCurve();
            [InspectorName("높이 또는 높이 곡선")]
            [Min(0)] public float height = .65f;
            [Tooltip("끄면 높이를 일정하게 유지하는 평면입니다.")]
            [InspectorName("지면 높이를 따라 표시")]
            public bool conformToGround = true;
            [InspectorName("잔상 소멸 시간 (초)")]
            [Min(.05f)] public float fadeSeconds = .28f;
            [InspectorName("바람 띠 길이 (0~1)")]
            [Range(.1f, 1f)] public float waveLength = .48f;
            [InspectorName("색상")]
            [ColorUsage(true, true)] public Color color = new Color(.56f, .95f, .86f, .58f);
            [InspectorName("밝기·강도 배율")]
            [Range(0f, 5f)] public float intensity = 1.25f;
            [Header("바람결 파티클")]
            [InspectorName("초당 입자 생성 수")]
            [Min(0)] public float particlesPerSecond = 170f;
            [InspectorName("애니메이션 재생 배속")]
            [Min(.1f)] public float speed = 20f;
            [InspectorName("입자 크기 최소·최대 (m)")]
            public Vector2 size = new Vector2(.3f, .65f);
            [Header("A/B안 갈래 설정 — C안 원화는 한 장만 표시")]
            [InspectorName("바람 갈래 수")]
            [Range(3, 5)] public int branchCount = 3;

            public float EvaluateTravel(float time) => Mathf.Clamp01(travelProgress.Evaluate(Mathf.Clamp01(time)));

            public float EvaluateSpeed(float time)
            {
                float before = Mathf.Clamp01(time - .005f);
                float after = Mathf.Clamp01(time + .005f);
                return Mathf.Max(.03f, (EvaluateTravel(after) - EvaluateTravel(before)) / Mathf.Max(.001f, after - before));
            }

            public Vector3 StretchDirection(Vector3 direction) => new Vector3(direction.x, direction.y, direction.z * forwardScale);
        }

        public static AnimationCurve CreateWindTravelCurve() => new AnimationCurve(
            new Keyframe(0, 0, 2.6f, 2.6f), new Keyframe(1, 1, .12f, .12f));

        [Serializable]
        public class Dust
        {
            [InspectorName("초당 입자 생성 수")]
            [Min(0)] public float particlesPerSecond = 95f;
            [InspectorName("입자 크기 최소·최대 (m)")]
            public Vector2 size = new Vector2(.38f, .95f);
            [InspectorName("입자 수명 최소·최대 (초)")]
            public Vector2 lifetime = new Vector2(.35f, .75f);
            [InspectorName("먼지 바깥쪽 속도 (m/초)")]
            [Min(0)] public float outwardSpeed = 1.1f;
            [InspectorName("입자 상승 속도 (m/초)")]
            [Min(0)] public float upwardSpeed = .65f;
            [InspectorName("입자 불투명도 (0~1)")]
            [Range(0f, 1f)] public float opacity = .46f;
            [InspectorName("색상")]
            public Color color = new Color(.62f, .5f, .34f, 1f);
            [InspectorName("지면 위 표시 높이 (m)")]
            [Min(0)] public float groundOffset = .06f;
            [InspectorName("지면 판정 레이어")]
            public LayerMask groundMask = Physics.DefaultRaycastLayers;
            [Header("전방 끝 먼지")]
            [InspectorName("전방 먼지 입자 비율 (0~1)")]
            [Range(0f, 1f)] public float frontShare = .5f;
            [InspectorName("바람 끝 추가 먼지 입자 수")]
            [Min(0)] public int endBurstCount = 20;
            [InspectorName("전방 먼지 분출 속도 (m/초)")]
            [Min(0)] public float frontSpeed = 1.8f;
        }

        [Header("1타 / 2타 / 3타 — 각각 독립 조절")]
        [InspectorName("1타 설정")]
        public Sweep attack1 = new Sweep { label = "1타 · 세로 내려치기", startFrame = 19, endFrame = 28, radius = 2.45f,
            angle = 145, startPitch = 110, innerRadius = .45f, plane = SweepPlane.Vertical, tailLength = 1 };
        [InspectorName("2타 설정")]
        public Sweep attack2 = new Sweep { label = "2타 · 3m / 155°", startFrame = 28, endFrame = 34, angle = 155 };
        [InspectorName("3타 설정")]
        public Sweep attack3 = new Sweep { label = "3타 · 3m / 210°", startFrame = 20, endFrame = 40, angle = 210 };
        [Header("1타 바람 — 기존 3m / 35° 범위, 평면")]
        [InspectorName("1타 바람 설정")]
        public Wind attack1Wind = new Wind { radius = 3, angle = 35, startFrame = 25, endFrame = 38,
            height = .3f, conformToGround = false, forwardScale = 1.3f, particlesPerSecond = 95, size = new Vector2(.2f, .45f), fadeSeconds = .18f };
        [Header("3타 바람 / 주변 흙먼지")]
        [InspectorName("3타 바람 설정")]
        public Wind wind = new Wind();
        [InspectorName("흙먼지 설정")]
        public Dust dust = new Dust();
        [Header("공격 VFX 머티리얼")]
        [InspectorName("기본 베기 궤적 머티리얼")]
        public Material sweepMaterial;
        [Tooltip("1타에만 적용하는 구름 문양입니다. 비워 두면 기존 공용 머티리얼을 사용합니다.")]
        [InspectorName("1타 궤적 머티리얼")]
        public Material attack1SweepMaterial;
        [Tooltip("2타의 가로 베기에 적용하는 구름 문양입니다. 비워 두면 공용 머티리얼을 사용합니다.")]
        [InspectorName("2타 궤적 머티리얼")]
        public Material attack2SweepMaterial;
        [Tooltip("3타의 넓은 회전 궤적에 적용하는 구름 문양입니다. 비워 두면 공용 머티리얼을 사용합니다.")]
        [InspectorName("3타 궤적 머티리얼")]
        public Material attack3SweepMaterial;
        [Header("이펙트 디자인 — A/B/C 선택")]
        [InspectorName("바람 표현 방식")]
        public WindDesign windDesign;
        [Tooltip("A안: 기존 구름 머리와 꼬리 형태입니다.")]
        [InspectorName("기본 바람 머티리얼")]
        public Material windMaterial;
        [Tooltip("B안: 길게 흐르다가 끝에서 말리는 바람 띠입니다.")]
        [InspectorName("말려 흐르는 바람 머티리얼")]
        public Material windMaterialB;
        [InspectorName("기본 바람 입자 머티리얼")]
        public Material windParticleMaterial;
        [InspectorName("말려 흐르는 바람 입자 머티리얼")]
        public Material windParticleMaterialB;
        [InspectorName("흙먼지 머티리얼")]
        public Material dustMaterial;
        [Header("C안 — 구름 궤적 / 소용돌이 바람")]
        [InspectorName("구름 소용돌이 설정")]
        public CloudSpiral cloudSpiral = new CloudSpiral();
        public int Revision { get; private set; }
        [InspectorName("설정 데이터 버전")]
        [HideInInspector] public int layoutVersion;

        public Sweep GetSweep(int attack) => attack == 1 ? attack1 : attack == 2 ? attack2 : attack3;
        public Material GetSweepMaterial(int attack)
        {
            var material = attack == 1 ? attack1SweepMaterial : attack == 2 ? attack2SweepMaterial : attack3SweepMaterial;
            return material != null ? material : sweepMaterial;
        }

        public Wind GetWind(int attack) => attack == 1 ? attack1Wind : attack == 3 ? wind : null;
        public Material GetWindMaterial() => windDesign == WindDesign.B && windMaterialB != null ? windMaterialB : windMaterial;
        public Material GetWindParticleMaterial() => windDesign == WindDesign.B && windParticleMaterialB != null
            ? windParticleMaterialB : windParticleMaterial;

        public void NotifyChanged() => OnValidate();

        private void OnValidate()
        {
            Revision++;
            foreach (var sweep in new[] { attack1, attack2, attack3 })
            {
                sweep.radius = Mathf.Max(.1f, sweep.radius);
                sweep.innerRadius = Mathf.Clamp(sweep.innerRadius, 0, sweep.radius - .01f);
                sweep.endFrame = Mathf.Max(sweep.startFrame + 1, sweep.endFrame);
            }
            foreach (var gust in new[] { attack1Wind, wind })
            {
                gust.radius = Mathf.Max(gust.startRadius + .1f, gust.radius);
                gust.forwardScale = Mathf.Max(.1f, gust.forwardScale);
                gust.branchCount = Mathf.Clamp(gust.branchCount, 3, 5);
                if (gust.travelProgress == null || gust.travelProgress.length < 2) gust.travelProgress = CreateWindTravelCurve();
                gust.size.y = Mathf.Max(gust.size.x, gust.size.y);
                gust.endFrame = Mathf.Max(gust.startFrame + 1, gust.endFrame);
            }
            dust.size.y = Mathf.Max(dust.size.x, dust.size.y);
            dust.lifetime.x = Mathf.Max(.05f, dust.lifetime.x);
            dust.lifetime.y = Mathf.Max(dust.lifetime.x, dust.lifetime.y);
        }
    }
}
