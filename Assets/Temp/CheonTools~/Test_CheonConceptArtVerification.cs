using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cheon.Testing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class Test_CheonConceptArtVerification : MonoBehaviour
{
    private readonly List<string> results = new List<string>();
    private InputSettings originalSettings, testSettings;
    private Keyboard keyboard;
    private Test_CheonSession session;
    private Test_CheonPlayer player;
    private Test_CheonBoss boss;
    private Test_CheonWorld world;
    private Camera view;
    private void Check(bool value, string label)
    {
        results.Add((value ? "PASS " : "FAIL ") + label);
        File.WriteAllLines("Assets/Temp/Test_CheonConceptPlayResults.txt",results);
    }
    private void Place(Vector3 point)
    {
        var controller=player.GetComponent<CharacterController>(); controller.enabled=false;
        player.transform.position=point; controller.enabled=true;
    }
    private IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(key)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null; yield return null;
    }
    private IEnumerator WalkForward()
    {
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.D));
        yield return new WaitForSeconds(.7f);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame(); var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("Assets/Temp/Test_CheonConcept_"+name+".png",image.EncodeToPNG()); Destroy(image);
    }
    private IEnumerator Start()
    {
        originalSettings=InputSystem.settings; testSettings=Instantiate(originalSettings);
        testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=testSettings; keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        session=Object.FindFirstObjectByType<Test_CheonSession>(); player=Object.FindFirstObjectByType<Test_CheonPlayer>();
        boss=Object.FindFirstObjectByType<Test_CheonBoss>(FindObjectsInactive.Include); world=Object.FindFirstObjectByType<Test_CheonWorld>();
        view=Object.FindFirstObjectByType<Test_CheonCamera>().GetComponent<Camera>();
        yield return Press(Key.Enter);
        float boundary=world.BoundaryZ;
        Check(Mathf.Abs(boundary+13.1f)<.001f,"Portal moved to 35 percent boundary");
        Check(session.CurrentChapter==Test_CheonSession.Chapter.Training && player.transform.position.z<boundary,"Player starts in reality");
        Check(Mathf.Abs(view.orthographicSize-8.4f)<.001f,"Existing exploration zoom preserved");
        player.TrySeal(); yield return Press(Key.F);
        Check(player.Energy>99,"Recovery works at relocated starting point");
        yield return Capture("Reality");
        Place(new Vector3(28,0,-30)); yield return new WaitForSeconds(.15f);
        Check(player.transform.position.x<=23.041f,"Reality width stays at 48");
        Place(new Vector3(10,0,boundary-2)); yield return WalkForward();
        Check(session.CurrentChapter==Test_CheonSession.Chapter.Training && player.transform.position.z<boundary,"Wall prevents crossing beside relocated portal");
        Place(new Vector3(0,0,boundary-1.8f)); yield return WalkForward();
        yield return new WaitForSeconds(3.1f);
        Check(session.CurrentChapter==Test_CheonSession.Chapter.Battle && world.SpiritAmount>.99f,"Walking through negative Z portal starts battle");
        Check(Mathf.Abs(view.orthographicSize-10.08f)<.025f,"Existing battle zoom preserved");
        Place(new Vector3(28,0,25)); yield return new WaitForSeconds(.15f);
        Check(player.transform.position.x>27.9f,"Spirit side expansion is accessible");
        Place(new Vector3(0,0,boundary-5)); yield return new WaitForSeconds(.2f);
        Check(player.transform.position.z>=boundary+1.29f && player.transform.position.z<0,"Battle retreat limit follows new boundary");
        Place(new Vector3(0,0,25)); player.Restore(); yield return new WaitForSeconds(1.2f);
        yield return Capture("Spirit");
        Place(new Vector3(0,0,52)); yield return new WaitForSeconds(.2f);
        Check(player.transform.position.z>51.5f,"New northern length is accessible");
        boss.transform.position=new Vector3(0,0,44); yield return new WaitForSeconds(.2f);
        Check(boss.transform.position.z>42,"Boss movement bounds follow enlarged spirit zone");
        boss.transform.position=new Vector3(26.5f,0,30); yield return null; yield return null;
        Check(boss.transform.position.x>23.75f && boss.transform.position.x<=24.701f,"Boss can use the wider spirit realm");
        var floor=world.transform.Find("Test_MossTerrace").gameObject.AddComponent<MeshCollider>();
        floor.sharedMesh=floor.GetComponent<MeshFilter>().sharedMesh;
        yield return new WaitForSeconds(.1f);
        bool supported=true;
        foreach(var point in new[]{new Vector3(0,0,-48),new Vector3(-22,0,-35),new Vector3(30,0,35),new Vector3(-30,0,35),new Vector3(0,0,53),new Vector3(30,0,boundary+2)})
            supported &= floor.Raycast(new Ray(point+Vector3.up*5,Vector3.down),out _,10);
        Check(supported,"Resized visible terrain supports outer areas");
        Check(!floor.Raycast(new Ray(new Vector3(28,5,-30),Vector3.down),out _,10),"Reality terrain has not widened");
        boss.TakeDamage(10000); yield return new WaitForSeconds(.2f);
        Place(new Vector3(0,0,boundary+2)); yield return new WaitForSeconds(1.8f);
        Check(world.SpiritAmount>.99f,"Victory stays in spirit until actual gate crossing");
        Place(new Vector3(0,0,boundary-2)); yield return new WaitForSeconds(1.8f);
        Check(world.SpiritAmount<.01f,"Crossing relocated gate returns to reality");
        Place(new Vector3(100,0,100)); yield return new WaitForSeconds(.2f);
        Check(player.transform.position.x<29.953f && player.transform.position.z<53.91f,"Wider spirit and longer movement bounds applied");
        Check(floor.Raycast(new Ray(player.transform.position+Vector3.up*5,Vector3.down),out _,10),"New asymmetric corner remains on visible ground");
        results.Add("COMPLETE"); File.WriteAllLines("Assets/Temp/Test_CheonConceptPlayResults.txt",results); Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if(originalSettings!=null) InputSystem.settings=originalSettings;
        if(testSettings!=null) Destroy(testSettings);
    }
}
