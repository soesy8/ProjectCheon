using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    public class PassHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximum = 100f;
        [SerializeField, Min(0f)] private float hitRecovery = 0.65f;
        private float nextDamageTime;
        public float Current { get; private set; }
        public float Maximum => maximum;
        public float Fraction => Mathf.Clamp01(Current / maximum);
        public bool IsDead => Current <= 0f;
        public bool Invulnerable { get; set; }
        public event Action<float> Damaged;
        public event Action Died;
        public void Configure(float health, float recovery) { maximum = Mathf.Max(1f, health); hitRecovery = Mathf.Max(0f, recovery); }
        private void Awake() => Restore();
        public bool TakeDamage(float damage)
        {
            if (IsDead || Invulnerable || damage <= 0f || Time.time < nextDamageTime) return false;
            Current = Mathf.Max(0f, Current - damage);
            nextDamageTime = Time.time + hitRecovery;
            Damaged?.Invoke(damage);
            if (IsDead) Died?.Invoke();
            return true;
        }
        public void Heal(float amount) { if (!IsDead) Current = Mathf.Min(maximum, Current + Mathf.Max(0f, amount)); }
        public void Restore() { Current = maximum; nextDamageTime = 0f; Invulnerable = false; }
    }
}
