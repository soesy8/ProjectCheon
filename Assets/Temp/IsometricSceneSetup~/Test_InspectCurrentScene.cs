var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
Func<Transform, string> path = null;
path = t => t.parent == null ? t.name : path(t.parent) + "/" + t.name;
var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
return new {
 playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused,
 scene = scene.path, dirty = scene.isDirty,
 roots = scene.GetRootGameObjects().Select(g => new {g.name, active = g.activeSelf}).ToArray(),
 players = all.Where(t => t.GetComponent<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>() != null).Select(t => new {path = path(t), position = t.position.ToString("F4"), rotation = t.eulerAngles.ToString("F2"), movement = JsonUtility.ToJson(t.GetComponent<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>()), body = new { center = t.GetComponent<CharacterController>().center.ToString(), height = t.GetComponent<CharacterController>().height, radius = t.GetComponent<CharacterController>().radius }}).ToArray(),
 animation = all.Select(t => t.GetComponent<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>()).Where(c => c != null).Select(c => new {path = path(c.transform), values = JsonUtility.ToJson(c), savedTimings = c.SavedTimings != null ? JsonUtility.ToJson(c.SavedTimings) : null, runtimeTimings = c.RuntimeTimings != null ? JsonUtility.ToJson(c.RuntimeTimings) : null, savedPlayback = c.SavedPlaybackSettings != null ? JsonUtility.ToJson(c.SavedPlaybackSettings) : null}).ToArray(),
 cameras = all.Select(t => t.GetComponent<Camera>()).Where(c => c != null).Select(c => new {path = path(c.transform), enabled = c.enabled, position = c.transform.position.ToString("F4"), rotation = c.transform.eulerAngles.ToString("F2"), ortho = c.orthographic, size = c.orthographicSize, components = c.GetComponents<Component>().Select(x => x.GetType().FullName).ToArray(), follow = c.GetComponent<Cheon.Testing.MountainMap.Test_MountainPlayerCamera>() != null ? JsonUtility.ToJson(c.GetComponent<Cheon.Testing.MountainMap.Test_MountainPlayerCamera>()) : null}).ToArray(),
 stairs = all.Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, "stair|approach|landing|spawn|step", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Take(120).Select(t => new {path = path(t), position = t.position.ToString("F4"), rotation = t.eulerAngles.ToString("F2"), scale = t.lossyScale.ToString("F2"), renderers = t.GetComponentsInChildren<Renderer>().Select(r => new {r.name, min = r.bounds.min.ToString("F4"), max = r.bounds.max.ToString("F4")}).ToArray()}).ToArray()
};


