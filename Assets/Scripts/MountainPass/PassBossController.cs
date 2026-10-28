using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 보스 공통 참조와 생명주기만 담당한다. 공격 수치와 패턴은 패턴 설정에서 조절한다.
    [RequireComponent(typeof(PassHealth))]
    public abstract class PassBossController : MonoBehaviour
    {
        public enum BossState { Dormant, Pursuing, Windup, Wave, Recovering, Defeated }
        [InspectorName("플레이어")]
        [SerializeField] protected PassPlayerMovement player;
        [InspectorName("플레이어 생명력")]
        [SerializeField] protected PassHealth playerHealth;
        [InspectorName("게임 진행 제어기")]
        [SerializeField] protected PassGameFlow game;
        [InspectorName("공통 시각 효과 제어기")]
        [SerializeField] protected PassEffects effects;
        [InspectorName("게임 카메라 제어기")]
        [SerializeField] protected PassCamera view;
        [InspectorName("효과음 제어기")]
        [SerializeField] protected PassAudio audioControl;
        [InspectorName("보스 표시 모델")]
        [SerializeField] protected Transform visual;
        private Vector3 visualPosition;
        private Quaternion visualRotation;
        public PassHealth Health { get; private set; }
        public BossState State { get; protected set; }
        public abstract bool Enraged { get; }
        public abstract int AttacksPerformed { get; }
        public abstract string AttackLabel { get; }

        public void Configure(PassPlayerMovement target, PassHealth vitality, PassGameFlow flow,
            PassEffects visuals, PassCamera cameraControl, PassAudio sounds, Transform model)
        { player = target; playerHealth = vitality; game = flow; effects = visuals; view = cameraControl; audioControl = sounds; visual = model; }
        protected virtual void Awake()
        {
            Health = GetComponent<PassHealth>();
            visualPosition = visual.localPosition; visualRotation = visual.localRotation;
            Health.Died += OnDefeated;
        }
        public virtual void ResetEncounter()
        {
            Health.Restore(); Health.Invulnerable = true;
            RestoreVisualPose();
            State = BossState.Dormant;
        }
        // 등장 Timeline을 중간에 건너뛰어도 기울어진 포즈를 전투에 넘기지 않는다.
        public void RestoreVisualPose() => visual.SetLocalPositionAndRotation(visualPosition, visualRotation);
        public abstract void WakeUp();
        protected abstract void OnDefeated();
        protected virtual void OnDestroy()
        { if (Health != null) Health.Died -= OnDefeated; }
    }
}
