using System;
using UnityEngine;

namespace Cheon.Tests.PlayerReactions
{
    [CreateAssetMenu(menuName = "Cheon/Tests/Player Reaction Settings")]
    public class Test_PlayerReactionSettings : ScriptableObject
    {
        [Serializable]
        public class MotionRange
        {
            public AnimationClip clip;
            [Min(0)] public int startFrame;
            [Min(1)] public int endFrame = 1;
            [Range(0.05f, 3f)] public float speed = 1f;
            public int MaximumFrame => clip == null ? 1 : Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
            public float FrameRate => clip == null ? 30f : clip.frameRate;
            public float Duration => (endFrame - startFrame) / FrameRate / Mathf.Max(0.05f, speed);

            public void Validate()
            {
                startFrame = Mathf.Clamp(startFrame, 0, MaximumFrame - 1);
                endFrame = Mathf.Clamp(endFrame, startFrame + 1, MaximumFrame);
                speed = Mathf.Clamp(speed, 0.05f, 3f);
            }
        }

        [Header("강한 패턴 피격")]
        public MotionRange heavyHit = new MotionRange();
        [Header("강피격 후 일어나기")]
        public MotionRange getUp = new MotionRange();
        [Header("사망 — 같은 강피격 원본, 독립적인 재생 구간")]
        public MotionRange death = new MotionRange();
        [Header("모션 연결")]
        [Range(0f, 3f)] public float downHoldSeconds = 0.35f;
        [Range(0f, 0.5f)] public float transitionSeconds = 0.35f;
        [Tooltip("일어나기의 마지막 구간과 대기 모션을 겹쳐 재생하는 시간입니다.")]
        [Range(0f, 0.8f)] public float returnToIdleSeconds = 0.55f;
        public AnimationClip idleClip;

        [Header("착지와 입력 캔슬 · 테스트 전용")]
        [Tooltip("원본 강피격 클립에서 몸이 처음 바닥에 닿는 프레임입니다.")]
        [Min(0)] public int landingFrame = 26;
        [Tooltip("배속에 영향을 받지 않는 실제 시간입니다.")]
        [Range(0f, 2f)] public float landedRecoveryDelay = 0.5f;
        [Range(0f, 1f)] public float getUpDodgeCancelProgress = 0.55f;
        [Range(0f, 1f)] public float getUpMoveCancelProgress = 0.9f;
        [Header("일어나기 머리 자세 보정")]
        [Tooltip("목과 머리에 나누어 적용하는 추가 숙임 각도입니다. 대기 자세에는 적용하지 않습니다.")]
        [Range(0f, 20f)] public float getUpHeadBowDegrees = 10f;
        [Header("이동·회피 입력 확인")]
        public MotionRange dodge = new MotionRange();
        [Min(0f)] public float moveSpeed = 4f;
        [Min(0f)] public float dodgeDistance = 3.5f;
        [Range(0.05f, 0.4f)] public float inputTransitionSeconds = 0.18f;

        public MotionRange GetRange(int index) => index == 1 ? getUp : index == 2 ? death : index == 3 ? dodge : heavyHit;
        public void Validate()
        {
            heavyHit.Validate(); getUp.Validate(); death.Validate();
            downHoldSeconds = Mathf.Clamp(downHoldSeconds, 0f, 3f);
            transitionSeconds = Mathf.Clamp(transitionSeconds, 0f, 0.5f);
            returnToIdleSeconds = Mathf.Clamp(returnToIdleSeconds, 0f, 0.8f);
            dodge.Validate();
            landingFrame = Mathf.Clamp(landingFrame, 0, heavyHit.MaximumFrame);
            landedRecoveryDelay = Mathf.Clamp(landedRecoveryDelay, 0f, 2f);
            getUpDodgeCancelProgress = Mathf.Clamp01(getUpDodgeCancelProgress);
            getUpMoveCancelProgress = Mathf.Clamp(getUpMoveCancelProgress, getUpDodgeCancelProgress, 1f);
            getUpHeadBowDegrees = Mathf.Clamp(getUpHeadBowDegrees, 0f, 20f);
            moveSpeed = Mathf.Max(0f, moveSpeed); dodgeDistance = Mathf.Max(0f, dodgeDistance);
            inputTransitionSeconds = Mathf.Clamp(inputTransitionSeconds, 0.05f, 0.4f);
        }
        private void OnValidate() => Validate();
    }
}
