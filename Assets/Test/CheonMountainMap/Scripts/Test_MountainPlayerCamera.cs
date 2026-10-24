using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cheon.Testing.MountainMap
{
    [RequireComponent(typeof(Camera))]
    public class Test_MountainPlayerCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField, Range(2f, 14f)] private float distance = 6.5f;
        [SerializeField] private float yaw = 45f;
        [SerializeField, Range(5f, 65f)] private float pitch = 24f;
        [SerializeField, Min(0.01f)] private float sensitivity = 0.16f;
        [SerializeField, Min(0.1f)] private float followSpeed = 12f;
        [SerializeField] private LayerMask collisionMask = ~0;
        private Vector3 focus;
        private readonly RaycastHit[] hits = new RaycastHit[24];

        public void Configure(Transform player)
        {
            target = player;
            focus = target.position + targetOffset;
            ApplyView();
        }

        private void OnEnable()
        {
            if (target != null) focus = target.position + targetOffset;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            var mouse = Mouse.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && !overUi && !Test_PlayerTestUi.IsTyping)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * sensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * sensitivity, 5f, 65f);
                }
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.004f, 2f, 14f);
            }
            Vector3 desiredFocus = target.position + targetOffset;
            focus = Vector3.Distance(focus, desiredFocus) > 15f ? desiredFocus :
                Vector3.Lerp(focus, desiredFocus, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            ApplyView();
        }

        private void ApplyView()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 backward = -(rotation * Vector3.forward);
            float visibleDistance = distance;
            int count = Physics.SphereCastNonAlloc(focus, 0.18f, backward, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].transform.IsChildOf(target)) continue;
                visibleDistance = Mathf.Min(visibleDistance, Mathf.Max(0.35f, hits[i].distance - 0.08f));
            }
            transform.SetPositionAndRotation(focus + backward * visibleDistance, rotation);
        }
    }
}
