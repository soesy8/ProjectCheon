var veil = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var serialized = new SerializedObject(veil);
var material = (Material)serialized.FindProperty("cloudMaterial").objectReferenceValue;
var shader = material.shader;
if (ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Cloud noise shader failed to compile.");
material.SetFloat("_NoiseStrength", serialized.FindProperty("noiseStrength").floatValue);
material.SetFloat("_NoiseScale", serialized.FindProperty("noiseScale").floatValue);
material.SetFloat("_NoiseTime", 0f);
EditorUtility.SetDirty(material);
AssetDatabase.SaveAssetIfDirty(material);
EditorUtility.SetDirty(veil);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(veil.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(veil.gameObject.scene);
return new { layers = serialized.FindProperty("layersPerCloud").intValue * 6,
    noise = serialized.FindProperty("noiseStrength").floatValue,
    scale = serialized.FindProperty("noiseScale").floatValue,
    shaderErrors = ShaderUtil.ShaderHasError(shader) };
