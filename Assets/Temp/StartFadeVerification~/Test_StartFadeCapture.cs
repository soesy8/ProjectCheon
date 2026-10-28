using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cheon.Prototype.MountainPass;
using Newtonsoft.Json;
using UnityEngine;

public static class Test_StartFadeCapture
{
    private const string Folder = "Assets/Temp/StartFadeVerification~/";

    public static object Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("플레이 모드에서 실행하세요.");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var fade = UnityEngine.Object.FindFirstObjectByType<PassScreenFade>();
        var hud = UnityEngine.Object.FindFirstObjectByType<PassHud>();
        Directory.CreateDirectory(Folder);
        fade.StartCoroutine(CaptureStart(game, fade, hud));
        return new { started = true, report = Folder + "start_fade_report.json" };
    }

    private static IEnumerator CaptureStart(PassGameFlow game, PassScreenFade fade, PassHud hud)
    {
        while (fade.IsTransitioning) yield return null;
        var veil = hud.transform.Find("MountainPassHUD/ScreenFade").GetComponent<UnityEngine.UI.Image>();
        var button = hud.transform.Find("MountainPassHUD/Introduction/TitleColumn/BeginJourney").GetComponent<UnityEngine.UI.Button>();
        yield return new WaitForEndOfFrame();
        Capture("T_Title.png");
        int firstFrame = Time.frameCount;
        float started = Time.realtimeSinceStartup;
        button.onClick.Invoke();
        var samples = new List<object>();
        bool capturedOut = false, capturedBlack = false, capturedIn = false, duplicateClicked = false;
        int playingTransitions = 0;
        var previousState = PassGameFlow.RunState.Intro;
        while (fade.IsTransitioning && Time.realtimeSinceStartup - started < 5f)
        {
            float elapsed = Time.realtimeSinceStartup - started;
            float alpha = veil.color.a;
            samples.Add(new { elapsed, frame = Time.frameCount, alpha, state = game.State.ToString(), timeScale = Time.timeScale, running = game.IsRunning, blocksClicks = veil.raycastTarget });
            if (game.State == PassGameFlow.RunState.Playing && previousState != game.State) playingTransitions++;
            previousState = game.State;
            if (!duplicateClicked && elapsed > .1f) { duplicateClicked = true; button.onClick.Invoke(); }
            string screenshot = null;
            if (!capturedOut && game.State == PassGameFlow.RunState.Intro && alpha >= .45f && alpha <= .8f)
            { capturedOut = true; screenshot = "T_FadeOut.png"; }
            else if (!capturedBlack && game.State == PassGameFlow.RunState.Playing && alpha >= .999f)
            { capturedBlack = true; screenshot = "T_Black.png"; }
            else if (!capturedIn && capturedBlack && alpha <= .6f && alpha >= .3f)
            { capturedIn = true; screenshot = "T_FadeIn.png"; }
            if (screenshot != null)
            {
                yield return new WaitForEndOfFrame();
                Capture(screenshot);
            }
            yield return null;
        }
        yield return new WaitForEndOfFrame();
        Capture("T_Gameplay.png");
        var report = new
        {
            passed = capturedOut && capturedBlack && capturedIn && playingTransitions == 1 &&
                !fade.IsTransitioning && game.IsRunning && !veil.raycastTarget &&
                veil.color.a <= .001f && Mathf.Approximately(Time.timeScale, 1f) && Time.frameCount > firstFrame,
            seconds = Time.realtimeSinceStartup - started,
            firstFrame, finalFrame = Time.frameCount,
            capturedOut, capturedBlack, capturedIn, playingTransitions,
            finalAlpha = veil.color.a, finalTimeScale = Time.timeScale,
            finalState = game.State.ToString(), samples
        };
        File.WriteAllText(Folder + "start_fade_report.json", JsonConvert.SerializeObject(report, Formatting.Indented));
    }

    private static void Capture(string name)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands"))
            .First(value => value != null);
        var method = type.GetMethod("EncodeScreenToPng", BindingFlags.Static | BindingFlags.NonPublic);
        var png = (byte[])method.Invoke(null, new object[] { 1024, 576 });
        File.WriteAllBytes(Folder + name, png);
    }
}
