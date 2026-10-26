using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [CreateAssetMenu(menuName = "Cheon/Player Reaction Settings")]
    public class PassPlayerReactionSettings : ScriptableObject
    {
        [Serializable]
        public class MotionRange
        {
            public AnimationClip clip;
            [Min(0)] public int startFrame;
            [Min(1)] public int endFrame = 1;
            [Range(0.05f, 3f)] public float speed = 1f;
            public float FrameRate => clip != null ? clip.frameRate : 30f;
            public float Duration => (endFrame - startFrame) / FrameRate / Mathf.Max(0.05f, speed);
            public void Validate()
            {
                int maximum = clip != null ? Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate)) : 1;
                startFrame = Mathf.Clamp(startFrame, 0, maximum - 1);
                endFrame = Mathf.Clamp(endFrame, startFrame + 1, maximum);
                speed = Mathf.Clamp(speed, 0.05f, 3f);
            }
        }

        public MotionRange heavyHit = new MotionRange();
        public MotionRange getUp = new MotionRange();
        public MotionRange death = new MotionRange();
        public AnimationClip sheathedIdleClip, drawnIdleClip;
        [Header("착지와 회복 입력")]
        [Min(0)] public int landingFrame = 26;
        [Range(0f, 2f)] public float landedRecoveryDelay = 0.5f;
        [Range(0f, 3f)] public float downHoldSeconds = 0.35f;
        [Range(0f, 1f)] public float getUpDodgeCancelProgress = 0.45f;
        [Range(0f, 1f)] public float getUpMoveCancelProgress = 0.9f;
        [Header("전환과 자세")]
        [Range(0f, 0.5f)] public float transitionSeconds = 0.35f;
        [Range(0f, 0.8f)] public float returnToIdleSeconds = 0.55f;
        [Range(0.05f, 0.4f)] public float inputTransitionSeconds = 0.18f;
        [Range(0f, 20f)] public float getUpHeadBowDegrees = 10f;

        public MotionRange GetRange(string state) => state == "GET_UP" ? getUp : state == "DEATH" ? death : heavyHit;
        private void OnValidate()
        {
            heavyHit.Validate(); getUp.Validate(); death.Validate();
            landingFrame = Mathf.Clamp(landingFrame, 0, heavyHit.endFrame);
            getUpMoveCancelProgress = Mathf.Clamp(getUpMoveCancelProgress, getUpDodgeCancelProgress, 1f);
        }
    }
}
