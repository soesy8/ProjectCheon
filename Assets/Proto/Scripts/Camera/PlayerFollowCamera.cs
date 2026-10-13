using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class PlayerFollowCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(10f, 16f, -15f);
    [SerializeField, Min(0f)] private float followSmoothTime = 0.12f;

    private Vector3 followVelocity;

    private void Start()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        // 플레이어 이동이 끝난 뒤 따라가며, 회전은 고정해 화면 기준 이동 방향을 유지한다.
        transform.position = Vector3.SmoothDamp(
            transform.position, target.position + followOffset, ref followVelocity, followSmoothTime);
        if (followOffset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-followOffset, Vector3.up);
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        followVelocity = Vector3.zero;
        transform.position = target.position + followOffset;
        if (followOffset.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-followOffset, Vector3.up);
    }
}
