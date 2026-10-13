using UnityEngine;

public class WorldGate : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Color realityBackground = new Color(0.14f, 0.19f, 0.19f);
    [SerializeField] private Color spiritBackground = new Color(0.12f, 0.10f, 0.22f);
    [SerializeField, Min(0.1f)] private float colorTransitionSpeed = 3f;
    [SerializeField] private bool isSpiritWorld;

    public bool IsSpiritWorld => isSpiritWorld;

    private void LateUpdate()
    {
        if (player == null || worldCamera == null)
            return;

        // 문 양옆의 충돌 경계가 중앙 통로만 열어 두므로 문을 지나면 세계가 바뀐다.
        // 두 영역이 같은 씬에 있어 영적 세계를 미리 보고 로딩 없이 왕복할 수 있다.
        isSpiritWorld = transform.InverseTransformPoint(player.position).x > 0f;
        Color targetColor = isSpiritWorld ? spiritBackground : realityBackground;
        worldCamera.backgroundColor = Color.Lerp(
            worldCamera.backgroundColor, targetColor, colorTransitionSpeed * Time.deltaTime);
    }
}
