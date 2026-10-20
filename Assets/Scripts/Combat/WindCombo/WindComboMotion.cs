using UnityEngine;

namespace Cheon.Combat
{
    // 편집 가능한 AnimationClip을 샘플링한다. 공격 판정은 WindComboAttack의 시간축 한 곳에서 처리한다.
    public class WindComboMotion : MonoBehaviour
    {
        [SerializeField] private GameObject animationRoot;
        [SerializeField] private AnimationClip[] attackClips = new AnimationClip[3];
        private Transform[] joints;
        private Vector3[] restPositions, startPositions;
        private Quaternion[] restRotations, startRotations;
        private bool returning;

        private void Awake()
        {
            joints = animationRoot.GetComponentsInChildren<Transform>();
            restPositions = new Vector3[joints.Length];
            startPositions = new Vector3[joints.Length];
            restRotations = new Quaternion[joints.Length];
            startRotations = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                restPositions[i] = joints[i].localPosition;
                restRotations[i] = joints[i].localRotation;
            }
        }

        public void BeginAttack()
        {
            returning = false;
            for (int i = 0; i < joints.Length; i++)
            {
                startPositions[i] = joints[i].localPosition;
                startRotations[i] = joints[i].localRotation;
            }
        }

        public void Sample(int stage, float time, bool chained)
        {
            if (stage < 1 || stage > attackClips.Length || attackClips[stage - 1] == null) return;
            AnimationClip clip = attackClips[stage - 1];
            clip.SampleAnimation(animationRoot, Mathf.Min(time, clip.length));
            float entry = Mathf.SmoothStep(0, 1, time / .035f);
            float neutral = stage < 3 && !chained ? Mathf.SmoothStep(0, 1, (time - clip.length + .10f) / .10f) : 0;
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i].localPosition = Vector3.Lerp(startPositions[i], joints[i].localPosition, entry);
                joints[i].localRotation = Quaternion.Slerp(startRotations[i], joints[i].localRotation, entry);
                joints[i].localPosition = Vector3.Lerp(joints[i].localPosition, restPositions[i], neutral);
                joints[i].localRotation = Quaternion.Slerp(joints[i].localRotation, restRotations[i], neutral);
            }
        }

        public void ReturnToIdle() { returning = true; }

        private void LateUpdate()
        {
            if (!returning || joints == null) return;
            float blend = 1 - Mathf.Exp(-22f * Time.deltaTime);
            for (int i = 0; i < joints.Length; i++)
            {
                joints[i].localPosition = Vector3.Lerp(joints[i].localPosition, restPositions[i], blend);
                joints[i].localRotation = Quaternion.Slerp(joints[i].localRotation, restRotations[i], blend);
            }
        }
    }
}
