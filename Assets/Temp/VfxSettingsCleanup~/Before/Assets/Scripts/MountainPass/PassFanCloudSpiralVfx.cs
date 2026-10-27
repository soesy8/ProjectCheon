using UnityEngine;
using UnityEngine.Rendering;

namespace Cheon.Prototype.MountainPass
{
    // C안의 원화를 기존 공격 프레임에 맞춰 표시한다. 피해 판정과 이동은 기존 코드가 담당한다.
    public class PassFanCloudSpiralVfx : MonoBehaviour
    {
        private PassFanAttackVfxProfile.CloudSpiral settings;
        private Transform owner;
        private MeshRenderer slash;
        private MeshRenderer windWave;
        private Mesh verticalSlashMesh;
        private float verticalLowerScale = -1;
        private MaterialPropertyBlock properties;
        private const int SweepSamples = 128;
        private Texture2D sweepFadeTexture;
        private readonly Color32[] sweepFadePixels = new Color32[SweepSamples];
        private readonly float[] sweepRevealedAt = new float[SweepSamples];
        private bool sweepAnchored;
        private Vector3 sweepPosition;
        private Quaternion sweepOrientation;
        private float previousRevealHead, previousRevealTime;
        private Texture2D windFadeTexture;
        private readonly Color32[] windFadePixels = new Color32[SweepSamples];
        private readonly float[] windRevealedAt = new float[SweepSamples];
        private bool windAnchored;
        private float previousWindHead, previousWindTime;
        public bool IsSweepVisible => slash != null && slash.enabled;
        public int VisibleWindBranches => windWave != null && windWave.enabled ? 1 : 0;

        public void Configure(PassFanAttackVfxProfile.CloudSpiral style, Transform player)
        {
            settings = style;
            owner = player;
            Hide();
        }

        public void ShowSweep(PassFanAttackVfxProfile.Sweep style, float progress, float opacity)
        {
            if (settings == null || settings.slashMesh == null || settings.slashMaterial == null || owner == null) return;
            if (slash == null) slash = CreateRenderer("PassC_Slash", settings.slashMesh, settings.slashMaterial);
            bool vertical = style.plane == PassFanAttackVfxProfile.SweepPlane.Vertical;
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
            // 수평 베기는 판정에 쓰는 전체 각도를 그대로 읽어 원화가 뒤쪽으로 더 뻗지 않게 한다.
            properties.SetFloat("_FitSweepArc", vertical ? 0 : 1);
            properties.SetFloat("_SweepArcAngle", style.angle);
            slash.SetPropertyBlock(properties);
        }

        private bool UpdateSweepFade(float head, float fadeSeconds)
        {
            if (sweepFadeTexture == null)
                sweepFadeTexture = new Texture2D(SweepSamples, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "T_FanSweepFade", hideFlags = HideFlags.DontSave,
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

        public void ShowWind(PassFanAttackVfxProfile.Wind wind, float progress, Vector3 origin, Quaternion rotation,
            Mesh rangeMesh, float opacity)
        {
            if (settings == null || rangeMesh == null || settings.windMaterial == null) return;
            if (windWave == null)
                windWave = CreateRenderer("PassC_WindWave", rangeMesh, settings.windMaterial);
            windWave.GetComponent<MeshFilter>().sharedMesh = rangeMesh;
            windWave.sharedMaterial = settings.windMaterial;
            if (!windAnchored)
            {
                previousWindHead = 0;
                previousWindTime = Time.time - Time.deltaTime;
                for (int index = 0; index < SweepSamples; index++) windRevealedAt[index] = -1;
                windAnchored = true;
            }
            // 실제 판정과 같은 부채꼴 전체를 고정하고, 원화의 가까운 부분부터 정면 끝까지 드러낸다.
            // 지면 높이와 1타의 전방 늘림도 기존 범위 메시가 이미 반영한다.
            windWave.transform.SetPositionAndRotation(origin, rotation);
            windWave.transform.localScale = Vector3.one;
            bool visible = UpdateWindFade(progress, wind.fadeSeconds);
            SetOpacity(windWave, visible ? opacity * settings.windOpacity : 0);
            properties.SetFloat("_UseWindMask", 1);
            properties.SetFloat("_WindHead", progress);
            properties.SetTexture("_WindMask", windFadeTexture);
            windWave.SetPropertyBlock(properties);
        }

        private bool UpdateWindFade(float head, float fadeSeconds)
        {
            if (windFadeTexture == null)
                windFadeTexture = new Texture2D(SweepSamples, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "T_FanWindFade", hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
            bool visible = false;
            for (int index = 0; index < SweepSamples; index++)
            {
                float along = index / (float)(SweepSamples - 1);
                if (windRevealedAt[index] < 0 && along <= head)
                {
                    float crossed = Mathf.InverseLerp(previousWindHead, head, along);
                    windRevealedAt[index] = Mathf.Lerp(previousWindTime, Time.time, crossed);
                }
                // 각 거리 구간은 드러난 시각부터 따로 흐려져 앞쪽으로 이어지는 바람 궤적이 된다.
                float age = windRevealedAt[index] < 0 ? 1 : (Time.time - windRevealedAt[index]) / Mathf.Max(.01f, fadeSeconds);
                byte alpha = (byte)Mathf.RoundToInt(255 * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(age))));
                windFadePixels[index] = new Color32(alpha, alpha, alpha, 255);
                visible |= alpha > 0;
            }
            windFadeTexture.SetPixels32(windFadePixels);
            windFadeTexture.Apply(false, false);
            previousWindHead = head;
            previousWindTime = Time.time;
            return visible;
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
            verticalSlashMesh = new Mesh { name = "PassC_VerticalSlash", hideFlags = HideFlags.DontSave };
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
            windAnchored = false;
            if (slash != null) slash.enabled = false;
            if (windWave != null) windWave.enabled = false;
        }

        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            if (verticalSlashMesh != null) Destroy(verticalSlashMesh);
            if (sweepFadeTexture != null) Destroy(sweepFadeTexture);
            if (windFadeTexture != null) Destroy(windFadeTexture);
        }
    }
}
