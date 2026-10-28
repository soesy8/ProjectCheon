using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Cheon.Prototype.MountainPass;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(-80)]
    public class PassBossPatternSettings : MonoBehaviour
    {
        [Serializable]
        public class SelectionSettings
        {
            [Tooltip("아직 사용하지 않았거나 사용 시각이 같은 공격끼리 비교할 때 적용하는 거리 기준입니다.")]
            [InspectorName("근거리 판단 거리 (m)"), Min(.1f)] public float closeRange = 5f;
            [InspectorName("같은 일반 공격 최대 연속 횟수"), Min(1)] public int maximumConsecutive = 2;
            [Tooltip("다른 공격이 전부 불가능하면 이 시간 뒤 연속 사용 제한을 풀어 멈춰 있는 상황을 피합니다.")]
            [InspectorName("다른 공격을 기다리는 최대 시간 (초)"), Min(.1f)] public float repeatFallbackWait = 2f;
        }

        [Serializable]
        public class JumpSettings
        {
            [InspectorName("보스 쿨타임 (종료 후)"), Min(0)] public float bossCooldown = 3f;
            [InspectorName("미니 쿨타임 (종료 후)"), Min(0)] public float minionCooldown = 1.5f;
            [InspectorName("최대 이동 거리"), Min(.1f)] public float maximumDistance = 10f;
            [InspectorName("점프 높이"), Min(0)] public float height = 5f;
            [InspectorName("최소 체공 시간"), Min(.01f)] public float minimumFlight = .45f;
            [InspectorName("최대 체공 시간"), Min(.01f)] public float maximumFlight = .9f;
            [InspectorName("선딜레이"), Min(0)] public float windup = 1f;
            [InspectorName("후딜레이"), Min(0)] public float recovery = 1.5f;
            [InspectorName("착지 공격 반경"), Min(.1f)] public float attackRadius = 2.5f;
            [InspectorName("플레이어 주변 최소 간격"), Min(.1f)] public float offsetMinimum = 1.75f;
            [InspectorName("플레이어 주변 최대 간격"), Min(.1f)] public float offsetMaximum = 3f;
        }

        [Serializable]
        public class FallSettings
        {
            [InspectorName("보스 쿨타임 최소 (종료 후)"), Min(0)] public float bossCooldownMinimum = 4f;
            [InspectorName("보스 쿨타임 최대 (종료 후)"), Min(0)] public float bossCooldownMaximum = 6f;
            [InspectorName("미니 쿨타임 최소"), Min(0)] public float minionCooldownMinimum = 4f;
            [InspectorName("미니 쿨타임 최대"), Min(0)] public float minionCooldownMaximum = 6f;
            [InspectorName("선딜레이 합계"), Min(.01f)] public float windup = 1f;
            [InspectorName("앞으로 기울이기 비율"), Range(.05f, .8f)] public float forwardFraction = .2f;
            [InspectorName("앞으로 기울이기 각도")] public float forwardAngle = 10f;
            [InspectorName("뒤로 기울이기 각도")] public float backwardAngle = 30f;
            [InspectorName("넘어지는 시간 / 타격 활성 시간"), Min(.01f)] public float fallDuration = .25f;
            [InspectorName("피격 시 최소 옆 밀림 거리"), Min(0)]
            [Tooltip("몸체 안에 남지 않도록 필요한 충돌 여유만큼은 추가로 밀어냅니다.")]
            public float hitSidePushDistance = 1f;
            [InspectorName("넘어진 상태 유지"), Min(0)] public float holdDuration = 2f;
            [InspectorName("기상 시간"), Min(.01f)] public float riseDuration = 1f;
            // 누워 있는 2초와 기상 1초를 합쳐 공격 판정 종료 뒤 총 3초를 사용한다.
            [InspectorName("기상 후 후딜레이"), Min(0)] public float recovery = 0f;
            [InspectorName("넘어지는 최종 각도"), Range(60f, 90f)] public float finalAngle = 90f;
        }

        [Serializable]
        public class SummonSettings
        {
            [FormerlySerializedAs("requiredJumps")]
            [InspectorName("첫 소환까지 일반 공격 완료 횟수"), Min(1)] public int requiredNormalAttacks = 3;
            [InspectorName("한 번에 소환할 수"), Min(1)] public int spawnCount = 2;
            [InspectorName("필드 최대 생존 수"), Min(1)] public int maximumAlive = 2;
            [Tooltip("살아 있거나 소멸 연출 중인 장승과 대기 중인 장승을 모두 포함합니다. 풀이 가득 차면 소멸 연출이 끝날 때까지 다음 소환을 기다립니다.")]
            [InspectorName("소환 장승 풀 최대 수"), Min(1)] public int poolCapacity = 4;
            [FormerlySerializedAs("afterClearCooldown")]
            [InspectorName("소환·보충 재사용 대기 (초)"), Min(0)] public float replenishCooldown = 15f;
            [InspectorName("좌우 소환 간격"), Min(.1f)] public float sideDistance = 4f;
            [InspectorName("미니 장승 크기"), Range(.05f, 1f)] public float minionScale = .25f;
            [InspectorName("소환 선딜레이 (임시)"), Min(0)] public float windup = 1f;
            [InspectorName("소환 후딜레이 (임시)"), Min(0)] public float recovery = 1f;
            [InspectorName("소멸 후 미니 최소 잔류 시간"), Min(0)] public float deathLifetime = 1f;
            [InspectorName("미니 장승 모델")] public GameObject visualPrefab;
            [InspectorName("원본 모델 배율")] public Vector3 modelScale = new Vector3(1.5f, 2.25f, 1.5f);
        }

        [Serializable]
        public class CombatSettings
        {
            [InspectorName("보스 체력"), Min(1)] public float bossHealth = 1000f;
            [InspectorName("미니 체력 (임시)"), Min(1)] public float minionHealth = 60f;
            [InspectorName("보스 점프 피해 (임시)"), Min(0)] public float bossJumpDamage = 20f;
            [InspectorName("보스 엎어지기 피해 (임시)"), Min(0)] public float bossFallDamage = 28f;
            [InspectorName("미니 점프 피해 (임시)"), Min(0)] public float minionJumpDamage = 8f;
            [InspectorName("미니 엎어지기 피해 (임시)"), Min(0)] public float minionFallDamage = 12f;
            [InspectorName("적 피격 무적 시간 (임시)"), Min(0)] public float enemyHitRecovery = 1f;
            // 이전 범위 판정 에셋과의 호환용이다. 현재 피해량은 공격별 피해 설정에서만 조절한다.
            [HideInInspector, InspectorName("이전 플레이어 타격 피해 (호환용)")] public Vector3 playerDamage = new Vector3(10, 15, 40);
        }

        [Serializable]
        public class TurnSettings
        {
            [InspectorName("정면에서 벗어난 각도"), Range(1, 180)] public float triggerAngle = 45f;
            [InspectorName("제자리 점프 체공 / 회전 시간"), Min(.01f)] public float duration = .5f;
            [InspectorName("제자리 점프 높이"), Min(0)] public float height = 1f;
            [InspectorName("착지 후 재사용 대기시간"), Min(0)] public float cooldown = 1f;
            [InspectorName("공격 전 최대 연속 사용"), Min(1)] public int maximumConsecutive = 3;
            [InspectorName("전투 시작 대기"), Min(0)] public float openingDelay = 1f;
        }

        [Serializable]
        public class CrackSettings
        {
            [InspectorName("전방 연속 폭발 사용")] public bool enabled = true;
            [InspectorName("앞으로 기울이는 선딜 시간"), Min(.01f)] public float windup = 1.25f;
            [Tooltip("기본 자세로 돌아온 뒤 다음 행동까지 추가로 기다리는 시간입니다.")]
            [InspectorName("자세 복귀 후 후딜레이"), Min(0)] public float recovery = .45f;
            [InspectorName("종료 후 쿨타임"), Min(0)] public float cooldown = 8f;
            [InspectorName("연속 폭발 지점 수"), Min(1)] public int stepCount = 5;
            [InspectorName("폭발 지점 간격"), Min(.1f)] public float stepDistance = 2f;
            [InspectorName("폭발 시간 간격"), Min(.01f)] public float stepInterval = .15f;
            [InspectorName("각 지점 공격 반경"), Min(.1f)] public float radius = 1.2f;
            [InspectorName("각 지점 활성 시간"), Min(.01f)] public float activeDuration = .25f;
            [InspectorName("피해량 (한 패턴에 1회)"), Min(0)] public float damage = 18f;
            [InspectorName("전조 앞으로 기울기"), Range(0, 45)] public float windupTilt = 20f;
            [InspectorName("공격 뒤로 젖히는 각도"), Range(0, 60)] public float strikeTilt = 30f;
            [Tooltip("앞으로 기울이는 선딜이 끝나면 폭발과 동시에 뒤로 젖히기 시작합니다.")]
            [InspectorName("공격과 함께 뒤로 젖히는 시간"), Min(.01f)] public float recoilDuration = .25f;
            [InspectorName("뒤로 젖힌 자세 유지 시간"), Min(0)] public float holdDuration = .75f;
            [InspectorName("기본 자세로 돌아오는 시간"), Min(.01f)] public float returnDuration = .35f;
            [InspectorName("폭발 충격파 유지"), Min(.01f)] public float effectLifetime = 1f;
            [InspectorName("원형 충격파 두께"), Min(.01f)] public float lineWidth = .12f;
            [InspectorName("원형 충격파 색상")] public Color color = new Color(.3f, .16f, .055f, .65f);
            [InspectorName("원형 충격파 머티리얼")] public Material material;
            [InspectorName("중앙 큰 먼지 크기 배율"), Min(.1f)] public float centerDustScale = 3f;
            [InspectorName("중앙 큰 먼지 입자 수"), Range(1, 64)] public int centerDustCount = 24;
            [InspectorName("중앙 큰 먼지 분출 속도"), Min(0)] public float centerDustLift = 3f;
            [InspectorName("폭발 먼지 불투명도"), Range(0, 1)] public float explosionOpacity = .72f;
            [InspectorName("주변 중간 먼지 무리 수"), Range(3, 16)] public int mediumDustClusters = 6;
            [InspectorName("주변 중간 먼지 크기 배율"), Min(.1f)] public float mediumDustScale = 1.3f;
            [InspectorName("주변 작은 먼지 무리 수"), Range(3, 24)] public int smallDustClusters = 10;
            [InspectorName("주변 작은 먼지 크기 배율"), Min(.1f)] public float smallDustScale = .6f;
        }

        [Serializable]
        public class PursuitSlamSettings
        {
            [InspectorName("추적 연속 점프 사용")] public bool enabled = true;
            [InspectorName("코 김 선딜 (초)"), Min(.01f)] public float windup = 1f;
            [InspectorName("추적 점프 횟수"), Min(1)] public int trackingJumps = 2;
            [InspectorName("추적 점프 체공 (초)"), Min(.01f)] public float trackingFlight = 1f;
            [InspectorName("추적 점프 높이 (m)"), Min(.1f)] public float trackingHeight = 5f;
            [InspectorName("매 착지 후 대기 (초)"), Min(0)] public float landingPause = .65f;
            [InspectorName("추적 착지 공격 반경 (m)"), Min(.1f)] public float trackingRadius = 4f;
            [InspectorName("추적 착지 피해량"), Min(0)] public float trackingDamage = 20f;
            [InspectorName("강점프 상승 (초)"), Min(.01f)] public float heavyRise = 1.5f;
            [InspectorName("강점프 하강 (초)"), Min(.01f)] public float heavyFall = .75f;
            [InspectorName("강점프 높이 (m)"), Min(.1f)] public float heavyHeight = 12f;
            [InspectorName("강점프 충격파 반경 (m)"), Min(.1f)] public float heavyRadius = 6f;
            [InspectorName("강점프 기본 피해량"), Min(0)] public float heavyDamage = 28f;
            [InspectorName("강점프 몸체 직접 충돌 배율"), Min(1)] public float bodyDamageMultiplier = 1.5f;
            [InspectorName("강점프 착지 후 총 후딜 (초)"), Min(.01f)] public float recovery = 7f;
            [Tooltip("총 후딜 마지막 구간에 일어납니다. 총 후딜에 포함되는 시간입니다.")]
            [InspectorName("후딜에 포함된 기상 시간 (초)"), Min(.01f)] public float standUpDuration = 1f;
            [Header("코에서 아래로 흩어지는 흰 김")]
            [InspectorName("보스 기준 코 위치 (m)")] public Vector3 noseOffset = new Vector3(0, 7.95f, 1.7f);
            [InspectorName("콧김 입자 수"), Range(1, 128)] public int steamParticles = 16;
            [InspectorName("콧김 입자 가로 / 세로 크기 (m)")] public Vector2 steamSize = new Vector2(.45f, .65f);
            [InspectorName("콧김 수명 (초)"), Min(.01f)] public float steamLifetime = .8f;
            [InspectorName("콧김 순차 분출 시간 (초)"), Min(.01f)] public float steamEmissionDuration = .42f;
            [InspectorName("콧김 초기 하강 속도 (m/초)"), Min(0)] public float steamDownSpeed = 4.5f;
            [InspectorName("콧김 마지막 속도 비율"), Range(0f, 1f)] public float steamEndSpeedRatio = .08f;
            [InspectorName("콧김 최종 크기 배율"), Min(1f)] public float steamGrowth = 1.25f;
            [Header("착지 범위를 보여 주는 흙먼지")]
            [InspectorName("외곽 먼지 무리 수"), Range(8, 48)] public int dustClusters = 20;
            [InspectorName("착지 먼지 크기 배율"), Min(.1f)] public float dustSize = 1.8f;
        }

        [Serializable]
        public class DeathSettings
        {
            [InspectorName("노이즈 소멸 머티리얼")] public Material material;
            [InspectorName("가루 입자 머티리얼")] public Material powderMaterial;
            [InspectorName("어두운 보라색으로 물드는 시간"), Min(.01f)] public float tintDuration = 1f;
            [InspectorName("투명해지며 사라지는 시간"), Min(.01f)] public float dissolveDuration = 1f;
            [InspectorName("사라진 후 가루 잔류 시간"), Min(0)] public float particleTail = 1f;
            [InspectorName("노이즈 크기"), Min(.01f)] public float noiseScale = 3f;
            [InspectorName("소멸 경계 부드러움"), Range(.01f, .5f)] public float edgeWidth = .12f;
            [InspectorName("물드는 어두운 보라색")] public Color tint = new Color(.13f, .025f, .21f, 1f);
            [InspectorName("소멸 경계 색상")] public Color edgeColor = new Color(.32f, .08f, .44f, 1f);
            [InspectorName("본체 초당 가루 입자 수"), Range(1, 1024)] public int particlesPerSecond = 240;
            [InspectorName("가루 크기 최소 / 최대")] public Vector2 particleSize = new Vector2(.06f, .14f);
            [InspectorName("가루 수명"), Min(.01f)] public float particleLifetime = 1f;
            [InspectorName("가루 퍼짐 속도"), Min(0)] public float particleSpeed = 1f;
            [InspectorName("가루 흘러가는 방향")] public Vector3 particleDrift = new Vector3(.4f, 1f, .2f);
            [InspectorName("가루 색상")] public Color particleColor = new Color(.58f, .32f, .72f, .95f);
        }

        [Serializable]
        public class LinkedAttackSettings
        {
            [InspectorName("연계 공격 사용")] public bool enabled = true;
            [InspectorName("필요한 잡몹 수"), Min(1)] public int participants = 2;
            [InspectorName("협동 사이 일반 공격 최소 횟수"), Min(1)] public int minimumNormalAttacks = 2;
            [InspectorName("협동 사이 일반 공격 최대 횟수"), Min(1)] public int maximumNormalAttacks = 3;
            [InspectorName("참가자·본체 준비 최대 대기 (초)"), Min(.1f)] public float preparationTimeout = 10f;
            [InspectorName("첫 점프 전 대기"), Min(0)] public float windup = .5f;
            [InspectorName("잡몹 점프 시작 간격"), Min(0)] public float jumpInterval = .5f;
            [InspectorName("마지막 착지 후 본체 타격 판정까지"), Min(0)] public float attackGap = .5f;
            [InspectorName("종료 후 쿨타임"), Min(0)] public float cooldown = 6f;
            [InspectorName("안전한 점프 지점 탐색 제한"), Min(.1f)] public float startTimeout = 3f;
            [InspectorName("순간이동 좌우 간격"), Min(.1f)] public float teleportSideDistance = 4f;
            [InspectorName("순간이동 전체 시간"), Min(.01f)]
            [Tooltip("일반 점프의 최소 체공 시간보다 짧게 자동 제한합니다.")]
            public float teleportDuration = .25f;
            [InspectorName("사라지기 전 작은 점프 높이"), Min(0)] public float teleportHopHeight = .3f;
            [InspectorName("순간이동 중 사라지는 시점"), Range(.05f, .8f)] public float vanishFraction = .3f;
            [InspectorName("순간이동 중 나타나는 시점"), Range(.1f, .95f)] public float appearFraction = .7f;
            [InspectorName("막힌 순간이동 지점 주변 탐색 반경"), Min(0)] public float teleportSearchRadius = 1f;
            [Header("소환·순간이동 공용 흰 연기")]
            [InspectorName("흰 연기 입자 수"), Range(1, 256)] public int smokeParticles = 48;
            [InspectorName("흰 연기 입자 크기 최소 / 최대")] public Vector2 smokeSize = new Vector2(.6f, 1.2f);
            [InspectorName("흰 연기 수명"), Min(.01f)] public float smokeLifetime = 1f;
            [InspectorName("흰 연기 퍼짐 속도"), Min(0)] public float smokeSpeed = 1f;
            [InspectorName("흰 연기 색상")] public Color smokeColor = new Color(.96f, .97f, 1f, .85f);
        }

        [Serializable]
        public class CollisionSettings
        {
            [InspectorName("본체 반지름"), Min(.05f)] public float radius = .99f;
            [InspectorName("본체 전체 길이"), Min(.1f)] public float height = 12.2175f;
            [InspectorName("개체 간 여유 간격"), Min(.01f)] public float clearance = .08f;
            [InspectorName("지면 높이 허용 차이"), Min(.1f)] public float groundTolerance = 2f;
            [InspectorName("회전 경로 검사 간격"), Range(1, 10)] public float sweepDegrees = 3f;
        }

        [Serializable]
        public class DustSettings
        {
            [InspectorName("재활용 먼지 머티리얼")] public Material material;
            [InspectorName("착지 입자 수"), Range(1, 256)] public int landingParticles = 48;
            [InspectorName("엎어지기 입자 수"), Range(1, 256)] public int fallParticles = 72;
            [InspectorName("입자 크기 최소 / 최대")] public Vector2 size = new Vector2(.35f, .9f);
            [InspectorName("입자 수명 최소 / 최대")] public Vector2 lifetime = Vector2.one;
            [InspectorName("불투명도"), Range(0, 1)] public float opacity = .42f;
            [InspectorName("색상")] public Color color = new Color(.62f, .5f, .34f);
        }

        [Header("점프 이동 — 미터 / 초")]
        [InspectorName("점프 패턴 설정")]
        public JumpSettings jump = new JumpSettings();
        [Header("공격 선택 — 오래 안 쓴 공격 우선")]
        [InspectorName("일반 공격 선택 설정")]
        public SelectionSettings selection = new SelectionSettings();
        [Header("전방 엎어지기 — 도 / 초")]
        [InspectorName("엎어지기 패턴 설정")]
        public FallSettings fall = new FallSettings();
        [Header("공격 없는 축 정렬 회전")]
        [InspectorName("방향 정렬 점프 설정")]
        public TurnSettings turn = new TurnSettings();
        [Header("보스 본체 — 전방 원형 연속 폭발")]
        [InspectorName("전방 연속 폭발 설정")]
        public CrackSettings crack = new CrackSettings();
        [Header("코 김 → 추적 점프 → 등으로 강점프 찍기")]
        [InspectorName("추적 연속 점프·강점프 설정")]
        public PursuitSlamSettings pursuitSlam = new PursuitSlamSettings();
        [Header("잡몹 순간이동 → 연속 점프 → 본체 엎어지기 연계")]
        [InspectorName("협동 공격·소환 흰 연기 설정")]
        public LinkedAttackSettings linkedAttack = new LinkedAttackSettings();
        [Header("잡몹 소환")]
        [InspectorName("잡몹 소환 설정")]
        public SummonSettings summon = new SummonSettings();
        [Header("임시 전투 밸런스")]
        [InspectorName("전투 수치")]
        public CombatSettings combat = new CombatSettings();
        [Header("몸체 충돌 / 안전한 착지")]
        [InspectorName("보스 몸체 충돌 설정")]
        public CollisionSettings collision = new CollisionSettings();
        [Header("먼지 VFX")]
        [InspectorName("흙먼지 설정")]
        public DustSettings dust = new DustSettings();
        [Header("본체 / 미니 장승 사망 VFX")]
        [InspectorName("사망 동작·소멸 설정")]
        public DeathSettings death = new DeathSettings();

        public readonly List<PassBossPatternController> Enemies = new List<PassBossPatternController>();
        public PassBossPatternController MainBoss { get; private set; }
        public Vector3 ArenaCenter { get; private set; }

        public void Register(PassBossPatternController enemy)
        {
            if (!Enemies.Contains(enemy)) Enemies.Add(enemy);
            if (!enemy.IsMinion) { MainBoss = enemy; ArenaCenter = enemy.transform.position; }
        }

        private void LateUpdate()
        {
            if (MainBoss == null || !MainBoss.EncounterRunning) return;
            Physics.SyncTransforms();
            // 키네마틱 몸체끼리는 물리 엔진이 밀어내지 않으므로 실제 캡슐의 겹침을 해소한다.
            for (int pass = 0; pass < 12; pass++)
            {
                bool moved = false;
                for (int first = 0; first < Enemies.Count; first++)
                    for (int second = first + 1; second < Enemies.Count; second++)
                    {
                        var a = Enemies[first]; var b = Enemies[second];
                        if (!a.IsAlive || !b.IsAlive || !a.Body.enabled || !b.Body.enabled) continue;
                        if (!a.Body.bounds.Intersects(b.Body.bounds)) continue;
                        if (!Physics.ComputePenetration(a.Body, a.Body.transform.position, a.Body.transform.rotation,
                            b.Body, b.Body.transform.position, b.Body.transform.rotation, out Vector3 direction, out float distance)) continue;
                        direction.y = 0;
                        if (direction.sqrMagnitude < .001f)
                            direction = Vector3.ProjectOnPlane(a.transform.position - b.transform.position, Vector3.up);
                        if (direction.sqrMagnitude < .001f) direction = a.transform.right;
                        Vector3 separation = direction.normalized * (distance + collision.clearance);
                        if (!a.IsMinion) b.DisplaceForCollision(-separation);
                        else if (!b.IsMinion) a.DisplaceForCollision(separation);
                        else { a.DisplaceForCollision(separation * .5f); b.DisplaceForCollision(-separation * .5f); }
                        moved = true;
                    }
                if (!moved) break;
                Physics.SyncTransforms();
            }
            foreach (var enemy in Enemies) if (enemy.IsAlive) enemy.PushPlayerOutsideBody();
        }

        private void OnValidate()
        {
            selection.maximumConsecutive = Mathf.Max(1, selection.maximumConsecutive);
            summon.maximumAlive = Mathf.Max(1, summon.maximumAlive);
            summon.poolCapacity = Mathf.Max(summon.maximumAlive, summon.poolCapacity);
            summon.spawnCount = Mathf.Clamp(summon.spawnCount, 1, summon.maximumAlive);
            linkedAttack.participants = Mathf.Clamp(linkedAttack.participants, 1, summon.maximumAlive);
            linkedAttack.maximumNormalAttacks = Mathf.Max(linkedAttack.minimumNormalAttacks, linkedAttack.maximumNormalAttacks);
            jump.maximumFlight = Mathf.Max(jump.minimumFlight, jump.maximumFlight);
            jump.offsetMaximum = Mathf.Max(jump.offsetMinimum, jump.offsetMaximum);
            fall.bossCooldownMaximum = Mathf.Max(fall.bossCooldownMinimum, fall.bossCooldownMaximum);
            fall.minionCooldownMaximum = Mathf.Max(fall.minionCooldownMinimum, fall.minionCooldownMaximum);
            collision.height = Mathf.Max(collision.radius * 2f, collision.height);
            linkedAttack.appearFraction = Mathf.Max(linkedAttack.vanishFraction + .05f, linkedAttack.appearFraction);
            linkedAttack.smokeSize.y = Mathf.Max(linkedAttack.smokeSize.x, linkedAttack.smokeSize.y);
        }
    }
}
