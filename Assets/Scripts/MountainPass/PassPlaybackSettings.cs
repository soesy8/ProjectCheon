using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [CreateAssetMenu(menuName = "Cheon/Prototype/Player Playback Settings")]
    public class PassPlaybackSettings : ScriptableObject
    {
        [Header("구르기 — 원본 FBX 프레임")]
        [Min(0.05f)] public float rollSpeed = 1.25f;
        [Min(0)] public int rollStartFrame;
        [Min(1)] public int rollEndFrame = 28;
        [Header("달리기 — 원본 FBX 프레임")]
        [Min(0.05f)] public float run2Speed = 1.6f;
        [Min(0)] public int run2StartFrame;
        [Min(1)] public int run2EndFrame = 21;

        public void Validate(int rollMaximumFrame, int runMaximumFrame)
        {
            rollSpeed = ValidSpeed(rollSpeed);
            run2Speed = ValidSpeed(run2Speed);
            rollMaximumFrame = Mathf.Max(1, rollMaximumFrame);
            runMaximumFrame = Mathf.Max(1, runMaximumFrame);
            rollStartFrame = Mathf.Clamp(rollStartFrame, 0, rollMaximumFrame - 1);
            rollEndFrame = Mathf.Clamp(rollEndFrame, rollStartFrame + 1, rollMaximumFrame);
            run2StartFrame = Mathf.Clamp(run2StartFrame, 0, runMaximumFrame - 1);
            run2EndFrame = Mathf.Clamp(run2EndFrame, run2StartFrame + 1, runMaximumFrame);
        }

        private static float ValidSpeed(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(0.05f, value);
    }
}

