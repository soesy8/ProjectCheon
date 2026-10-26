using UnityEngine;

namespace Cheon.Tests.PlayerReactions
{
    // 하나의 호리병만 사용하고, 허리와 손 사이에서 중복 표시되거나 순간 이동하지 않게 한다.
    [DefaultExecutionOrder(100)]
    public class Test_GourdAttachment : MonoBehaviour
    {
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Test_GourdDrinking drinking;
        [SerializeField] private Transform gourd;
        [SerializeField] private Transform beltSocket;
        [SerializeField] private Transform handSocket;
        [SerializeField] private GameObject cork;
        [Header("장착 전환")]
        [SerializeField, Range(0.02f, 0.3f)] private float attachmentSeconds = 0.12f;
        [Header("입 접촉 보정 · 머리 본 기준")]
        [SerializeField] private Vector3 mouthLocalPosition = new Vector3(0, 0.035f, 0.095f);
        [SerializeField] private Vector3 gourdMouthLocalPosition = new Vector3(0, 0.13f, 0);
        [SerializeField, Range(0f, 1f)] private float contactCorrection = 1f;
        [SerializeField, Range(0f, 1f)] private float beltReachCorrection = 0.7f;
        private Transform upperArm, forearm, hand, head;
        private Test_PlayerReactionPreview preview;
        private Vector3 attachmentPosition;
        private Quaternion attachmentRotation;
        private float attachmentTime = 1f;
        private float pendingPoseSeconds;
        public bool IsInHand { get; private set; }
        public Transform Gourd => gourd;
        public Transform BeltSocket => beltSocket;
        public Transform HandSocket => handSocket;
        public float MouthDistance => gourd == null || head == null ? 0f :
            Vector3.Distance(gourd.TransformPoint(gourdMouthLocalPosition), head.TransformPoint(mouthLocalPosition));

        public void Configure(Animator animator, Test_GourdDrinking motion, Transform model, Transform belt, Transform grip, GameObject stopper)
        { characterAnimator = animator; drinking = motion; gourd = model; beltSocket = belt; handSocket = grip; cork = stopper; }

        private void Awake()
        {
            if (characterAnimator == null) return;
            upperArm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            forearm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            head = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
            preview = drinking != null ? drinking.GetComponent<Test_PlayerReactionPreview>() : null;
        }

        private void Start()
        {
            if (preview != null) preview.BeforePoseEvaluation += AccumulatePoseTime;
        }
        private void AccumulatePoseTime(float seconds) => pendingPoseSeconds += Mathf.Max(0f, seconds) * (drinking != null ? drinking.PlaybackSpeed : 1f);

        public void SetInHand(bool value, bool immediate = false)
        {
            if (gourd == null || beltSocket == null || handSocket == null) return;
            Transform parent = value ? handSocket : beltSocket;
            IsInHand = value;
            if (gourd.parent != parent)
            {
                gourd.SetParent(parent, true);
                attachmentPosition = gourd.localPosition;
                attachmentRotation = gourd.localRotation;
                attachmentTime = 0f;
            }
            if (immediate) { attachmentTime = attachmentSeconds; gourd.localPosition = Vector3.zero; gourd.localRotation = Quaternion.identity; }
            if (cork != null) cork.SetActive(!value);
        }

        private void LateUpdate()
        {
            if (gourd == null || drinking == null || hand == null) return;
            if (!characterAnimator.isActiveAndEnabled) return;
            // 수동 프레임 진행과 일시정지도 애니메이션 그래프가 사용한 시간에 맞춘다.
            float seconds = pendingPoseSeconds; pendingPoseSeconds = 0f;
            attachmentTime = Mathf.Min(attachmentSeconds, attachmentTime + seconds);
            float blend = Mathf.SmoothStep(0f, 1f, attachmentTime / Mathf.Max(0.01f, attachmentSeconds));
            gourd.localPosition = Vector3.Lerp(attachmentPosition, Vector3.zero, blend);
            gourd.localRotation = Quaternion.Slerp(attachmentRotation, Quaternion.identity, blend);
            float reach = drinking.ReachWeight * beltReachCorrection;
            if (reach > 0f) CorrectLeftArm(beltSocket.position, reach);
            if (IsInHand && drinking.SipWeight > 0f && head != null)
            {
                Vector3 correction = head.TransformPoint(mouthLocalPosition) - gourd.TransformPoint(gourdMouthLocalPosition);
                CorrectLeftArm(hand.position + correction, drinking.SipWeight * contactCorrection);
            }
        }

        private void CorrectLeftArm(Vector3 requestedPosition, float weight)
        {
            if (upperArm == null || forearm == null) return;
            Vector3 shoulder = upperArm.position;
            Vector3 elbow = forearm.position;
            Vector3 target = Vector3.Lerp(hand.position, requestedPosition, Mathf.Clamp01(weight));
            Quaternion wristRotation = hand.rotation;
            float upperLength = Vector3.Distance(shoulder, elbow), lowerLength = Vector3.Distance(elbow, hand.position);
            Vector3 direction = target - shoulder;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f, upperLength + lowerLength - 0.001f);
            if (direction.sqrMagnitude < 0.000001f) return;
            direction.Normalize(); target = shoulder + direction * distance;
            Vector3 bend = Vector3.ProjectOnPlane(elbow - shoulder, direction).normalized;
            if (bend.sqrMagnitude < 0.01f) bend = Vector3.ProjectOnPlane(-characterAnimator.transform.right, direction).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 desiredElbow = shoulder + direction * along + bend * height;
            upperArm.rotation = Quaternion.FromToRotation(elbow - shoulder, desiredElbow - shoulder) * upperArm.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, target - forearm.position) * forearm.rotation;
            hand.rotation = wristRotation;
        }

        private void OnDisable()
        {
            if (preview != null) preview.BeforePoseEvaluation -= AccumulatePoseTime;
            pendingPoseSeconds = 0f;
        }
    }
}
