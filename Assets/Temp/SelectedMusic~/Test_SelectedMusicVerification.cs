using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_SelectedMusicVerification
{
    static PassGameFlow game;
    static Test_TraditionalSoundscape mix;
    static readonly List<object> steps = new List<object>();
    static int maximumConcurrent;
    static int sampledFrames;

    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        mix = UnityEngine.Object.FindFirstObjectByType<Test_TraditionalSoundscape>();
        UnityEngine.Object.FindFirstObjectByType<Test_AudioCreditsPanel>().CloseCredits();
        Sample("title", "Test_Bgm_OurSounds");
        game.BeginJourney();
        await Wait(PassGameFlow.RunState.Playing);
        Sample("field", "Test_Bgm_SeventhNight");
        game.BeginBossChallenge();
        await Wait(PassGameFlow.RunState.Playing);
        game.PlayerHealth.Invulnerable = true;
        Sample("boss", "Test_Bgm_Samulnori");
        await Escape();
        await Wait(PassGameFlow.RunState.Paused);
        Sample("pause", "Test_Bgm_Samulnori");
        await Escape();
        await Wait(PassGameFlow.RunState.Playing);
        game.PlayerHealth.Invulnerable = true;
        Sample("resume", "Test_Bgm_Samulnori");
        game.Boss.Health.Invulnerable = false;
        if (!game.Boss.Health.TakeDamage(99999, false, true)) throw new Exception("Boss damage rejected");
        await Wait(PassGameFlow.RunState.Victory);
        Sample("victory", "Test_Bgm_OurSounds");
        game.BeginBossChallenge();
        await Wait(PassGameFlow.RunState.Playing);
        Sample("retry", "Test_Bgm_Samulnori");
        game.PlayerHealth.Invulnerable = false;
        if (!game.PlayerHealth.TakeDamage(99999, false, true)) throw new Exception("Player damage rejected");
        await Wait(PassGameFlow.RunState.Defeat);
        Sample("defeat", null);
        var report = new { passed = true, maximumConcurrent, sampledFrames, steps };
        System.IO.File.WriteAllText("Assets/Temp/SelectedMusic~/Test_VerificationReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    static async Task Escape()
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));
        await Task.Delay(120);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        await Task.Delay(120);
    }

    static AudioSource[] Sources() => mix.GetComponentsInChildren<AudioSource>()
        .Where(s => s.clip != null && s.clip.name.StartsWith("Test_Bgm_", StringComparison.Ordinal)).ToArray();

    static void Observe()
    {
        var active = Sources().Where(s => s.isPlaying).ToArray();
        maximumConcurrent = Math.Max(maximumConcurrent, active.Length);
        sampledFrames++;
        if (active.Length > 1) throw new Exception("Overlapping BGM: " + string.Join(", ", active.Select(s => s.clip.name)));
        if (active.Any(s => s.clip.name.Contains("Dokkaebi"))) throw new Exception("Archived track played");
    }

    static async Task Wait(PassGameFlow.RunState state)
    {
        float limit = Time.realtimeSinceStartup + 9;
        while (game.State != state || game.IsTransitioning)
        {
            Observe();
            if (Time.realtimeSinceStartup > limit) throw new Exception("State timeout: " + state + " / " + game.State);
            await Task.Delay(20);
        }
        for (int i = 0; i < 35; i++) { Observe(); await Task.Delay(20); }
    }

    static void Sample(string step, string expected)
    {
        Observe();
        string actual = mix.CurrentMusic != null ? mix.CurrentMusic.name : null;
        if (actual != expected) throw new Exception(step + ": expected " + expected + ", got " + actual);
        var playing = Sources().Where(s => s.isPlaying).ToArray();
        if (expected != null && playing.Length != 1) throw new Exception(step + ": no playing BGM");
        var buffer = new float[1024];
        float peak = 0;
        foreach (var source in playing)
        {
            source.GetOutputData(buffer, 0);
            foreach (float value in buffer) peak = Math.Max(peak, Mathf.Abs(value));
        }
        steps.Add(new { step, state = game.State.ToString(), actual, playing = playing.Length,
            looping = playing.All(s => s.loop), volume = playing.Length == 1 ? playing[0].volume : 0,
            outputPeak = peak, inEncounter = game.InEncounter });
    }
}
