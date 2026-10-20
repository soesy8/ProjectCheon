using UnityEngine;
using UnityEditor;
using System.Linq;
public static class InspectEnvironment
{
    public static object Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var bounds = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.name.Contains("Ground") || r.name.Contains("Floor") || r.name.Contains("Island") || r.name.Contains("지면"))
            .Select(r => new { r.name, center = r.bounds.center, size = r.bounds.size }).Take(20).ToArray();
        return new {
            scene = scene.path,
            cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Select(c => new { c.name, c.enabled, c.transform.position, c.transform.eulerAngles, c.orthographicSize }).ToArray(),
            bounds,
            rootChildren = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>()).Select(t => t.name).Take(45).ToArray()
        };
    }
}
