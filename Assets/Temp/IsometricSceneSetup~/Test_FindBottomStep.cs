var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Temp/IsometricSceneSetup~/Test_SceneBefore.unity", true);
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = player.AnimationControl;
System.IO.File.WriteAllText("Assets/Temp/IsometricSceneSetup~/Test_AnimationBefore.json", JsonUtility.ToJson(control, true));
System.IO.File.WriteAllText("Assets/Temp/IsometricSceneSetup~/Test_TimingsBefore.json", JsonUtility.ToJson(control.SavedTimings, true));
System.IO.File.WriteAllText("Assets/Temp/IsometricSceneSetup~/Test_PlaybackBefore.json", JsonUtility.ToJson(control.SavedPlaybackSettings, true));
var stairs = GameObject.Find("Test_CheonMountainMap/Stairway_Winding_Approach");
var collider = stairs.GetComponent<MeshCollider>();
var mesh = stairs.GetComponent<MeshFilter>().sharedMesh;
var samples = new System.Collections.Generic.List<object>();
for (float z = 98f; z <= 104f; z += 0.5f) {
 var row = new System.Collections.Generic.List<Vector3>();
 for (float x = -30f; x <= 4f; x += 0.25f) {
  RaycastHit hit;
  if (collider.Raycast(new Ray(new Vector3(x, 50f, z), Vector3.down), out hit, 60f) && hit.normal.y > 0.9f) row.Add(hit.point);
 }
 if (row.Count > 0) samples.Add(new {z, minX = row.Min(p=>p.x), maxX = row.Max(p=>p.x), minY = row.Min(p=>p.y), maxY=row.Max(p=>p.y), center = row[row.Count/2].ToString("F4")});
}
return new { mesh = mesh.name, readable = mesh.isReadable, cameraNear = Camera.main.nearClipPlane, cameraFar = Camera.main.farClipPlane, samples };
