var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
var player = control.transform.parent.gameObject;
var sheet = new Texture2D(1024, 768, TextureFormat.RGB24, false);
var preview = new PreviewRenderUtility();
try
{
    var model = UnityEngine.Object.Instantiate(player);
    model.hideFlags = HideFlags.HideAndDontSave;
    foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
    model.GetComponent<CharacterController>().enabled = false;
    model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    preview.AddSingleGO(model);
    var copyControl = model.GetComponentInChildren<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
    var attachment = model.GetComponentInChildren<Cheon.Testing.PlayerFan.Test_PlayerFanAttachment>();
    attachment.SetSequencePose(true, 1f, true);
    var animator = copyControl.CharacterAnimator;
    animator.Rebind();
    animator.SetLayerWeight(1, 0f);
    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    preview.camera.orthographic = true;
    preview.camera.orthographicSize = 1.5f;
    preview.camera.nearClipPlane = 0.01f;
    preview.camera.farClipPlane = 30f;
    preview.camera.clearFlags = CameraClearFlags.SolidColor;
    preview.camera.backgroundColor = new Color(0.1f, 0.13f, 0.16f);
    preview.camera.transform.position = new Vector3(3.3f, 2.8f, 5f);
    preview.camera.transform.LookAt(new Vector3(0f, 1f, 0f));
    preview.lights[0].intensity = 1.5f;
    preview.lights[0].transform.rotation = Quaternion.Euler(35f, -30f, 0f);
    preview.lights[1].intensity = 0.8f;
    preview.ambientColor = Color.gray;
    for (int attack = 1; attack <= 3; attack++)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Test/PlayerFanAnimation/Animations/Test_Attack" + attack + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        for (int column = 0; column < 4; column++)
        {
            int frame = (attack == 2 ? 30 : 15) + 10 * column;
            animator.Play("ATTACK" + attack, 0, frame / clip.frameRate / clip.length);
            animator.Update(0f);
            preview.BeginStaticPreview(new Rect(0, 0, 256, 256));
            preview.Render(true);
            var texture = preview.EndStaticPreview();
            sheet.SetPixels(column * 256, (3 - attack) * 256, 256, 256, texture.GetPixels());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    sheet.Apply();
    System.IO.File.WriteAllBytes("Assets/Temp/AttackRangeEffects~/Test_AttackPoses.png", sheet.EncodeToPNG());
}
finally
{
    preview.Cleanup();
    UnityEngine.Object.DestroyImmediate(sheet);
}
return "Rows: attack1 F15/25/35/45, attack2 F30/40/50/60, attack3 F15/25/35/45";
