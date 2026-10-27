using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 자동 복귀 처리 전에 실제 애니메이션 프레임을 읽어 마지막 표시 프레임도 놓치지 않는다.
    [DefaultExecutionOrder(-20)]
    public class PassAttackRangeEffects : MonoBehaviour
    {
        [Serializable]
        public sealed class AttackRange
        {
            [InspectorName("공격 범위 메시")]
            public PassAttackSector sector;
            [InspectorName("공격 번호")]
            [Range(1, 3)] public int attack = 1;
            [Tooltip("원본 공격 FBX에서 범위 표시를 시작하는 프레임입니다.")]
            [InspectorName("시작 프레임")]
            [Min(0)] public int startFrame = 25;
            [Tooltip("원본 공격 FBX에서 범위 표시를 끝내는 프레임입니다.")]
            [InspectorName("종료 프레임")]
            [Min(1)] public int endFrame = 38;
        }

        [InspectorName("플레이어 동작 제어기")]
        [SerializeField] private PassAnimationController animationControl;
        [InspectorName("공격별 범위 표시 목록")]
        [SerializeField] private AttackRange[] ranges = Array.Empty<AttackRange>();
        private uint sequence;
        private int previousAttack;
        private float previousFrame;

        public void Configure(PassAnimationController control, AttackRange[] attackRanges)
        {
            animationControl = control;
            ranges = attackRanges;
        }

        private void LateUpdate()
        {
            if (animationControl == null || !animationControl.isActiveAndEnabled ||
                animationControl.CharacterAnimator == null || !animationControl.CharacterAnimator.isActiveAndEnabled)
            {
                HideRanges();
                return;
            }

            int attack = animationControl.SelectedState switch
            {
                "ATTACK1" => 1, "ATTACK2" => 2, "ATTACK3" => 3, _ => 0
            };
            if (attack == 0)
            {
                previousAttack = 0;
                HideRanges();
                return;
            }

            float frame = animationControl.NormalizedTime * animationControl.GetAttackMaximumFrame(attack);
            int clipStart = animationControl.GetAttackStartFrame(attack);
            int clipEnd = animationControl.GetAttackEndFrame(attack);
            if (sequence != animationControl.PlaybackSequence || previousAttack != attack)
            {
                sequence = animationControl.PlaybackSequence;
                previousFrame = clipStart;
            }

            foreach (var range in ranges)
            {
                if (range.sector == null) continue;
                float start = Mathf.Max(clipStart, range.startFrame);
                float end = Mathf.Min(clipEnd, range.endFrame);
                bool inWindow = frame >= start && frame <= end;
                // 높은 배속에서 한 프레임 사이에 표시 구간을 통과해도 한 번은 확인할 수 있다.
                bool crossedWindow = previousFrame < start && frame > end;
                bool visible = range.attack == attack && start < end && (inWindow || crossedWindow);
                if (visible)
                {
                    float fade = Mathf.InverseLerp(Mathf.Lerp(start, end, 0.7f), end, frame);
                    range.sector.Show(crossedWindow ? 1f : Mathf.Lerp(1f, 0.25f, fade));
                }
                else range.sector.Hide();
            }
            previousAttack = attack;
            previousFrame = frame;
        }

        private void HideRanges()
        {
            foreach (var range in ranges)
                if (range.sector != null) range.sector.Hide();
        }

        private void OnDisable()
        {
            previousAttack = 0;
            HideRanges();
        }
    }
}

