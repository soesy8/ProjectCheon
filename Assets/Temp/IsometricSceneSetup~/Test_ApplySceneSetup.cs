if (EditorApplication.isPlaying) throw new System.InvalidOperationException("배치는 편집 모드에서만 적용합니다.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var stairs = GameObject.Find("Test_CheonMountainMap/Stairway_Winding_Approach").GetComponent<MeshCollider>();
var points = new System.Collections.Generic.List<Vector3>();
for (float z = 90f; z <= 104f; z += 0.15f)
 for (float x = -30f; x <= -14f; x += 0.15f) {
  RaycastHit hit;
  if (stairs.Raycast(new Ray(new Vector3(x, 50f, z), Vector3.down), out hit, 60f) && hit.normal.y > 0.98f) points.Add(hit.point);
 }
float bottomHeight = points.Min(p => p.y);
var bottomPoints = points.Where(p => Mathf.Abs(p.y - bottomHeight) < 0.015f).ToArray();
var bottomCenter = new Vector3(bottomPoints.Average(p=>p.x), bottomHeight, bottomPoints.Average(p=>p.z));
Undo.RecordObject(player.transform, "캐릭터를 가장 아래 계단 끝으로 이동");
float nextHeight = points.Where(p => p.y > bottomHeight + 0.1f).Min(p => p.y);
var nextPoints = points.Where(p => Mathf.Abs(p.y - nextHeight) < 0.015f).ToArray();
var uphillDirection = new Vector3(nextPoints.Average(p => p.x) - bottomCenter.x, 0f, nextPoints.Average(p => p.z) - bottomCenter.z);
player.transform.SetPositionAndRotation(bottomCenter + Vector3.up * 0.05f, Quaternion.LookRotation(uphillDirection));
Physics.SyncTransforms();
var camera = Camera.main;
var followCamera = camera.GetComponent<Cheon.Testing.MountainMap.Test_MountainPlayerCamera>();
Undo.RecordObject(camera.transform, "카메라 시작 위치 갱신");
followCamera.Configure(player.transform);
var rig = new GameObject("Test_IsometricView");
Undo.RegisterCreatedObjectUndo(rig, "테스트 아이소메트릭 시점 추가");
rig.transform.position = player.transform.position;
var isometric = rig.AddComponent<Cheon.Testing.MountainMap.Test_IsometricView>();
isometric.Configure(camera, player.transform);
var control = player.AnimationControl;
Undo.RecordObject(control.SavedPlaybackSettings, "현재 애니메이션 재생 수치 저장");
control.CopyPlaybackTo(control.SavedPlaybackSettings);
EditorUtility.SetDirty(control.SavedPlaybackSettings);
AssetDatabase.SaveAssetIfDirty(control.SavedPlaybackSettings);
control.SavedTimings.Validate();
EditorUtility.SetDirty(control.SavedTimings);
AssetDatabase.SaveAssetIfDirty(control.SavedTimings);
EditorUtility.SetDirty(control);
EditorUtility.SetDirty(camera);
EditorUtility.SetDirty(followCamera);
EditorUtility.SetDirty(isometric);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
Selection.activeGameObject = rig;
if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(player.transform.position + Vector3.up, Quaternion.Euler(35.26439f, 135f, 0f), 14f, true, true);
return new { savedScene = scene.path, bottomSurface = bottomCenter.ToString("F4"), bottomSamples = bottomPoints.Length, bottomMin = new { x=bottomPoints.Min(p=>p.x), z=bottomPoints.Min(p=>p.z)}, bottomMax = new { x=bottomPoints.Max(p=>p.x), z=bottomPoints.Max(p=>p.z)}, playerPosition = player.transform.position.ToString("F4"), cameraRotation = camera.transform.eulerAngles.ToString("F4"), cameraPosition = camera.transform.position.ToString("F4"), cameraOrthographic = camera.orthographic, cameraSize = camera.orthographicSize, rig = JsonUtility.ToJson(isometric) };

