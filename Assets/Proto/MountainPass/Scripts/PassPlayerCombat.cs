using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 표시용 부채 범위와 같은 원본 프레임 구간에서 실제 타격을 한 번만 처리한다.
    [DefaultExecutionOrder(-15)]
    public class PassPlayerCombat : MonoBehaviour
    {
        [SerializeField] private PassAnimationController animationControl;
        [SerializeField] private PassBossController boss;
        [SerializeField] private PassGameFlow game;
        [SerializeField] private PassEffects effects;
        [SerializeField] private PassCamera view;
        [SerializeField] private PassAudio audioControl;
        [SerializeField] private Vector3 attackDamage = new Vector3(22f, 28f, 40f);
        [SerializeField] private Vector3 attackReach = new Vector3(4.2f, 4.8f, 5.3f);
        private uint sequence;
        private float previousFrame;
        private bool hit;
        public int HitsLanded { get; private set; }

        public void Configure(PassAnimationController animation, PassBossController enemy, PassGameFlow flow, PassEffects visuals, PassCamera cameraControl, PassAudio sounds)
        { animationControl = animation; boss = enemy; game = flow; effects = visuals; view = cameraControl; audioControl = sounds; }

        private void LateUpdate()
        {
            if (game == null || !game.IsRunning || !game.InEncounter || boss == null || boss.Health.IsDead) return;
            int attack = animationControl.SelectedState == "ATTACK1" ? 1 : animationControl.SelectedState == "ATTACK2" ? 2 : animationControl.SelectedState == "ATTACK3" ? 3 : 0;
            if (attack == 0) return;
            if (view.BossLocked)
            {
                Vector3 facing = boss.transform.position - transform.position; facing.y = 0f;
                if (facing.sqrMagnitude > 0.1f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 720f * Time.deltaTime);
            }
            if (sequence != animationControl.PlaybackSequence)
            { sequence = animationControl.PlaybackSequence; previousFrame = animationControl.GetAttackStartFrame(attack); hit = false; }
            float frame = animationControl.NormalizedTime * animationControl.GetAttackMaximumFrame(attack);
            float start = attack == 1 ? 25f : attack == 2 ? 40f : 20f;
            float end = attack == 1 ? 38f : attack == 2 ? 58f : 40f;
            bool active = frame >= start && frame <= end || previousFrame < start && frame > end;
            if (!hit && active)
            {
                Vector3 towardBoss = boss.transform.position - transform.position; towardBoss.y = 0f;
                float reach = attack == 1 ? attackReach.x : attack == 2 ? attackReach.y : attackReach.z;
                float angle = attack == 3 ? 90f : 65f;
                if (towardBoss.magnitude <= reach + 0.8f && Mathf.Abs(boss.transform.position.y - transform.position.y) < 3f && Vector3.Angle(transform.forward, towardBoss) <= angle)
                {
                    float damage = attack == 1 ? attackDamage.x : attack == 2 ? attackDamage.y : attackDamage.z;
                    if (boss.Health.TakeDamage(damage))
                    {
                        hit = true; HitsLanded++;
                        effects.Impact(boss.transform.position + towardBoss.normalized * -0.8f, new Color(0.3f, 1f, 0.8f), attack == 3 ? 3.5f : 2f);
                        view.AddShake(attack == 3 ? 0.7f : 0.35f);
                        audioControl.Strike();
                    }
                }
            }
            previousFrame = frame;
        }

        public void ResetHits() => HitsLanded = 0;
    }
}
