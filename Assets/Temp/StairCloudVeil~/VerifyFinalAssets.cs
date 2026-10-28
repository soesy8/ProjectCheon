const string texturePath = "Assets/Arts/Effects/StairMist/Textures/T_StairMistCloud.png";
AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Arts/Shaders/MountainPass/S_StairMistCloud.shader");
var veil = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
var serialized = new SerializedObject(veil);
return new { width = texture.width, height = texture.height, importer.maxTextureSize,
    platforms = new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL" }
        .Select(p => new { platform = p, maxSize = importer.GetPlatformTextureSettings(p).maxTextureSize }).ToArray(),
    shaderErrors = ShaderUtil.ShaderHasError(shader),
    opacity = serialized.FindProperty("maximumOpacity").floatValue,
    textureLinked = serialized.FindProperty("cloudTexture").objectReferenceValue == texture,
    materialLinked = serialized.FindProperty("cloudMaterial").objectReferenceValue != null,
    scene = veil.gameObject.scene.path };
