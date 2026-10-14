using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cheon.Testing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class Test_CheonPlayVerification : MonoBehaviour
{
    private readonly List<string> results = new List<string>();
    private Test_CheonSession session;
    private Test_CheonPlayer player;
    private Test_CheonBoss boss;
    private Test_CheonWorld world;
    private InputSettings previousInputSettings, verificationInputSettings;
    private Keyboard simulatedKeyboard;
    private Mouse simulatedMouse;
    private readonly HashSet<Key> heldKeys = new HashSet<Key>();
    private void SetKey(Key key, bool down)
    {
        if (down) heldKeys.Add(key); else heldKeys.Remove(key);
        InputSystem.QueueStateEvent(simulatedKeyboard, new KeyboardState(heldKeys.ToArray()));
    }
    private void Check(bool condition, string name) { results.Add((condition ? "PASS " : "FAIL ") + name); Flush(); }
    private void Flush() { File.WriteAllLines("Assets/Temp/Test_CheonPlayResults.txt", results); }
    private void Find()
    {
        session = Object.FindFirstObjectByType<Test_CheonSession>(); player = Object.FindFirstObjectByType<Test_CheonPlayer>();
        boss = Object.FindFirstObjectByType<Test_CheonBoss>(FindObjectsInactive.Include); world = Object.FindFirstObjectByType<Test_CheonWorld>();
    }
    private void Move(Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>(); controller.enabled = false; player.transform.position = position; controller.enabled = true;
    }
    private IEnumerator Press(Key key)
    {
        SetKey(key, true);
        yield return null; yield return null;
        SetKey(key, false);
        yield return null; yield return null;
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("Assets/Temp/Test_Cheon_" + name + ".png", texture.EncodeToPNG()); Destroy(texture);
    }
    private IEnumerator ClickPrimaryButton()
    {
        var button = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b => b.name == "PrimaryButton");
        var rect = button.GetComponent<RectTransform>();
        Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(simulatedMouse, new MouseState { position = position }); yield return null; yield return null;
        InputSystem.QueueStateEvent(simulatedMouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null; yield return null;
        InputSystem.QueueStateEvent(simulatedMouse, new MouseState { position = position }); yield return null; yield return null;
    }
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        previousInputSettings = InputSystem.settings;
        verificationInputSettings = Instantiate(previousInputSettings);
        verificationInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        verificationInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = verificationInputSettings;
        simulatedKeyboard = InputSystem.AddDevice<Keyboard>(); simulatedKeyboard.MakeCurrent();
        simulatedMouse = InputSystem.AddDevice<Mouse>(); simulatedMouse.MakeCurrent();
        Find();
        yield return null;
        Check(session != null && player != null && boss != null, "Scene references");
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Introduction, "Introduction");
        yield return Capture("Introduction");
        yield return Press(Key.Enter);
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Training, "Enter starts training");
        Vector3 before = player.transform.position;
        SetKey(Key.D, true);
        yield return new WaitForSeconds(.25f);
        SetKey(Key.D, false); yield return null;
        Check(Vector3.Distance(before, player.transform.position) > .5f, "Real D input moves player");
        Move(new Vector3(0, 0, -1.6f));
        SetKey(Key.W, true); SetKey(Key.D, true);
        yield return new WaitForSeconds(.7f);
        SetKey(Key.W, false); SetKey(Key.D, false);
        yield return new WaitForSeconds(1.6f);
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Battle && world.SpiritAmount > .95f, "Walking through gate enters spirit battle");
        yield return Capture("Spirit");
        Move(boss.transform.position + Vector3.back * 2.7f);
        float health = boss.Health;
        yield return Press(Key.J);
        Check(boss.Health < health, "J fan attack deals damage");
        health = boss.Health;
        yield return Press(Key.Q);
        yield return new WaitForSeconds(.35f);
        Check(boss.Health < health && player.TalismanRemaining > 0, "Q projectile hits and starts cooldown");
        float energy = player.Energy;
        yield return Press(Key.Q);
        Check(player.Energy >= energy - 2, "Cooldown prevents repeated spell cost");
        player.Restore(); health = boss.Health;
        yield return Press(Key.E);
        Check(boss.Health < health && player.SealRemaining > 0 && boss.Intent.Contains("봉인"), "E interrupts boss and deals damage");
        player.Restore();
        SetKey(Key.Space, true); yield return null; yield return null;
        health = player.Health; bool dodging = player.IsDodging;
        player.TakeDamage(10);
        Check(dodging && player.Health == health, "Space dodge grants invulnerability");
        SetKey(Key.Space, false);
        yield return new WaitForSeconds(.8f);
        player.TakeDamage(10); Check(player.Health < health, "Player takes damage outside invulnerability");
        yield return Press(Key.Escape);
        Check(session.Paused && Time.timeScale == 0, "Escape pauses");
        yield return Press(Key.Escape);
        Check(!session.Paused && Time.timeScale == 1, "Escape resumes");
        var patterns = new HashSet<string>();
        float end = Time.time + 16;
        while (Time.time < end)
        {
            player.Restore(); patterns.Add(boss.Intent); yield return new WaitForSeconds(.08f);
        }
        Check(System.Linq.Enumerable.Any(patterns, s => s.Contains("내려찍기")), "Boss slam telegraph");
        Check(System.Linq.Enumerable.Any(patterns, s => s.Contains("휘두르기")), "Boss sweep telegraph");
        Check(System.Linq.Enumerable.Any(patterns, s => s.Contains("돌진")), "Boss charge telegraph");
        boss.TakeDamage(400); yield return null;
        Check(boss.Enraged, "Boss phase two");
        yield return Capture("Enraged");
        boss.TakeDamage(10000); yield return null;
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Victory, "Boss defeat completes purification");
        yield return Capture("Victory");
        Move(new Vector3(0, 0, -2)); yield return new WaitForSeconds(1.8f);
        Check(world.SpiritAmount < .05f, "Return through gate restores reality");
        yield return Capture("Reality");
        session.Restart(); yield return new WaitForSeconds(.7f); Find();
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Introduction, "Victory restart reloads standalone scene");
        session.BeginTraining(); Move(new Vector3(0, 0, 3)); yield return new WaitForSeconds(.2f);
        player.TakeDamage(10000); yield return null;
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Defeat, "Player defeat");
        yield return Capture("Defeat");
        yield return Press(Key.R); yield return new WaitForSeconds(.7f); Find();
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Introduction, "R restarts after defeat");
        yield return ClickPrimaryButton();
        Check(session.CurrentChapter == Test_CheonSession.Chapter.Training, "Mouse clicks start button");
        session.TogglePause(); yield return null;
        yield return ClickPrimaryButton();
        Check(!session.Paused, "Mouse clicks resume button");
        player.TrySeal(); yield return Press(Key.F);
        Check(player.Energy > 99, "F restores energy near shrine");
        var sound = Object.FindFirstObjectByType<Test_CheonAudio>(); bool muted = sound.Muted;
        yield return Press(Key.M); Check(sound.Muted != muted, "M toggles sound");
        session.Restart(); yield return new WaitForSeconds(.7f);
        results.Add("COMPLETE"); Flush(); Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (simulatedKeyboard != null && simulatedKeyboard.added) InputSystem.RemoveDevice(simulatedKeyboard);
        if (simulatedMouse != null && simulatedMouse.added) InputSystem.RemoveDevice(simulatedMouse);
        if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
        if (verificationInputSettings != null) Destroy(verificationInputSettings);
    }
}
