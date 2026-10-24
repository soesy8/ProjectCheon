var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (EditorApplication.isPlaying || scene.path != "Assets/Test/CheonMountainMap/Scenes/Test_CheonMountainMap.unity")
    throw new InvalidOperationException("마운틴 씬의 편집 모드에서만 배치합니다.");
if (GameObject.Find("Test_ShrineEntranceWalls") != null)
    throw new InvalidOperationException("이미 배치한 담 그룹이 있습니다.");

var middlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/SelfModeling/Wall_Middle.prefab");
var endPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/SelfModeling/Wall_End.prefab");
if (middlePrefab == null || endPrefab == null)
    throw new InvalidOperationException("담 프리팹을 찾지 못했습니다.");

Physics.SyncTransforms();
Func<Vector3, float> groundHeight = point => Physics.RaycastAll(new Vector3(point.x, 55f, point.z), Vector3.down, 50f)
    .Where(hit => hit.collider.name == "Mountain_Continuous_Core" || hit.collider.name == "Shrine_Floor_Flat_UV")
    .Select(hit => hit.point.y).DefaultIfEmpty(27.12f).Max();

Undo.IncrementCurrentGroup();
var undoGroup = Undo.GetCurrentGroup();
Undo.SetCurrentGroupName("사당 계단 입구 담 배치");
var root = new GameObject("Test_ShrineEntranceWalls");
Undo.RegisterCreatedObjectUndo(root, "사당 입구 담 그룹");
root.transform.position = new Vector3(0f, 27.12f, 37f);

var paths = new [] {
    new [] { new Vector3(13.8f,0,42.3f), new Vector3(4.4f,0,40.9f) },
    new [] { new Vector3(-0.6f,0,36.6f), new Vector3(-5f,0,33.4f), new Vector3(-8f,0,33.2f), new Vector3(-11.6f,0,35.5f), new Vector3(-15.4f,0,39f) }
};
var groups = new [] { "Test_EntranceWall_Left", "Test_EntranceWall_Right" };
var report = new List<object>();

for (var side = 0; side < paths.Length; side++)
{
    var parent = new GameObject(groups[side]);
    Undo.RegisterCreatedObjectUndo(parent, "담 구간 그룹");
    parent.transform.SetParent(root.transform, false);
    var segmentIndex = 0;
    var path = paths[side];
    for (var leg = 0; leg < path.Length - 1; leg++)
    {
        var delta = path[leg + 1] - path[leg];
        var length = delta.magnitude;
        var direction = delta / length;
        var count = Mathf.Max(1, Mathf.RoundToInt(length / 2.4f));
        var segmentLength = length / count;
        var rotation = Quaternion.FromToRotation(Vector3.right, direction);
        for (var index = 0; index < count; index++)
        {
            var start = path[leg] + direction * segmentLength * index;
            var finish = start + direction * segmentLength;
            // 밑면이 경사면 위에 뜨지 않도록 구간 내 지면의 최저 높이에 맞춥니다.
            var height = Enumerable.Range(0, 9).Select(sample => groundHeight(Vector3.Lerp(start, finish, sample / 8f))).Min() - 0.04f;
            var wall = (GameObject)PrefabUtility.InstantiatePrefab(middlePrefab, scene);
            Undo.RegisterCreatedObjectUndo(wall, "담 몸체 배치");
            wall.name = "Test_Wall_Middle_" + (++segmentIndex).ToString("D2");
            wall.transform.SetParent(parent.transform, true);
            wall.transform.SetPositionAndRotation(new Vector3(finish.x, height, finish.z), rotation);
            wall.transform.localScale = new Vector3((segmentLength + 0.025f) / 2.4f, 1f, 1f);
            foreach (var mesh in wall.GetComponentsInChildren<MeshFilter>())
            {
                var collider = Undo.AddComponent<MeshCollider>(mesh.gameObject);
                collider.sharedMesh = mesh.sharedMesh;
            }
            report.Add(new { side=side, part=wall.name, position=wall.transform.position.ToString("F2"), length=segmentLength });
        }
    }

    // 입구 끝과 꺾이는 지점은 같은 에셋의 마감 기둥으로 연결합니다.
    for (var index = 0; index < path.Length; index++)
    {
        var direction = index == path.Length - 1 ? (path[index] - path[index - 1]).normalized : (path[index + 1] - path[index]).normalized;
        var height = groundHeight(path[index]) - 0.04f;
        var post = (GameObject)PrefabUtility.InstantiatePrefab(endPrefab, scene);
        Undo.RegisterCreatedObjectUndo(post, "담 마감 배치");
        post.name = "Test_Wall_End_" + (index + 1).ToString("D2");
        post.transform.SetParent(parent.transform, true);
        post.transform.SetPositionAndRotation(new Vector3(path[index].x, height, path[index].z) - direction * 0.24f,
            Quaternion.FromToRotation(Vector3.right, direction));
        foreach (var mesh in post.GetComponentsInChildren<MeshFilter>())
        {
            var collider = Undo.AddComponent<MeshCollider>(mesh.gameObject);
            collider.sharedMesh = mesh.sharedMesh;
        }
    }
}
Undo.CollapseUndoOperations(undoGroup);
Physics.SyncTransforms();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
Selection.activeGameObject = root;
SceneView.RepaintAll();
return new { group=root.name, segments=report, colliders=root.GetComponentsInChildren<Collider>().Length, saved=false };
