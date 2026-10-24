var root = "Assets/Test/PlayerFanAnimation/";
var path = root + "Animations/Test_Roll.fbx";
var importer = (ModelImporter)AssetImporter.GetAtPath(path);
var rows = new List<object>();
foreach (var option in new[] { (bakeY: true, feet: false, bakeXZ: true), (bakeY: false, feet: false, bakeXZ: false), (bakeY: true, feet: true, bakeXZ: false), (bakeY: false, feet: true, bakeXZ: false) })
{
    var clips = importer.clipAnimations;
    clips[0].lockRootHeightY = option.bakeY;
    clips[0].heightFromFeet = option.feet;
    clips[0].lockRootPositionXZ = option.bakeXZ;
    importer.clipAnimations = clips;
    importer.SaveAndReimport();
    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root + "Models/Test_CheonPlayer.fbx"));
    model.hideFlags = HideFlags.HideAndDontSave;
    try
    {
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(root + "Animations/Test_Player.controller");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        var samples = new List<object>();
        foreach (float time in new[] { 0f, 0.25f, 0.5f, 0.75f, 0.95f })
        {
            animator.Play("ROLL", 0, time);
            animator.Update(0f);
            samples.Add(new { time, hips = animator.GetBoneTransform(HumanBodyBones.Hips).position.ToString(), head = animator.GetBoneTransform(HumanBodyBones.Head).position.ToString(), foot = animator.GetBoneTransform(HumanBodyBones.RightFoot).position.ToString() });
        }
        rows.Add(new { option, samples });
    }
    finally { UnityEngine.Object.DestroyImmediate(model); }
}
return rows;
