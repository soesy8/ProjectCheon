using UnityEngine;
using UnityEngine.Events;

namespace Cheon.Combat
{
    public class WindComboTarget : MonoBehaviour
    {
        [SerializeField] private Transform reactionRoot;
        [SerializeField] private UnityEvent<float> onDamage;
        public int HitCount { get; private set; }
        public float TotalDamage { get; private set; }
        private Quaternion restRotation;
        private float reaction;

        private void Awake() { if (reactionRoot != null) restRotation = reactionRoot.localRotation; }
        public void ReceiveHit(float damage, Vector3 direction, bool strong)
        {
            HitCount++;
            TotalDamage += damage;
            reaction = strong ? 16 : 6;
            onDamage?.Invoke(damage);
        }
        private void Update()
        {
            reaction = Mathf.MoveTowards(reaction, 0, 65 * Time.deltaTime);
            if (reactionRoot != null) reactionRoot.localRotation = restRotation * Quaternion.Euler(-reaction, 0, 0);
        }
    }
}
