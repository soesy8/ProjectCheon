using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PassFanSweepMesh : MonoBehaviour
    {
        private Mesh runtimeMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private static readonly int Head = Shader.PropertyToID("_Head");
        private static readonly int Tail = Shader.PropertyToID("_Tail");
        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int RibbonShape = Shader.PropertyToID("_RibbonShape");
        private static readonly int SurfaceFill = Shader.PropertyToID("_SurfaceFill");
        private static readonly int TipSerration = Shader.PropertyToID("_TipSerration");
        private static readonly int BranchCount = Shader.PropertyToID("_BranchCount");
        public bool IsVisible => meshRenderer != null && meshRenderer.enabled;

        public static Mesh CreateGeometry(PassFanAttackVfxProfile.Sweep style, bool wind = false, float forwardScale = 1)
        {
            const int segments = 80;
            const int rings = 8;
            var vertices = new Vector3[(segments + 1) * (rings + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * rings * 6];
            bool shaped = !wind && style.shapedRibbon;
            float visualAngle = style.angle + (shaped ? style.endExtension : 0f);
            for (int segment = 0; segment <= segments; segment++)
            {
                float progress = segment / (float)segments;
                float sweep = style.reverse ? 1 - progress : progress;
                float radians = ((sweep - .5f) * style.angle + style.yawOffset) * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(radians), 0, Mathf.Cos(radians));
                float outerRadius = style.radius;
                float innerRadius = style.innerRadius;
                if (shaped)
                {
                    // 시작 위치의 외곽은 유지하고, 중앙만 부풀리면서 양끝의 면 폭을 줄인다.
                    outerRadius += Mathf.Sin(sweep * Mathf.PI) * style.middleBulge;
                    float width = (style.radius - style.innerRadius) * Mathf.Max(.01f, style.ribbonWidth.Evaluate(sweep));
                    innerRadius = Mathf.Max(0, outerRadius - width);
                }
                for (int ring = 0; ring <= rings; ring++)
                {
                    float radial = ring / (float)rings;
                    int index = segment * (rings + 1) + ring;
                    float radius = Mathf.Lerp(innerRadius, outerRadius, radial);
                    float height = style.height.Evaluate(progress) * style.heightMultiplier + style.heightOffset;
                    height += Mathf.Sin(radial * Mathf.PI) * style.archHeight;
                    if (!wind && style.plane == PassFanAttackVfxProfile.SweepPlane.Vertical)
                    {
                        float pitch = (style.startPitch - sweep * visualAngle) * Mathf.Deg2Rad;
                        Vector3 arc = new Vector3(0, Mathf.Sin(pitch), Mathf.Cos(pitch)) * radius;
                        if (shaped && arc.y < 0) arc.y *= style.lowerArcHeightScale;
                        arc = Quaternion.Euler(0, style.yawOffset, style.roll) * arc;
                        vertices[index] = arc + Vector3.up * (style.pivotHeight + style.heightOffset);
                    }
                    else vertices[index] = new Vector3(direction.x, 0, direction.z * (wind ? forwardScale : 1)) * radius + Vector3.up * height;
                    // 일반 궤적은 각도 방향, 바람은 거리 방향으로 마스크를 진행한다.
                    uv[index] = wind ? new Vector2(radial, progress) : new Vector2(progress, radial);
                }
            }
            int triangle = 0;
            for (int segment = 0; segment < segments; segment++)
                for (int ring = 0; ring < rings; ring++)
                {
                    int a = segment * (rings + 1) + ring;
                    int b = a + rings + 1;
                    triangles[triangle++] = a; triangles[triangle++] = b; triangles[triangle++] = a + 1;
                    triangles[triangle++] = a + 1; triangles[triangle++] = b; triangles[triangle++] = b + 1;
                }
            var mesh = new Mesh { name = wind ? "PassWindSectorMesh" : "PassFanSweepMesh" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        public void Rebuild(PassFanAttackVfxProfile.Sweep style, Material material, bool wind = false, float forwardScale = 1)
        {
            EnsureRenderer();
            if (runtimeMesh != null) Destroy(runtimeMesh);
            runtimeMesh = CreateGeometry(style, wind, forwardScale);
            runtimeMesh.hideFlags = HideFlags.DontSave;
            meshFilter.sharedMesh = runtimeMesh;
            meshRenderer.sharedMaterial = material;
            properties.SetFloat(RibbonShape, !wind && style.shapedRibbon ? 1f : 0f);
            properties.SetFloat(SurfaceFill, !wind && style.shapedRibbon ? style.surfaceFill : 0f);
            properties.SetFloat(TipSerration, !wind && style.shapedRibbon ? style.tipSerration : 0f);
            meshRenderer.SetPropertyBlock(properties);
        }

        public void Show(float head, float tail, Color color, float opacity, float intensity)
        {
            EnsureRenderer();
            properties.SetFloat(Head, head);
            properties.SetFloat(Tail, tail);
            properties.SetColor(Tint, color);
            properties.SetFloat(Opacity, opacity);
            properties.SetFloat(Intensity, intensity);
            meshRenderer.SetPropertyBlock(properties);
            meshRenderer.enabled = opacity > .001f;
        }

        public void SetWindBranches(int count)
        {
            EnsureRenderer();
            // 표시 갈래 수는 타격 범위를 바꾸지 않으며 두 디자인에 동일하게 전달한다.
            properties.SetFloat(BranchCount, Mathf.Clamp(count, 3, 5));
            meshRenderer.SetPropertyBlock(properties);
        }

        public void Hide() { EnsureRenderer(); meshRenderer.enabled = false; }

        public void ConformToGround(Transform owner, LayerMask groundMask, float height)
        {
            if (runtimeMesh == null) return;
            var vertices = runtimeMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = transform.TransformPoint(vertices[i]);
                int count = Physics.RaycastNonAlloc(world + Vector3.up * 12, Vector3.down, groundHits, 30,
                    groundMask, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                for (int hitIndex = 0; hitIndex < count; hitIndex++)
                {
                    var hit = groundHits[hitIndex];
                    if (owner != null && hit.transform.IsChildOf(owner) || hit.normal.y < .4f || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    world.y = hit.point.y + height;
                }
                vertices[i] = transform.InverseTransformPoint(world);
            }
            runtimeMesh.vertices = vertices;
            runtimeMesh.RecalculateBounds();
        }

        private void EnsureRenderer()
        {
            if (meshRenderer != null) return;
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            properties = new MaterialPropertyBlock();
        }

        private void OnDestroy() { if (runtimeMesh != null) Destroy(runtimeMesh); }
    }
}
