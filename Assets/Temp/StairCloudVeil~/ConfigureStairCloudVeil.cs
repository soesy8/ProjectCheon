AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
const string texturePath = "Assets/Arts/Effects/StairMist/Textures/T_StairMistCloud.png";
var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
importer.textureType = TextureImporterType.Default;
importer.maxTextureSize = 1024;
var defaultSettings = importer.GetDefaultPlatformTextureSettings();
defaultSettings.maxTextureSize = 1024;
importer.SetPlatformTextureSettings(defaultSettings);
importer.alphaIsTransparency = true;
importer.mipmapEnabled = false;
importer.wrapMode = TextureWrapMode.Clamp;
importer.filterMode = FilterMode.Bilinear;
importer.textureCompression = TextureImporterCompression.Uncompressed;
foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
{
    var settings = importer.GetPlatformTextureSettings(platform);
    settings.name = platform;
    settings.overridden = true;
    settings.maxTextureSize = 1024;
    settings.format = TextureImporterFormat.RGBA32;
    importer.SetPlatformTextureSettings(settings);
}
importer.SaveAndReimport();
if (!AssetDatabase.IsValidFolder("Assets/Arts/Effects/StairMist/Materials"))
    AssetDatabase.CreateFolder("Assets/Arts/Effects/StairMist", "Materials");
var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Arts/Shaders/MountainPass/S_StairMistCloud.shader");
if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Cloud shader did not compile.");
const string materialPath = "Assets/Arts/Effects/StairMist/Materials/M_StairMistCloud.mat";
var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
if (material == null)
{
    material = new Material(shader);
    AssetDatabase.CreateAsset(material, materialPath);
}
material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var mist = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassMountainMist>();
if (game == null || mist == null) throw new System.Exception("Mountain pass scene references missing.");
var veil = game.GetComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
if (veil == null) veil = Undo.AddComponent<Cheon.Prototype.MountainPass.PassStairCloudVeil>(game.gameObject);
var serialized = new SerializedObject(veil);
serialized.FindProperty("game").objectReferenceValue = game;
serialized.FindProperty("mountainMist").objectReferenceValue = mist;
serialized.FindProperty("cloudTexture").objectReferenceValue = material.mainTexture;
serialized.FindProperty("cloudMaterial").objectReferenceValue = material;
serialized.FindProperty("maximumOpacity").floatValue = 0.22f;
serialized.ApplyModifiedProperties();
EditorUtility.SetDirty(material);
EditorUtility.SetDirty(veil);
AssetDatabase.SaveAssetIfDirty(material);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(game.gameObject.scene);
return new { texture = texturePath, width = material.mainTexture.width, height = material.mainTexture.height, shaderErrors = ShaderUtil.ShaderHasError(shader), scene = game.gameObject.scene.path, component = veil.GetType().Name };
