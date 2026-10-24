var mode = System.IO.File.Exists("Assets/Temp/AnimationDiagnosis~/Test_Applied.json") ? "After" : "Before";
var root = "Assets/Test/PlayerFanAnimation/";
var previews = new List<string>();
for (int attack = 1; attack <= 3; attack++)
{
    var preview = new PreviewRenderUtility();
    Texture2D texture = null;
    try
    {
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root + "Models/Test_CheonPlayer.fbx"));
        model.hideFlags = HideFlags.HideAndDontSave;
        preview.AddSingleGO(model);
        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(root + "Animations/Test_Player.controller");
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.SetFloat("Attack1Speed", 1f); animator.SetFloat("Attack2Speed", 1f); animator.SetFloat("Attack3Speed", 1f);
        var clip = AssetDatabase.LoadAllAssetsAtPath(root + "Animations/Test_Attack" + attack + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        int frame = attack == 1 ? 36 : attack == 2 ? 49 : 27;
        animator.Play("ATTACK" + attack, 0, frame / clip.frameRate / clip.length);
        animator.Update(0f);
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        // 전후 비교에 같은 상체 기준 카메라를 사용하여 목 자세 차이를 확인한다.
        var focus = chest.position + Vector3.up * 0.25f;
        preview.camera.orthographic = true;
        preview.camera.orthographicSize = 0.48f;
        preview.camera.nearClipPlane = 0.01f;
        preview.camera.farClipPlane = 10f;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.1f,0.12f,0.15f);
        preview.camera.transform.position = focus + new Vector3(0.55f,0.12f,1.8f);
        preview.camera.transform.LookAt(focus);
        preview.lights[0].intensity = 1.6f;
        preview.lights[0].transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        preview.lights[1].intensity = 0.9f;
        preview.ambientColor = new Color(0.5f,0.5f,0.5f);
        preview.BeginStaticPreview(new Rect(0,0,600,600));
        preview.Render(true);
        texture = preview.EndStaticPreview();
        var path = "Assets/Temp/AnimationDiagnosis~/Test_" + mode + "_Attack" + attack + "_F" + frame + ".png";
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        previews.Add(path);
    }
    finally
    {
        if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        preview.Cleanup();
    }
}
return previews;
