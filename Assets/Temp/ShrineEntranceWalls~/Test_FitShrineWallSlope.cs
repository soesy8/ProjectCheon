var root = GameObject.Find("Test_ShrineEntranceWalls");
if (root == null || EditorApplication.isPlaying)
    throw new InvalidOperationException("편집 모드의 담 그룹이 필요합니다.");
Physics.SyncTransforms();
Func<Vector3, float> groundHeight = point => Physics.RaycastAll(new Vector3(point.x, 55f, point.z), Vector3.down, 50f)
    .Where(hit => hit.collider.name == "Mountain_Continuous_Core" || hit.collider.name == "Shrine_Floor_Flat_UV")
    .Select(hit => hit.point.y).DefaultIfEmpty(27.12f).Max();
var paths = new [] {
    new [] { new Vector3(13.8f,0,42.3f), new Vector3(4.4f,0,40.9f) },
    new [] { new Vector3(-0.6f,0,36.6f), new Vector3(-5f,0,33.4f), new Vector3(-8f,0,33.2f), new Vector3(-11.6f,0,35.5f), new Vector3(-15.4f,0,39f) }
};
var groups = new [] { "Test_EntranceWall_Left", "Test_EntranceWall_Right" };
var report = new List<object>();
Undo.IncrementCurrentGroup();
var undoGroup = Undo.GetCurrentGroup();
Undo.SetCurrentGroupName("담 연결부 경사 맞춤");

for (var side = 0; side < paths.Length; side++)
{
    var parent = root.transform.Find(groups[side]);
    var path = paths[side];
    var segmentIndex = 0;
    for (var leg = 0; leg < path.Length - 1; leg++)
    {
        var start = path[leg];
        var finish = path[leg + 1];
        start.y = groundHeight(start) - 0.04f;
        finish.y = groundHeight(finish) - 0.04f;
        // 구간의 양 끝을 연결하는 경사를 사용하여 지붕과 몸체가 연속되도록 맞춥니다.
        var belowGroundOffset = Enumerable.Range(0, 33)
            .Select(sample => { var point=Vector3.Lerp(start, finish, sample/32f); return point.y-groundHeight(point)+0.04f; })
            .Max();
        start.y -= Mathf.Max(0f, belowGroundOffset);
        finish.y -= Mathf.Max(0f, belowGroundOffset);
        var delta = finish - start;
        var flatLength = new Vector2(delta.x, delta.z).magnitude;
        var count = Mathf.Max(1, Mathf.RoundToInt(flatLength / 2.4f));
        var yaw = Mathf.Atan2(-delta.z, delta.x) * Mathf.Rad2Deg;
        var pitch = Mathf.Atan2(delta.y, flatLength) * Mathf.Rad2Deg;
        var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, pitch);
        for (var index = 0; index < count; index++)
        {
            var wall = parent.Find("Test_Wall_Middle_" + (++segmentIndex).ToString("D2"));
            Undo.RecordObject(wall, "담 경사 정렬");
            wall.SetPositionAndRotation(Vector3.Lerp(start, finish, (index+1f)/count), rotation);
            wall.localScale = new Vector3((delta.magnitude/count + 0.025f)/2.4f, 1f, 1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(wall);
        }
        report.Add(new {side=side, leg=leg, start=start.ToString("F2"), end=finish.ToString("F2"), slope=pitch});
    }
}
Undo.CollapseUndoOperations(undoGroup);
Physics.SyncTransforms();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
SceneView.RepaintAll();
return report;
