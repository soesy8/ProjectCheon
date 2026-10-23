using System;
using UnityEngine;

namespace Cheon.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
    public class NoiseTerrainGenerator : MonoBehaviour
    {
        [Header("맵 크기와 시드")]
        [SerializeField] private Vector2 mapSize = new Vector2(100f, 100f);
        [SerializeField] private int seed = 1022;
        [SerializeField] private int heightmapResolution = 257;

        [Header("산봉우리와 평평한 정상")]
        [SerializeField, Min(0f)] private float baseHeight = 2f;
        [SerializeField, Min(1f)] private float mountainHeight = 28f;
        [SerializeField] private Vector2 summitCenter = new Vector2(.55f, .69f);
        [SerializeField, Min(1f)] private float summitRadius = 11f;
        [SerializeField, Min(.1f)] private float summitBlendWidth = 3f;

        [Header("능선과 등산로")]
        [SerializeField, Min(1f)] private float ridgeWidth = 24f;
        [SerializeField, Min(.5f)] private float pathWidth = 4.5f;
        [SerializeField, Min(.1f)] private float pathShoulderWidth = 2f;

        [Header("산 표면 노이즈")]
        [SerializeField, Min(1f)] private float noiseScale = 24f;
        [SerializeField, Min(0f)] private float noiseStrength = 5f;
        [SerializeField, Range(1, 6)] private int octaves = 4;
        [SerializeField, Range(0f, 1f)] private float persistence = .45f;

        public Vector2 MapSize => mapSize;
        public int Seed => seed;
        public Vector2 SummitPosition => Vector2.Scale(summitCenter, mapSize);
        public float SummitHeight => baseHeight + mountainHeight;
        public float SummitRadius => summitRadius;
        public float PathWidth => pathWidth;

        // 생성 버튼을 눌렀을 때만 높이를 갱신해 수작업 수정 중에는 지형을 보존한다.
        public void Generate()
        {
            ValidateSettings();
            Terrain terrain = GetComponent<Terrain>();
            TerrainData data = terrain.terrainData;
            if (data == null) throw new InvalidOperationException("Terrain Data를 먼저 지정해주세요.");

            var random = new System.Random(seed);
            var offsets = new Vector2[octaves];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = new Vector2(random.Next(-10000, 10000), random.Next(-10000, 10000));
            Vector2[] route = GetAscentRoute();
            float routeLength = GetRouteLength(route);
            data.heightmapResolution = heightmapResolution;
            float verticalSize = SummitHeight + noiseStrength + 2f;
            data.size = new Vector3(mapSize.x, verticalSize, mapSize.y);
            var heights = new float[heightmapResolution, heightmapResolution];

            for (int z = 0; z < heightmapResolution; z++)
                for (int x = 0; x < heightmapResolution; x++)
                {
                    var position = new Vector2(x * mapSize.x / (heightmapResolution - 1), z * mapSize.y / (heightmapResolution - 1));
                    float progress;
                    float distance = GetRouteDistance(position, route, routeLength, out progress);
                    float ascent = GetAscentHeight(progress);
                    Vector2 delta = position - SummitPosition;
                    float radial = new Vector2(delta.x / (mapSize.x * .46f), delta.y / (mapSize.y * .36f)).magnitude;
                    // 기본 산 모양에도 정상 폭을 반영해 평탄화 경계가 수직 절벽이 되지 않게 한다.
                    float peak = mountainHeight * Mathf.Clamp01((1f - radial) / .74f);
                    float ridge = (ascent - baseHeight) * Mathf.Pow(Mathf.Clamp01(1f - distance / ridgeWidth), 1.25f);
                    float body = Mathf.Max(peak, ridge);
                    float noise = (SampleNoise(position, offsets) - .5f) * noiseStrength;
                    float height = baseHeight + Mathf.Max(0f, body + noise * Mathf.Clamp01(body / 8f));
                    float trail = 1f - SmoothRange(pathWidth * .5f, pathWidth * .5f + pathShoulderWidth, distance);
                    height = Mathf.Lerp(height, ascent, trail);
                    // 정상은 마지막에 보정하므로 노이즈와 등산로가 평지를 울퉁불퉁하게 만들지 않는다.
                    height = Mathf.Lerp(height, SummitHeight, GetSummitMask(position));
                    heights[z, x] = Mathf.Clamp01(height / verticalSize);
                }

            data.SetHeights(0, 0, heights);
            PaintTerrain(data, route, routeLength);
            GetComponent<TerrainCollider>().terrainData = data;
            terrain.Flush();
            var scenery = GetComponent<MountainSceneryGenerator>();
            if (scenery != null) scenery.Generate();
        }

        public void SetSeed(int value) { seed = value; }

        public Vector2[] GetAscentRoute()
        {
            Vector2[] offsets = {
                new Vector2(-.35f, -.61f), new Vector2(-.29f, -.46f),
                new Vector2(-.15f, -.37f), new Vector2(-.17f, -.23f),
                new Vector2(-.03f, -.13f), Vector2.zero
            };
            var route = new Vector2[offsets.Length];
            for (int i = 0; i < route.Length; i++)
            {
                route[i] = SummitPosition + Vector2.Scale(offsets[i], mapSize);
                route[i] = new Vector2(Mathf.Clamp(route[i].x, 3f, mapSize.x - 3f), Mathf.Clamp(route[i].y, 3f, mapSize.y - 3f));
            }
            // 제어점 사이를 곡선으로 이어 능선과 돌담이 꺾이는 부분을 완만하게 만든다.
            const int samplesPerSegment = 6;
            var curved = new Vector2[(route.Length - 1) * samplesPerSegment + 1];
            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector2 first = route[Mathf.Max(0, i - 1)], start = route[i];
                Vector2 end = route[i + 1], last = route[Mathf.Min(route.Length - 1, i + 2)];
                for (int sample = 0; sample < samplesPerSegment; sample++)
                {
                    float t = (float)sample / samplesPerSegment;
                    curved[i * samplesPerSegment + sample] = .5f * ((2f * start) + (-first + end) * t
                        + (2f * first - 5f * start + 4f * end - last) * t * t
                        + (-first + 3f * start - 3f * end + last) * t * t * t);
                }
            }
            curved[curved.Length - 1] = route[route.Length - 1];
            return curved;
        }

        public float GetPathDistance(Vector2 position)
        {
            Vector2[] route = GetAscentRoute();
            float progress;
            return GetRouteDistance(position, route, GetRouteLength(route), out progress);
        }

        public Vector2 GetPathPosition(float progress)
        {
            Vector2[] route = GetAscentRoute();
            float remaining = Mathf.Clamp01(progress) * GetRouteLength(route);
            for (int i = 1; i < route.Length; i++)
            {
                float length = Vector2.Distance(route[i - 1], route[i]);
                if (remaining <= length)
                    return Vector2.Lerp(route[i - 1], route[i], remaining / Mathf.Max(.001f, length));
                remaining -= length;
            }
            return route[route.Length - 1];
        }

        private float GetAscentHeight(float progress)
        {
            // 정상 평지에 닿기 전에 등반을 마쳐 진입부에 급격한 턱이 생기지 않게 한다.
            return baseHeight + mountainHeight * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .90f));
        }

        private static float GetRouteLength(Vector2[] route)
        {
            float length = 0f;
            for (int i = 1; i < route.Length; i++) length += Vector2.Distance(route[i - 1], route[i]);
            return length;
        }

        private static float GetRouteDistance(Vector2 position, Vector2[] route, float routeLength, out float progress)
        {
            float nearest = float.MaxValue, travelled = 0f;
            progress = 0f;
            for (int i = 1; i < route.Length; i++)
            {
                Vector2 segment = route[i] - route[i - 1];
                float length = segment.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(position - route[i - 1], segment) / Mathf.Max(.0001f, segment.sqrMagnitude));
                float distance = Vector2.Distance(position, route[i - 1] + segment * t);
                if (distance < nearest)
                {
                    nearest = distance;
                    progress = (travelled + length * t) / Mathf.Max(.001f, routeLength);
                }
                travelled += length;
            }
            return nearest;
        }

        private float SampleNoise(Vector2 position, Vector2[] offsets)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, total = 0f;
            for (int i = 0; i < offsets.Length; i++)
            {
                float noise = Mathf.PerlinNoise(position.x / noiseScale * frequency + offsets[i].x, position.y / noiseScale * frequency + offsets[i].y);
                sum += noise * amplitude;
                total += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }
            return Mathf.Clamp01(sum / total);
        }

        private float GetSummitMask(Vector2 position)
        {
            Vector2 delta = position - SummitPosition;
            float angle = Mathf.Atan2(delta.y, delta.x);
            float edgeVariation = Mathf.Max(0f, 1f + Mathf.Sin(angle * 3f) * .8f + Mathf.Sin(angle * 5f + .7f) * .6f);
            // 반지름 안의 평지는 보장하면서 바깥 윤곽만 불규칙하게 확장한다.
            return 1f - SmoothRange(summitRadius, summitRadius + summitBlendWidth, delta.magnitude - edgeVariation);
        }

        private void PaintTerrain(TerrainData data, Vector2[] route, float routeLength)
        {
            if (data.terrainLayers.Length != 3) return;
            int resolution = data.alphamapResolution;
            var weights = new float[resolution, resolution, 3];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float u = (float)x / (resolution - 1), v = (float)z / (resolution - 1);
                    var position = new Vector2(u * mapSize.x, v * mapSize.y);
                    float progress;
                    float distance = GetRouteDistance(position, route, routeLength, out progress);
                    float path = 1f - SmoothRange(pathWidth * .5f - .3f, pathWidth * .5f + .7f, distance);
                    float summit = GetSummitMask(position);
                    float soil = .45f + Mathf.PerlinNoise(position.x * .16f, position.y * .16f) * .3f;
                    float dirt = Mathf.Max(path, summit * soil);
                    float rock = Mathf.Max(SmoothRange(27f, 49f, data.GetSteepness(u, v)), summit * .65f) * (1f - dirt);
                    weights[z, x, 0] = 1f - dirt - rock;
                    weights[z, x, 1] = dirt;
                    weights[z, x, 2] = rock;
                }
            data.SetAlphamaps(0, 0, weights);
        }

        private static float SmoothRange(float minimum, float maximum, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
        }

        private void OnValidate() { ValidateSettings(); }

        private void ValidateSettings()
        {
            mapSize = new Vector2(Mathf.Max(30f, mapSize.x), Mathf.Max(30f, mapSize.y));
            heightmapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(32, heightmapResolution - 1)) + 1, 33, 2049);
            baseHeight = Mathf.Max(0f, baseHeight);
            mountainHeight = Mathf.Max(1f, mountainHeight);
            summitCenter = new Vector2(Mathf.Clamp(summitCenter.x, .35f, .70f), Mathf.Clamp(summitCenter.y, .55f, .75f));
            summitRadius = Mathf.Clamp(summitRadius, 1f, Mathf.Min(mapSize.x, mapSize.y) * .16f);
            summitBlendWidth = Mathf.Max(.1f, summitBlendWidth);
            ridgeWidth = Mathf.Max(1f, ridgeWidth);
            pathWidth = Mathf.Clamp(pathWidth, .5f, summitRadius);
            pathShoulderWidth = Mathf.Max(.1f, pathShoulderWidth);
            noiseScale = Mathf.Max(1f, noiseScale);
            noiseStrength = Mathf.Max(0f, noiseStrength);
            octaves = Mathf.Clamp(octaves, 1, 6);
            persistence = Mathf.Clamp01(persistence);
        }
    }
}
