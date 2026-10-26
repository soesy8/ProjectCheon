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
        [Range(0f, 0.5f)] public float transitionSeconds = 0.12f;
        [Tooltip("일어나기의 마지막 구간과 대기 모션을 겹쳐 재생하는 시간입니다.")]
        [Range(0f, 0.8f)] public float returnToIdleSeconds = 0.3f;
        public AnimationClip idleClip;

        public MotionRange GetRange(int index) => index == 1 ? getUp : index == 2 ? death : heavyHit;
        public void Validate()
        {
            heavyHit.Validate(); getUp.Validate(); death.Validate();
            downHoldSeconds = Mathf.Clamp(downHoldSeconds, 0f, 3f);
            transitionSeconds = Mathf.Clamp(transitionSeconds, 0f, 0.5f);
            returnToIdleSeconds = Mathf.Clamp(returnToIdleSeconds, 0f, 0.8f);
        }
        private void OnValidate() => Validate();
    }
}
