using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [DisallowMultipleComponent]
    public class PassWorldTransition : MonoBehaviour
    {
        [Header("통과 판정")]
        [Tooltip("비워 두면 Player 태그를 찾습니다. 플레이어가 없으면 아래 옵션에 따라 카메라를 사용합니다.")]
        [SerializeField] private Transform player;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Light sun;
        [SerializeField] private bool useCameraWhenPlayerMissing = true;
        [Tooltip("이 오브젝트를 통로 중앙에 두고 파란색 Z축을 어두워질 영역으로 향하게 합니다. X는 통로 폭, Y는 높이입니다.")]
        [SerializeField] private Vector2 passageSize = new Vector2(6f, 9f);
        [SerializeField, Min(0.01f)] private float crossingMargin = 0.15f;

        [Header("영계 연출")]
        [SerializeField, Min(0.01f)] private float transitionDuration = 1.5f;
        [SerializeField] private Color spiritBackground = new Color(0.12f, 0.13f, 0.25f);
        [SerializeField] private Color spiritAmbient = new Color(0.4f, 0.36f, 0.58f);
        [SerializeField] private Color spiritSun = new Color(0.64f, 0.66f, 1f);
        [SerializeField, Min(0f)] private float spiritSunIntensity = 0.85f;
        [SerializeField] private Color spiritSky = new Color(0.06f, 0.14f, 0.28f);
        [SerializeField] private Color spiritCloud = new Color(0.27f, 0.4f, 0.56f);

        private static readonly string[] SkyColorProperties =
            { "_ZenithColor", "_HorizonColor", "_CloudColor", "_SkyTint", "_Tint", "_GroundColor" };

        private Transform trackedPlayer;
        private Vector3 previousPosition;
        private bool hasPreviousPosition;
        private bool hasEnvironmentSnapshot;
        private Color originalBackground, originalFog;
        private Color originalAmbientSky, originalAmbientEquator, originalAmbientGround;
        private Color originalSunColor;
        private float originalSunIntensity;
        private float approachBrightness = 1f;
        private Material originalSky, runtimeSky;
        private readonly Color[] originalSkyColors = new Color[SkyColorProperties.Length];

        public bool IsSpiritWorld { get; private set; }
        public float SpiritAmount { get; private set; }
        public void SetSpiritWorld(bool active) => IsSpiritWorld = active;
        public void SetApproachBrightness(float brightness) => approachBrightness = Mathf.Clamp(brightness, 0.4f, 1f);

        private void OnEnable()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (sun == null) sun = RenderSettings.sun;
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }

            // 현세는 씬에 설정된 색과 밝기를 그대로 기억하므로 다른 맵에도 재사용할 수 있다.
            originalBackground = worldCamera != null ? worldCamera.backgroundColor : RenderSettings.fogColor;
            originalFog = RenderSettings.fogColor;
            originalAmbientSky = RenderSettings.ambientSkyColor;
            originalAmbientEquator = RenderSettings.ambientEquatorColor;
            originalAmbientGround = RenderSettings.ambientGroundColor;
            if (sun != null)
            {
                originalSunColor = sun.color;
                originalSunIntensity = sun.intensity;
            }

            originalSky = RenderSettings.skybox;
            if (originalSky != null)
            {
                // 하늘도 런타임 사본만 변경해 공유 머티리얼 에셋에 색이 남지 않게 한다.
                runtimeSky = new Material(originalSky);
                for (int i = 0; i < SkyColorProperties.Length; i++)
                    if (runtimeSky.HasProperty(SkyColorProperties[i]))
                        originalSkyColors[i] = runtimeSky.GetColor(SkyColorProperties[i]);
                RenderSettings.skybox = runtimeSky;
            }

            IsSpiritWorld = false;
            SpiritAmount = 0f;
            hasPreviousPosition = false;
            hasEnvironmentSnapshot = true;
        }

        private void LateUpdate()
        {
            Transform target = player != null ? player :
                useCameraWhenPlayerMissing && worldCamera != null ? worldCamera.transform : null;
            if (target != trackedPlayer)
            {
                trackedPlayer = target;
                hasPreviousPosition = false;
            }
            if (target != null) CheckGateCrossing(target.position);

            float nextAmount = Mathf.MoveTowards(SpiritAmount, IsSpiritWorld ? 1f : 0f,
                Time.deltaTime / Mathf.Max(0.01f, transitionDuration));
            SpiritAmount = nextAmount;
            ApplyEnvironment();
        }

        private void CheckGateCrossing(Vector3 position)
        {
            Vector3 current = transform.InverseTransformPoint(position);
            // 문 바로 위에서 멈추거나 작은 진동이 생겨도 전환 방향이 반복해서 뒤집히지 않게 한다.
            if (Mathf.Abs(current.z) < crossingMargin) return;
            if (hasPreviousPosition && Mathf.Sign(current.z) != Mathf.Sign(previousPosition.z))
            {
                // 이동 구간과 문 평면의 교점을 사용하므로 빠르게 지나가도 판정을 놓치지 않는다.
                float progress = previousPosition.z / (previousPosition.z - current.z);
                Vector3 crossing = Vector3.Lerp(previousPosition, current, progress);
                if (Mathf.Abs(crossing.x) <= passageSize.x * 0.5f &&
                    Mathf.Abs(crossing.y) <= passageSize.y * 0.5f)
                    IsSpiritWorld = current.z > 0f;
            }
            previousPosition = current;
            hasPreviousPosition = true;
        }

        private void ApplyEnvironment()
        {
            float brightness = Mathf.Lerp(approachBrightness, 1f, SpiritAmount);
            if (worldCamera != null)
                worldCamera.backgroundColor = Color.Lerp(originalBackground, spiritBackground, SpiritAmount);
            RenderSettings.fogColor = Color.Lerp(originalFog, spiritBackground, SpiritAmount);
            RenderSettings.ambientSkyColor = Color.Lerp(originalAmbientSky, spiritAmbient, SpiritAmount) * brightness;
            RenderSettings.ambientEquatorColor = Color.Lerp(originalAmbientEquator, spiritAmbient * 0.75f, SpiritAmount) * brightness;
            RenderSettings.ambientGroundColor = Color.Lerp(originalAmbientGround, spiritAmbient * 0.5f, SpiritAmount) * brightness;
            if (sun != null)
            {
                sun.color = Color.Lerp(originalSunColor, spiritSun, SpiritAmount);
                sun.intensity = Mathf.Lerp(originalSunIntensity, spiritSunIntensity, SpiritAmount) * brightness;
            }
            if (runtimeSky == null) return;
            for (int i = 0; i < SkyColorProperties.Length; i++)
            {
                if (!runtimeSky.HasProperty(SkyColorProperties[i])) continue;
                Color target = i == 1 || i == 5 ? spiritBackground : i == 2 ? spiritCloud : spiritSky;
                runtimeSky.SetColor(SkyColorProperties[i], Color.Lerp(originalSkyColors[i], target, SpiritAmount));
            }
        }

        private void OnDisable()
        {
            if (!hasEnvironmentSnapshot) return;
            if (worldCamera != null) worldCamera.backgroundColor = originalBackground;
            RenderSettings.fogColor = originalFog;
            RenderSettings.ambientSkyColor = originalAmbientSky;
            RenderSettings.ambientEquatorColor = originalAmbientEquator;
            RenderSettings.ambientGroundColor = originalAmbientGround;
            if (sun != null)
            {
                sun.color = originalSunColor;
                sun.intensity = originalSunIntensity;
            }
            if (runtimeSky != null)
            {
                if (RenderSettings.skybox == runtimeSky) RenderSettings.skybox = originalSky;
                Destroy(runtimeSky);
                runtimeSky = null;
            }
            hasEnvironmentSnapshot = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.65f, 1f);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(passageSize.x, passageSize.y, crossingMargin * 2f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 3f);
            Gizmos.DrawLine(Vector3.forward * 3f, new Vector3(-0.4f, 0f, 2.4f));
            Gizmos.DrawLine(Vector3.forward * 3f, new Vector3(0.4f, 0f, 2.4f));
            Gizmos.matrix = previousMatrix;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.TransformPoint(Vector3.forward * 3.2f), "영계 · 어두워지는 방향");
#endif
        }
    }
}

