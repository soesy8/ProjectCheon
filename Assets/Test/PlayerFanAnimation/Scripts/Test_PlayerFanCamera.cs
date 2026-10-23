using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cheon.Testing.PlayerFan
{
    public class Test_PlayerFanCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.05f, 0f);
        [SerializeField, Range(1f, 8f)] private float distance = 3.8f;
        [SerializeField] private float yaw = 155f;
        [SerializeField, Range(-15f, 70f)] private float pitch = 12f;
        [SerializeField, Min(0.01f)] private float rotationSensitivity = 0.2f;

        public void Configure(Transform character) { target = character; ApplyView(); }
        public void ResetView() { distance = 3.8f; yaw = 155f; pitch = 12f; ApplyView(); }

        private void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * rotationSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * rotationSensitivity, -15f, 70f);
                }
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.003f, 1f, 8f);
            }
            ApplyView();
        }

        private void ApplyView()
        {
            if (target == null) return;
            Vector3 focus = target.position + targetOffset;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
