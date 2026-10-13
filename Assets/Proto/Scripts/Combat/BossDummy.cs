using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class BossDummy : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;
    [SerializeField] private Color hitColor = Color.white;
    [SerializeField] private int currentHealth;
    [SerializeField] private int hitCount;

    private Renderer bodyRenderer;
    private MaterialPropertyBlock hitProperties;
    private float flashEndTime;
    private bool isFlashing;

    public int CurrentHealth => currentHealth;
    public int HitCount => hitCount;

    private void Awake()
    {
        bodyRenderer = GetComponent<Renderer>();
        hitProperties = new MaterialPropertyBlock();
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (!isFlashing || Time.time < flashEndTime)
            return;

        bodyRenderer.SetPropertyBlock(null);
        isFlashing = false;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        hitCount++;
        hitProperties.SetColor("_BaseColor", hitColor);
        bodyRenderer.SetPropertyBlock(hitProperties);
        flashEndTime = Time.time + hitFlashDuration;
        isFlashing = true;

        // 허수아비는 사라지지 않고 체력을 회복하므로 계속 타격을 연습할 수 있다.
        if (currentHealth == 0)
            currentHealth = maxHealth;
    }
}
