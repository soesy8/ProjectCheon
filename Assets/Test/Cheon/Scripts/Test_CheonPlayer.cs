using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Testing
{
    [RequireComponent(typeof(CharacterController))]
    public class Test_CheonPlayer : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Test_CheonSession session;
        [SerializeField] private Test_CheonBoss boss;
        [SerializeField] private Test_CheonEffects effects;
        [SerializeField] private Test_CheonAudio sound;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform model;
        [SerializeField] private Transform fan;
        [Header("이동 / 회피")]
        [SerializeField] private float moveSpeed = 6;
        [SerializeField] private float dodgeSpeed = 21;
        [SerializeField] private float dodgeDuration = 0.23f;
        [SerializeField] private float dodgeCost = 27;
        [Header("맵 이동 경계")]
        [SerializeField] private Vector2 horizontalBounds = new Vector2(-10.8f, 10.8f);
        [SerializeField] private Vector2 depthBounds = new Vector2(-14.3f, 16.4f);
        [SerializeField, Min(0)] private float cornerInset;
        [SerializeField, Min(0)] private float cornerDepthInset;
        [SerializeField, Min(1)] private float spiritWidthMultiplier = 1;
        [Header("생명 / 기력")]
        [SerializeField] private float maxHealth = 120;
        [SerializeField] private float maxEnergy = 100;
        [SerializeField] private float energyRecovery = 22;
        [Header("부채 / 도술")]
        [SerializeField] private float attackDamage = 20;
        [SerializeField] private float attackReach = 3.5f;
        [SerializeField] private float talismanDamage = 42;
        [SerializeField] private float talismanCooldown = 3.2f;
        [SerializeField] private float sealDamage = 65;
        [SerializeField] private float sealCooldown = 7;
        public float Health { get; private set; }
        public float Energy { get; private set; }
        public float HealthRatio => Health / maxHealth;
        public float EnergyRatio => Energy / maxEnergy;
        public float TalismanRemaining => Mathf.Max(0, nextTalisman - Time.time);
        public float SealRemaining => Mathf.Max(0, nextSeal - Time.time);
        public float DodgeRemaining => Mathf.Max(0, nextDodge - Time.time);
        public bool IsDodging => Time.time < dodgeUntil;
        public bool IsInvulnerable => IsDodging || Time.time < invulnerableUntil;
        public int Combo { get; private set; }
        private CharacterController controller;
        private Vector3 dodgeDirection;
        private float dodgeUntil, nextDodge, invulnerableUntil, nextAttack, nextTalisman, nextSeal, lastAttack;
        private float fanPose;
        private Vector3 modelHome;

        private void Awake() { controller = GetComponent<CharacterController>(); modelHome = model.localPosition; Restore(); }

        private void Update()
        {
            if (!session.CanMove) return;
            Energy = Mathf.Min(maxEnergy, Energy + energyRecovery * Time.deltaTime);
            Vector2 input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().magnitude > .1f) input = Gamepad.current.leftStick.ReadValue();
            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
            Vector3 movement = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1);
            if ((keyboard != null && keyboard.spaceKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) TryDodge(movement);
            controller.Move((IsDodging ? dodgeDirection * dodgeSpeed : movement * moveSpeed) * Time.deltaTime + Vector3.down * 8 * Time.deltaTime);
            ClampToIsland();
            if (movement.sqrMagnitude > .01f && !IsDodging)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement), Time.deltaTime * 16);
            bool attack = (keyboard != null && keyboard.jKey.isPressed) || (Mouse.current != null && Mouse.current.leftButton.isPressed) || (Gamepad.current != null && Gamepad.current.buttonWest.isPressed);
            if (attack) TryAttack();
            if ((keyboard != null && keyboard.qKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame)) TryTalisman();
            if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame)) TrySeal();
            model.localPosition = modelHome + Vector3.up * (IsDodging ? .1f : Mathf.Abs(Mathf.Sin(Time.time * 12)) * .075f * movement.magnitude);
            model.localRotation = Quaternion.Euler(IsDodging ? 24 : 0, 0, Mathf.Sin(Time.time * 6) * 2);
            fanPose = Mathf.MoveTowards(fanPose, 0, Time.deltaTime * 5);
            fan.localRotation = Quaternion.Euler(-12, -60 + fanPose * 190, -25);
            if (IsDodging && Time.frameCount % 3 == 0) effects.Burst(transform.position + Vector3.up * .4f, Test_CheonEffects.Jade, 3, .25f);
        }

        private void ClampToIsland()
        {
            Vector3 position = transform.position;
            position.z = Mathf.Clamp(position.z, depthBounds.x, depthBounds.y);
            // 섬의 잘린 모서리에서도 땅 밖으로 나가지 않도록 이동 경계를 좁힌다.
            float depthInset = cornerDepthInset > 0 ? cornerDepthInset : cornerInset;
            float cornerDistance = Mathf.Max(depthBounds.x + depthInset - position.z, position.z - (depthBounds.y - depthInset));
            float inset = depthInset > 0 ? Mathf.Clamp01(cornerDistance / depthInset) * cornerInset : 0;
            // 영적 영역의 가로만 넓어지므로 현재 위치에 맞춰 양옆 경계를 선택한다.
            float widthMultiplier = position.z >= session.BoundaryZ ? spiritWidthMultiplier : 1;
            position.x = Mathf.Clamp(position.x, (horizontalBounds.x + inset) * widthMultiplier, (horizontalBounds.y - inset) * widthMultiplier);
            // 문 주변의 담장을 건너뛰거나 전투 중 수련지로 도망갈 수 없게 한다.
            float gateDistance = position.z - session.BoundaryZ;
            if (Mathf.Abs(gateDistance) < 1.1f && Mathf.Abs(position.x) > 2.3f) position.z = session.BoundaryZ + (gateDistance > 0 ? 1.12f : -1.12f);
            if (session.CurrentChapter == Test_CheonSession.Chapter.Battle) position.z = Mathf.Max(position.z, session.BoundaryZ + 1.3f);
            position.y = 0;
            transform.position = position;
        }

        private Vector3 Aim()
        {
            if (boss.gameObject.activeInHierarchy && !boss.Defeated)
            {
                Vector3 delta = boss.transform.position - transform.position;
                if (delta.sqrMagnitude < 225) { delta.y = 0; return delta.normalized; }
            }
            if (Mouse.current != null)
            {
                Ray ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
                {
                    Vector3 direction = ray.GetPoint(distance) - transform.position;
                    direction.y = 0;
                    if (direction.sqrMagnitude > .2f) return direction.normalized;
                }
            }
            return transform.forward;
        }

        public bool TryDodge(Vector3 direction)
        {
            if (!session.CanMove || Time.time < nextDodge || Energy < dodgeCost) return false;
            Energy -= dodgeCost;
            dodgeDirection = direction.sqrMagnitude > .05f ? direction.normalized : transform.forward;
            dodgeUntil = Time.time + dodgeDuration;
            nextDodge = Time.time + .65f;
            effects.Ring(transform.position, .7f, Test_CheonEffects.Jade, .35f);
            sound.PlayCue(Test_CheonAudio.Cue.Dodge);
            return true;
        }

        public bool TryAttack()
        {
            if (!session.CanMove || IsDodging || Time.time < nextAttack) return false;
            Combo = Time.time - lastAttack > 1.1f ? 1 : Combo % 3 + 1;
            nextAttack = Time.time + (Combo == 3 ? .56f : .34f);
            lastAttack = Time.time;
            Vector3 direction = Aim();
            transform.rotation = Quaternion.LookRotation(direction);
            fanPose = 1;
            effects.Slash(transform.position + Vector3.up * .65f, direction, attackReach, Combo == 3 ? Test_CheonEffects.Gold : Test_CheonEffects.Jade);
            sound.PlayCue(Test_CheonAudio.Cue.Attack);
            Vector3 delta = boss.transform.position - transform.position;
            if (boss.gameObject.activeInHierarchy && delta.magnitude < attackReach + 1 && Vector3.Dot(direction, delta.normalized) > .05f)
                boss.TakeDamage(attackDamage * (Combo == 3 ? 1.6f : 1), Combo == 3 ? .25f : 0);
            return true;
        }

        public bool TryTalisman()
        {
            if (!session.CanMove || IsDodging || Time.time < nextTalisman || Energy < 22) return false;
            Energy -= 22;
            nextTalisman = Time.time + talismanCooldown;
            effects.Talisman(transform.position + Vector3.up, Aim(), boss, talismanDamage);
            sound.PlayCue(Test_CheonAudio.Cue.Spell);
            return true;
        }

        public bool TrySeal()
        {
            if (!session.CanMove || IsDodging || Time.time < nextSeal || Energy < 38) return false;
            Energy -= 38;
            nextSeal = Time.time + sealCooldown;
            effects.Seal(transform.position, 5);
            if (boss.gameObject.activeInHierarchy && Vector3.Distance(transform.position, boss.transform.position) < 6) boss.TakeDamage(sealDamage, 1.8f);
            sound.PlayCue(Test_CheonAudio.Cue.Seal);
            return true;
        }

        public bool TakeDamage(float damage)
        {
            if (session.CurrentChapter != Test_CheonSession.Chapter.Battle || Health <= 0) return false;
            if (IsInvulnerable) { if (IsDodging) session.RecordDodge(); return false; }
            Health = Mathf.Max(0, Health - damage);
            invulnerableUntil = Time.time + .7f;
            effects.Burst(transform.position + Vector3.up, Test_CheonEffects.Red, 12, .45f);
            effects.Shake(.2f);
            sound.PlayCue(Test_CheonAudio.Cue.Hurt);
            if (Health <= 0) session.Lose();
            return true;
        }

        public void Restore() { Health = maxHealth; Energy = maxEnergy; }
    }
}
