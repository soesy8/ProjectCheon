if (Application.isPlaying) throw new System.Exception("Configure fog markers in Edit Mode.");
var group = GameObject.Find("ApproachLanterns").transform;
Vector3 fourth = (group.Find("LeftLantern_65").position + group.Find("RightLantern_65").position) * 0.5f;
Vector3 top = (group.Find("LeftLantern_92").position + group.Find("RightLantern_92").position) * 0.5f;
Vector3 second = (group.Find("LeftLantern_83").position + group.Find("RightLantern_83").position) * 0.5f;
Vector3[] positions = { fourth, (top + second) * 0.5f };
string[] names = { "FogFadeStart_FourthLanternPair", "FogFadeEnd_BetweenTopLanternPairs" };
var markers = new Transform[2];
for (int i = 0; i < positions.Length; i++)
{
    var hits = Physics.RaycastAll(new Vector3(positions[i].x, 100f, positions[i].z), Vector3.down, 120f)
        .Where(h => h.normal.y > 0.8f).OrderByDescending(h => h.point.y).ToArray();
    if (hits.Length == 0) throw new System.Exception("Stair floor not found for fog marker.");
    var marker = group.Find(names[i]);
    if (marker == null)
    {
        var item = new GameObject(names[i]);
        Undo.RegisterCreatedObjectUndo(item, "Create fog range marker");
        marker = item.transform;
        marker.SetParent(group, false);
    }
    Undo.RecordObject(marker, "Position fog range marker");
    marker.position = hits[0].point;
    markers[i] = marker;
}
var mist = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassMountainMist>();
Undo.RecordObject(mist, "Set lantern fog range");
var serialized = new SerializedObject(mist);
serialized.FindProperty("fadeStartPoint").objectReferenceValue = markers[0];
serialized.FindProperty("fadeEndPoint").objectReferenceValue = markers[1];
serialized.ApplyModifiedProperties();
mist.ApproachFog.SetSidewaysOpening(Vector3.zero, Vector3.right, 0f, 4f);
mist.ApproachFog.ApplySettings();
EditorUtility.SetDirty(mist);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mist.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(mist.gameObject.scene);
return new { start = markers[0].position.ToString("F4"), end = markers[1].position.ToString("F4"),
    distance = Vector2.Distance(new Vector2(markers[0].position.x, markers[0].position.z),
        new Vector2(markers[1].position.x, markers[1].position.z)) };
