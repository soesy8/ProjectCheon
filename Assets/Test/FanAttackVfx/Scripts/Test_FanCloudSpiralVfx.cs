using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Tests.FanAttackVfx
{
    // C안의 원화를 기존 공격 프레임에 맞춰 표시한다. 피해 판정과 이동은 기존 코드가 담당한다.
    public class Test_FanCloudSpiralVfx : MonoBehaviour
    {
        private Test_FanAttackVfxProfile.CloudSpiral settings;
        private Transform owner;
        private MeshRenderer slash;
        private readonly MeshRenderer[] gusts = new MeshRenderer[5];
        private Mesh verticalSlashMesh;
        private float verticalLowerScale = -1;
        private MaterialPropertyBlock properties;
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        private const int SweepSamples = 128;
        private Texture2D sweepFadeTexture;
        private readonly Color32[] sweepFadePixels = new Color32[SweepSamples];
        private readonly float[] sweepRevealedAt = new float[SweepSamples];
        private bool sweepAnchored;
        private Vector3 sweepPosition;
        private Quaternion sweepOrientation;
        private float previousRevealHead, previousRevealTime;
        public bool IsSweepVisible => slash != null && slash.enabled;
        public int VisibleWindBranches
        {
            get
            {
                int count = 0;
                foreach (var gust in gusts) if (gust != null && gust.enabled) count++;
                return count;
            }
        }

        public void Configure(Test_FanAttackVfxProfile.CloudSpiral style, Transform player)
        {
            settings = style;
            owner = player;
            Hide();
        }

        public void ShowSweep(Test_FanAttackVfxProfile.Sweep style, float progress, float opacity)
        {
            if (settings == null || settings.slashMesh == null || settings.slashMaterial == null || owner == null) return;
            if (slash == null) slash = CreateRenderer("Test_C_Slash", settings.slashMesh, settings.slashMaterial);
            bool vertical = style.plane == Test_FanAttackVfxProfile.SweepPlane.Vertical;
            slash.GetComponent<MeshFilter>().sharedMesh = vertical ? GetVerticalMesh(style.lowerArcHeightScale) : settings.slashMesh;
            slash.sharedMaterial = settings.slashMaterial;
            if (!sweepAnchored)
            {
                sweepPosition = owner.position + Vector3.up * (vertical
                    ? style.pivotHeight + style.heightOffset
                    : style.height.Evaluate(progress) * style.heightMultiplier + style.heightOffset);
                sweepOrientation = owner.rotation * Quaternion.Euler(0, style.yawOffset, vertical ? style.roll - 90 : 0);
                previousRevealHead = 0;
                previousRevealTime = Time.time - Time.deltaTime;
                for (int index = 0; index < SweepSamples; index++) sweepRevealedAt[index] = -1;
                sweepAnchored = true;
            }
            // 부모가 플레이어를 따라 움직여도 이미 그린 원화의 월드 위치·방향·크기는 고정한다.
            slash.transform.SetPositionAndRotation(sweepPosition, sweepOrientation);
            slash.transform.localScale = Vector3.one * (style.radius * 2 * settings.sweepScale);
            bool visible = UpdateSweepFade(progress, style.fadeSeconds);
            SetOpacity(slash, visible ? opacity * settings.sweepOpacity : 0);
            properties.SetFloat("_UseSweepMask", 1);
            properties.SetFloat("_SweepHead", progress);
            properties.SetFloat("_SweepReverse", style.reverse ? 1 : 0);
            properties.SetTexture("_SweepMask", sweepFadeTexture);
            slash.SetPropertyBlock(properties);
        }

        private bool UpdateSweepFade(float head, float fadeSeconds)
        {
            if (sweepFadeTexture == null)
                sweepFadeTexture = new Texture2D(SweepSamples, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "Test_C_SweepFade", hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
            bool visible = false;
            for (int index = 0; index < SweepSamples; index++)
            {
                float along = index / (float)(SweepSamples - 1);
                if (sweepRevealedAt[index] < 0 && along <= head)
                {
                    float crossed = Mathf.InverseLerp(previousRevealHead, head, along);
                    sweepRevealedAt[index] = Mathf.Lerp(previousRevealTime, Time.time, crossed);
                }
                // 각 부분이 실제로 나타난 시각부터 따로 흐려져, 부채가 지나간 자리에 잔상이 남는다.
                float age = sweepRevealedAt[index] < 0 ? 1 : (Time.time - sweepRevealedAt[index]) / Mathf.Max(.01f, fadeSeconds);
                byte alpha = (byte)Mathf.RoundToInt(255 * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(age))));
                sweepFadePixels[index] = new Color32(alpha, alpha, alpha, 255);
                visible |= alpha > 0;
            }
            sweepFadeTexture.SetPixels32(sweepFadePixels);
            sweepFadeTexture.Apply(false, false);
            previousRevealHead = head;
            previousRevealTime = Time.time;
            return visible;
        }

        public void ShowWind(Test_FanAttackVfxProfile.Wind wind, float progress, Vector3 origin, Quaternion rotation,
            LayerMask groundMask, float opacity)
        {
            if (settings == null || settings.windMesh == null || settings.windMaterial == null) return;
            int count = Mathf.Clamp(wind.branchCount, 3, gusts.Length);
            for (int index = 0; index < gusts.Length; index++)
            {
                if (index >= count) { if (gusts[index] != null) gusts[index].enabled = false; continue; }
                if (gusts[index] == null)
                    gusts[index] = CreateRenderer("Test_C_Wind" + (index + 1), settings.windMesh, settings.windMaterial);
                var gust = gusts[index];
                gust.sharedMaterial = settings.windMaterial;
                float fraction = count == 1 ? .5f : index / (float)(count - 1);
                float angle = Mathf.Lerp(-wind.angle * .5f, wind.angle * .5f, fraction);
                Vector3 direction = wind.StretchDirection(Quaternion.Euler(0, angle, 0) * Vector3.forward);
                float distance = Mathf.Lerp(wind.startRadius, wind.radius, progress);
                Vector3 head = origin + rotation * direction * distance;
                Vector3 forward = rotation * direction.normalized;
                float length = Mathf.Min(settings.windLength, wind.radius * .65f) * Mathf.Lerp(.75f, 1, progress);
                // 원화의 오른쪽 구름 머리가 기존 타격 범위의 선두를 넘지 않게 중심을 뒤로 옮긴다.
                length = Mathf.Min(length, Mathf.Max(.1f, distance * direction.magnitude - .1f));
                Vector3 center = head - forward * (length * .48f);
                center.y = origin.y + wind.height;
                if (wind.conformToGround) SetGroundHeight(ref center, groundMask, wind.height);
                gust.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
                gust.transform.localScale = Vector3.one * length;
                SetOpacity(gust, opacity * settings.windOpacity * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .08f, progress)));
            }
        }

        private void SetGroundHeight(ref Vector3 center, LayerMask mask, float height)
        {
            int count = Physics.RaycastNonAlloc(center + Vector3.up * 12, Vector3.down, groundHits, 30,
                mask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                var hit = groundHits[index];
                if (hit.normal.y < .4f || hit.distance >= nearest || owner != null && hit.transform.IsChildOf(owner)) continue;
                center.y = hit.point.y + height;
                nearest = hit.distance;
            }
        }

        private Mesh GetVerticalMesh(float lowerScale)
        {
            if (verticalSlashMesh != null && Mathf.Abs(verticalLowerScale - lowerScale) < .001f) return verticalSlashMesh;
            if (verticalSlashMesh != null) Destroy(verticalSlashMesh);
            verticalLowerScale = lowerScale;
            const int divisions = 12;
            var vertices = new Vector3[(divisions + 1) * (divisions + 1)];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[divisions * divisions * 6];
            int triangle = 0;
            for (int row = 0; row <= divisions; row++)
                for (int column = 0; column <= divisions; column++)
                {
                    int index = row * (divisions + 1) + column;
                    float u = column / (float)divisions;
                    float v = row / (float)divisions;
                    float across = .5f - v;
                    // 세로 베기의 아래쪽만 줄여 구름 문양이 바닥 아래로 잘리지 않게 한다.
                    if (across > 0) across *= lowerScale;
                    vertices[index] = new Vector3(across, 0, u - .5f);
                    uv[index] = new Vector2(u, v);
                    colors[index] = Color.white;
                    if (row == divisions || column == divisions) continue;
                    int next = index + divisions + 1;
                    triangles[triangle++] = index; triangles[triangle++] = index + 1; triangles[triangle++] = next;
                    triangles[triangle++] = next; triangles[triangle++] = index + 1; triangles[triangle++] = next + 1;
                }
            verticalSlashMesh = new Mesh { name = "Test_C_VerticalSlash", hideFlags = HideFlags.DontSave };
            verticalSlashMesh.vertices = vertices;
            verticalSlashMesh.uv = uv;
            verticalSlashMesh.colors = colors;
            verticalSlashMesh.triangles = triangles;
            verticalSlashMesh.RecalculateBounds();
            verticalSlashMesh.RecalculateNormals();
            return verticalSlashMesh;
        }

        private MeshRenderer CreateRenderer(string title, Mesh mesh, Material material)
        {
            var item = new GameObject(title, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(transform, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        private void SetOpacity(MeshRenderer renderer, float opacity)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.Clear();
            properties.SetColor("_BaseColor", new Color(1, 1, 1, Mathf.Clamp01(opacity)));
            renderer.SetPropertyBlock(properties);
            renderer.enabled = opacity > .001f;
        }

        public void Hide()
        {
            sweepAnchored = false;
            if (slash != null) slash.enabled = false;
            foreach (var gust in gusts) if (gust != null) gust.enabled = false;
        }

        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            if (verticalSlashMesh != null) Destroy(verticalSlashMesh);
            if (sweepFadeTexture != null) Destroy(sweepFadeTexture);
        }
    }
}
