var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Test/CheonMountainMap/Scenes/Test_CheonMountainMap.unity")
    throw new System.InvalidOperationException("현재 씬이 변경되어 안개 생성을 중단했습니다.");
if (GameObject.Find("HeightFog_VFX") != null)
    throw new System.InvalidOperationException("HeightFog_VFX가 이미 있습니다.");

var shader = Shader.Find("Cheon/VFX/Height Fog Volume");
if (shader == null || !shader.isSupported || UnityEditor.ShaderUtil.ShaderHasError(shader))
    throw new System.InvalidOperationException("안개 셰이더를 먼저 확인해야 합니다.");

const string materialPath = "Assets/Arts/Materials/M_HeightFogVolume.mat";
var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
if (material == null)
{
    material = new Material(shader) { name = "M_HeightFogVolume", enableInstancing = true };
    AssetDatabase.CreateAsset(material, materialPath);
}

Undo.IncrementCurrentGroup();
int undoGroup = Undo.GetCurrentGroup();
Undo.SetCurrentGroupName("Create height fog VFX");
var root = new GameObject("HeightFog_VFX");
Undo.RegisterCreatedObjectUndo(root, "Create height fog VFX");
root.transform.position = new Vector3(-28f, 24f, -21f);
var fog = Undo.AddComponent<Cheon.World.HeightFogVolume>(root);
var volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
volume.name = "Fog Volume";
Undo.RegisterCreatedObjectUndo(volume, "Create fog volume renderer");
Undo.SetTransformParent(volume.transform, root.transform, "Parent fog volume");
Undo.DestroyObjectImmediate(volume.GetComponent<Collider>());
var renderer = volume.GetComponent<MeshRenderer>();
renderer.sharedMaterial = material;
renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
renderer.receiveShadows = false;
renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
renderer.allowOcclusionWhenDynamic = false;
var settings = new SerializedObject(fog);
settings.FindProperty("volumeRenderer").objectReferenceValue = renderer;
settings.ApplyModifiedProperties();
fog.ApplySettings();
Undo.CollapseUndoOperations(undoGroup);
EditorUtility.SetDirty(fog);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
AssetDatabase.SaveAssets();
Selection.activeGameObject = root;
SceneView.RepaintAll();
return new { root.name, position = root.transform.position.ToString(), rendererBounds = renderer.bounds.ToString(), shader = shader.name, shaderErrors = UnityEditor.ShaderUtil.GetShaderMessages(shader), material = materialPath };
