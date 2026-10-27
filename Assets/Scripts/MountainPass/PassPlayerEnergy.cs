using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 이동에서 소비를 확정한 뒤 재생한다. 같은 프레임에 소비와 재생이 상쇄되지 않는다.
    [DefaultExecutionOrder(25)]
    public class PassPlayerEnergy : MonoBehaviour
    {
        [SerializeField, InspectorName("최대 도력"), Min(1f)] private float maximum = 100f;
        [SerializeField, InspectorName("회피 도력 소모"), Min(0f)] private float dodgeCost = 25f;
        [SerializeField, InspectorName("달리기 초당 소모"), Min(0f)] private float sprintCostPerSecond = 5f;
        [SerializeField, InspectorName("적을 등지고 달릴 때 초당 소모"), Min(0f)] private float retreatCostPerSecond = 10f;
        [SerializeField, InspectorName("초당 도력 재생"), Min(0f)] private float regenerationPerSecond = 10f;
        [SerializeField, InspectorName("게임 진행 제어기")] private PassGameFlow game;
        private PassPlayerMovement movement;
        private PassHealth health;
        private int consumedFrame = -1;
        public float Current { get; private set; }
        public float Maximum => maximum;
        public float Fraction => Mathf.Clamp01(Current / maximum);
        public bool CanDodge => Current + 0.0001f >= dodgeCost;
        public bool CanSprint => Current > 0.001f;

        public void Configure(PassGameFlow flow) => game = flow;
        private void Awake()
        {
            movement = GetComponent<PassPlayerMovement>();
            health = GetComponent<PassHealth>();
            Restore();
        }
        public bool SpendDodge()
        {
            if (!CanDodge) return false;
            Consume(dodgeCost);
            return true;
        }
        public bool SpendSprint(float seconds, Vector3 direction)
        {
            if (!CanSprint || seconds <= 0f) return false;
            Consume(SprintRate(direction) * seconds);
            return true;
        }
        public float SprintRate(Vector3 direction)
        {
            // 전투 중 살아 있는 적에게서 멀어지는 방향이면 등진 달리기로 처리한다.
            if (game != null && game.InEncounter && game.Boss is PassBossPatternController boss)
            {
                foreach (var enemy in boss.Settings.Enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    Vector3 toward = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up);
                    if (toward.sqrMagnitude > 0.01f && Vector3.Dot(direction.normalized, toward.normalized) < -0.1f)
                        return retreatCostPerSecond;
                }
            }
            return sprintCostPerSecond;
        }
        private void Consume(float amount)
        { Current = Mathf.Max(0f, Current - Mathf.Max(0f, amount)); consumedFrame = Time.frameCount; }
        private void LateUpdate()
        {
            if (game == null || !game.IsRunning || game.IsTransitioning || health == null || health.IsDead ||
                movement == null || movement.AnimationControl.IsPaused || movement.AnimationControl.IsDodging ||
                consumedFrame == Time.frameCount) return;
            Current = Mathf.Min(maximum, Current + regenerationPerSecond * Time.deltaTime);
        }
        public void Restore() { Current = maximum; consumedFrame = -1; }
    }
}
