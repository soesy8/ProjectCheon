using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 현재 구름 바람이 사용하는 부채꼴 메시만 만든다. 표시 렌더러는 원화 제어기가 담당한다.
    public class PassFanSweepMesh : MonoBehaviour
    {
        private Mesh runtimeMesh;
        private Vector3[] flatVertices, groundVertices;
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        public Mesh Mesh => runtimeMesh;

        public void Rebuild(PassFanAttackVfxProfile.Wind wind)
        {
            if (runtimeMesh != null) Destroy(runtimeMesh);
            const int segments = 80, rings = 8;
            flatVertices = new Vector3[(segments + 1) * (rings + 1)];
            groundVertices = new Vector3[flatVertices.Length];
            var uv = new Vector2[flatVertices.Length];
            var triangles = new int[segments * rings * 6];
            for (int segment = 0; segment <= segments; segment++)
            {
                float progress = segment / (float)segments;
                float angle = (progress - .5f) * wind.angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle) * wind.forwardScale);
                for (int ring = 0; ring <= rings; ring++)
                {
                    int index = segment * (rings + 1) + ring;
                    float radial = ring / (float)rings;
                    flatVertices[index] = direction * Mathf.Lerp(wind.startRadius, wind.radius, radial) + Vector3.up * wind.height;
                    uv[index] = new Vector2(radial, progress);
                }
            }
            int triangle = 0;
            for (int segment = 0; segment < segments; segment++)
                for (int ring = 0; ring < rings; ring++)
                {
                    int first = segment * (rings + 1) + ring, next = first + rings + 1;
                    triangles[triangle++] = first; triangles[triangle++] = next; triangles[triangle++] = first + 1;
                    triangles[triangle++] = first + 1; triangles[triangle++] = next; triangles[triangle++] = next + 1;
                }
            runtimeMesh = new Mesh { name = "FanWindRange", hideFlags = HideFlags.DontSave };
            runtimeMesh.vertices = flatVertices; runtimeMesh.uv = uv; runtimeMesh.triangles = triangles;
            runtimeMesh.RecalculateBounds(); runtimeMesh.RecalculateNormals();
        }

        public void ConformToGround(Transform owner, LayerMask groundMask, float height)
        {
            if (runtimeMesh == null) return;
            for (int index = 0; index < flatVertices.Length; index++)
            {
                Vector3 world = transform.TransformPoint(flatVertices[index]);
                int count = Physics.RaycastNonAlloc(world + Vector3.up * 12f, Vector3.down, groundHits, 30f, groundMask, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                for (int hitIndex = 0; hitIndex < count; hitIndex++)
                {
                    var hit = groundHits[hitIndex];
                    if (owner != null && hit.transform.IsChildOf(owner) || hit.normal.y < .4f || hit.distance >= nearest) continue;
                    nearest = hit.distance; world.y = hit.point.y + height;
                }
                groundVertices[index] = transform.InverseTransformPoint(world);
            }
            runtimeMesh.vertices = groundVertices; runtimeMesh.RecalculateBounds();
        }
        private void OnDestroy() { if (runtimeMesh != null) Destroy(runtimeMesh); }
    }
}
