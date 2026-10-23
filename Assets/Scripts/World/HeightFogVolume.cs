using UnityEngine;

namespace Cheon.World
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cheon/VFX/Height Fog Volume")]
    public class HeightFogVolume : MonoBehaviour
    {
        [Header("안개 영역 (Transform Y = 안개 상한 높이)")]
        [Tooltip("월드 X/Z 방향의 안개 영역 크기입니다. 상한 높이는 이 오브젝트의 Y 위치로 조절합니다.")]
        [SerializeField] private Vector2 areaSize = new Vector2(1000f, 1120f);
        [Tooltip("상한 높이에서 아래로 안개가 채워지는 깊이입니다.")]
        [SerializeField, Min(1f)] private float volumeDepth = 110f;
        [Tooltip("상한선 아래에서 안개가 서서히 짙어지는 높이입니다.")]
        [SerializeField, Min(0.1f)] private float heightFade = 10f;
        [SerializeField, Min(0.1f)] private float edgeFade = 180f;

        [Header("안개 색상과 농도")]
        [SerializeField] private Color fogColor = new Color(0.76f, 0.83f, 0.85f, 1f);
        [SerializeField, Range(0f, 0.3f)] private float density = 0.065f;
        [SerializeField, Range(0f, 1f)] private float maxOpacity = 0.96f;

        [Header("흐르는 안개 무늬")]
        [Tooltip("값이 작을수록 안개 덩어리가 커집니다.")]
        [SerializeField, Min(0.001f)] private float noiseScale = 0.035f;
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.65f;
        [Tooltip("월드 X/Z 방향으로 안개 무늬가 흐르는 속도입니다 (초당 미터).")]
        [SerializeField] private Vector2 windVelocity = new Vector2(0.7f, 0.3f);

        [Header("렌더링 참조")]
        [SerializeField] private MeshRenderer volumeRenderer;

        private MaterialPropertyBlock properties;

        private static readonly int VolumeMinId = Shader.PropertyToID("_VolumeMin");
        private static readonly int VolumeMaxId = Shader.PropertyToID("_VolumeMax");
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int HeightFadeId = Shader.PropertyToID("_HeightFade");
        private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
        private static readonly int MaxOpacityId = Shader.PropertyToID("_MaxOpacity");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int WindVelocityId = Shader.PropertyToID("_WindVelocity");

        private void OnEnable()
        {
            ApplySettings();
        }

        private void Update()
        {
            // 편집 모드에서도 이동과 Inspector 수정을 바로 미리 볼 수 있게 한다.
            ApplySettings();
        }

        private void OnValidate()
        {
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            volumeDepth = Mathf.Max(1f, volumeDepth);
            heightFade = Mathf.Clamp(heightFade, 0.1f, volumeDepth);
            edgeFade = Mathf.Max(0.1f, edgeFade);
            noiseScale = Mathf.Max(0.001f, noiseScale);
        }

        public void ApplySettings()
        {
            if (volumeRenderer == null) return;

            Vector3 size = new Vector3(areaSize.x, volumeDepth, areaSize.y);
            Vector3 center = transform.position - Vector3.up * (volumeDepth * 0.5f);
            Transform volumeTransform = volumeRenderer.transform;

            // 안개 높이는 항상 월드 Y를 기준으로 한다. 부모의 스케일과 회전으로 높이가 틀어지는 것을 막는다.
            if (volumeTransform.position != center || volumeTransform.rotation != Quaternion.identity)
                volumeTransform.SetPositionAndRotation(center, Quaternion.identity);
            Vector3 parentScale = volumeTransform.parent == null ? Vector3.one : volumeTransform.parent.lossyScale;
            Vector3 localSize = new Vector3(
                size.x / SafeScale(parentScale.x),
                size.y / SafeScale(parentScale.y),
                size.z / SafeScale(parentScale.z));
            if (volumeTransform.localScale != localSize) volumeTransform.localScale = localSize;

            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetVector(VolumeMinId, center - size * 0.5f);
            properties.SetVector(VolumeMaxId, center + size * 0.5f);
            properties.SetColor(FogColorId, QualitySettings.activeColorSpace == ColorSpace.Linear ? fogColor.linear : fogColor);
            properties.SetFloat(DensityId, density);
            properties.SetFloat(HeightFadeId, heightFade);
            properties.SetFloat(EdgeFadeId, edgeFade);
            properties.SetFloat(MaxOpacityId, maxOpacity);
            properties.SetFloat(NoiseScaleId, noiseScale);
            properties.SetFloat(NoiseStrengthId, noiseStrength);
            properties.SetVector(WindVelocityId, new Vector4(windVelocity.x, windVelocity.y, 0f, 0f));
            volumeRenderer.SetPropertyBlock(properties);
            volumeRenderer.enabled = isActiveAndEnabled;
        }

        private static float SafeScale(float scale)
        {
            return Mathf.Abs(scale) < 0.001f ? 0.001f : scale;
        }

        private void OnDisable()
        {
            if (volumeRenderer != null) volumeRenderer.enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 size = new Vector3(areaSize.x, volumeDepth, areaSize.y);
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 0.5f);
            Gizmos.DrawWireCube(transform.position - Vector3.up * (volumeDepth * 0.5f), size);
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 1f);
            Gizmos.DrawWireCube(transform.position, new Vector3(areaSize.x, 0f, areaSize.y));
        }
    }
}
