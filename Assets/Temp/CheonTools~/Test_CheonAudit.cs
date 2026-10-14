var root = "Assets/Test/Cheon";
var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(root + "/Arts/Fonts/Test_CheonKorean.asset");
var shaderErrors = UnityEditor.AssetDatabase.FindAssets("t:Shader", new[] { root })
    .Select(id => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(UnityEditor.AssetDatabase.GUIDToAssetPath(id)))
    .SelectMany(shader => UnityEditor.ShaderUtil.GetShaderMessages(shader).Where(message => message.severity.ToString() == "Error").Select(message => shader.name + ": " + message.message)).ToArray();
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var objects = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<UnityEngine.Transform>(true)).Select(item => item.gameObject).ToArray();
return new { scene = scene.path, objects = objects.Length, missingScripts = objects.Sum(UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount), fontCharacters = font.characterTable.Count, atlasAssets = font.atlasTextures.Select(UnityEditor.AssetDatabase.GetAssetPath).ToArray(), shaderErrors = shaderErrors };
