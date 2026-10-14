var camera = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.Test_CheonCamera>().GetComponent<UnityEngine.Camera>();
var originalPosition = camera.transform.position; var originalRotation = camera.transform.rotation;
float originalSize = camera.orthographicSize; bool originalFog = UnityEngine.RenderSettings.fog;
var originalTarget = camera.targetTexture;
camera.transform.position = new UnityEngine.Vector3(48,80,-67); camera.transform.LookAt(new UnityEngine.Vector3(0,0,2.5f)); camera.orthographicSize=51;
UnityEngine.RenderSettings.fog=false;
var texture = new UnityEngine.RenderTexture(1600,1600,24); var previousTarget = UnityEngine.RenderTexture.active;
try
{
    camera.targetTexture=texture; camera.Render(); UnityEngine.RenderTexture.active=texture;
    var pixels=new UnityEngine.Texture2D(1600,1600,UnityEngine.TextureFormat.RGB24,false);
    pixels.ReadPixels(new UnityEngine.Rect(0,0,1600,1600),0,0); pixels.Apply();
    System.IO.File.WriteAllBytes("Assets/Temp/Test_CheonExpanded_Overview.png",pixels.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(pixels);
}
finally { UnityEngine.RenderTexture.active=previousTarget; camera.targetTexture=originalTarget; camera.transform.position=originalPosition; camera.transform.rotation=originalRotation; camera.orthographicSize=originalSize; UnityEngine.RenderSettings.fog=originalFog; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
return true;
