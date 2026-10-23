var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var view = SceneView.lastActiveSceneView;
return new
{
    scene = scene.path,
    scene.isDirty,
    playing = EditorApplication.isPlaying,
    roots = scene.GetRootGameObjects().Select(g => new
    {
        g.name,
        position = g.transform.position.ToString(),
        components = g.GetComponents<Component>().Select(c => c == null ? "Missing" : c.GetType().Name).ToArray()
    }).ToArray(),
    terrains = Terrain.activeTerrains.Select(t => new
    {
        t.name,
        position = t.transform.position.ToString(),
        size = t.terrainData.size.ToString()
    }).ToArray(),
    cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Select(c => new
    {
        c.name,
        position = c.transform.position.ToString(),
        angles = c.transform.eulerAngles.ToString(),
        c.farClipPlane,
        c.nearClipPlane
    }).ToArray(),
    sceneView = view == null ? null : new { pivot = view.pivot.ToString(), view.size, position = view.camera.transform.position.ToString(), angles = view.camera.transform.eulerAngles.ToString() },
    pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? null : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,
    fog = new { RenderSettings.fog, RenderSettings.fogDensity, color = RenderSettings.fogColor.ToString() }
};
