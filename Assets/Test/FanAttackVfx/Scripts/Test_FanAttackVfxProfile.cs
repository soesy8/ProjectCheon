using System;
using UnityEngine;

namespace Cheon.Tests.FanAttackVfx
{
    [CreateAssetMenu(menuName = "Cheon/Test/Fan Attack VFX", fileName = "Test_FanAttackVfxProfile")]
    public class Test_FanAttackVfxProfile : ScriptableObject
    {
        public enum SweepPlane { Horizontal, Vertical }
        public enum WindDesign
        {
            [InspectorName("A안 · 구름 갈래")] A,
            [InspectorName("B안 · 말려 흐르는 바람")] B,
            [InspectorName("C안 · 청록 구름 소용돌이")] C
        }

        [Serializable]
        public class CloudSpiral
        {
            public Material slashMaterial;
            public Material windMaterial;
            public Mesh slashMesh;
            public Mesh windMesh;
            [Range(.5f, 1.5f)] public float sweepScale = 1f;
            [Range(0, 1)] public float sweepOpacity = 1f;
            [Min(.2f)] public float windLength = 2.7f;
            [Range(0, 1)] public float windOpacity = .95f;
        }

        [Serializable]
        public class Sweep
        {
            public string label;
            [Header("원본 FBX 기준 표시 프레임")]
            [Min(0)] public float startFrame;
            [Min(1)] public float endFrame;
            [Header("공격 범위 — 미터 / 전체 각도")]
            [Min(.1f)] public float radius = 3f;
            [Range(1f, 360f)] public float angle = 35f;
            [Min(0)] public float innerRadius = .45f;
            public float yawOffset;
            public SweepPlane plane;
            [Header("세로 궤적 — 중심 높이 / 시작 각도 / 옆 기울기")]
            public float pivotHeight = 1.45f;
            [Tooltip("90도를 넘기면 머리 위에서 캐릭터 뒤쪽까지 시작 영역이 펼쳐집니다. 뒤쪽만 늘리려면 Angle도 같은 양만큼 늘리세요.")]
            public float startPitch = 65;
            public float roll = -18;
            [Header("모션에서 추출한 높이 / 진행률 — 재샘플링 메뉴로 갱신")]
            public AnimationCurve height = AnimationCurve.Linear(0, 1, 1, 1);
            public AnimationCurve motionProgress = AnimationCurve.Linear(0, 0, 1, 1);
            public bool reverse;
            [Header("모양 / 색상 — 재생 중 조절 가능")]
            public float heightOffset;
            [Range(0f, 2f)] public float heightMultiplier = 1f;
            [Range(0f, 1f)] public float archHeight = .18f;
            [Range(.05f, 1f)] public float tailLength = .62f;
            [Min(.01f)] public float fadeSeconds = .14f;
            [ColorUsage(true, true)] public Color color = new Color(.55f, 1f, .88f, .8f);
            [Range(0f, 5f)] public float intensity = 1.5f;
            [Header("궤적 면의 외형 — 타격 범위에는 영향 없음")]
            public bool shapedRibbon;
            [Tooltip("시작부터 끝까지의 폭 배율입니다. 양끝을 낮추면 중간이 넓은 초승달 형태가 됩니다.")]
            public AnimationCurve ribbonWidth = AnimationCurve.Linear(0, 1, 1, 1);
            [Min(0)] public float middleBulge;
            [Range(0, 90)] public float endExtension;
            [Tooltip("중심 아래쪽 곡선의 높이 배율입니다. 끝을 늘려도 바닥에 묻히지 않게 조절합니다.")]
            [Range(.1f, 1f)] public float lowerArcHeightScale = 1f;
            [Range(0, 1)] public float surfaceFill;
            [Range(0, .15f)] public float tipSerration;
            [Header("타격 구간 — 음수이면 궤적 표시 구간과 동일")]
            public float hitStartFrame = -1;
            public float hitEndFrame = -1;
            [Min(.05f)] public float verticalHitHalfWidth = .6f;
        }

