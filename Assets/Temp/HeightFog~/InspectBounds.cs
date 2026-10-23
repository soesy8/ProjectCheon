var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
return scene.GetRootGameObjects().Select(root => new
{
    root.name,
    children = Enumerable.Range(0, root.transform.childCount).Select(i => root.transform.GetChild(i)).Select(t => new
    {
        t.name,
        position = t.position.ToString(),
        rendererCount = t.GetComponentsInChildren<Renderer>().Length,
        bounds = t.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate(new Bounds(t.position, Vector3.zero), (b, next) => { b.Encapsulate(next); return b; }).ToString(),
        components = t.GetComponents<Component>().Select(c => c == null ? "Missing" : c.GetType().Name).ToArray()
    }).ToArray()
}).ToArray();
