using UnityEngine;

namespace Cheon.Combat
{
    [DefaultExecutionOrder(100)]
    public class WindComboArms : MonoBehaviour
    {
        [SerializeField] private Transform rightShoulder, rightElbow, rightHand, rightUpper, rightLower;
        [SerializeField] private Transform leftShoulder, leftElbow, leftHand, leftUpper, leftLower;
        private void LateUpdate() { AlignArms(); }

        public void AlignArms()
        {
            AlignSegment(rightUpper, rightShoulder, rightElbow, .30f);
            AlignSegment(rightLower, rightElbow, rightHand, .22f);
            AlignSegment(leftUpper, leftShoulder, leftElbow, .30f);
            AlignSegment(leftLower, leftElbow, leftHand, .22f);
        }

        private static void AlignSegment(Transform segment, Transform from, Transform to, float width)
        {
            if (segment == null || from == null || to == null) return;
            Vector3 delta = to.position - from.position;
            segment.position = (from.position + to.position) * .5f;
            segment.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            segment.localScale = new Vector3(width, delta.magnitude * .5f + width * .2f, width);
        }
    }
}
