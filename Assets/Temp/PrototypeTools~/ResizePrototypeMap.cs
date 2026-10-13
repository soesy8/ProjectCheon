var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying || scene.path != "Assets/Proto/Scenes/PC_Prototype.unity")
    throw new System.InvalidOperationException("Open the prototype scene in Edit Mode before resizing.");

var ground = GameObject.Find("Ground");
var spiritGround = GameObject.Find("SpiritGround");
var movement = GameObject.Find("Player").GetComponent<PlayerMovement>();
var gate = GameObject.Find("WorldGate").transform;
var camera = Camera.main;
var boundsBefore = ground.GetComponent<Renderer>().bounds;
boundsBefore.Encapsulate(spiritGround.GetComponent<Renderer>().bounds);
float targetWidth = 100f;
float targetDepth = 50f;
float scaleFactor = targetWidth / boundsBefore.size.x;
if (!Mathf.Approximately(boundsBefore.size.z * scaleFactor, targetDepth))
    throw new System.Exception("Expected a map with a 2:1 aspect ratio.");
var playerPositionBefore = movement.transform.position;
var boss = GameObject.Find("BossDummy").transform;
var bossPositionBefore = boss.position;
var bossScaleBefore = boss.localScale;

UnityEditor.Undo.IncrementCurrentGroup();
int undoGroup = UnityEditor.Undo.GetCurrentGroup();
UnityEditor.Undo.SetCurrentGroupName("Resize prototype map to 100x50");

foreach (var floor in new[] { ground.transform, spiritGround.transform })
{
    UnityEditor.Undo.RecordObject(floor, "Resize prototype floor");
    Vector3 offset = floor.position - gate.position;
    floor.position = gate.position + new Vector3(offset.x * scaleFactor, offset.y, offset.z * scaleFactor);
    Vector3 scale = floor.localScale;
    floor.localScale = new Vector3(scale.x * scaleFactor, scale.y, scale.z * scaleFactor);
}

var boundsAfter = ground.GetComponent<Renderer>().bounds;
boundsAfter.Encapsulate(spiritGround.GetComponent<Renderer>().bounds);
var movementSettings = new UnityEditor.SerializedObject(movement);
movementSettings.FindProperty("mapHalfSize").vector2Value = new Vector2(boundsAfter.extents.x, boundsAfter.extents.z);
movementSettings.ApplyModifiedProperties();

// 중앙 문 크기를 유지하면서 양옆 충돌 경계를 변경한 바닥 끝에 맞춘다.
foreach (var name in new[] { "BoundaryFront", "BoundaryBack" })
{
    var boundary = gate.Find(name);
    var collider = boundary.GetComponent<BoxCollider>();
    UnityEditor.Undo.RecordObject(boundary, "Extend world boundary");
    UnityEditor.Undo.RecordObject(collider, "Extend world boundary");
    float side = Mathf.Sign(boundary.localPosition.z);
    float innerEdge = Mathf.Abs(boundary.localPosition.z) - collider.size.z * 0.5f;
    float newLength = boundsAfter.extents.z - innerEdge;
    var position = boundary.localPosition;
    position.z = side * (innerEdge + newLength * 0.5f);
    boundary.localPosition = position;
    var size = collider.size;
    size.z = newLength;
    collider.size = size;
}

UnityEditor.Undo.RecordObject(camera.transform, "Frame resized map");
UnityEditor.Undo.RecordObject(camera, "Frame resized map");
camera.transform.position = gate.position + (camera.transform.position - gate.position) * scaleFactor;
camera.orthographicSize *= scaleFactor;
camera.farClipPlane *= scaleFactor;
if (UnityEditor.SceneView.lastActiveSceneView != null)
    UnityEditor.SceneView.lastActiveSceneView.LookAt(boundsAfter.center, camera.transform.rotation, boundsAfter.size.magnitude * 0.6f);

if (!Mathf.Approximately(boundsAfter.size.x, boundsBefore.size.x * scaleFactor)
    || !Mathf.Approximately(boundsAfter.size.z, boundsBefore.size.z * scaleFactor)
    || !Mathf.Approximately(boundsAfter.size.x / boundsAfter.size.z, 2f))
    throw new System.Exception("Map dimensions do not match the requested scale.");
if (movement.transform.position != playerPositionBefore || boss.position != bossPositionBefore || boss.localScale != bossScaleBefore)
    throw new System.Exception("Unexpected character or boss change.");

UnityEditor.Undo.CollapseUndoOperations(undoGroup);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new
{
    scene = scene.path,
    before = boundsBefore.size.ToString(),
    after = boundsAfter.size.ToString(),
    mapHalfSize = movementSettings.FindProperty("mapHalfSize").vector2Value.ToString(),
    cameraSize = camera.orthographicSize,
    saved = !scene.isDirty
};
