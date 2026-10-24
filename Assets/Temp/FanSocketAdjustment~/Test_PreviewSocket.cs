var scenePath = "Assets/Temp/FanSocketAdjustment/Test_Snapshot.unity";
var results = new List<object>();
foreach (int variant in new[] {0,-1,1})
{
    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(scenePath);
    var preview = new PreviewRenderUtility();
    Texture2D texture = null;
    try
    {
        var attachment = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Cheon.Testing.PlayerFan.Test_PlayerFanAttachment>(true)).Single();
        var control = attachment.GetComponent<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
        var animator = control.CharacterAnimator;
        preview.AddSingleGO(animator.transform.root.gameObject);
        var settings = new SerializedObject(attachment);
        Vector3 basePosition = settings.FindProperty("socketLocalPosition").vector3Value;
        Vector3 baseEuler = settings.FindProperty("socketLocalEulerAngles").vector3Value;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); control.ApplyAttackSpeeds();
        animator.Play("DRAWN_IDLE",0,0f); animator.Update(0f);
        var socket = attachment.HandSocket;
        socket.localPosition = basePosition + (variant == 0 ? Vector3.zero : new Vector3(-0.015f,0f,0f));
        socket.localRotation = Quaternion.Euler(baseEuler + new Vector3(0f,0f,variant*12f));
        attachment.SetSequencePose(true,1f,true);
        foreach(var renderer in animator.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
        var focus = animator.transform.position + new Vector3(0f,1.2f,0f);
        preview.camera.orthographic = true;
        preview.camera.orthographicSize = 0.95f;
        preview.camera.nearClipPlane = 0.01f; preview.camera.farClipPlane=15f;
        preview.camera.clearFlags=CameraClearFlags.SolidColor;
        preview.camera.backgroundColor=new Color(0.12f,0.14f,0.17f);
        preview.camera.transform.position=focus+new Vector3(0.2f,0.2f,3f);
        preview.camera.transform.LookAt(focus);
        preview.lights[0].intensity=1.6f; preview.lights[0].transform.rotation=Quaternion.Euler(35f,-30f,0f);
        preview.lights[1].intensity=0.9f; preview.ambientColor=new Color(0.5f,0.5f,0.5f);
        preview.BeginStaticPreview(new Rect(0f,0f,800f,800f)); preview.Render(true); texture=preview.EndStaticPreview();
        var path="Assets/Temp/FanSocketAdjustment~/Test_Variant"+variant+".png";
        System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
        var fanBounds = attachment.FanRoot.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).ToArray();
        var bounds = fanBounds.First(); foreach(var part in fanBounds.Skip(1)) bounds.Encapsulate(part);
        results.Add(new {variant,path,position=socket.localPosition.ToString("F6"),euler=socket.localEulerAngles.ToString("F3"),fanCenter=animator.transform.InverseTransformPoint(bounds.center).ToString("F4"),hand=animator.GetBoneTransform(HumanBodyBones.RightHand).position.ToString("F4")});
    }
    finally { if(texture!=null) UnityEngine.Object.DestroyImmediate(texture); preview.Cleanup(); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
}
System.IO.File.WriteAllText("Assets/Temp/FanSocketAdjustment~/Test_Variants.json",Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
return results;
