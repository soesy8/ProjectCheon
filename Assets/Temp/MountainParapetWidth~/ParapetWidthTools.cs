using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

public static class ParapetWidthTools
{
    private const string Folder = "Assets/Temp/MountainParapetWidth~/";

    private static float[] Coordinates(Vector3 point) => new[] { point.x, point.y, point.z };

    private static Vector3[] Centers(MeshFilter filter)
    {
        var vertices = filter.sharedMesh.vertices;
        var centers = new Vector3[vertices.Length / 8];
        for (int ring = 0; ring < centers.Length; ring++)
        {
            for (int corner = 0; corner < 8; corner++)
                centers[ring] += filter.transform.TransformPoint(vertices[ring * 8 + corner]);
            centers[ring] /= 8;
            centers[ring].y -= .2525f;
        }
        return centers;
    }

    public static string Inspect()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Proto/MountainPass/Scenes/MountainPass.unity")
            throw new InvalidOperationException("산길 씬 편집 모드에서 실행해야 합니다.");
        var left = GameObject.Find("ApproachLeftParapet").GetComponent<MeshFilter>();
        var right = GameObject.Find("ApproachRightParapet").GetComponent<MeshFilter>();
        var leftCenters = Centers(left);
        var rightCenters = Centers(right);
        var view = SceneView.lastActiveSceneView;
        var report = new
        {
            scene = scene.path,
            dirty = scene.isDirty,
            selected = Selection.gameObjects.Select(item => item.name).ToArray(),
            roots = scene.GetRootGameObjects().Select(item => item.name).ToArray(),
            view = new { pivot = Coordinates(view.pivot), rotation = new[] { view.rotation.x, view.rotation.y, view.rotation.z, view.rotation.w }, size = view.size, orthographic = view.orthographic },
            objects = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.name.Contains("Parapet") || item.name.Contains("Stairway") || item.name.Contains("SafetyWalls") || item.name.Contains("Lanterns"))
                .Select(item => new { name = item.name, position = Coordinates(item.position), parent = item.parent == null ? null : item.parent.name }).ToArray(),
            rails = new[] { left, right }.Select(filter => new
            {
                name = filter.name, asset = AssetDatabase.GetAssetPath(filter.sharedMesh),
                vertices = filter.sharedMesh.vertexCount, rings = Centers(filter).Select(Coordinates).ToArray(),
                transform = new { position = Coordinates(filter.transform.position), scale = Coordinates(filter.transform.lossyScale) }
            }).ToArray(),
            widths = Enumerable.Range(0, leftCenters.Length).Where(index => index % 4 == 0).Select(index => new
            {
                step = index / 4,
                center = Coordinates((leftCenters[index] + rightCenters[index]) * .5f),
                width = new Vector2(leftCenters[index].x - rightCenters[index].x, leftCenters[index].z - rightCenters[index].z).magnitude
            }).ToArray()
        };
        File.WriteAllText(Folder + "Inspection.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        File.Copy(scene.path, Folder + "SceneBefore.unity.txt", false);
        return JsonConvert.SerializeObject(new { report.scene, report.dirty, report.selected, report.roots, report.objects, report.view, rails = report.rails.Select(item => new { item.name, item.asset, item.vertices, item.transform }) });
    }

    public static string SurveyExpansion()
    {
        var left = Centers(GameObject.Find("ApproachLeftParapet").GetComponent<MeshFilter>());
        var right = Centers(GameObject.Find("ApproachRightParapet").GetComponent<MeshFilter>());
        var ground = GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
        var results = new System.Collections.Generic.List<object>();
        for (int step = 44; step <= 93; step++)
        {
            int index = step * 4;
            Vector3 outward = left[index] - right[index];
            outward.y = 0;
            outward.Normalize();
            float expansion = .3f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(44, 51, step));
            for (int side = 0; side < 2; side++)
            {
                var direction = side == 0 ? outward : -outward;
                var original = side == 0 ? left[index] : right[index];
                var proposed = original + direction * expansion;
                RaycastHit hit;
                bool centerSupported = ground.Raycast(new Ray(proposed + Vector3.up * 3, Vector3.down), out hit, 6);
                bool innerSupported = ground.Raycast(new Ray(proposed - direction * .3f + Vector3.up * 3, Vector3.down), out hit, 6);
                results.Add(new { step, side, expansion, centerSupported, innerSupported });
            }
        }
        File.WriteAllText(Folder + "SupportSurvey.json", JsonConvert.SerializeObject(results, Formatting.Indented));
        var stairs = ground.GetComponent<MeshFilter>();
        return JsonConvert.SerializeObject(new { stairs = new { vertices = stairs.sharedMesh.vertexCount, asset = AssetDatabase.GetAssetPath(stairs.sharedMesh) }, results });
    }

    public static string Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Proto/MountainPass/Scenes/MountainPass.unity" || scene.isDirty)
            throw new InvalidOperationException("저장된 산길 씬 편집 모드에서 실행해야 합니다.");
        var filters = new[] { GameObject.Find("ApproachLeftParapet").GetComponent<MeshFilter>(), GameObject.Find("ApproachRightParapet").GetComponent<MeshFilter>() };
        var originalCenters = filters.Select(Centers).ToArray();
        var newCenters = originalCenters.Select(points => (Vector3[])points.Clone()).ToArray();
        var ground = GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
        var safety = GameObject.Find("ApproachSafetyWalls").transform;
        var lanterns = GameObject.Find("ApproachLanterns").transform;
        var records = new System.Collections.Generic.List<object>();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("계단 상부의 낮은 벽 간격 확장");
        for (int ring = 0; ring < originalCenters[0].Length; ring++)
        {
            var outward = originalCenters[0][ring] - originalCenters[1][ring];
            outward.y = 0;
            float expansion = .3f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(44, 51, ring / 4f));
            var offset = outward.normalized * expansion;
            newCenters[0][ring] += offset;
            newCenters[1][ring] -= offset;
        }
        for (int side = 0; side < 2; side++)
        {
            var filter = filters[side];
            var mesh = filter.sharedMesh;
            string assetPath = AssetDatabase.GetAssetPath(mesh);
            File.Copy(assetPath, Folder + filter.name + ".Before.asset.txt", false);
            File.Copy(assetPath + ".meta", Folder + filter.name + ".Before.meta.txt", false);
            var vertices = mesh.vertices;
            File.WriteAllText(Folder + filter.name + ".Before.vertices.json", JsonConvert.SerializeObject(vertices.Select(Coordinates).ToArray()));
            Undo.RegisterCompleteObjectUndo(mesh, "낮은 벽의 간격 확장");
            int changedRings = 0;
            for (int ring = 0; ring < newCenters[side].Length; ring++)
            {
                Vector3 shift = newCenters[side][ring] - originalCenters[side][ring];
                if (shift.sqrMagnitude < .00000001f) continue;
                changedRings++;
                for (int corner = 0; corner < 8; corner++)
                {
                    var world = filter.transform.TransformPoint(vertices[ring * 8 + corner]) + shift;
                    // 벽을 옮긴 자리의 계단 높이에 맞춰 밑면이 뜨지 않게 한다.
                    if (corner < 2 && ground.Raycast(new Ray(world + Vector3.up * 2, Vector3.down), out var hit, 5))
                        world.y = Mathf.Min(world.y, hit.point.y - .06f);
                    vertices[ring * 8 + corner] = filter.transform.InverseTransformPoint(world);
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var collider = filter.GetComponent<MeshCollider>();
            Undo.RecordObject(collider, "낮은 벽 콜라이더 갱신");
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            int changedLamps = 0;
            for (int step = 2; step < newCenters[side].Length / 4 + 1; step += 9)
            {
                int ring = step * 4;
                Vector3 shift = newCenters[side][ring] - originalCenters[side][ring];
                if (shift.sqrMagnitude < .00000001f) continue;
                var lamp = lanterns.Find((side == 0 ? "LeftLantern_" : "RightLantern_") + step.ToString("00"));
                Undo.RecordObject(lamp, "확장한 낮은 벽에 등불 정렬");
                lamp.position += shift;
                changedLamps++;
            }
            int changedSafetyWalls = 0;
            for (int step = 0; step < (newCenters[side].Length - 1) / 4; step++)
            {
                int ring = step * 4;
                if ((newCenters[side][ring] - originalCenters[side][ring]).sqrMagnitude < .00000001f &&
                    (newCenters[side][ring + 4] - originalCenters[side][ring + 4]).sqrMagnitude < .00000001f) continue;
                var wall = safety.Find((side == 0 ? "LeftSafety_" : "RightSafety_") + step.ToString("00"));
                var box = wall.GetComponent<BoxCollider>();
                Undo.RecordObjects(new UnityEngine.Object[] { wall, box }, "확장한 낮은 벽에 추락 방지 경계 정렬");
                var direction = newCenters[side][ring + 4] - newCenters[side][ring];
                direction.y = 0;
                var midpoint = (newCenters[side][ring] + newCenters[side][ring + 4]) * .5f;
                midpoint.y = wall.position.y;
                wall.SetPositionAndRotation(midpoint, Quaternion.LookRotation(direction));
                var size = box.size;
                size.z = direction.magnitude + .32f;
                box.size = size;
                changedSafetyWalls++;
            }
            records.Add(new { name = filter.name, guid = AssetDatabase.AssetPathToGUID(assetPath), changedRings, changedLamps, changedSafetyWalls });
        }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        File.WriteAllText(Folder + "Application.json", JsonConvert.SerializeObject(records, Formatting.Indented));
        SceneView.RepaintAll();
        return JsonConvert.SerializeObject(records);
    }

    public static string Verify()
    {
        var filters = new[] { GameObject.Find("ApproachLeftParapet").GetComponent<MeshFilter>(), GameObject.Find("ApproachRightParapet").GetComponent<MeshFilter>() };
        var centers = filters.Select(Centers).ToArray();
        var before = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Folder + "Inspection.json"));
        var ground = GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
        var safety = GameObject.Find("ApproachSafetyWalls").transform;
        Physics.SyncTransforms();
        int floorSamples = 0;
        int floorMisses = 0;
        int originalFloorMisses = 0;
        int newFloorMisses = 0;
        int wallMisses = 0;
        int safetyMisses = 0;
        var failures = new System.Collections.Generic.List<string>();
        for (int step = 44; step < 93; step++)
        {
            int ring = step * 4 + 2;
            var center = (centers[0][ring] + centers[1][ring]) * .5f;
            for (int across = 1; across < 10; across++)
            {
                var point = Vector3.Lerp(centers[0][ring], centers[1][ring], across / 10f);
                var originalLeft = before["rails"][0]["rings"][ring];
                var originalRight = before["rails"][1]["rings"][ring];
                var oldPoint = Vector3.Lerp(new Vector3((float)originalLeft[0], (float)originalLeft[1], (float)originalLeft[2]), new Vector3((float)originalRight[0], (float)originalRight[1], (float)originalRight[2]), across / 10f);
                bool originalSupport = ground.Raycast(new Ray(oldPoint + Vector3.up * 3, Vector3.down), out var originalHit, 6);
                if (!originalSupport) originalFloorMisses++;
                floorSamples++;
                if (!ground.Raycast(new Ray(point + Vector3.up * 3, Vector3.down), out var hit, 6))
                {
                    floorMisses++;
                    if (originalSupport) newFloorMisses++;
                    failures.Add("floor:" + step + ":" + across);
                }
            }
            for (int side = 0; side < 2; side++)
            {
                var direction = centers[side][ring] - center;
                direction.y = 0;
                float distance = direction.magnitude + 1;
                direction.Normalize();
                if (!filters[side].GetComponent<MeshCollider>().Raycast(new Ray(center + Vector3.up * .4f, direction), out var wallHit, distance))
                {
                    wallMisses++;
                    failures.Add("wall:" + step + ":" + side);
                }
                var box = safety.Find((side == 0 ? "LeftSafety_" : "RightSafety_") + step.ToString("00")).GetComponent<BoxCollider>();
                if (!box.Raycast(new Ray(center + Vector3.up, direction), out var safetyHit, distance))
                {
                    safetyMisses++;
                    failures.Add("safety:" + step + ":" + side);
                }
            }
        }
        var railChecks = filters.Select((filter, side) =>
        {
            var oldVertices = JsonConvert.DeserializeObject<float[][]>(File.ReadAllText(Folder + filter.name + ".Before.vertices.json"));
            var current = filter.sharedMesh.vertices;
            bool lowerSectionPreserved = Enumerable.Range(0, 177 * 8).All(index => Vector3.Distance(current[index], new Vector3(oldVertices[index][0], oldVertices[index][1], oldVertices[index][2])) < .00001f);
            string assetPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
            float maxLampOffset = 0;
            for (int step = 47; step <= 92; step += 9)
            {
                var lamp = GameObject.Find("ApproachLanterns").transform.Find((side == 0 ? "LeftLantern_" : "RightLantern_") + step.ToString("00"));
                var delta = lamp.position - centers[side][step * 4];
                maxLampOffset = Mathf.Max(maxLampOffset, new Vector2(delta.x, delta.z).magnitude);
            }
            return new { name = filter.name, lowerSectionPreserved, meshColliderMatches = filter.GetComponent<MeshCollider>().sharedMesh == filter.sharedMesh,
                metaPreserved = File.ReadAllText(assetPath + ".meta") == File.ReadAllText(Folder + filter.name + ".Before.meta.txt"), maxLampOffset };
        }).ToArray();
        var widthChecks = new[] { 44, 48, 51, 54, 65, 80, 93 }.Select(step =>
        {
            var delta = centers[0][step * 4] - centers[1][step * 4];
            return new { step, before = (float)before["widths"][step]["width"], after = new Vector2(delta.x, delta.z).magnitude };
        }).ToArray();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var report = new { scene = scene.path, saved = !scene.isDirty, railChecks, widthChecks, floorSamples, floorMisses, originalFloorMisses, newFloorMisses, wallMisses, safetyMisses, failures };
        File.WriteAllText(Folder + "Verification.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return JsonConvert.SerializeObject(report);
    }

    public static string InspectBendFloor()
    {
        var report = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Folder + "Inspection.json"));
        var rails = report["rails"].Select(rail => rail["rings"].Select(point => new Vector3((float)point[0], (float)point[1], (float)point[2])).ToArray()).ToArray();
        var ground = GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
        var results = new System.Collections.Generic.List<object>();
        for (int step = 52; step <= 58; step++)
        {
            int ring = step * 4 + 2;
            var outward = rails[0][ring] - rails[1][ring];
            outward.y = 0;
            outward.Normalize();
            foreach (float expansion in new[] { 0f, .1f, .15f, .2f, .25f, .3f })
            {
                var left = rails[0][ring] + outward * expansion;
                var right = rails[1][ring] - outward * .3f;
                var misses = new System.Collections.Generic.List<int>();
                for (int across = 1; across < 10; across++)
                    if (!ground.Raycast(new Ray(Vector3.Lerp(left, right, across / 10f) + Vector3.up * 3, Vector3.down), out var hit, 6)) misses.Add(across);
                results.Add(new { step, expansion, misses });
            }
        }
        return JsonConvert.SerializeObject(results);
    }

    public static string FitBend()
    {
        var report = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Folder + "Inspection.json"));
        var original = report["rails"].Select(rail => rail["rings"].Select(point => new Vector3((float)point[0], (float)point[1], (float)point[2])).ToArray()).ToArray();
        var filter = GameObject.Find("ApproachLeftParapet").GetComponent<MeshFilter>();
        var current = Centers(filter);
        var vertices = filter.sharedMesh.vertices;
        var shifts = new Vector3[current.Length];
        Undo.RegisterCompleteObjectUndo(filter.sharedMesh, "굽이의 계단 가장자리에 낮은 벽 맞춤");
        for (int ring = 0; ring < current.Length; ring++)
        {
            float step = ring / 4f;
            // 급한 굽이에서는 기존 계단의 바닥 경계를 따라 확장량을 부드럽게 줄인다.
            float reduction = .2f * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Abs(step - 55.5f) / 4)));
            if (reduction < .00001f) continue;
            var outward = original[0][ring] - original[1][ring];
            outward.y = 0;
            var target = original[0][ring] + outward.normalized * (.3f - reduction);
            shifts[ring] = target - current[ring];
            shifts[ring].y = 0;
            for (int corner = 0; corner < 8; corner++) vertices[ring * 8 + corner] += filter.transform.InverseTransformVector(shifts[ring]);
        }
        filter.sharedMesh.vertices = vertices;
        filter.sharedMesh.RecalculateNormals();
        filter.sharedMesh.RecalculateBounds();
        EditorUtility.SetDirty(filter.sharedMesh);
        var collider = filter.GetComponent<MeshCollider>();
        Undo.RecordObject(collider, "굽이의 벽 콜라이더 갱신");
        collider.sharedMesh = null;
        collider.sharedMesh = filter.sharedMesh;
        var lamp = GameObject.Find("ApproachLanterns/LeftLantern_56").transform;
        Undo.RecordObject(lamp, "굽이의 낮은 벽에 등불 정렬");
        lamp.position += shifts[56 * 4];
        var updated = Centers(filter);
        var safety = GameObject.Find("ApproachSafetyWalls").transform;
        for (int step = 51; step < 60; step++)
        {
            var wall = safety.Find("LeftSafety_" + step.ToString("00"));
            var box = wall.GetComponent<BoxCollider>();
            Undo.RecordObjects(new UnityEngine.Object[] { wall, box }, "굽이의 추락 방지 경계 정렬");
            var direction = updated[(step + 1) * 4] - updated[step * 4];
            direction.y = 0;
            var midpoint = (updated[(step + 1) * 4] + updated[step * 4]) * .5f;
            midpoint.y = wall.position.y;
            wall.SetPositionAndRotation(midpoint, Quaternion.LookRotation(direction));
            var size = box.size;
            size.z = direction.magnitude + .32f;
            box.size = size;
        }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(filter.gameObject.scene);
        EditorSceneManager.SaveScene(filter.gameObject.scene);
        SceneView.RepaintAll();
        return "굽은 모서리의 확장량을 계단 바닥에 맞췄습니다.";
    }
}
