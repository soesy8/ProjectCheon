var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
return new {
    scene = scene.path, dirty = scene.isDirty,
    roots = scene.GetRootGameObjects().Select(root => new { root.name, position = root.transform.position.ToString(), components = root.GetComponents<Component>().Select(c => c == null ? "Missing" : c.GetType().FullName).ToArray() }).ToArray(),
    selected = Selection.gameObjects.Select(o => o.name).ToArray(),
    candidates = objects.Where(t => new [] { "gate", "door", "player", "경계", "문", "도사" }.Any(word => t.name.ToLowerInvariant().Contains(word))).Take(70).Select(t => new { t.name, id = t.gameObject.GetInstanceID(), position = t.position.ToString(), rotation = t.eulerAngles.ToString(), forward = t.forward.ToString(), components = t.GetComponents<Component>().Select(c => c == null ? "Missing" : c.GetType().FullName).ToArray() }).ToArray(),
    cameras = objects.Select(t => t.GetComponent<Camera>()).Where(c => c != null).Select(c => new { c.name, c.clearFlags, background = c.backgroundColor.ToString(), position = c.transform.position.ToString(), rotation = c.transform.eulerAngles.ToString() }).ToArray(),
    lights = objects.Select(t => t.GetComponent<Light>()).Where(l => l != null).Select(l => new { l.name, l.type, l.intensity, color = l.color.ToString() }).ToArray(),
    ambientMode = RenderSettings.ambientMode.ToString(), ambient = RenderSettings.ambientLight.ToString(), ambientIntensity = RenderSettings.ambientIntensity,
    fog = RenderSettings.fog, fogColor = RenderSettings.fogColor.ToString(), fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
    sky = RenderSettings.skybox == null ? null : new { RenderSettings.skybox.name, shader = RenderSettings.skybox.shader.name, properties = Enumerable.Range(0, RenderSettings.skybox.shader.GetPropertyCount()).Select(i => new { name = RenderSettings.skybox.shader.GetPropertyName(i), type = RenderSettings.skybox.shader.GetPropertyType(i).ToString() }).ToArray() }
};
