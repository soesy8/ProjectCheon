using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField, Min(1)] private int attackDamage = 10;
    [SerializeField, Min(0.1f)] private float attackReach = 1.25f;
    [SerializeField, Min(0.1f)] private float attackRadius = 0.9f;
    [SerializeField, Min(0.01f)] private float attackCooldown = 0.35f;

    private PlayerMovement playerMovement;
    private float nextAttackTime;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        bool attackPressed = (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        if (!attackPressed || playerMovement.IsDodging || Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;
        Vector3 attackCenter = transform.position + transform.forward * attackReach;
        Collider[] targets = Physics.OverlapSphere(attackCenter, attackRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider target in targets)
        {
            if (target.TryGetComponent(out BossDummy bossDummy))
                bossDummy.TakeDamage(attackDamage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + transform.forward * attackReach, attackRadius);
    }
}
