using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cheon.Combat
{
    [DisallowMultipleComponent]
    public class WindComboAttack : MonoBehaviour
    {
        [Header("모션 / 이펙트")]
        [SerializeField] private WindComboMotion motion;
        [SerializeField] private WindComboEffects effects;
        [Header("기획 시안 v2 · 초")]
        [SerializeField] private Vector3 durations = new Vector3(.28f, .32f, 1f);
        [SerializeField] private Vector3 hitTimes = new Vector3(.09f, .11f, .40f);
        [SerializeField, Min(0)] private float bufferStart = .05f;
        [SerializeField, Min(0)] private float continuationWindow = .30f;
        [Header("판정")]
        [SerializeField] private float baseDamage = 20f;
        [SerializeField] private Vector2 meleeRanges = new Vector2(2f, 2.4f);
        [SerializeField] private Vector2 meleeAngles = new Vector2(90f, 110f);
        [SerializeField] private float finalDamageMultiplier = 2.8f;
        [SerializeField] private float projectileRange = 4.5f;
        [SerializeField] private float projectileWidth = 1.8f;
        [SerializeField] private float projectileFlightTime = .35f;
        [SerializeField] private float stepDistance = .25f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        public bool IsAttacking => Stage > 0;
        public int Stage { get; private set; }
        public float Elapsed { get; private set; }
        public bool HasBufferedInput { get; private set; }
        public bool DirectionLocked => IsAttacking && Elapsed >= (Stage == 3 ? .20f : hitTimes[Stage - 1]);
        public bool CanDodge => !IsAttacking || (Stage == 1 ? Elapsed >= .14f :
            Stage == 2 ? Elapsed >= .16f : Elapsed < .40f || Elapsed >= .68f);
        public event Action<int, float> Strike;

        private CharacterController characterController;
        private int previousStage;
        private float finishedAt = float.NegativeInfinity;
        private bool strikeSent;
        private int requestedAtFrame = -1;
        private readonly HashSet<WindComboTarget> struckTargets = new HashSet<WindComboTarget>();

        private void Awake() { characterController = GetComponent<CharacterController>(); }

        public bool RequestAttack()
        {
            if (!enabled) return false;
            if (IsAttacking)
            {
                if (Stage == 3 || Elapsed < bufferStart || HasBufferedInput) return false;
                HasBufferedInput = true;
                return true;
            }
            BeginAttack(previousStage < 3 && Time.time - finishedAt <= continuationWindow ? previousStage + 1 : 1);
            requestedAtFrame = Time.frameCount;
            return true;
        }

        private void BeginAttack(int stage)
        {
            Stage = stage;
            Elapsed = 0;
            HasBufferedInput = false;
            strikeSent = false;
            motion.BeginAttack();
            motion.Sample(Stage, 0, true);
            if (Stage == 3) effects.BeginGather();
        }

        private void Update()
        {
            // 입력이 발생한 프레임의 지난 시간을 새 공격에 다시 더하지 않는다.
            if (!IsAttacking || requestedAtFrame == Time.frameCount) return;
            float remaining = Time.deltaTime;
            // 프레임이 경계를 넘어도 남은 시간을 다음 타격에 전달하여 숨은 대기를 만들지 않는다.
            while (IsAttacking && remaining > 0)
            {
                float before = Elapsed;
                float amount = Mathf.Min(remaining, durations[Stage - 1] - Elapsed);
                Elapsed += amount;
                remaining -= amount;
                if (!strikeSent && Elapsed >= hitTimes[Stage - 1])
                {
                    strikeSent = true;
                    PerformStrike();
                }
                if (Stage == 3)
                {
                    float step = Mathf.Clamp01((Elapsed - .40f) / .12f) - Mathf.Clamp01((before - .40f) / .12f);
                    if (step > 0 && characterController != null && characterController.enabled)
                        characterController.Move(transform.forward * (step * stepDistance));
                }
                motion.Sample(Stage, Elapsed, HasBufferedInput);
                if (Stage == 3) effects.UpdateGather(Mathf.Clamp01(Elapsed / .40f));
                if (Elapsed + .00001f < durations[Stage - 1]) break;
                int completedStage = Stage;
                bool chain = HasBufferedInput && Stage < 3;
                previousStage = Stage;
                finishedAt = Time.time - remaining;
                Stage = 0;
                if (chain) BeginAttack(completedStage + 1);
                else { HasBufferedInput = false; motion.ReturnToIdle(); }
            }
        }

        private void PerformStrike()
        {
            Strike?.Invoke(Stage, Time.time - (Elapsed - hitTimes[Stage - 1]));
            if (Stage == 3)
            {
                // 발사 기준점을 먼저 고정한 뒤 스텝 이동한다. 부채 끝에서 4.5m를 더하지 않는다.
                effects.EndGather();
                effects.Launch(transform.position, transform.forward, projectileRange,
                    projectileWidth, projectileFlightTime, baseDamage * finalDamageMultiplier, collisionLayers);
                return;
            }
            effects.Slash(Stage);
            struckTargets.Clear();
            float range = meleeRanges[Stage - 1];
            foreach (Collider candidate in Physics.OverlapSphere(transform.position + Vector3.up * .9f,
                range, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                var target = candidate.GetComponentInParent<WindComboTarget>();
                if (target == null || struckTargets.Contains(target) || target.transform.IsChildOf(transform)) continue;
                Vector3 point = candidate.ClosestPoint(transform.position + Vector3.up * .9f);
                Vector3 delta = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
                if (delta.magnitude > range || Vector3.Angle(transform.forward, delta) > meleeAngles[Stage - 1] * .5f) continue;
                if (Physics.Linecast(transform.position + Vector3.up, point, out RaycastHit obstruction,
                    collisionLayers, QueryTriggerInteraction.Ignore) &&
                    obstruction.collider.GetComponentInParent<WindComboTarget>() != target &&
                    !obstruction.transform.IsChildOf(transform)) continue;
                struckTargets.Add(target);
                target.ReceiveHit(baseDamage, transform.forward, false);
                effects.Impact(point, false);
            }
        }

        public bool TryCancelForDodge()
        {
            if (!CanDodge) return false;
            CancelAttack();
            return true;
        }

        public void CancelAttack()
        {
            Stage = previousStage = 0;
            Elapsed = 0;
            HasBufferedInput = false;
            finishedAt = float.NegativeInfinity;
            if (effects != null) effects.EndGather();
            if (motion != null) motion.ReturnToIdle();
        }

        private void OnDisable() { CancelAttack(); }
    }
}
