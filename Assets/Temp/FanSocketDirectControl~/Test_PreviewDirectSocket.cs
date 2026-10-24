var results = new List<object>();
foreach(bool adjusted in new[]{false,true})
{
    var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Test/PlayerFanAnimation/Scenes/Test_PlayerFanAnimation.unity");
    var preview=new PreviewRenderUtility();
    Texture2D texture=null;
    try
    {
        var attachment=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Cheon.Testing.PlayerFan.Test_PlayerFanAttachment>(true)).Single();
        var control=attachment.GetComponent<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
        var animator=control.CharacterAnimator;
        preview.AddSingleGO(animator.transform.root.gameObject);
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();control.ApplyAttackSpeeds();
        animator.Play("DRAWN_IDLE",0,0f);animator.Update(0f);
        var socket=attachment.HandSocket;
        if(adjusted)
        {
            socket.localPosition+=new Vector3(-0.015f,0f,0f);
            socket.localRotation*=Quaternion.AngleAxis(12f,Vector3.forward);
        }
        attachment.SetSequencePose(true,1f,true);
        foreach(var renderer in animator.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.updateWhenOffscreen=true;
        var focus=animator.GetBoneTransform(HumanBodyBones.RightHand).position+new Vector3(-0.07f,0.12f,0f);
        preview.camera.orthographic=true;preview.camera.orthographicSize=0.43f;
        preview.camera.nearClipPlane=0.01f;preview.camera.farClipPlane=10f;
        preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(0.12f,0.14f,0.17f);
        preview.camera.transform.position=focus+new Vector3(0.25f,0.15f,2f);preview.camera.transform.LookAt(focus);
        preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35f,-30f,0f);
        preview.lights[1].intensity=0.9f;preview.ambientColor=new Color(0.5f,0.5f,0.5f);
        preview.BeginStaticPreview(new Rect(0,0,800,800));preview.Render(true);texture=preview.EndStaticPreview();
        string path="Assets/Temp/FanSocketDirectControl~/Test_"+(adjusted?"Adjusted":"Current")+".png";
        System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
        results.Add(new{adjusted,path,position=socket.localPosition.ToString("F6"),euler=socket.localEulerAngles.ToString("F3")});
    }
    finally{if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);preview.Cleanup();UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
}
return results;
