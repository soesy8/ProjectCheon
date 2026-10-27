using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    // 충돌체나 타격 처리는 만들지 않고 범위의 면과 외곽선만 그린다.
    public class PassAttackSector : MonoBehaviour
    {
        [Header("전방 중심 부채꼴 — 미터 / 전체 각도")]
        [InspectorName("공격 반경 (m)")]
        [SerializeField, Min(0.01f)] private float radius = 3f;
        [InspectorName("전체 범위 각도 (도)")]
        [SerializeField, Range(1f, 360f)] private float angle = 25f;
        [InspectorName("색상")]
        [SerializeField] private Color color = new Color(0.2f, 0.9f, 1f, 1f);
        [InspectorName("공격 범위 내부 불투명도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.2f;
        [InspectorName("공격 범위 테두리 두께 (m)")]
        [SerializeField, Min(0.005f)] private float outlineWidth = 0.035f;
        [InspectorName("공격 범위 표시 머티리얼")]
        [SerializeField] private Material rangeMaterial;
        [Header("바닥 높이에 맞추기")]
        [InspectorName("플레이어 기준 위치")]
        [SerializeField] private Transform playerRoot;
        [InspectorName("지면 판정 레이어")]
        [SerializeField] private LayerMask groundMask = ~0;
        [InspectorName("지면 위 표시 높이 (m)")]
        [SerializeField, Min(0.01f)] private float groundOffset = 0.06f;
        [InspectorName("지면 탐색 시작 높이 (m)")]
        [SerializeField, Min(0.1f)] private float groundRayHeight = 12f;
        [InspectorName("지면 탐색 깊이 (m)")]
        [SerializeField, Min(0.1f)] private float groundRayDepth = 30f;

        private Mesh rangeMesh;
        private MeshRenderer fillRenderer;
        private LineRenderer outline;
        private MaterialPropertyBlock properties;
        private readonly RaycastHit[] groundHits = new RaycastHit[32];
        private Matrix4x4 previousMatrix;
        private float previousRadius, previousAngle;
        private Color previousColor;
        private bool geometryReady;
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");

        public float Radius => radius;
        public float Angle => angle;
        public bool IsVisible => fillRenderer != null && fillRenderer.enabled;

        public void Configure(Transform owner, Material material, float distance, float degrees, Color tint)
        {
            playerRoot = owner;
            rangeMaterial = material;
            radius = distance;
            angle = degrees;
            color = tint;
            geometryReady = false;
        }

        public void Show(float opacity)
        {
            if (!isActiveAndEnabled || rangeMaterial == null) { Hide(); return; }
            EnsureRenderers();
            if (!geometryReady || previousMatrix != transform.localToWorldMatrix ||
                previousRadius != radius || previousAngle != angle || previousColor != color)
                RebuildGeometry();

            properties.SetFloat(Opacity, Mathf.Clamp01(opacity));
            fillRenderer.SetPropertyBlock(properties);
            outline.SetPropertyBlock(properties);
            outline.widthMultiplier = outlineWidth;
            fillRenderer.enabled = outline.enabled = true;
        }

        public void Hide()
        {
            if (fillRenderer != null) fillRenderer.enabled = false;
            if (outline != null) outline.enabled = false;
            geometryReady = false;
        }

        private void EnsureRenderers()
        {
            if (rangeMesh != null) return;
            rangeMesh = new Mesh { name = "Test_AttackRangeRuntimeMesh", hideFlags = HideFlags.DontSave };
            rangeMesh.MarkDynamic();
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = rangeMesh;
            fillRenderer = gameObject.AddComponent<MeshRenderer>();
            outline = gameObject.AddComponent<LineRenderer>();
            foreach (var renderer in new Renderer[] { fillRenderer, outline })
            {
                renderer.sharedMaterial = rangeMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
            }
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = outlineWidth;
            outline.numCornerVertices = 3;
            outline.numCapVertices = 3;
            properties = new MaterialPropertyBlock();
        }

        private void RebuildGeometry()
        {
            float distance = Mathf.Max(0.01f, radius);
            float degrees = Mathf.Clamp(angle, 1f, 360f);
            int segments = Mathf.Max(4, Mathf.CeilToInt(degrees / 4f));
            int rings = Mathf.Max(1, Mathf.CeilToInt(distance / 0.75f));
            int stride = segments + 1;
            var vertices = new Vector3[1 + rings * stride];
            var colors = new Color[vertices.Length];
            var triangles = new int[(segments + (rings - 1) * segments * 2) * 3];
            var boundary = new Vector3[2 * rings + segments];
            Color fill = new Color(color.r, color.g, color.b, fillAlpha);
            Vector3 origin = transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            vertices[0] = GroundPoint(origin);
            colors[0] = fill;
            int triangle = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                float ringRadius = distance * (ring + 1) / rings;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float radians = Mathf.Lerp(-degrees * 0.5f, degrees * 0.5f, (float)segment / segments) * Mathf.Deg2Rad;
                    int vertex = 1 + ring * stride + segment;
                    Vector3 point = origin + (right * Mathf.Sin(radians) + forward * Mathf.Cos(radians)) * ringRadius;
                    vertices[vertex] = GroundPoint(point);
                    colors[vertex] = fill;
                    if (segment == segments) continue;
                    if (ring == 0)
                    {
                        triangles[triangle++] = 0;
                        triangles[triangle++] = vertex;
                        triangles[triangle++] = vertex + 1;
                    }
                    else
                    {
                        int inner = vertex - stride;
                        triangles[triangle++] = inner;
                        triangles[triangle++] = vertex;
                        triangles[triangle++] = vertex + 1;
                        triangles[triangle++] = inner;
                        triangles[triangle++] = vertex + 1;
                        triangles[triangle++] = inner + 1;
                    }
                }
            }
            // 양쪽 직선에도 높이 표본을 넣어 계단과 굴곡에서 외곽선이 바닥에 묻히지 않게 한다.
            int edge = 0;
            boundary[edge++] = vertices[0];
            for (int ring = 0; ring < rings; ring++) boundary[edge++] = vertices[1 + ring * stride];
            for (int segment = 1; segment <= segments; segment++)
                boundary[edge++] = vertices[1 + (rings - 1) * stride + segment];
            for (int ring = rings - 2; ring >= 0; ring--)
                boundary[edge++] = vertices[1 + ring * stride + segments];
            rangeMesh.Clear();
            rangeMesh.vertices = vertices;
            rangeMesh.colors = colors;
            rangeMesh.triangles = triangles;
            rangeMesh.RecalculateBounds();
            outline.positionCount = boundary.Length;
            outline.SetPositions(boundary);
            outline.startColor = outline.endColor = color;
            previousMatrix = transform.localToWorldMatrix;
            previousRadius = radius;
            previousAngle = angle;
            previousColor = color;
            geometryReady = true;
        }

        private Vector3 GroundPoint(Vector3 point)
        {
            // 수평 거리와 각도는 유지하고 높이만 투영해 경사에서도 실제 범위를 읽을 수 있다.
            int count = Physics.RaycastNonAlloc(point + Vector3.up * groundRayHeight, Vector3.down,
                groundHits, groundRayHeight + groundRayDepth, groundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;
                if (hit.normal.y < 0.4f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                point.y = hit.point.y;
            }
            return transform.InverseTransformPoint(point + Vector3.up * groundOffset);
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (rangeMesh != null) Destroy(rangeMesh);
        }
    }
}

