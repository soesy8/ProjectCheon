var control=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>().AnimationControl;
var original=control.CharacterAnimator;
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var actor=UnityEngine.Object.Instantiate(original.gameObject);
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor,scene);
actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
actor.transform.localScale=original.transform.lossyScale;
var animator=actor.GetComponent<Animator>();
animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
animator.Rebind();animator.Update(0f);animator.SetFloat("RollSpeed",1f);
if(animator.layerCount>1)animator.SetLayerWeight(1,0f);
foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.updateWhenOffscreen=true;
var cameraObject=new GameObject("Test_RollPoseCamera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;
camera.backgroundColor=new Color(0.18f,0.20f,0.22f);camera.orthographic=true;camera.orthographicSize=1.1f;camera.nearClipPlane=0.01f;camera.farClipPlane=20f;
var lightObject=new GameObject("Test_RollPoseLight");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,scene);
var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40f,-35f,0f);
var frames=new [] {0,8,12,16,20,24,28,40};
var texture=new Texture2D(1024,512,TextureFormat.RGB24,false);
var target=RenderTexture.GetTemporary(256,256,24);
var previousActive=RenderTexture.active;
var data=new List<object>();
try
{
    for(int index=0;index<frames.Length;index++)
    {
        animator.Play("ROLL",0,frames[index]/40f);animator.Update(0f);
        var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
        var head=animator.GetBoneTransform(HumanBodyBones.Head);
        var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        data.Add(new {frame=frames[index],hips=hips.position.ToString("F3"),head=head.position.ToString("F3"),torsoTilt=Vector3.Angle(chest.position-hips.position,Vector3.up)});
        var focus=new Vector3(hips.position.x,0.9f,hips.position.z);
        camera.transform.SetPositionAndRotation(focus+new Vector3(3f,0f,0f),Quaternion.LookRotation(Vector3.left,Vector3.up));
        camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        texture.ReadPixels(new Rect(0,0,256,256),(index%4)*256,(1-index/4)*256,false);
    }
    texture.Apply();
    System.IO.File.WriteAllBytes("Assets/Temp/RollRecoverySteering~/Test_RollFrames_0_8_12_16_20_24_28_40.png",texture.EncodeToPNG());
}
finally
{
    camera.targetTexture=null;RenderTexture.active=previousActive;RenderTexture.ReleaseTemporary(target);
    UnityEngine.Object.DestroyImmediate(texture);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
System.IO.File.WriteAllText("Assets/Temp/RollRecoverySteering~/Test_RollPoseAnalysis.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));
return data;
