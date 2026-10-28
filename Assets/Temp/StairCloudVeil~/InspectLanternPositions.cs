var group = GameObject.Find("ApproachLanterns");
var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
return new { playing = Application.isPlaying, dirty = game.gameObject.scene.isDirty,
    lanterns = group.GetComponentsInChildren<Transform>(true)
        .Where(t => t.name.StartsWith("LeftLantern_") || t.name.StartsWith("RightLantern_"))
        .OrderByDescending(t => t.position.y).Select(t => new {
            t.name, x = t.position.x, y = t.position.y, z = t.position.z }).ToArray() };
