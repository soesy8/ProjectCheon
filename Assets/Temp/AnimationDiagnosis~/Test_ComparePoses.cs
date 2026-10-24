var root = "Assets/Test/PlayerFanAnimation/";
var tempRoot = "Assets/Temp/AnimationDiagnosis";
if (!AssetDatabase.IsValidFolder(tempRoot)) AssetDatabase.CreateFolder("Assets/Temp", "AnimationDiagnosis");
var targetPath = root + "Models/Test_CheonPlayer.fbx";
var targetAvatar = AssetDatabase.LoadAllAssetsAtPath(targetPath).OfType<Avatar>().First();
var results = new List<object>();
object Sample(AnimationClip clip, string rigPath, bool humanoid)
{
    // 본 계층은 별도 Preview Scene에 복제하여 사용자의 씬을 건드리지 않는다.
    var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(rigPath));
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, scene);
    model.hideFlags = HideFlags.HideAndDontSave;
    var animator = model.GetComponent<Animator>();
    var neck = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:Neck");
    var head = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:Head");
    var chest = model.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:Spine2");
    var graph = default(UnityEngine.Playables.PlayableGraph);
    var samples = new List<object>();
    try
    {
        UnityEngine.Animations.AnimationClipPlayable playable = default;
        if (humanoid)
        {
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = UnityEngine.Playables.PlayableGraph.Create("Test_NeckDiagnosis");
            graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
            playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Pose", animator);
            UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
            graph.Play();
        }
        Quaternion previousNeck = Quaternion.identity, previousHead = Quaternion.identity;
        int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
        var muscleBindings = AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("Neck ") || b.propertyName.StartsWith("Head ")).ToArray();
        for (int f = 0; f <= frames; f++)
        {
            float time = Mathf.Min(clip.length, f / clip.frameRate);
            if (humanoid) { UnityEngine.Playables.PlayableExtensions.SetTime(playable, time); graph.Evaluate(0f); }
            else clip.SampleAnimation(model, time);
            var combined = Quaternion.Inverse(chest.rotation) * head.rotation;
            samples.Add(new {
                frame = f,
                neck = new[] {neck.localRotation.x, neck.localRotation.y, neck.localRotation.z, neck.localRotation.w},
                head = new[] {head.localRotation.x, head.localRotation.y, head.localRotation.z, head.localRotation.w},
                neckAngle = Quaternion.Angle(Quaternion.identity, neck.localRotation),
                headAngle = Quaternion.Angle(Quaternion.identity, head.localRotation),
                headRelativeChestAngle = Quaternion.Angle(Quaternion.identity, combined),
                neckStep = f == 0 ? 0f : Quaternion.Angle(previousNeck, neck.localRotation),
                headStep = f == 0 ? 0f : Quaternion.Angle(previousHead, head.localRotation),
                muscles = muscleBindings.ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(clip, b).Evaluate(time))
            });
            previousNeck = neck.localRotation; previousHead = head.localRotation;
        }
        return new { clip = clip.name, clipPath = AssetDatabase.GetAssetPath(clip), humanoid, rigPath, samples };
    }
    finally
    {
        if (graph.IsValid()) graph.Destroy();
        UnityEngine.Object.DestroyImmediate(model);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
    }
}
for (int i = 1; i <= 3; i++)
{
    var originalPath = root + "Animations/Test_Attack" + i + ".fbx";
    var originalClip = AssetDatabase.LoadAllAssetsAtPath(originalPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    results.Add(new { attack = i, mode = "original", data = Sample(originalClip, targetPath, true) });
    var tempPath = tempRoot + "/Test_Attack" + i + ".fbx";
    if (!AssetDatabase.CopyAsset(originalPath, tempPath)) throw new Exception("Temporary copy failed: " + tempPath);
    try
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(tempPath);
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        // FBX 루트 축은 유지하고, 이름이 같은 본의 기준 자세만 캐릭터의 T 자세로 맞춘다.
        var targetBones = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath).GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
        var description = importer.humanDescription;
        var skeleton = description.skeleton;
        for (int b = 0; b < skeleton.Length; b++)
        {
            if (!targetBones.TryGetValue(skeleton[b].name, out var targetBone)) continue;
            var bone = skeleton[b];
            bone.position = targetBone.localPosition;
            bone.rotation = targetBone.localRotation;
            bone.scale = targetBone.localScale;
            skeleton[b] = bone;
        }
        description.skeleton = skeleton;
        importer.humanDescription = description;
        importer.SaveAndReimport();
        var copiedClip = AssetDatabase.LoadAllAssetsAtPath(tempPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (copiedClip != null) results.Add(new { attack = i, mode = "neutralAvatar", data = Sample(copiedClip, targetPath, true) });
        importer = (ModelImporter)AssetImporter.GetAtPath(tempPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.sourceAvatar = null;
        importer.SaveAndReimport();
        var rawClip = AssetDatabase.LoadAllAssetsAtPath(tempPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        results.Add(new { attack = i, mode = "rawGeneric", data = Sample(rawClip, tempPath, false) });
    }
    finally { AssetDatabase.DeleteAsset(tempPath); }
}
var json = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("Assets/Temp/AnimationDiagnosis~/Test_PoseComparison.json", json);
AssetDatabase.DeleteAsset(tempRoot);
return new { comparisons = results.Count, report = "Assets/Temp/AnimationDiagnosis~/Test_PoseComparison.json" };
