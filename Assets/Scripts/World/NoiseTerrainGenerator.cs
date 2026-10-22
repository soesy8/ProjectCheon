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

        [Header("산지 노이즈")]
        [SerializeField, Min(1f)] private float mountainHeight = 22f;
        [SerializeField, Min(1f)] private float noiseScale = 32f;
        [SerializeField, Range(1, 6)] private int octaves = 4;
        [SerializeField, Range(0f, 1f)] private float persistence = 0.45f;

        [Header("골짜기와 이동로")]
        [SerializeField, Min(0f)] private float valleyFloorHeight = 2f;
        [SerializeField, Min(1f)] private float valleyWidth = 9f;
        [SerializeField, Min(1f)] private float valleyBlendWidth = 15f;
        [SerializeField, Min(0f)] private float valleyBend = 10f;
        [SerializeField, Min(0.5f)] private float pathWidth = 4.5f;

        [Header("중앙 전투 공간")]
        [SerializeField, Min(0f)] private float clearingRadius = 12f;
        [SerializeField, Min(0.1f)] private float clearingBlendWidth = 7f;

        public Vector2 MapSize => mapSize;
        public int Seed => seed;

        // 자동 재생성하지 않아 수작업으로 다듬은 Terrain이 임의로 덮어써지지 않게 한다.
        public void Generate()
        {
            ValidateSettings();
            Terrain terrain = GetComponent<Terrain>();
            TerrainData data = terrain.terrainData;
            if (data == null)
                throw new InvalidOperationException("Terrain Data를 먼저 지정해주세요.");

            var random = new System.Random(seed);
            var offsets = new Vector2[octaves];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = new Vector2(random.Next(-10000, 10000), random.Next(-10000, 10000));

            data.heightmapResolution = heightmapResolution;
            float verticalSize = valleyFloorHeight + mountainHeight + 1f;
            data.size = new Vector3(mapSize.x, verticalSize, mapSize.y);
            var heights = new float[heightmapResolution, heightmapResolution];

            for (int z = 0; z < heightmapResolution; z++)
            {
                for (int x = 0; x < heightmapResolution; x++)
                {
                    var position = new Vector2(
                        x * mapSize.x / (heightmapResolution - 1),
                        z * mapSize.y / (heightmapResolution - 1));
                    float noise = SampleMountainNoise(position, offsets);
                    float mountain = mountainHeight * (0.28f + noise * 0.72f);
                    float valley = GetValleyMask(position);
                    float clearing = GetClearingMask(position);
                    float height = valleyFloorHeight + mountain * (1f - valley) * (1f - clearing);
                    // Terrain의 높이 배열은 [z, x], 값은 Terrain 높이에 대한 0~1 비율이다.
                    heights[z, x] = Mathf.Clamp01(height / verticalSize);
                }
            }

            data.SetHeights(0, 0, heights);
            PaintTerrain(data);
            GetComponent<TerrainCollider>().terrainData = data;
            terrain.Flush();
        }

        public void SetSeed(int value)
        {
            seed = value;
        }

        private float SampleMountainNoise(Vector2 position, Vector2[] offsets)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float totalAmplitude = 0f;
            for (int i = 0; i < offsets.Length; i++)
            {
                float sample = Mathf.PerlinNoise(
                    position.x / noiseScale * frequency + offsets[i].x,
                    position.y / noiseScale * frequency + offsets[i].y);
                // 능선 모양의 노이즈를 섞어 둥근 언덕에만 머무르지 않도록 한다.
                float ridge = 1f - Mathf.Abs(sample * 2f - 1f);
                sum += Mathf.Lerp(sample, ridge, 0.35f) * amplitude;
                totalAmplitude += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }
            return Mathf.Clamp01(sum / totalAmplitude);
        }

        public float GetValleyCenter(float localZ)
        {
            float t = localZ / mapSize.y - 0.5f;
            // 중앙 전투 공간을 반드시 지나도록 굽은 길의 중심을 고정한다.
            return mapSize.x * 0.5f + Mathf.Sin(t * Mathf.PI * 1.6f) * valleyBend;
        }

        private float GetValleyMask(Vector2 position)
        {
            float distance = Mathf.Abs(position.x - GetValleyCenter(position.y));
            return 1f - SmoothRange(valleyWidth * 0.5f, valleyWidth * 0.5f + valleyBlendWidth, distance);
        }

        private float GetClearingMask(Vector2 position)
        {
            float distance = Vector2.Distance(position, mapSize * 0.5f);
            return 1f - SmoothRange(clearingRadius, clearingRadius + clearingBlendWidth, distance);
        }

        private void PaintTerrain(TerrainData data)
        {
            // Terrain Layer의 순서는 풀, 흙길, 암석이다. 레이어가 없으면 높이만 생성한다.
            if (data.terrainLayers.Length != 3)
                return;

            int resolution = data.alphamapResolution;
            var weights = new float[resolution, resolution, 3];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = (float)x / (resolution - 1);
                    float v = (float)z / (resolution - 1);
                    var position = new Vector2(u * mapSize.x, v * mapSize.y);
                    float pathDistance = Mathf.Abs(position.x - GetValleyCenter(position.y));
                    float path = 1f - SmoothRange(pathWidth * 0.5f, pathWidth * 0.5f + 1.5f, pathDistance);
                    float clearing = GetClearingMask(position) * 0.75f;
                    float dirt = Mathf.Max(path, clearing);
                    float rock = SmoothRange(25f, 48f, data.GetSteepness(u, v)) * (1f - dirt);
                    weights[z, x, 0] = 1f - dirt - rock;
                    weights[z, x, 1] = dirt;
                    weights[z, x, 2] = rock;
                }
            }
            data.SetAlphamaps(0, 0, weights);
        }

        private static float SmoothRange(float minimum, float maximum, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
        }

        private void OnValidate()
        {
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            mapSize = new Vector2(Mathf.Max(10f, mapSize.x), Mathf.Max(10f, mapSize.y));
            heightmapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.Max(32, heightmapResolution - 1)) + 1, 33, 2049);
            mountainHeight = Mathf.Max(1f, mountainHeight);
            noiseScale = Mathf.Max(1f, noiseScale);
            octaves = Mathf.Clamp(octaves, 1, 6);
            persistence = Mathf.Clamp01(persistence);
            valleyFloorHeight = Mathf.Max(0f, valleyFloorHeight);
            valleyWidth = Mathf.Clamp(valleyWidth, 1f, mapSize.x * 0.5f);
            valleyBlendWidth = Mathf.Max(1f, valleyBlendWidth);
            valleyBend = Mathf.Clamp(valleyBend, 0f, (mapSize.x - valleyWidth) * 0.4f);
            pathWidth = Mathf.Clamp(pathWidth, 0.5f, valleyWidth);
            clearingRadius = Mathf.Clamp(clearingRadius, 0f, Mathf.Min(mapSize.x, mapSize.y) * 0.4f);
            clearingBlendWidth = Mathf.Max(0.1f, clearingBlendWidth);
        }
    }
}
