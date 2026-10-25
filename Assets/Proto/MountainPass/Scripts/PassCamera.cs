using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Prototype.MountainPass
{
    [RequireComponent(typeof(Camera)), DefaultExecutionOrder(100)]
    public class PassCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform boss;
        [SerializeField] private PassGameFlow game;
        [SerializeField, Range(4f, 14f)] private float distance = 7.5f;
        [SerializeField, Range(10f, 22f)] private float combatDistance = 17f;
        [SerializeField, Range(-75f, 88f)] private float pitch = 28f;
        [SerializeField, Range(-75f, 0f)] private float minimumPitch = -65f;
        [SerializeField, Range(45f, 88f)] private float maximumPitch = 85f;
        [SerializeField, Min(0.01f)] private float sensitivity = 0.15f;
        [SerializeField, Min(1f)] private float followSpeed = 10f;
        private float yaw, currentDistance, shake;
        private Vector3 focus;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private bool applicationFocused = true;
        private int ignoreMouseUntilFrame;
        public bool BossLocked { get; private set; }
        public float Pitch => pitch;
        public void Configure(Transform target, Transform enemy, PassGameFlow flow)
        {
            player = target; boss = enemy; game = flow;
            GetComponent<Camera>().orthographic = false;
            GetComponent<Camera>().fieldOfView = 60f;
            SnapBehind();
        }
        private void Start() => SnapBehind();
        public void SnapBehind()
        {
            if (player == null) return;
            yaw = player.eulerAngles.y;
            focus = player.position + Vector3.up * 1.35f;
            currentDistance = distance;
            ApplyView();
        }
        public void LockBoss(bool locked) => BossLocked = locked;
        public void AddShake(float amount) => shake = Mathf.Max(shake, amount);
        public void RefreshCursor()
        {
            bool locked = applicationFocused && game != null && game.IsRunning;
            if (locked && Cursor.lockState != CursorLockMode.Locked) ignoreMouseUntilFrame = Time.frameCount + 1;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        private void OnApplicationFocus(bool focused) { applicationFocused = focused; RefreshCursor(); }
        private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void LateUpdate()
        {
            if (player == null) return;
            RefreshCursor();
            float delta = Time.deltaTime;
            bool running = game != null && game.IsRunning;
            if (running && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && game.InEncounter) BossLocked = !BossLocked;
            if (game != null && !game.InEncounter) BossLocked = false;
            if (running && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null && Time.frameCount > ignoreMouseUntilFrame)
            {
                Vector2 motion = Mouse.current.delta.ReadValue();
                if (BossLocked && Mathf.Abs(motion.x) > 1f) BossLocked = false;
                yaw += motion.x * sensitivity;
                pitch = Mathf.Clamp(pitch - motion.y * sensitivity, minimumPitch, maximumPitch);
                distance = Mathf.Clamp(distance - Mouse.current.scroll.ReadValue().y * 0.004f, 4f, 14f);
            }
            Vector3 desiredFocus = player.position + Vector3.up * 1.35f;
            float desiredDistance = distance;
            if (BossLocked && boss != null)
            {
                Vector3 towardBoss = boss.position - player.position;
                towardBoss.y = 0f;
                if (towardBoss.sqrMagnitude > 0.1f) yaw = Mathf.LerpAngle(yaw, Quaternion.LookRotation(towardBoss).eulerAngles.y, 1f - Mathf.Exp(-5f * delta));
                // 주시점을 멀리 보내면 카메라가 플레이어 바로 위로 오므로 수평 이동은 3m 안으로 제한한다.
                desiredFocus += towardBoss.normalized * Mathf.Min(3f, towardBoss.magnitude * 0.5f);
                desiredFocus.y = Mathf.Lerp(player.position.y + 1.35f, boss.position.y + 10.85f, 0.5f);
                desiredDistance = combatDistance;
            }
            focus = Vector3.Distance(focus, desiredFocus) > 20f ? desiredFocus : Vector3.Lerp(focus, desiredFocus, 1f - Mathf.Exp(-followSpeed * delta));
            currentDistance = Mathf.Lerp(currentDistance, desiredDistance, 1f - Mathf.Exp(-4f * delta));
            shake = Mathf.MoveTowards(shake, 0f, delta * 2f);
            ApplyView();
        }
        private void ApplyView()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 backward = -(rotation * Vector3.forward);
            float visibleDistance = currentDistance;
            int count = Physics.SphereCastNonAlloc(focus, 0.22f, backward, hits, currentDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].transform.IsChildOf(player)) continue;
                visibleDistance = Mathf.Min(visibleDistance, Mathf.Max(0.5f, hits[i].distance - 0.12f));
            }
            Vector3 offset = rotation * Vector3.right * 0.35f;
            Vector3 tremor = Application.isPlaying ? Random.insideUnitSphere * shake * 0.12f : Vector3.zero;
            transform.SetPositionAndRotation(focus + backward * visibleDistance + offset + tremor, rotation);
        }
    }
}
