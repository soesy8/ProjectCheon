using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class PassStairCloudVeil : MonoBehaviour
    {
        [Header("기존 계단 안개와 연결")]
        [SerializeField, InspectorName("게임 진행 제어기")] private PassGameFlow game;
        [SerializeField, InspectorName("산길 안개 제어기")] private PassMountainMist mountainMist;
        [SerializeField, InspectorName("구름 이미지")] private Texture2D cloudTexture;
        [SerializeField, InspectorName("구름 머티리얼")] private Material cloudMaterial;

        [Header("구름 모양과 농도")]
        [SerializeField, Range(0f, 1f), InspectorName("최대 불투명도")] private float maximumOpacity = 0.22f;
        [SerializeField, Range(0f, 0.25f), InspectorName("구름 안쪽 배치 비율")] private float inwardAmount = 0.1f;
        [SerializeField, InspectorName("구름 색상")] private Color cloudTint = new Color(0.94f, 0.98f, 1f, 1f);
        [SerializeField, InspectorName("중앙 비움 반경 (화면 비율)")] private Vector2 clearRadius = new Vector2(0.24f, 0.28f);
        [SerializeField, Range(0.05f, 1f), InspectorName("중앙 경계 부드러움")] private float clearFeather = 0.65f;
        [SerializeField, Range(0f, 8f), InspectorName("구름 흐림 폭 (텍셀)")] private float blurRadius = 3f;
        [SerializeField, Range(0f, 0.8f), InspectorName("걷힐 때 좌우 이동 거리 (화면 비율)")] private float openingDistance = 0.5f;
        [SerializeField, Range(0f, 0.05f), InspectorName("잔잔한 흐름 폭 (화면 비율)")] private float driftAmount = 0.008f;
        [SerializeField, Min(0f), InspectorName("잔잔한 흐름 속도")] private float driftSpeed = 0.24f;

        [Header("겹치는 구름의 불규칙함")]
        [SerializeField, Range(1, 5), InspectorName("가장자리별 구름 겹 수")] private int layersPerCloud = 3;
        [SerializeField, Range(1, 3), InspectorName("모바일 가장자리별 구름 겹 수")] private int mobileLayersPerCloud = 2;
        [SerializeField, InspectorName("배치 무작위 시드")] private int randomSeed = 1739;
        [SerializeField, InspectorName("겹치는 구름 위치 변화 (화면 비율)")] private Vector2 layerSpread = new Vector2(0.09f, 0.12f);
        [SerializeField, Range(0f, 0.5f), InspectorName("겹치는 구름 크기 변화")] private float sizeVariation = 0.24f;
        [SerializeField, Range(0f, 45f), InspectorName("겹치는 구름 각도 변화")] private float angleVariation = 20f;
        [SerializeField, InspectorName("겹별 불투명도 범위")] private Vector2 layerOpacityRange = new Vector2(0.5f, 0.9f);
        [SerializeField, Range(0f, 1f), InspectorName("불규칙한 투명도 강도")] private float noiseStrength = 0.9f;
        [SerializeField, Min(0.1f), InspectorName("투명도 노이즈 무늬 크기")] private float noiseScale = 5.2f;
        [SerializeField, Range(0f, 0.2f), InspectorName("투명도 노이즈 흐름 속도")] private float noiseDriftSpeed = 0.045f;

        private static readonly Vector2[] CloudCenters =
        {
            new Vector2(-0.025f, 0.72f), new Vector2(1.035f, 0.69f),
            new Vector2(0.015f, 0.19f), new Vector2(0.99f, 0.17f),
            new Vector2(0.43f, 1.09f), new Vector2(0.55f, -0.1f)
        };
        private static readonly float[] CloudWidths = { 0.72f, 0.78f, 0.8f, 0.74f, 1.08f, 1.04f };
        private static readonly float[] CloudAngles = { -12f, 14f, 8f, -8f, 180f, 0f };
        private static readonly int ClearRadiusId = Shader.PropertyToID("_ClearRadius");
        private static readonly int ClearFeatherId = Shader.PropertyToID("_ClearFeather");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseTimeId = Shader.PropertyToID("_NoiseTime");

        // 플레이 중 에디터가 재컴파일되어도 구름별 배치와 UI 참조를 복원한다.
        [System.Serializable]
        private struct CloudLayer
        {
            public RectTransform Rect;
            public UnityEngine.UI.RawImage Image;
            public Vector2 Center;
            public float Width;
            public float Opacity;
            public float DriftPhase;
            public float DriftSpeed;
        }

        private Canvas effectCanvas;
        private CanvasGroup visibility;
        private RectTransform canvasRect;
        private CloudLayer[] clouds;
        private Material runtimeMaterial;
        private float driftTime;
        private float noiseTime;
        private int activeLayerCount;
        private Vector2 previousCanvasSize;

        private void Start()
        {
            if (game == null || mountainMist == null || cloudTexture == null || cloudMaterial == null) return;
            runtimeMaterial = new Material(cloudMaterial);
            bool mobile = Cheon.Systems.MobilePerformance.UsesMobileQuality;
            runtimeMaterial.SetFloat("_MobileQuality", mobile ? 1f : 0f);
            var root = new GameObject("StairCloudVeil", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            canvasRect = (RectTransform)root.transform;
            effectCanvas = root.GetComponent<Canvas>();
            // 공간 안개를 그린 뒤 합성하되 HUD(정렬 순서 30)보다 뒤에서 그린다.
            effectCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            effectCanvas.sortingOrder = -100;
            visibility = root.GetComponent<CanvasGroup>();
            visibility.alpha = 0f;
            visibility.interactable = false;
            visibility.blocksRaycasts = false;

            // 전역 Random 상태를 건드리지 않아 전투·효과음의 무작위 선택에 영향을 주지 않는다.
            var random = new System.Random(randomSeed);
            activeLayerCount = Mathf.Clamp(mobile ? Mathf.Min(mobileLayersPerCloud, layersPerCloud) : layersPerCloud, 1, 5);
            clouds = new CloudLayer[CloudCenters.Length * activeLayerCount];
            for (int i = 0; i < clouds.Length; i++)
            {
                int group = i % CloudCenters.Length;
                bool firstLayer = i < CloudCenters.Length;
                Vector2 offset = firstLayer ? Vector2.zero : new Vector2(
                    Mathf.Lerp(-layerSpread.x, layerSpread.x, (float)random.NextDouble()),
                    Mathf.Lerp(-layerSpread.y, layerSpread.y, (float)random.NextDouble()));
                float widthScale = firstLayer ? 1f : Mathf.Lerp(1f - sizeVariation, 1f + sizeVariation, (float)random.NextDouble());
                float angle = CloudAngles[group] + (firstLayer ? 0f : Mathf.Lerp(-angleVariation, angleVariation, (float)random.NextDouble()));
                var item = new GameObject("Cloud" + (i + 1), typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                var rect = (RectTransform)item.transform;
                rect.SetParent(canvasRect, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
                var image = item.GetComponent<UnityEngine.UI.RawImage>();
                image.texture = cloudTexture;
                image.material = runtimeMaterial;
                image.raycastTarget = false;
                if ((i & 1) != 0) image.uvRect = new Rect(1f, 0f, -1f, 1f);
                clouds[i] = new CloudLayer
                {
                    Rect = rect,
                    Image = image,
                    Center = CloudCenters[group] + offset,
                    Width = CloudWidths[group] * widthScale,
                    Opacity = Mathf.Lerp(layerOpacityRange.x, layerOpacityRange.y, (float)random.NextDouble()),
                    DriftPhase = (float)random.NextDouble() * Mathf.PI * 2f,
                    DriftSpeed = Mathf.Lerp(0.7f, 1.3f, (float)random.NextDouble())
                };
            }
        }

        private void LateUpdate()
        {
            if (effectCanvas == null) return;
            bool show = mountainMist.isActiveAndEnabled && !game.InEncounter && game.State != PassGameFlow.RunState.Intro;
            if (!show)
            {
                visibility.alpha = 0f;
                effectCanvas.enabled = false;
                return;
            }
            // 공간 안개와 같은 위치 진행값을 사용해 정지·하강·시작 페이드에도 농도가 어긋나지 않게 한다.
            float openingProgress = mountainMist.SummitClarity;
            // 겹 수를 줄여도 구름의 전체 농도가 크게 달라지지 않도록 합성 불투명도를 보정한다.
            float opacity = 1f - Mathf.Pow(1f - maximumOpacity, layersPerCloud / (float)activeLayerCount);
            visibility.alpha = opacity * (1f - openingProgress);
            effectCanvas.enabled = visibility.alpha > 0.001f;
            if (!effectCanvas.enabled) return;

            runtimeMaterial.SetVector(ClearRadiusId, new Vector4(clearRadius.x, clearRadius.y, 0f, 0f));
            runtimeMaterial.SetFloat(ClearFeatherId, clearFeather);
            runtimeMaterial.SetFloat(BlurRadiusId, blurRadius);
            runtimeMaterial.SetFloat(NoiseStrengthId, noiseStrength);
            runtimeMaterial.SetFloat(NoiseScaleId, noiseScale);
            driftTime += Time.deltaTime * driftSpeed;
            noiseTime += Time.deltaTime * noiseDriftSpeed;
            runtimeMaterial.SetFloat(NoiseTimeId, noiseTime);
            Vector2 size = canvasRect.rect.size;
            bool resized = size != previousCanvasSize;
            previousCanvasSize = size;
            float textureAspect = (float)cloudTexture.width / cloudTexture.height;
            float opening = openingProgress * openingDistance;
            for (int i = 0; i < clouds.Length; i++)
            {
                CloudLayer layer = clouds[i];
                Vector2 center = Vector2.Lerp(layer.Center, new Vector2(0.5f, 0.5f), inwardAmount);
                // 위아래 구름도 가까운 화면 측면으로 밀어, 전체가 좌우로 스르륵 걷히게 한다.
                center.x += (layer.Center.x < 0.5f ? -1f : 1f) * opening;
                float phase = driftTime * layer.DriftSpeed + layer.DriftPhase;
                center += new Vector2(Mathf.Sin(phase), Mathf.Cos(phase * 0.7f)) * driftAmount;
                layer.Rect.anchoredPosition = Vector2.Scale(center - new Vector2(0.5f, 0.5f), size);
                float width = size.x * layer.Width;
                if (resized) layer.Rect.sizeDelta = new Vector2(width, width / textureAspect);
                Color tint = new Color(cloudTint.r, cloudTint.g, cloudTint.b, cloudTint.a * layer.Opacity);
                if (layer.Image.color != tint) layer.Image.color = tint;
            }
        }

        private void OnDisable()
        {
            if (visibility != null) visibility.alpha = 0f;
            if (effectCanvas != null) effectCanvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (effectCanvas != null) Destroy(effectCanvas.gameObject);
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
