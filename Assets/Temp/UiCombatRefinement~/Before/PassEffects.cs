using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    public class PassEffects : MonoBehaviour
    {
        [InspectorName("충격파 머티리얼")]
        [SerializeField] private Material ringMaterial;
        [InspectorName("충격파 원형 분할 수")]
        [SerializeField, Range(24, 128)] private int ringSegments = 64;
        [InspectorName("동시 충격파 최대 수")]
        [SerializeField, Min(1)] private int maximumRipples = 24;
        private sealed class Ripple { public LineRenderer line; public Vector3 position; public Color color; public float age, radius; }
        private readonly List<Ripple> ripples = new List<Ripple>();
        private readonly Stack<Ripple> availableRipples = new Stack<Ripple>();
        private Vector3[] circle, directions;
        private void PrepareCircle()
        {
            int count = Mathf.Clamp(ringSegments, 24, 128);
            if (circle != null && circle.Length == count) return;
            circle = new Vector3[count]; directions = new Vector3[count];
            for (int index = 0; index < count; index++)
            {
                float angle = index * Mathf.PI * 2f / count;
                directions[index] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
        }
        public void Configure(Material material) => ringMaterial = material;
        public LineRenderer CreateRing(string title, Color color)
        {
            PrepareCircle();
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
            PrepareCircle();
            if (line.positionCount != circle.Length) line.positionCount = circle.Length;
            line.enabled = radius > 0.01f; line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            for (int i = 0; i < circle.Length; i++)
            {
                circle[i] = center + directions[i] * radius;
            }
            line.SetPositions(circle);
        }
        public void Impact(Vector3 position, Color color, float radius = 2f)
        {
            Ripple ripple;
            if (availableRipples.Count > 0) ripple = availableRipples.Pop();
            else if (ripples.Count >= maximumRipples)
            { ripple = ripples[0]; ripples.RemoveAt(0); }
            else ripple = new Ripple { line = CreateRing("영혼 충격파", color) };
            ripple.position = position + Vector3.up * 0.12f;
            ripple.color = color; ripple.radius = radius; ripple.age = 0f;
            ripples.Add(ripple);
        }
        private void Update()
        {
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var ripple = ripples[i]; ripple.age += Time.deltaTime;
                float progress = ripple.age / 0.55f;
                // 같은 충격파를 다음 타격에서 재사용해 생성·삭제에 따른 할당과 프레임 튐을 줄인다.
                if (progress >= 1f) { ripple.line.enabled = false; availableRipples.Push(ripple); ripples.RemoveAt(i); continue; }
                var color = ripple.color; color.a *= 1f - progress;
                DrawRing(ripple.line, ripple.position, Mathf.Lerp(0.15f, ripple.radius, progress), color, 0.14f * (1f - progress));
            }
        }
        private void OnDisable()
        {
            foreach (var ripple in ripples) { ripple.line.enabled = false; availableRipples.Push(ripple); }
            ripples.Clear();
        }
    }
}
