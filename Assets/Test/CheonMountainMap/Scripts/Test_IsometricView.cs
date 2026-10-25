using UnityEngine;

namespace Cheon.Testing.MountainMap
{
    // 빈 오브젝트를 끄면 기존 카메라 설정과 마우스 시점 조작을 복원한다.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public class Test_IsometricView : MonoBehaviour
    {
        [Header("시점 대상")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
        [Header("아이소메트릭 시점")]
        [SerializeField] private float yaw = 135f;
        [SerializeField, Range(5f, 85f)] private float pitch = 35.26439f;
        [SerializeField, Min(1f)] private float orthographicSize = 7f;
        [SerializeField, Min(1f)] private float distance = 22f;
        [SerializeField, Min(0.1f)] private float followSpeed = 12f;

        // 씬을 다시 열어도 오브젝트를 끄면 원래 시점으로 돌아가도록 함께 저장한다.
        [SerializeField, HideInInspector] private bool hasSavedView;
        [SerializeField, HideInInspector] private bool previousOrthographic;
        [SerializeField, HideInInspector] private float previousOrthographicSize;
        [SerializeField, HideInInspector] private bool previousFollowEnabled;
        [SerializeField, HideInInspector] private Vector3 previousPosition;
        [SerializeField, HideInInspector] private Quaternion previousRotation;
        private Vector3 focus;

        public void Configure(Camera camera, Transform player)
        {
            RestoreView();
            targetCamera = camera;
            target = player;
            if (isActiveAndEnabled) ActivateView();
        }

        private void OnEnable() => ActivateView();

        private void ActivateView()
        {
            if (targetCamera == null || target == null) return;
            var followCamera = targetCamera.GetComponent<Test_MountainPlayerCamera>();
            if (!hasSavedView)
            {
                previousOrthographic = targetCamera.orthographic;
                previousOrthographicSize = targetCamera.orthographicSize;
                previousPosition = targetCamera.transform.position;
                previousRotation = targetCamera.transform.rotation;
                previousFollowEnabled = followCamera != null && followCamera.enabled;
                hasSavedView = true;
            }
            if (followCamera != null) followCamera.enabled = false;
            focus = target.position + targetOffset;
            ApplyView();
        }

        private void LateUpdate()
        {
            if (targetCamera == null || target == null) return;
            if (!hasSavedView) ActivateView();
            Vector3 desiredFocus = target.position + targetOffset;
            focus = !Application.IsPlaying(gameObject) || Vector3.Distance(focus, desiredFocus) > 15f
                ? desiredFocus
                : Vector3.Lerp(focus, desiredFocus, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            ApplyView();
        }

        private void ApplyView()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize;
            targetCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }

        private void OnDisable() => RestoreView();

        private void RestoreView()
        {
            if (!hasSavedView || targetCamera == null) return;
            targetCamera.orthographic = previousOrthographic;
            targetCamera.orthographicSize = previousOrthographicSize;
            targetCamera.transform.SetPositionAndRotation(previousPosition, previousRotation);
            var followCamera = targetCamera.GetComponent<Test_MountainPlayerCamera>();
            if (followCamera != null)
            {
                followCamera.enabled = previousFollowEnabled;
                if (previousFollowEnabled && target != null) followCamera.Configure(target);
            }
            hasSavedView = false;
        }

        private void OnValidate()
        {
            pitch = Mathf.Clamp(pitch, 5f, 85f);
            orthographicSize = Mathf.Max(1f, orthographicSize);
            distance = Mathf.Max(1f, distance);
            followSpeed = Mathf.Max(0.1f, followSpeed);
        }
    }
}
