if (Application.isPlaying) throw new System.Exception("Move the fog range in Edit Mode.");
var group = GameObject.Find("ApproachLanterns").transform;
var mist = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassMountainMist>();
var serialized = new SerializedObject(mist);
var start = (Transform)serialized.FindProperty("fadeStartPoint").objectReferenceValue;
var end = (Transform)serialized.FindProperty("fadeEndPoint").objectReferenceValue;
Vector3 endBefore = end.position;
int startId = start.GetInstanceID();
Vector3 fourth = (group.Find("LeftLantern_65").position + group.Find("RightLantern_65").position) * 0.5f;
var hits = Physics.RaycastAll(new Vector3(fourth.x, 100f, fourth.z), Vector3.down, 120f)
    .Where(h => h.normal.y > 0.8f).OrderByDescending(h => h.point.y).ToArray();
if (hits.Length == 0) throw new System.Exception("No stair floor between the fourth lantern pair.");
Undo.RecordObject(start, "Move fog start to fourth lantern pair");
Undo.RecordObject(start.gameObject, "Rename fog start marker");
start.position = hits[0].point;
start.name = "FogFadeStart_FourthLanternPair";
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mist.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(mist.gameObject.scene);
var calculate = mist.GetType().GetMethod("CalculateClarity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
Vector3 third = (group.Find("LeftLantern_74").position + group.Find("RightLantern_74").position) * 0.5f;
return new { start = start.position.ToString("F4"), endUnchanged = end.position == endBefore,
    referencePreserved = start.GetInstanceID() == startId,
    clarityAtFourth = calculate.Invoke(mist, new object[] { fourth }),
    clarityAtThird = calculate.Invoke(mist, new object[] { third }),
    clarityAtEnd = calculate.Invoke(mist, new object[] { end.position }) };
