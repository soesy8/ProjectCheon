var boss = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassBossPatternController>();
var serialized = new SerializedObject(boss);
var visual = (Transform)serialized.FindProperty("visual").objectReferenceValue;
var ancestors = new System.Collections.Generic.List<object>();
for (var node = visual; node != null; node = node.parent)
    ancestors.Add(new { node.name, localAngles = node.localEulerAngles.ToString("F4"), worldAngles = node.eulerAngles.ToString("F4"),
        localScale = node.localScale.ToString("F4"), worldScale = node.lossyScale.ToString("F4"), up = node.up.ToString("F4") });
return new { playing = Application.isPlaying, visual = visual.name, ancestors,
    components = boss.GetComponents<Component>().Select(c => c.GetType().Name).ToArray(),
    modelNodes = visual.GetComponentsInChildren<Transform>(true).Take(35).Select(t => new { t.name,
        rotation = t.localEulerAngles.ToString("F3"), scale = t.localScale.ToString("F3"),
        renderer = t.GetComponent<Renderer>() != null, animator = t.GetComponent<Animator>() != null }).ToArray() };
