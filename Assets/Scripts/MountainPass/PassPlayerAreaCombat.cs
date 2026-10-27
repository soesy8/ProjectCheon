using System.Collections.Generic;
using UnityEngine;
using Cheon.Prototype.MountainPass;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(-15)]
    public class PassPlayerAreaCombat : PassPlayerCombat
    {
        [InspectorName("보스 패턴 설정")]
        [SerializeField] private PassBossPatternSettings settings;
        [InspectorName("공격 범위 목록")]
        [SerializeField] private PassAttackRangeEffects.AttackRange[] areas;
        private readonly HashSet<PassHealth> hitEnemies = new HashSet<PassHealth>();
        private uint playbackSequence;
        private int previousAttack;
        private float previousAttackFrame;

        public void ConfigureAreaCombat(PassBossPatternSettings configuration, PassAttackRangeEffects.AttackRange[] attackAreas)
        { settings = configuration; areas = attackAreas; }

        protected override void LateUpdate()
        {
            if (game == null || !game.IsRunning || !game.InEncounter || settings == null) return;
            int attack = animationControl.SelectedState == "ATTACK1" ? 1 : animationControl.SelectedState == "ATTACK2" ? 2 : animationControl.SelectedState == "ATTACK3" ? 3 : 0;
            if (attack == 0) { previousAttack = 0; return; }
            if (playbackSequence != animationControl.PlaybackSequence || previousAttack != attack)
            {
                playbackSequence = animationControl.PlaybackSequence; hitEnemies.Clear();
                previousAttackFrame = animationControl.GetAttackStartFrame(attack);
            }
            float frame = animationControl.NormalizedTime * animationControl.GetAttackMaximumFrame(attack);
            bool landed = false;
            foreach (var area in areas)
            {
                if (area.attack != attack || area.sector == null) continue;
                float start = Mathf.Max(area.startFrame, animationControl.GetAttackStartFrame(attack));
                float end = Mathf.Min(area.endFrame, animationControl.GetAttackEndFrame(attack));
                if (start > end || !(frame >= start && frame <= end || previousAttackFrame < start && frame > end)) continue;
                // 한 동작에 맞은 적만 기록하여 같은 적의 중복 타격을 막고, 다른 적은 계속 검사한다.
                foreach (var enemy in settings.Enemies)
                {
                    if (!enemy.IsAlive || hitEnemies.Contains(enemy.Health) || !IntersectsArea(enemy, area.sector)) continue;
                    float damage = attack == 1 ? settings.combat.playerDamage.x : attack == 2 ? settings.combat.playerDamage.y : settings.combat.playerDamage.z;
                    if (!enemy.Health.TakeDamage(damage)) continue;
                    hitEnemies.Add(enemy.Health); HitsLanded++; landed = true;
                    Vector3 hitPoint = enemy.Body.ClosestPoint(transform.position + Vector3.up);
                    effects.Hit(hitPoint, transform.position + Vector3.up - hitPoint, false, attack == 3);
                }
            }
            if (landed) { view.AddShake(attack == 3 ? .7f : .35f); audioControl.Strike(); }
            previousAttack = attack; previousAttackFrame = frame;
        }

        private bool IntersectsArea(PassBossPatternController enemy, PassAttackSector sector)
        {
            enemy.GetBodySegment(out Vector3 first, out Vector3 second);
            Vector3 origin = sector.transform.position;
            float radius = enemy.Body.radius;
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(first, second) / Mathf.Max(.1f, radius)));
            for (int index = 0; index <= samples; index++)
            {
                Vector3 point = Vector3.Lerp(first, second, index / (float)samples);
                if (Mathf.Abs(point.y - (transform.position.y + 1f)) > 1f + radius) continue;
                Vector3 direction = Vector3.ProjectOnPlane(point - origin, Vector3.up);
                float distance = direction.magnitude;
                if (distance > sector.Radius + radius) continue;
                if (distance <= radius) return true;
                float margin = Mathf.Asin(Mathf.Clamp01(radius / distance)) * Mathf.Rad2Deg;
                if (Vector3.Angle(sector.transform.forward, direction) <= sector.Angle * .5f + margin) return true;
            }
            return false;
        }

        public override void ResetHits()
        { base.ResetHits(); hitEnemies.Clear(); previousAttack = 0; }
    }
}
