var root = "Assets/Test/PlayerFanAnimation/Animations/";
var sources = new[] { (source: @"E:\C.J.I\Animation\구르기.fbx", name: "Roll"), (source: @"E:\C.J.I\Animation\달리기2.fbx", name: "Run2") };
var reports = new List<object>();
foreach (var source in sources)
{
    var path = root + "Test_" + source.name + ".fbx";
    if (System.IO.File.Exists(path)) throw new Exception("이미 존재하는 애니메이션입니다: " + path);
    System.IO.File.Copy(source.source, path);
    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
    importer.animationType = ModelImporterAnimationType.Human;
    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
    importer.importCameras = false;
    importer.importLights = false;
    importer.materialImportMode = ModelImporterMaterialImportMode.None;
    importer.animationCompression = ModelImporterAnimationCompression.Off;
    importer.SaveAndReimport();
    var clips = importer.defaultClipAnimations;
    if (clips.Length != 1) throw new Exception("원본 클립 수를 확인하세요: " + path);
    clips[0].name = source.name;
    clips[0].loopTime = source.name == "Run2";
    // 미리보기 무대에서 제자리 재생하고 구르기의 높이 변화는 유지한다.
    clips[0].loopPose = source.name == "Run2";
    clips[0].lockRootRotation = true;
    clips[0].lockRootHeightY = true;
    clips[0].lockRootPositionXZ = source.name == "Run2";
    clips[0].keepOriginalOrientation = true;
    clips[0].keepOriginalPositionY = false;
    clips[0].keepOriginalPositionXZ = false;
    clips[0].heightFromFeet = true;
    importer.clipAnimations = clips;
    importer.SaveAndReimport();
    var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    if (avatar == null || !avatar.isValid || !avatar.isHuman || !clip.humanMotion) throw new Exception("Humanoid 임포트 실패: " + path);
    reports.Add(new { path, guid = AssetDatabase.AssetPathToGUID(path), avatarValid = avatar.isValid, clip.name, clip.length, clip.frameRate, clip.isLooping,
        bones = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray() });
}
return reports;
