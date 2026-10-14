var camera=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonCamera>().GetComponent<UnityEngine.Camera>();
var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;bool fog=UnityEngine.RenderSettings.fog;var clear=camera.clearFlags;
var originalTarget=camera.targetTexture;var previousTarget=UnityEngine.RenderTexture.active;var texture=new UnityEngine.RenderTexture(1600,1600,24);
try
{
    UnityEngine.RenderSettings.fog=false;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
    camera.transform.position=new UnityEngine.Vector3(46,90,-85);camera.transform.LookAt(new UnityEngine.Vector3(0,0,2.5f));camera.orthographicSize=56;
    camera.targetTexture=texture;camera.Render();UnityEngine.RenderTexture.active=texture;
    var image=new UnityEngine.Texture2D(1600,1600,UnityEngine.TextureFormat.RGB24,false);
    image.ReadPixels(new UnityEngine.Rect(0,0,1600,1600),0,0);image.Apply();
    System.IO.File.WriteAllBytes("Assets/Temp/Test_CheonConcept_SurfaceDebug.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
}
finally
{
    camera.targetTexture=originalTarget;UnityEngine.RenderTexture.active=previousTarget;
    camera.transform.position=position;camera.transform.rotation=rotation;camera.orthographicSize=size;UnityEngine.RenderSettings.fog=fog;camera.clearFlags=clear;
    texture.Release();UnityEngine.Object.DestroyImmediate(texture);
}
return true;
