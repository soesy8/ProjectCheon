using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 공격 방향과 공통 연결을 관리하며, 직접 타격과 바람 판정은 파생 컴포넌트가 담당한다.
    [DefaultExecutionOrder(-15)]
    public abstract class PassPlayerCombat : MonoBehaviour
    {
        [InspectorName("플레이어 동작 제어기")]
        [SerializeField] protected PassAnimationController animationControl;
        [InspectorName("보스 제어기")]
        [SerializeField] protected PassBossController boss;
        [InspectorName("게임 진행 제어기")]
        [SerializeField] protected PassGameFlow game;
        [InspectorName("공통 시각 효과 제어기")]
        [SerializeField] protected PassEffects effects;
        [InspectorName("게임 카메라 제어기")]
        [SerializeField] protected PassCamera view;
        [InspectorName("효과음 제어기")]
        [SerializeField] protected PassAudio audioControl;
        [Tooltip("연속 공격이 연결되는 순간에만 적용하는 최대 회전 각도입니다.")]
        [InspectorName("공격 사이 최대 회전 각도")]
        [SerializeField, Range(0f, 45f)] private float maximumComboTurnAngle = 45f;
        [InspectorName("방향 입력 타겟 탐색 거리 (m)")]
        [SerializeField, Min(0.1f)] private float directionalTargetRange = 8f;
        [Tooltip("보스 주시 중 입력 방향을 중심으로 양쪽에서 적을 찾는 각도입니다.")]
        [InspectorName("방향 입력 타겟 탐색 반각")]
        [SerializeField, Range(0f, 90f)] private float directionalTargetHalfAngle = 45f;

        private PassPlayerMovement movement;

        public int HitsLanded { get; protected set; }
        public void Configure(PassAnimationController animation, PassBossController enemy, PassGameFlow flow,
            PassEffects visuals, PassCamera cameraControl, PassAudio sounds)
        {
            if (animationControl != null) animationControl.StateChanging -= FaceNextAttack;
            animationControl = animation; boss = enemy; game = flow; effects = visuals; view = cameraControl; audioControl = sounds;
            if (isActiveAndEnabled && animationControl != null) animationControl.StateChanging += FaceNextAttack;
        }

        protected virtual void OnEnable()
        {
            movement = GetComponent<PassPlayerMovement>();
            if (animationControl != null) animationControl.StateChanging += FaceNextAttack;
        }

        protected virtual void OnDisable()
        {
            if (animationControl != null) animationControl.StateChanging -= FaceNextAttack;
        }

        private void FaceNextAttack(string from, string to, float blendDuration)
        {
            if (!IsAttackState(to)) return;
            Vector3 direction = movement != null ? movement.InputDirection : Vector3.zero;
            if (view != null && view.BossLocked)
            {
                if (direction.sqrMagnitude > 0.01f) direction = FindDirectionalTarget(direction);
                else if (boss != null && boss.isActiveAndEnabled && boss.Health != null && !boss.Health.IsDead)
                    direction = Vector3.ProjectOnPlane(boss.transform.position - transform.position, Vector3.up);
            }
            if (direction.sqrMagnitude <= 0.01f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            // 입력은 타격을 시작할 때만 반영한다. 직전 타격 방향에서 제한해 타수마다 회전이 누적된다.
            transform.rotation = IsAttackState(from) ? Quaternion.RotateTowards(transform.rotation, targetRotation,
                Mathf.Clamp(maximumComboTurnAngle, 0f, 45f)) : targetRotation;
        }

        private Vector3 FindDirectionalTarget(Vector3 inputDirection)
        {
            Vector3 direction = inputDirection;
            float nearestDistanceSquared = directionalTargetRange * directionalTargetRange;
            // 공격 전환 때만 적 목록을 조회해 주변 지형 콜라이더 수와 관계없이 타겟을 선택한다.
            foreach (var enemy in FindObjectsByType<PassBossPatternController>(FindObjectsSortMode.None))
            {
                if (!enemy.isActiveAndEnabled || !enemy.IsAlive || !enemy.Body.enabled) continue;
                Vector3 offset = enemy.transform.position - transform.position;
                if (Mathf.Abs(offset.y) > 3f) continue;
                Vector3 targetDirection = Vector3.ProjectOnPlane(offset, Vector3.up);
                float distanceSquared = targetDirection.sqrMagnitude;
                if (distanceSquared <= 0.01f || distanceSquared >= nearestDistanceSquared ||
                    Vector3.Angle(inputDirection, targetDirection) > directionalTargetHalfAngle) continue;
                nearestDistanceSquared = distanceSquared;
                direction = targetDirection;
            }
            // 입력 방향에 적이 없으면 보스에게 되돌리지 않고 입력한 방향으로 공격한다.
            return direction;
        }

        private static bool IsAttackState(string state) => state == "ATTACK1" || state == "ATTACK2" || state == "ATTACK3";

        protected abstract void LateUpdate();
        public virtual void ResetHits() => HitsLanded = 0;
    }
}
