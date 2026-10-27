using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Test_AudioVerification
{
    static PassGameFlow game;
    static Test_TraditionalSoundscape mix;
    static AudioSource[] sources;
    static void Load()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        game = Object.FindFirstObjectByType<PassGameFlow>();
        mix = Object.FindFirstObjectByType<Test_TraditionalSoundscape>();
        sources = game.Audio.GetComponents<AudioSource>();
    }
    static async Task Until(Func<bool> condition, float limit = 8f)
    {
        float end = Time.realtimeSinceStartup + limit;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > end) throw new Exception("실행 조건 시간 초과");
            await Task.Delay(20);
        }
    }
    static async Task Seconds(float duration)
    {
        float until = Time.realtimeSinceStartup + duration;
        await Until(() => Time.realtimeSinceStartup >= until, duration + 3f);
    }
    static object Finish(string name, Dictionary<string, object> data)
    {
        var failures = data.Where(x => x.Value is bool && !(bool)x.Value).Select(x => x.Key).ToArray();
        data["passed"] = failures.Length == 0;
        File.WriteAllText("Assets/Temp/TraditionalAudio~/" + name + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
        if (failures.Length > 0) throw new Exception("실패: " + string.Join(", ", failures));
        return data;
    }
    public static async Task<object> JourneyAndPause()
    {
        Load();
        int frame = Time.frameCount;
        game.BeginJourney();
        await Until(() => game.IsRunning);
        await Seconds(2.6f);
        var explore = GameObject.Find("Test_ExplorationMusic").GetComponent<AudioSource>();
        float normal = mix.ExplorationLevel;
        var result = new Dictionary<string, object> {
            {"framesAdvance", Time.frameCount > frame + 10},
            {"explorationOnly", mix.MusicPlaying && normal > .3f && mix.CombatLevel < .001f},
            {"singleListener", Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.enabled) == 1}
        };
        // 루프 끝에서 실제 재생 커서가 처음으로 돌아오고 소스가 중단되지 않는지 확인한다.
        explore.time = explore.clip.length - .2f;
        await Seconds(.6f);
        result["explorationLoop"] = explore.isPlaying && explore.time < 1.5f;
        var pause = Object.FindFirstObjectByType<Test_PausePresentation>();
        var hud = Object.FindFirstObjectByType<PassHud>();
        var panel = hud.transform.Find("MountainPassHUD/Result").GetComponent<Image>();
        var fade = hud.transform.Find("MountainPassHUD/ScreenFade").GetComponent<Image>();
        game.Audio.Slam(); game.Audio.Footstep(true);
        float before = explore.time;
        pause.TogglePauseImmediately();
        result["immediatePause"] = game.State == PassGameFlow.RunState.Paused && !game.IsTransitioning;
        await Seconds(.55f);
        result["blackPanel70"] = panel.color == new Color(0, 0, 0, .7f) && fade.color.a < .001f;
        result["pauseMusicQuiet"] = mix.ExplorationLevel > .08f && mix.ExplorationLevel < normal * .31f;
        result["pauseStopsGameplayTails"] = !sources[0].isPlaying && !sources[1].isPlaying;
        result["musicContinuesDuringPause"] = explore.isPlaying && explore.time > before;
        pause.TogglePauseImmediately();
        await Seconds(.8f);
        result["resumeMusic"] = mix.ExplorationLevel > normal * .97f && Time.timeScale == 1f;
        // 비활성화/활성화 후 소스가 중복 생성되지 않는지도 확인한다.
        mix.enabled = false;
        await Seconds(.1f);
        mix.enabled = true;
        await Seconds(.3f);
        result["reenableNoDuplicateMusic"] = mix.MusicPlaying && mix.GetComponentsInChildren<AudioSource>().Count(s => s.name == "Test_ExplorationMusic") == 1;
        float peak = 0;
        var output = new float[1024];
        var methods = new Action[] { () => game.Audio.Swing(1), () => game.Audio.Strike(), () => game.Audio.Strike(true),
            game.Audio.Hurt, game.Audio.Slam, game.Audio.Bell, game.Audio.Dodge, game.Audio.Drink,
            game.Audio.Confirm, game.Audio.Spirit, game.Audio.Victory, game.Audio.Defeat, game.Audio.Equip };
        int audible = 0;
        var levels = new List<float>();
        foreach (var method in methods)
        {
            sources[0].Stop(); method();
            float effectPeak = 0;
            for (int sample = 0; sample < 12; sample++)
            {
                await Seconds(.025f);
                sources[0].GetOutputData(output, 0);
                effectPeak = Mathf.Max(effectPeak, output.Max(v => Mathf.Abs(v)));
                AudioListener.GetOutputData(output, 0);
                peak = Mathf.Max(peak, output.Max(v => Mathf.Abs(v)));
            }
            if (effectPeak > .00001f) audible++;
            levels.Add(effectPeak);
        }
        result["effectPeakLevels"] = levels;
        result["audibleEffectMethods"] = audible;
        result["allEffectsOutputSignal"] = audible == methods.Length;
        result["sampledMixPeak"] = peak;
        result["sampledMixHasHeadroom"] = peak > .0001f && peak < .98f;
        sources[0].Stop();
        return Finish("Test_JourneyAudioVerification", result);
    }
    public static async Task<object> Battle()
    {
        Load();
        game.BeginBossChallenge();
        await Until(() => !game.IsTransitioning);
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        await Until(() => game.IsRunning && game.InEncounter);
        game.PlayerHealth.Invulnerable = true;
        var boss = (PassBossPatternController)game.Boss;
        boss.enabled = false;
        await Seconds(3.2f);
        var battle = GameObject.Find("Test_CombatMusic").GetComponent<AudioSource>();
        var result = new Dictionary<string, object> {
            {"combatOnly", mix.CombatLevel > .3f && mix.ExplorationLevel < .001f && battle.isPlaying}
        };
        battle.time = battle.clip.length - .2f;
        await Seconds(.6f);
        result["combatLoop"] = battle.isPlaying && battle.time < 1.5f;
        boss.Health.Invulnerable = false;
        boss.Health.TakeDamage(1, false, true);
        await Seconds(.1f);
        result["impactDucksMusic"] = mix.ImpactDuck < .8f;
        await Seconds(.7f);
        result["musicRecoversAfterImpact"] = mix.ImpactDuck > .99f;
        var cues = Object.FindFirstObjectByType<Test_BossAudioCues>();
        int count = cues.PlayedCueCount;
        boss.enabled = true;
        await Until(() => cues.PlayedCueCount > count, 15f);
        result["naturalBossWarning"] = cues.LastCue != null;
        result["lastWarning"] = cues.LastCue.name;
        await Seconds(.12f);
        result["warningDucksMusic"] = mix.ImpactDuck < .85f;
        boss.enabled = false;
        var pause = Object.FindFirstObjectByType<Test_PausePresentation>();
        pause.TogglePauseImmediately();
        await Seconds(.15f);
        result["pauseStopsWarning"] = !GameObject.Find("Test_BossWarningAudio").GetComponent<AudioSource>().isPlaying;
        pause.TogglePauseImmediately();
        await Seconds(.8f);
        boss.Health.Kill();
        await Until(() => game.State == PassGameFlow.RunState.Victory && !game.IsTransitioning);
        await Seconds(.9f);
        result["endingStopsLoopMusic"] = !mix.MusicPlaying;
        game.RetryCheckpoint();
        await Until(() => !game.IsTransitioning);
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        await Until(() => game.IsRunning);
        await Seconds(1.2f);
        result["retryRestartsMusic"] = mix.MusicPlaying;
        return Finish("Test_BattleAudioVerification", result);
    }
}
