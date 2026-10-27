using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [CreateAssetMenu(menuName = "천/산길/강피격·기상·사망 설정")]
    public class PassPlayerReactionSettings : ScriptableObject
    {
        [Serializable]
        public class MotionRange
        {
            [InspectorName("원본 애니메이션")]
            public AnimationClip clip;
            [InspectorName("시작 프레임")]
            [Min(0)] public int startFrame;
            [InspectorName("종료 프레임")]
            [Min(1)] public int endFrame = 1;
            [InspectorName("애니메이션 재생 배속")]
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

        [InspectorName("강피격 애니메이션 설정")]
        public MotionRange heavyHit = new MotionRange();
        [InspectorName("일어나기 애니메이션 설정")]
        public MotionRange getUp = new MotionRange();
        [InspectorName("사망 동작·소멸 설정")]
        public MotionRange death = new MotionRange();
        [InspectorName("납도 대기 애니메이션")]
        public AnimationClip sheathedIdleClip;
        [InspectorName("발도 대기 애니메이션")]
        public AnimationClip drawnIdleClip;
        [Header("착지와 회복 입력")]
        [InspectorName("최초 착지 프레임")]
        [Min(0)] public int landingFrame = 26;
        [InspectorName("착지 후 기상 입력 대기 (초)")]
        [Range(0f, 2f)] public float landedRecoveryDelay = 0.5f;
        [InspectorName("넘어진 자세 유지 시간 (초)")]
        [Range(0f, 3f)] public float downHoldSeconds = 0.35f;
        [InspectorName("기상 중 회피 허용 시점 (0~1)")]
        [Range(0f, 1f)] public float getUpDodgeCancelProgress = 0.45f;
        [InspectorName("기상 중 이동 허용 시점 (0~1)")]
        [Range(0f, 1f)] public float getUpMoveCancelProgress = 0.9f;
        [Header("전환과 자세")]
        [InspectorName("피격·회복 동작 혼합 시간 (초)")]
        [Range(0f, 0.5f)] public float transitionSeconds = 0.35f;
        [InspectorName("기상에서 대기로 혼합 시간 (초)")]
        [Range(0f, 0.8f)] public float returnToIdleSeconds = 0.55f;
        [InspectorName("입력 동작 전환 시간 (초)")]
        [Range(0.05f, 0.4f)] public float inputTransitionSeconds = 0.18f;
        [InspectorName("기상 중 고개 숙임 보정 (도)")]
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
