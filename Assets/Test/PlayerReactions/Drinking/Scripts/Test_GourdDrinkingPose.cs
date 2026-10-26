using UnityEngine;

namespace Cheon.Tests.PlayerReactions
{
    // 몸통과 목의 과한 젖힘을 줄이고, 호리병 기울기는 유지한다.
    [RequireComponent(typeof(Test_PlayerReactionPreview), typeof(Test_GourdDrinking))]
    public class Test_GourdDrinkingPose : MonoBehaviour
    {
        [SerializeField] private Test_PlayerReactionPreview preview;
        [SerializeField] private Test_GourdDrinking drinking;
        [Header("마실 때 몸통 뒤로 젖힘 제한 · 도")]
        [SerializeField, Range(0f, 20f)] private float maxBackwardLeanDegrees = 5f;
        [Header("마실 때 목 뒤로 젖힘 줄임 · 도")]
        [SerializeField, Range(0f, 45f)] private float neckBackwardReductionDegrees = 15f;
        [Header("팔 내릴 때 고개 정면 복귀 · 마시기 진행률")]
        [SerializeField, Range(0f, 1f)] private float headReturnStart = 0.68f;
        [SerializeField, Range(0f, 1f)] private float headReturnEnd = 0.9f;
        private Transform spine, upperBodyTop, neck, head, leftHand;

        public void Configure(Test_PlayerReactionPreview target, Test_GourdDrinking motion)
        { preview = target; drinking = motion; }

        private void OnEnable()
        {
            if (preview == null) preview = GetComponent<Test_PlayerReactionPreview>();
            if (drinking == null) drinking = GetComponent<Test_GourdDrinking>();
            if (preview == null || preview.CharacterAnimator == null) return;
            var animator = preview.CharacterAnimator;
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            upperBodyTop = neck != null ? neck : animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (upperBodyTop == null) upperBodyTop = animator.GetBoneTransform(HumanBodyBones.Chest);
            preview.AfterPoseEvaluation += LimitBodyLean;
        }

        private void LimitBodyLean()
        {
            if (spine == null || upperBodyTop == null || drinking == null) return;
            if (drinking.Phase != Test_GourdDrinking.DrinkingPhase.Drinking &&
                drinking.Phase != Test_GourdDrinking.DrinkingPhase.Stowing) return;
            Transform actor = preview.CharacterAnimator.transform;
            Vector3 torso = upperBodyTop.position - spine.position;
            float backwardLean = Mathf.Atan2(-Vector3.Dot(torso, actor.forward), Vector3.Dot(torso, actor.up)) * Mathf.Rad2Deg;
            float correction = backwardLean - maxBackwardLeanDegrees;
            if (correction > 0f)
            {
                Quaternion neckRotation = neck != null ? neck.rotation : Quaternion.identity;
                Quaternion headRotation = head != null ? head.rotation : Quaternion.identity;
                Quaternion handRotation = leftHand != null ? leftHand.rotation : Quaternion.identity;
                spine.rotation = Quaternion.AngleAxis(correction, actor.right) * spine.rotation;
                // 척추 보정과 목 각도 조절을 분리해 몸통을 세워도 고개가 함께 숙여지지 않게 한다.
                if (neck != null) neck.rotation = neckRotation;
                if (head != null) head.rotation = headRotation;
                if (leftHand != null) leftHand.rotation = handRotation;
            }

            // 마시는 구간에 부드럽게 적용하고, 이동한 입 위치는 호리병 접촉 보정에서 다시 맞춘다.
            Transform neckTarget = neck != null ? neck : head;
            if (neckTarget != null)
                neckTarget.rotation = Quaternion.AngleAxis(neckBackwardReductionDegrees * drinking.SipWeight, actor.right) * neckTarget.rotation;
            RestoreForwardHeadPose(actor);
        }

        private void RestoreForwardHeadPose(Transform actor)
        {
            float weight = drinking.Phase == Test_GourdDrinking.DrinkingPhase.Stowing ? 1f :
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(headReturnStart, Mathf.Max(headReturnStart + 0.01f, headReturnEnd), drinking.Progress));
            if (weight <= 0f) return;
            // 팔이 내려가는 동안 목과 머리를 함께 세우고, 수납 중에도 캐릭터 정면을 바라본다.
            // 목 회전이 머리에 중복 적용되지 않도록 보정 전 머리의 월드 회전을 사용한다.
            Quaternion headRotation = head != null ? head.rotation : Quaternion.identity;
            if (neck != null) neck.rotation = Quaternion.Slerp(neck.rotation, actor.rotation, weight);
            if (head != null) head.rotation = Quaternion.Slerp(headRotation, actor.rotation, weight);
        }

        private void OnDisable()
        {
            if (preview != null) preview.AfterPoseEvaluation -= LimitBodyLean;
        }
    }
}