        [Serializable]
        public class Wind
        {
            [Header("원본 프레임 / 전방 부채꼴")]
            public float startFrame = 20;
            public float endFrame = 40;
            [Min(.1f)] public float radius = 8f;
            [Range(1f, 90f)] public float angle = 35f;
            [Min(0)] public float startRadius = .5f;
            [Tooltip("폭을 유지하면서 전방 길이만 늘립니다.")]
            [Min(.1f)] public float forwardScale = 1f;
            [Tooltip("시간 → 이동 거리 비율. 시작 기울기가 크고 끝 기울기가 작으면 점점 느려집니다.")]
            public AnimationCurve travelProgress = CreateWindTravelCurve();
            [Min(0)] public float height = .65f;
            [Tooltip("끄면 높이를 일정하게 유지하는 평면입니다.")]
            public bool conformToGround = true;
            [Min(.05f)] public float fadeSeconds = .28f;
            [Range(.1f, 1f)] public float waveLength = .48f;
            [ColorUsage(true, true)] public Color color = new Color(.56f, .95f, .86f, .58f);
            [Range(0f, 5f)] public float intensity = 1.25f;
            [Header("바람결 파티클")]
            [Min(0)] public float particlesPerSecond = 170f;
            [Min(.1f)] public float speed = 20f;
            public Vector2 size = new Vector2(.3f, .65f);
            [Header("바람 디자인 — A/B/C 공통")]
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
            [Min(0)] public float particlesPerSecond = 95f;
            public Vector2 size = new Vector2(.38f, .95f);
            public Vector2 lifetime = new Vector2(.35f, .75f);
            [Min(0)] public float outwardSpeed = 1.1f;
            [Min(0)] public float upwardSpeed = .65f;
            [Range(0f, 1f)] public float opacity = .46f;
            public Color color = new Color(.62f, .5f, .34f, 1f);
            [Min(0)] public float groundOffset = .06f;
            public LayerMask groundMask = Physics.DefaultRaycastLayers;
            [Header("전방 끝 먼지")]
            [Range(0f, 1f)] public float frontShare = .5f;
            [Min(0)] public int endBurstCount = 20;
            [Min(0)] public float frontSpeed = 1.8f;
        }

        [Header("1타 / 2타 / 3타 — 각각 독립 조절")]
        public Sweep attack1 = new Sweep { label = "1타 · 세로 내려치기", startFrame = 19, endFrame = 28, radius = 2.45f,
            angle = 145, startPitch = 110, innerRadius = .45f, plane = SweepPlane.Vertical, tailLength = 1 };
        public Sweep attack2 = new Sweep { label = "2타 · 3m / 155°", startFrame = 28, endFrame = 34, angle = 155 };
        public Sweep attack3 = new Sweep { label = "3타 · 3m / 210°", startFrame = 20, endFrame = 40, angle = 210 };
        [Header("1타 바람 — 기존 3m / 35° 범위, 평면")]
        public Wind attack1Wind = new Wind { radius = 3, angle = 35, startFrame = 25, endFrame = 38,
            height = .3f, conformToGround = false, forwardScale = 1.3f, particlesPerSecond = 95, size = new Vector2(.2f, .45f), fadeSeconds = .18f };
        [Header("3타 바람 / 주변 흙먼지")]
        public Wind wind = new Wind();
        public Dust dust = new Dust();
        [Header("테스트 전용 머티리얼")]
        public Material sweepMaterial;
        [Tooltip("1타에만 적용하는 구름 문양입니다. 비워 두면 기존 공용 머티리얼을 사용합니다.")]
        public Material attack1SweepMaterial;
        [Tooltip("2타의 가로 베기에 적용하는 구름 문양입니다. 비워 두면 공용 머티리얼을 사용합니다.")]
        public Material attack2SweepMaterial;
        [Tooltip("3타의 넓은 회전 궤적에 적용하는 구름 문양입니다. 비워 두면 공용 머티리얼을 사용합니다.")]
        public Material attack3SweepMaterial;
        [Header("이펙트 디자인 — A/B/C 선택")]
        public WindDesign windDesign;
        [Tooltip("A안: 기존 구름 머리와 꼬리 형태입니다.")]
        public Material windMaterial;
        [Tooltip("B안: 길게 흐르다가 끝에서 말리는 바람 띠입니다.")]
        public Material windMaterialB;
        public Material windParticleMaterial;
        public Material windParticleMaterialB;
        public Material dustMaterial;
        [Header("C안 — 구름 궤적 / 소용돌이 바람")]
        public CloudSpiral cloudSpiral = new CloudSpiral();
        public int Revision { get; private set; }
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
