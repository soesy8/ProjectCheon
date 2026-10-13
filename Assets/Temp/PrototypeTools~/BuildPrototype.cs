if (EditorApplication.isPlaying)
    throw new System.InvalidOperationException("Stop Play Mode before building the prototype.");

var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (originalScene.path != "Assets/Scenes/PC_ShowRoom.unity")
    throw new System.InvalidOperationException("Expected the existing PC_ShowRoom scene.");
if (originalScene.isDirty)
    throw new System.InvalidOperationException("The existing scene contains unsaved changes.");

void EnsureFolder(string parent, string name)
{
    if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        AssetDatabase.CreateFolder(parent, name);
}

EnsureFolder("Assets/Proto", "Scenes");
EnsureFolder("Assets/Proto", "Arts");
EnsureFolder("Assets/Proto/Arts", "Materials");
string scenePath = "Assets/Proto/Scenes/PC_Prototype.unity";
if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
    throw new System.InvalidOperationException("Prototype scene already exists.");

// 복사본을 열어 원래 쇼룸 씬과 GUID를 유지한다.
if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(originalScene, scenePath, true))
    throw new System.InvalidOperationException("Could not save the prototype scene copy.");
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

Material CreateMaterial(string name, Color color)
{
    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    material.name = name;
    material.SetColor("_BaseColor", color);
    material.SetFloat("_Smoothness", 0.1f);
    AssetDatabase.CreateAsset(material, "Assets/Proto/Arts/Materials/" + name + ".mat");
    return material;
}

var realityMaterial = CreateMaterial("M_RealityGround", new Color(0.35f, 0.48f, 0.34f));
var spiritMaterial = CreateMaterial("M_SpiritGround", new Color(0.32f, 0.24f, 0.47f));
var playerMaterial = CreateMaterial("M_Player", new Color(0.85f, 0.91f, 0.87f));
var bossMaterial = CreateMaterial("M_BossDummy", new Color(0.68f, 0.24f, 0.27f));
var gateMaterial = CreateMaterial("M_WorldGate", new Color(0.83f, 0.66f, 0.29f));

var ground = GameObject.Find("Ground");
Undo.RecordObject(ground.transform, "Configure prototype map");
ground.transform.position = new Vector3(-10f, 0f, 0f);
ground.transform.localScale = new Vector3(2f, 1f, 2f);
ground.GetComponent<Renderer>().sharedMaterial = realityMaterial;
var spiritGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
Undo.RegisterCreatedObjectUndo(spiritGround, "Create spirit ground");
spiritGround.name = "SpiritGround";
spiritGround.transform.position = new Vector3(10f, 0f, 0f);
spiritGround.transform.localScale = new Vector3(2f, 1f, 2f);
spiritGround.GetComponent<Renderer>().sharedMaterial = spiritMaterial;

var camera = Camera.main;
Undo.RecordObject(camera, "Configure prototype camera");
Undo.RecordObject(camera.transform, "Configure prototype camera");
camera.transform.position = new Vector3(20f, 32f, -30f);
camera.transform.LookAt(new Vector3(0f, 0f, 0f));
camera.orthographic = true;
camera.orthographicSize = 17f;
camera.nearClipPlane = 0.1f;
camera.farClipPlane = 150f;
camera.clearFlags = CameraClearFlags.SolidColor;
camera.backgroundColor = new Color(0.14f, 0.19f, 0.19f);

var light = GameObject.Find("Directional Light").GetComponent<Light>();
Undo.RecordObject(light, "Configure prototype light");
light.intensity = 1.4f;
light.shadows = LightShadows.Soft;
RenderSettings.skybox = null;
RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
RenderSettings.ambientLight = new Color(0.65f, 0.68f, 0.75f);
RenderSettings.fog = false;

var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
Undo.RegisterCreatedObjectUndo(player, "Create prototype player");
player.name = "Player";
player.tag = "Player";
player.transform.position = new Vector3(-10f, 1.05f, 0f);
player.transform.forward = Vector3.right;
player.GetComponent<Renderer>().sharedMaterial = playerMaterial;
UnityEngine.Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
var controller = player.AddComponent<CharacterController>();
controller.height = 2f;
controller.radius = 0.5f;
controller.skinWidth = 0.03f;
controller.stepOffset = 0.25f;
var movement = player.AddComponent<PlayerMovement>();
var movementSettings = new SerializedObject(movement);
movementSettings.FindProperty("movementCamera").objectReferenceValue = camera;
movementSettings.ApplyModifiedPropertiesWithoutUndo();
player.AddComponent<PlayerCombat>();

var boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
Undo.RegisterCreatedObjectUndo(boss, "Create boss dummy");
boss.name = "BossDummy";
boss.transform.position = new Vector3(11f, 3f, 0f);
boss.transform.localScale = Vector3.one * 3f;
boss.GetComponent<Renderer>().sharedMaterial = bossMaterial;
boss.AddComponent<BossDummy>();

var gate = new GameObject("WorldGate");
Undo.RegisterCreatedObjectUndo(gate, "Create world gate");
var worldGate = gate.AddComponent<WorldGate>();
var gateSettings = new SerializedObject(worldGate);
gateSettings.FindProperty("player").objectReferenceValue = player.transform;
gateSettings.FindProperty("worldCamera").objectReferenceValue = camera;
gateSettings.ApplyModifiedPropertiesWithoutUndo();

foreach (float z in new[] { -2f, 2f })
{
    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
    pillar.name = z < 0f ? "GatePillarFront" : "GatePillarBack";
    Undo.RegisterCreatedObjectUndo(pillar, "Create gate pillar");
    pillar.transform.SetParent(gate.transform);
    pillar.transform.position = new Vector3(0f, 2.5f, z);
    pillar.transform.localScale = new Vector3(0.8f, 5f, 0.8f);
    pillar.GetComponent<Renderer>().sharedMaterial = gateMaterial;
}

// 눈에 보이는 문은 기둥 두 개뿐이며, 양옆은 충돌체로 막아 문을 통해서만 왕복한다.
foreach (float side in new[] { -1f, 1f })
{
    var boundary = new GameObject(side < 0f ? "BoundaryFront" : "BoundaryBack");
    Undo.RegisterCreatedObjectUndo(boundary, "Create world boundary");
    boundary.transform.SetParent(gate.transform);
    boundary.transform.position = new Vector3(0f, 4f, side * 6.2f);
    var collider = boundary.AddComponent<BoxCollider>();
    collider.size = new Vector3(0.8f, 8f, 7.6f);
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
AssetDatabase.SaveAssets();
Selection.activeGameObject = player;
if (SceneView.lastActiveSceneView != null)
    SceneView.lastActiveSceneView.LookAt(Vector3.zero, camera.transform.rotation, 27f);

return new { scene = scene.path, mapWidth = 40, mapDepth = 20, playerHeight = 2, bossHeight = 6, roots = scene.rootCount };
