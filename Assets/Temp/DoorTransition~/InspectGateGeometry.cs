var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
var gate = objects.First(t => t.name == "Hanok_Gate_Placement");
return new {
    hierarchy = objects.Where(t => t == gate || t.IsChildOf(gate)).Select(t => new { t.name, position = t.position.ToString(), local = t.localPosition.ToString(), right = t.right.ToString(), up = t.up.ToString(), scale = t.lossyScale.ToString(), bounds = t.GetComponent<Renderer>() == null ? null : t.GetComponent<Renderer>().bounds.ToString(), meshBounds = t.GetComponent<MeshFilter>() == null ? null : t.GetComponent<MeshFilter>().sharedMesh.bounds.ToString() }).ToArray(),
    terrain = objects.Where(t => new [] { "Terrace", "Stairway", "Summit" }.Any(word => t.name.Contains(word))).Select(t => new { t.name, bounds = t.GetComponent<Renderer>() == null ? null : t.GetComponent<Renderer>().bounds.ToString() }).ToArray(),
    shaders = objects.Select(t => t.GetComponent<Renderer>()).Where(r => r != null).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.shader.name).Distinct().ToArray()
};
