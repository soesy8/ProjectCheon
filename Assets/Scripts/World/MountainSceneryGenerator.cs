using System.Collections.Generic;
using UnityEngine;

namespace Cheon.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NoiseTerrainGenerator))]
    public class MountainSceneryGenerator : MonoBehaviour
    {
        [Header("소나무 숲")]
        [SerializeField] private GameObject pinePrefab;
        [SerializeField, Min(0)] private int pineCount = 220;
        [SerializeField] private Vector2 pineScale = new Vector2(.65f, 1.15f);
        [SerializeField, Min(1f)] private float pineSpacing = 3.7f;

        [Header("능선 화강암")]
        [SerializeField] private GameObject rockPrefab;
        [SerializeField, Min(0)] private int rockCount = 48;

        [Header("등산로 옆 돌담")]
        [SerializeField] private bool generateStoneWall = true;
        [SerializeField] private Material wallMaterial;
        [SerializeField, Min(.1f)] private float wallHeight = 1.2f;

        private const string SceneryRootName = "Generated Mountain Scenery";

        // 지형과 함께 다시 생성해 시드를 바꿔도 나무와 바위가 공중에 남지 않게 한다.
        public void Generate()
        {
            var terrain = GetComponent<Terrain>();
            var generator = GetComponent<NoiseTerrainGenerator>();
            Transform previous = transform.Find(SceneryRootName);
            if (previous != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(previous.gameObject);
                else Destroy(previous.gameObject);
#else
                Destroy(previous.gameObject);
#endif
            }
            var root = new GameObject(SceneryRootName);
            root.transform.SetParent(transform, false);
            var random = new System.Random(generator.Seed);
            var positions = new List<Vector2>();
            Transform forest = CreateGroup("Pine Forest", root.transform);
            Transform rocks = CreateGroup("Granite Outcrops", root.transform);

            if (pinePrefab != null)
                for (int attempt = 0; attempt < pineCount * 40 && positions.Count < pineCount; attempt++)
                {
                    var p = new Vector2(3f + (float)random.NextDouble() * (generator.MapSize.x - 6f), 3f + (float)random.NextDouble() * (generator.MapSize.y - 6f));
                    float distance = Vector2.Distance(p, generator.SummitPosition);
                    float slope = terrain.terrainData.GetSteepness(p.x / generator.MapSize.x, p.y / generator.MapSize.y);
                    if (distance < generator.SummitRadius + 3f || generator.GetPathDistance(p) < generator.PathWidth * .5f + 2.5f || slope > 49f) continue;
                    bool crowded = false;
                    foreach (Vector2 other in positions)
                        if ((other - p).sqrMagnitude < pineSpacing * pineSpacing) { crowded = true; break; }
                    if (crowded) continue;
                    float scale = Mathf.Lerp(pineScale.x, pineScale.y, (float)random.NextDouble());
                    PlacePrefab(pinePrefab, forest, p, terrain, generator, random, Vector3.one * scale, 0f);
                    positions.Add(p);
                }

            if (rockPrefab != null)
                for (int attempt = 0, count = 0; attempt < rockCount * 50 && count < rockCount; attempt++)
                {
                    float progress = .18f + (float)random.NextDouble() * .68f;
                    Vector2 center = generator.GetPathPosition(progress);
                    Vector2 direction = (generator.GetPathPosition(progress + .002f) - center).normalized;
                    float side = random.Next(2) == 0 ? -1f : 1f;
                    var p = center + new Vector2(-direction.y, direction.x) * side * (5f + (float)random.NextDouble() * 8f);
                    if (attempt % 3 == 0)
                    {
                        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                        p = generator.SummitPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (generator.SummitRadius + 4f + (float)random.NextDouble() * 5f);
                    }
                    float distance = Vector2.Distance(p, generator.SummitPosition);
                    float slope = terrain.terrainData.GetSteepness(p.x / generator.MapSize.x, p.y / generator.MapSize.y);
                    float pathDistance = generator.GetPathDistance(p);
                    if (p.x < 3f || p.y < 3f || p.x > generator.MapSize.x - 3f || p.y > generator.MapSize.y - 3f) continue;
                    if (distance < generator.SummitRadius + 3.5f || pathDistance < generator.PathWidth * .5f + 2f || slope < 25f || slope > 65f) continue;
                    if (positions.Exists(other => Vector2.Distance(other, p) < 2.6f)) continue;
                    var scale = new Vector3(2.5f + (float)random.NextDouble() * 3f, 2f + (float)random.NextDouble() * 4f, 2f + (float)random.NextDouble() * 2.5f);
                    PlacePrefab(rockPrefab, rocks, p, terrain, generator, random, scale, -.65f);
                    count++;
                }

            if (generateStoneWall && wallMaterial != null)
                GenerateWall(CreateGroup("Ridge Stone Wall", root.transform), terrain, generator);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(root, "산지 환경 배치");
#endif
        }

        private static Transform CreateGroup(string groupName, Transform parent)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static float GetHeight(Vector2 p, Terrain terrain, NoiseTerrainGenerator generator)
        {
            return terrain.terrainData.GetInterpolatedHeight(p.x / generator.MapSize.x, p.y / generator.MapSize.y);
        }

        private static void PlacePrefab(GameObject prefab, Transform parent, Vector2 p, Terrain terrain, NoiseTerrainGenerator generator, System.Random random, Vector3 scale, float sink)
        {
            GameObject instance;
#if UNITY_EDITOR
            if (!Application.isPlaying) instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            else instance = Instantiate(prefab, parent);
#else
            instance = Instantiate(prefab, parent);
#endif
            instance.transform.localPosition = new Vector3(p.x, GetHeight(p, terrain, generator) + sink, p.y);
            instance.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            instance.transform.localScale = scale;
        }

        private void GenerateWall(Transform parent, Terrain terrain, NoiseTerrainGenerator generator)
        {
            const int segments = 65;
            Vector2 previous = Vector2.zero;
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(.03f, .78f, (float)i / segments);
                Vector2 center = generator.GetPathPosition(t);
                Vector2 forward = (generator.GetPathPosition(Mathf.Min(1f, t + .003f)) - center).normalized;
                Vector2 p = center + new Vector2(-forward.y, forward.x) * (generator.PathWidth * .5f + .65f);
                if (i > 0)
                {
                    float firstHeight = GetHeight(previous, terrain, generator);
                    float secondHeight = GetHeight(p, terrain, generator);
                    Vector3 first = new Vector3(previous.x, firstHeight, previous.y);
                    Vector3 second = new Vector3(p.x, secondHeight, p.y);
                    var section = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    section.name = "Stone Wall Section";
                    section.transform.SetParent(parent, false);
                    section.transform.localPosition = (first + second) * .5f + Vector3.up * (wallHeight * .5f - .1f);
                    section.transform.localRotation = Quaternion.LookRotation(second - first);
                    section.transform.localScale = new Vector3(.8f, wallHeight, Vector3.Distance(first, second) + .08f);
                    section.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                    if (i % 2 == 0)
                    {
                        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cap.name = "Stone Wall Merlon";
                        cap.transform.SetParent(section.transform, false);
                        cap.transform.localPosition = new Vector3(0f, .63f, 0f);
                        cap.transform.localScale = new Vector3(1.05f, .28f, .48f);
                        cap.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                    }
                }
                previous = p;
            }
        }
    }
}
