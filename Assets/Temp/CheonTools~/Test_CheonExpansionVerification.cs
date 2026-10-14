using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cheon.Testing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class Test_CheonExpansionVerification : MonoBehaviour
{
    private readonly List<string> results = new List<string>();
    private Test_CheonSession session;
    private Test_CheonPlayer player;
    private Test_CheonBoss boss;
    private Test_CheonWorld world;
    private Test_CheonCamera rig;
    private Camera view;
    private InputSettings originalSettings, verificationSettings;
    private Keyboard keyboard;
    private void Check(bool condition, string label)
    {
        results.Add((condition ? "PASS " : "FAIL ") + label);
        File.WriteAllLines("Assets/Temp/Test_CheonExpansionResults.txt", results);
    }
    private void PlacePlayer(Vector3 point)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false; player.transform.position = point; controller.enabled = true;
    }
    private IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("Assets/Temp/Test_CheonExpanded_" + name + ".png", image.EncodeToPNG()); Destroy(image);
    }
    private float CameraDistance()
    {
        Vector3 focus = (Vector3)typeof(Test_CheonCamera).GetField("focus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig);
        return Vector3.Distance(view.transform.position, focus);
    }
    private IEnumerator Start()
    {
        originalSettings = InputSystem.settings; verificationSettings = Instantiate(originalSettings);
        verificationSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        verificationSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = verificationSettings; keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        session = Object.FindFirstObjectByType<Test_CheonSession>(); player = Object.FindFirstObjectByType<Test_CheonPlayer>();
        boss = Object.FindFirstObjectByType<Test_CheonBoss>(FindObjectsInactive.Include); world = Object.FindFirstObjectByType<Test_CheonWorld>();
        rig = Object.FindFirstObjectByType<Test_CheonCamera>(); view = rig.GetComponent<Camera>();
        yield return Press(Key.Enter); yield return new WaitForSeconds(.2f);
        float baseDistance = new Vector3(15, 23, -23).magnitude * .7f;
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Training, "Enter starts expanded training map");
        Check(Mathf.Abs(view.orthographicSize - 8.4f) < .001f, "Exploration view is 30 percent closer");
        Check(Mathf.Abs(CameraDistance() - baseDistance) < .01f, "Exploration physical camera distance is 70 percent");
        Check(Vector3.Distance(player.transform.position, new Vector3(0,0,-25)) < .2f, "Expanded starting position");
        yield return Capture("Exploration");
        player.TrySeal(); yield return Press(Key.F);
        Check(player.Energy > 99, "Recovery shrine moved with map");
        PlacePlayer(new Vector3(-24,0,-20)); yield return new WaitForSeconds(.2f);
        Check(player.transform.position.x < -20, "Expanded west side is accessible beyond old bounds");
        PlacePlayer(new Vector3(-18,0,-13.5f)); yield return new WaitForSeconds(1.2f);
        yield return Capture("BellPavilion");
        var ground = world.transform.Find("Test_WalkableGround").GetComponent<BoxCollider>();
        Check(Mathf.Abs(ground.size.x-60)<.001f && Mathf.Abs(ground.size.z-80)<.001f, "Ground collider is 60 by 80");
        var floor = world.transform.Find("Test_MossTerrace").gameObject.AddComponent<MeshCollider>();
        floor.sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
        var points = new[] {new Vector3(-24,0,-20),new Vector3(24,0,-30),new Vector3(0,0,40),new Vector3(-25,0,28),new Vector3(26,0,10)};
        bool supported = true;
        foreach (var point in points) supported &= floor.Raycast(new Ray(point+Vector3.up*5, Vector3.down),out _,10);
        Check(supported, "Expanded terrain mesh supports outer areas");
        PlacePlayer(new Vector3(0,0,1.5f)); yield return new WaitForSeconds(3.2f);
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Battle && world.SpiritAmount > .99f, "Gate still enters spirit battle");
        Check(Mathf.Abs(view.orthographicSize-10.08f)<.025f, "Battle view is 120 percent of exploration");
        Check(Mathf.Abs(CameraDistance()/baseDistance-1.2f)<.01f, "Battle physical distance is 120 percent");
        PlacePlayer(new Vector3(0,0,23)); player.Restore(); yield return new WaitForSeconds(1.3f);
        yield return Capture("Battle");
        PlacePlayer(new Vector3(0,0,35)); yield return new WaitForSeconds(.2f);
        Check(player.transform.position.z > 34, "Expanded north side is accessible beyond old bounds");
        boss.transform.position = new Vector3(0,0,28); yield return new WaitForSeconds(.1f);
        Check(boss.transform.position.z > 20, "Boss movement bounds fit enlarged arena");
        boss.TakeDamage(10000); yield return new WaitForSeconds(3.2f);
        Check(Mathf.Abs(view.orthographicSize-8.4f)<.025f, "Camera returns to new baseline after battle");
        Check(Mathf.Abs(CameraDistance()/baseDistance-1)<.01f, "Physical camera returns to baseline");
        PlacePlayer(new Vector3(45,0,60)); yield return new WaitForSeconds(.2f);
        Vector3 clamped = player.transform.position;
        Check(clamped.x <= 28.8f && clamped.z <= 41.91f && clamped.x + clamped.z < 67.5f, "Expanded bevel corner keeps player on island");
        Check(floor.Raycast(new Ray(clamped+Vector3.up*5,Vector3.down),out _,10), "Clamped corner has visible ground");
        PlacePlayer(new Vector3(0,0,-2)); yield return new WaitForSeconds(1.8f);
        Check(world.SpiritAmount < .01f, "Returning through gate restores reality");
        results.Add("COMPLETE"); File.WriteAllLines("Assets/Temp/Test_CheonExpansionResults.txt",results);
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (originalSettings != null) InputSystem.settings = originalSettings;
        if (verificationSettings != null) Destroy(verificationSettings);
    }
}
