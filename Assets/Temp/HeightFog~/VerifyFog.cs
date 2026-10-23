var root = GameObject.Find("HeightFog_VFX");
var fog = root.GetComponent<Cheon.World.HeightFogVolume>();
var renderer = root.GetComponentInChildren<MeshRenderer>();
var settings = new SerializedObject(fog);
var shader = renderer.sharedMaterial.shader;
foreach (string suffix in new[] { "Before", "After", "Game", "Final", "Play", "Inside" })
{
    string source = "Assets/Temp/HeightFog_" + suffix + ".png";
    string target = "Assets/Temp/T_HeightFog_" + suffix + ".png";
    if (AssetDatabase.LoadMainAssetAtPath(source) != null)
    {
        string error = AssetDatabase.MoveAsset(source, target);
        if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
    }
}
AssetDatabase.SaveAssets();
return new
{
    root.name,
    independentRoot = root.transform.parent == null,
    rootComponents = root.GetComponents<Component>().Select(c => c.GetType().Name).ToArray(),
    cutoffHeight = root.transform.position.y,
    area = settings.FindProperty("areaSize").vector2Value.ToString(),
    renderer.enabled,
    rendererBounds = renderer.bounds.ToString(),
    colliderCount = root.GetComponentsInChildren<Collider>().Length,
    material = AssetDatabase.GetAssetPath(renderer.sharedMaterial),
    shaderErrors = ShaderUtil.GetShaderMessages(shader),
    root.scene.isDirty,
    playing = EditorApplication.isPlaying,
    originalCameraPosition = Camera.main.transform.position.ToString()
};
