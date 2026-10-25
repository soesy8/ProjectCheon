using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    public class PassEffects : MonoBehaviour
    {
        [SerializeField] private Material ringMaterial;
        private sealed class Ripple { public LineRenderer line; public Vector3 position; public Color color; public float age, radius; }
        private readonly List<Ripple> ripples = new List<Ripple>();
        private readonly Vector3[] circle = new Vector3[96];
        public void Configure(Material material) => ringMaterial = material;
        public LineRenderer CreateRing(string title, Color color)
        {
            var item = new GameObject(title);
            item.transform.SetParent(transform, false);
            var line = item.AddComponent<LineRenderer>();
            line.sharedMaterial = ringMaterial;
            line.useWorldSpace = true; line.loop = true; line.positionCount = circle.Length;
            line.widthMultiplier = 0.1f;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.startColor = line.endColor = color;
            line.enabled = false;
            return line;
        }
        public void DrawRing(LineRenderer line, Vector3 center, float radius, Color color, float width = 0.1f)
        {
            if (line == null) return;
            line.enabled = radius > 0.01f; line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            for (int i = 0; i < circle.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / circle.Length;
                circle[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            line.SetPositions(circle);
        }
        public void Impact(Vector3 position, Color color, float radius = 2f)
        {
            ripples.Add(new Ripple {line = CreateRing("SpiritRipple", color), position = position + Vector3.up * 0.12f, color = color, radius = radius});
        }
        private void Update()
        {
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var ripple = ripples[i]; ripple.age += Time.deltaTime;
                float progress = ripple.age / 0.55f;
                if (progress >= 1f) { Destroy(ripple.line.gameObject); ripples.RemoveAt(i); continue; }
                var color = ripple.color; color.a *= 1f - progress;
                DrawRing(ripple.line, ripple.position, Mathf.Lerp(0.15f, ripple.radius, progress), color, 0.14f * (1f - progress));
            }
        }
    }
}
