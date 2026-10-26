using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

public static class LowerCurveWidthTools
{
    private const string Folder = "Assets/Temp/MountainLowerCurve~/";
    private static float[] Point(Vector3 value) => new[] { value.x, value.y, value.z };
    private static MeshFilter[] Rails() => new[] { GameObject.Find("ApproachLeftParapet").GetComponent<MeshFilter>(), GameObject.Find("ApproachRightParapet").GetComponent<MeshFilter>() };
    private static Vector3[] Centers(MeshFilter filter)
    {
        var vertices = filter.sharedMesh.vertices;
        var centers = new Vector3[vertices.Length / 8];
        for (int ring = 0; ring < centers.Length; ring++)
        {
            for (int corner = 0; corner < 8; corner++) centers[ring] += filter.transform.TransformPoint(vertices[ring * 8 + corner]);
            centers[ring] /= 8;
            centers[ring].y -= .2525f;
        }
        return centers;
    }
    private static float Weight(float step) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - Mathf.Abs(step - 55.5f) / 7));
    private static Vector3[][] Expanded(Vector3[][] current)
    {
        var expanded = current.Select(points => (Vector3[])points.Clone()).ToArray();
        for (int ring = 0; ring < current[0].Length; ring++)
        {
            var direction = current[0][ring] - current[1][ring];
            direction.y = 0;
            direction.Normalize();
            float weight = Weight(ring / 4f);
            // 아래 굽이의 바닥이 충분한 쪽을 더 넓혀 기존 계단과의 연결을 유지한다.
            expanded[0][ring] += direction * (.05f * weight);
            expanded[1][ring] -= direction * (.25f * weight);
        }
        return expanded;
    }
    public static string Inspect()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Proto/MountainPass/Scenes/MountainPass.unity") throw new InvalidOperationException("산길 씬 편집 모드를 확인하세요.");
        var rails = Rails();
        var centers = rails.Select(Centers).ToArray();
        var proposed = Expanded(centers);
        var ground = GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
        int newMissing = 0, oldMissing = 0, samples = 0;
        for (int ring = 49 * 4; ring <= 62 * 4; ring++)
        for (int across = 1; across <= 9; across++)
        {
            samples++;
            var before = Vector3.Lerp(centers[0][ring], centers[1][ring], across / 10f);
            var after = Vector3.Lerp(proposed[0][ring], proposed[1][ring], across / 10f);
            bool wasSupported = ground.Raycast(new Ray(before + Vector3.up * 3, Vector3.down), out var oldHit, 6);
            bool supported = ground.Raycast(new Ray(after + Vector3.up * 3, Vector3.down), out var hit, 6);
            if (!wasSupported) oldMissing++;
            if (wasSupported && !supported) newMissing++;
        }
        var report = new { scene = scene.path, dirty = scene.isDirty, samples, oldMissing, newMissing,
            rails = rails.Select((rail, side) => new { rail.name, asset = AssetDatabase.GetAssetPath(rail.sharedMesh), vertices = rail.sharedMesh.vertices.Select(Point).ToArray(), centers = centers[side].Select(Point).ToArray(), meta = File.ReadAllText(AssetDatabase.GetAssetPath(rail.sharedMesh) + ".meta") }).ToArray() };
        File.WriteAllText(Folder + "Before.json", JsonConvert.SerializeObject(report));
        File.Copy(scene.path, Folder + "SceneBefore.unity.txt", true);
        return JsonConvert.SerializeObject(new { report.scene, report.dirty, report.samples, report.oldMissing, report.newMissing });
    }
    public static string Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Proto/MountainPass/Scenes/MountainPass.unity" || scene.isDirty) throw new InvalidOperationException("저장된 산길 씬 편집 모드를 확인하세요.");
        var rails = Rails();
        var current = rails.Select(Centers).ToArray();
        var expanded = Expanded(current);
        var safety = GameObject.Find("ApproachSafetyWalls").transform;
        var lanterns = GameObject.Find("ApproachLanterns").transform;
        var ground = GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
        var changes = new System.Collections.Generic.List<object>();
        for (int side = 0; side < 2; side++)
        {
            var rail = rails[side];
            var mesh = rail.sharedMesh;
            var asset = AssetDatabase.GetAssetPath(mesh);
            File.Copy(asset, Folder + rail.name + ".Before.asset.txt", false);
            Undo.RegisterCompleteObjectUndo(mesh, "계단 아래쪽 커브의 낮은 벽 추가 확장");
            var vertices = mesh.vertices;
            int changedRings = 0;
            for (int ring = 0; ring < current[side].Length; ring++)
            {
                var shift = expanded[side][ring] - current[side][ring];
                if (shift.sqrMagnitude < .00000001f) continue;
                changedRings++;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = rail.transform.TransformPoint(vertices[ring * 8 + corner]) + shift;
                    if (corner < 2 && ground.Raycast(new Ray(point + Vector3.up * 2, Vector3.down), out var hit, 5)) point.y = Mathf.Min(point.y, hit.point.y - .06f);
                    vertices[ring * 8 + corner] = rail.transform.InverseTransformPoint(point);
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var meshCollider = rail.GetComponent<MeshCollider>();
            Undo.RecordObject(meshCollider, "아래쪽 커브의 벽 콜라이더 갱신");
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            int movedLamps = 0, movedWalls = 0;
            for (int step = 2; step <= 92; step += 9)
            {
                var shift = expanded[side][step * 4] - current[side][step * 4];
                if (shift.sqrMagnitude < .00000001f) continue;
                var lamp = lanterns.Find((side == 0 ? "LeftLantern_" : "RightLantern_") + step.ToString("00"));
                Undo.RecordObject(lamp, "아래쪽 커브의 벽에 등불 정렬");
                lamp.position += shift;
                movedLamps++;
            }
            for (int step = 48; step <= 62; step++)
            {
                var wall = safety.Find((side == 0 ? "LeftSafety_" : "RightSafety_") + step.ToString("00"));
                var box = wall.GetComponent<BoxCollider>();
                Undo.RecordObjects(new UnityEngine.Object[] { wall, box }, "아래쪽 커브의 충돌 경계 정렬");
                var direction = expanded[side][(step + 1) * 4] - expanded[side][step * 4];
                direction.y = 0;
                var midpoint = (expanded[side][(step + 1) * 4] + expanded[side][step * 4]) * .5f;
                midpoint.y = wall.position.y;
                wall.SetPositionAndRotation(midpoint, Quaternion.LookRotation(direction));
                var size = box.size;
                size.z = direction.magnitude + .32f;
                box.size = size;
                movedWalls++;
            }
            changes.Add(new { rail.name, changedRings, movedLamps, movedWalls });
        }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SceneView.RepaintAll();
        return JsonConvert.SerializeObject(changes);
    }
    public static string Verify()
    {
        var before = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Folder + "Before.json"));
        var rails = Rails();
        var current = rails.Select(Centers).ToArray();
        var old = before["rails"].Select(rail => rail["centers"].Select(point => new Vector3((float)point[0], (float)point[1], (float)point[2])).ToArray()).ToArray();
        var ground = GameObject.Find("MountainPassEnvironment/Stairway_Winding_Approach").GetComponent<MeshCollider>();
        int samples = 0, newMissing = 0;
        for (int ring = 49 * 4; ring <= 62 * 4; ring++)
        for (int across = 1; across <= 9; across++)
        {
            samples++;
            var originalPoint = Vector3.Lerp(old[0][ring], old[1][ring], across / 10f);
            var currentPoint = Vector3.Lerp(current[0][ring], current[1][ring], across / 10f);
            if (ground.Raycast(new Ray(originalPoint + Vector3.up * 3, Vector3.down), out var originalHit, 6) &&
                !ground.Raycast(new Ray(currentPoint + Vector3.up * 3, Vector3.down), out var hit, 6)) newMissing++;
        }
        var checks = rails.Select((rail, side) =>
        {
            var vertices = rail.sharedMesh.vertices;
            bool outsidePreserved = true;
            for (int index = 0; index < vertices.Length; index++)
            {
                if (Weight(index / 8 / 4f) > .0001f) continue;
                var previous = before["rails"][side]["vertices"][index];
                if (Vector3.Distance(vertices[index], new Vector3((float)previous[0], (float)previous[1], (float)previous[2])) > .00001f) outsidePreserved = false;
            }
            return new { rail.name, outsidePreserved,
                colliderMatches = rail.GetComponent<MeshCollider>().sharedMesh == rail.sharedMesh,
                metaPreserved = File.ReadAllText(AssetDatabase.GetAssetPath(rail.sharedMesh) + ".meta") == (string)before["rails"][side]["meta"] };
        }).ToArray();
        var widths = new[] { 48, 51, 54, 55, 56, 59, 63, 65 }.Select(step => new
        {
            step, before = new Vector2(old[0][step * 4].x - old[1][step * 4].x, old[0][step * 4].z - old[1][step * 4].z).magnitude,
            after = new Vector2(current[0][step * 4].x - current[1][step * 4].x, current[0][step * 4].z - current[1][step * 4].z).magnitude
        }).ToArray();
        var report = new { saved = !UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, checks, samples, newMissing, widths };
        File.WriteAllText(Folder + "Verification.json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return JsonConvert.SerializeObject(report);
    }
}
