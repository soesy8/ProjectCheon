var root = "Assets/Test/PlayerFanAnimation/";
var targetBones = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Models/Test_CheonPlayer.fbx").GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
var reports = new List<object>();
for (int attack = 1; attack <= 3; attack++)
{
    var path = root + "Animations/Test_Attack" + attack + ".fbx";
    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
    if (importer.animationType != ModelImporterAnimationType.Human) throw new Exception("Unexpected rig type: " + path);
    var beforeGuid = AssetDatabase.AssetPathToGUID(path);
    var description = importer.humanDescription;
    var skeleton = description.skeleton;
    if (skeleton.Length == 0) throw new Exception("Avatar reference skeleton is missing: " + path);
    var rootBefore = skeleton[0];
    int matchedBones = 0;
    // 검증한 방식대로 루트 축과 계층을 보존하고, 대응 본의 기준 자세를 캐릭터 T 자세로 통일한다.
    for (int index = 0; index < skeleton.Length; index++)
    {
        if (!targetBones.TryGetValue(skeleton[index].name, out var targetBone)) continue;
        var bone = skeleton[index];
        bone.position = targetBone.localPosition;
        bone.rotation = targetBone.localRotation;
        bone.scale = targetBone.localScale;
        skeleton[index] = bone;
        matchedBones++;
    }
    if (matchedBones != 33) throw new Exception("Unexpected matching bone count: " + matchedBones + " in " + path);
    if (Quaternion.Angle(rootBefore.rotation, skeleton[0].rotation) > 0.001f || rootBefore.position != skeleton[0].position || rootBefore.scale != skeleton[0].scale)
        throw new Exception("FBX root transform changed: " + path);
    description.skeleton = skeleton;
    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
    importer.sourceAvatar = null;
    importer.humanDescription = description;
    importer.SaveAndReimport();
    var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    if (avatar == null || !avatar.isValid || !avatar.isHuman || clip == null || !clip.humanMotion)
        throw new Exception("Reimported Avatar or animation is invalid: " + path);
    if (beforeGuid != AssetDatabase.AssetPathToGUID(path)) throw new Exception("Asset GUID changed: " + path);
    reports.Add(new { attack, path, matchedBones, guid = beforeGuid, avatarValid = avatar.isValid, avatarHuman = avatar.isHuman, clip = clip.name, clip.length, clip.frameRate });
}
System.IO.File.WriteAllText("Assets/Temp/AnimationDiagnosis~/Test_Applied.json", Newtonsoft.Json.JsonConvert.SerializeObject(reports, Newtonsoft.Json.Formatting.Indented));
return reports;
