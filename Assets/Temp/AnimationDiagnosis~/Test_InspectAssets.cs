var root = "Assets/Test/PlayerFanAnimation/";
var names = new[] { "Models/Test_CheonPlayer.fbx", "Animations/Test_Attack1.fbx", "Animations/Test_Attack2.fbx", "Animations/Test_Attack3.fbx", "Animations/Test_DrawnIdle.fbx" };
var rows = new List<object>();
foreach (var name in names)
{
    var path = root + name;
    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
    rows.Add(new {
        path, avatarValid = avatar != null && avatar.isValid, avatarHuman = avatar != null && avatar.isHuman,
        avatarSetup = importer.avatarSetup.ToString(), sourceAvatar = importer.sourceAvatar == null ? null : AssetDatabase.GetAssetPath(importer.sourceAvatar),
        bones = model.GetComponentsInChildren<Transform>(true).Select(t => new { t.name, parent = t.parent == null ? null : t.parent.name, rotation = t.localRotation.ToString(), position = t.localPosition.ToString() }).ToArray(),
        clips = clips.Select(c => new { c.name, c.length, c.frameRate, c.humanMotion, bindings = AnimationUtility.GetCurveBindings(c).Select(b => b.path + "|" + b.propertyName).ToArray() }).ToArray()
    });
}
var json = Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("Assets/Temp/AnimationDiagnosis~/Test_AssetInspection.json", json);
return rows.Select(r => r).ToArray();
