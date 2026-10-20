using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Combat
{
    [RequireComponent(typeof(CharacterController), typeof(WindComboAttack))]
    [DefaultExecutionOrder(-50)]
    public class WindComboPlayer : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float moveSpeed = 6;
        [SerializeField] private float dodgeSpeed = 16;
        [SerializeField] private float dodgeDuration = .18f;
        [SerializeField] private float dodgeCooldown = .6f;
        [SerializeField] private Vector3 cameraOffset = new Vector3(7, 9, -10);
        [SerializeField] private bool followCamera = true;
        [SerializeField] private bool showGuide = true;
        [SerializeField] private Font guideFont;
        private CharacterController controller;
        private WindComboAttack attack;
        private Vector3 dodgeDirection, spawnPosition;
        private float dodgeUntil, nextDodge, verticalSpeed, nextDemonstration;
        private bool autoDemonstration;
        private int demonstrationStage;
        private GUIStyle titleStyle, bodyStyle;

        public bool IsDodging => Time.time < dodgeUntil;
        public bool AutoDemonstration { get => autoDemonstration; set { autoDemonstration = value; nextDemonstration = Time.time; } }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            attack = GetComponent<WindComboAttack>();
            spawnPosition = transform.position;
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void Update()
        {
            var keys = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (keys != null)
            {
                input.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                input.y = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
                if (keys.rKey.wasPressedThisFrame) AutoDemonstration = !AutoDemonstration;
                if (keys.vKey.wasPressedThisFrame)
                {
                    var fx = GetComponent<WindComboEffects>();
                    fx.ShowEffects = !fx.ShowEffects;
                }
                if (keys.digit1Key.wasPressedThisFrame) Time.timeScale = 1;
                if (keys.digit2Key.wasPressedThisFrame) Time.timeScale = .35f;
            }
            if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > .01f)
                input = Gamepad.current.leftStick.ReadValue();
            Vector3 forward = viewCamera != null ? Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 movement = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1);
            bool dodgePressed = (keys != null && keys.spaceKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            // 회피를 먼저 소비한다. 발사 직전 취소가 확정되면 그 프레임에는 발사하지 않는다.
            if (dodgePressed && Time.time >= nextDodge && attack.TryCancelForDodge())
            {
                autoDemonstration = false;
                dodgeDirection = movement.sqrMagnitude > .01f ? movement.normalized : -transform.forward;
                dodgeUntil = Time.time + dodgeDuration;
                nextDodge = dodgeUntil + dodgeCooldown;
            }
            if (!attack.DirectionLocked && !IsDodging && movement.sqrMagnitude > .01f)
                transform.forward = movement.normalized;
            bool pressed = (keys != null && keys.jKey.wasPressedThisFrame) ||
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
            if (pressed && !IsDodging) { autoDemonstration = false; attack.RequestAttack(); }
            if (autoDemonstration && !IsDodging)
            {
                if (!attack.IsAttacking && Time.time >= nextDemonstration)
                {
                    attack.RequestAttack();
                    demonstrationStage = 0;
                    nextDemonstration = Time.time + 2.4f;
                }
                if (attack.Stage > 0 && attack.Stage < 3 && attack.Elapsed >= .06f && demonstrationStage != attack.Stage)
                {
                    attack.RequestAttack();
                    demonstrationStage = attack.Stage;
                }
            }
            verticalSpeed = controller.isGrounded ? -2 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            Vector3 velocity = IsDodging ? dodgeDirection * dodgeSpeed : attack.IsAttacking ? Vector3.zero : movement * moveSpeed;
            controller.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (transform.position.y < -10)
            {
                controller.enabled = false;
                transform.position = spawnPosition;
                controller.enabled = true;
                verticalSpeed = 0;
            }
        }

        private void LateUpdate()
        {
            if (!followCamera || viewCamera == null) return;
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, transform.position + cameraOffset,
                1 - Mathf.Exp(-9 * Time.unscaledDeltaTime));
            viewCamera.transform.rotation = Quaternion.LookRotation(-cameraOffset + Vector3.up * .8f);
        }

        private void OnGUI()
        {
            if (!showGuide) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { font = guideFont, fontSize = 22, fontStyle = FontStyle.Bold };
                bodyStyle = new GUIStyle(GUI.skin.label) { font = guideFont, fontSize = 14 };
                titleStyle.normal.textColor = new Color(.95f, .91f, .82f);
                bodyStyle.normal.textColor = new Color(.85f, .91f, .86f);
            }
            GUI.Box(new Rect(16, 16, 410, 147), GUIContent.none);
            GUI.Label(new Rect(30, 25, 390, 32), "운풍 3연격  ·  모션 / 이펙트", titleStyle);
            GUI.Label(new Rect(30, 61, 390, 25), "J / 좌클릭  연타    WASD  이동    Space  회피", bodyStyle);
            GUI.Label(new Rect(30, 86, 390, 25), "R  자동 시연    V  이펙트    1  정상 / 2  느리게", bodyStyle);
            string state = attack.Stage == 1 ? "01 스침" : attack.Stage == 2 ? "02 되감기" :
                attack.Stage == 3 ? (attack.Elapsed < .4f ? "03 몰아치기 · 모으기" : "03 몰아치기 · 방출 / 회수") : "준비";
            GUI.Label(new Rect(30, 117, 390, 25), state + (attack.HasBufferedInput ? "  · 다음 타 예약" : "") +
                (autoDemonstration ? "  [자동]" : ""), bodyStyle);
        }

        private void OnDisable() { Time.timeScale = 1; }
    }
}
