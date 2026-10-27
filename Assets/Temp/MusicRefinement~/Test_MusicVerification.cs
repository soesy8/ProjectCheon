using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_MusicVerification
{
    static AudioSource calm,battle;
    static int simultaneous,sampledFrames,lastFrame;
    static void Observe()
    {
        if(lastFrame==Time.frameCount)return;
        lastFrame=Time.frameCount;sampledFrames++;
        if(calm.isPlaying && battle.isPlaying)simultaneous++;
    }
    static async Task Until(Func<bool> check,float limit=10)
    {
        float end=Time.realtimeSinceStartup+limit;
        while(!check()){Observe();if(Time.realtimeSinceStartup>end)throw new Exception("Timed out");await Task.Delay(5);}
        Observe();
    }
    static async Task Seconds(float duration)
    {
        float end=Time.realtimeSinceStartup+duration;
        await Until(()=>Time.realtimeSinceStartup>=end,duration+2);
    }
    static async Task<float> Output(AudioSource source)
    {
        float peak=0;float[] samples=new float[1024];source.time=0;
        for(int i=0;i<25;i++){await Seconds(.02f);source.GetOutputData(samples,0);peak=Mathf.Max(peak,samples.Max(Mathf.Abs));}
        return peak;
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var mix=UnityEngine.Object.FindFirstObjectByType<Test_TraditionalSoundscape>();
        var pause=UnityEngine.Object.FindFirstObjectByType<Test_PausePresentation>();
        calm=GameObject.Find("Test_ExplorationMusic").GetComponent<AudioSource>();
        battle=GameObject.Find("Test_CombatMusic").GetComponent<AudioSource>();
        simultaneous=sampledFrames=0;lastFrame=-1;
        var data=new Dictionary<string,object>();
        game.BeginJourney();await Until(()=>game.IsRunning);await Seconds(.6f);
        data["explorationOnly"]=calm.isPlaying && !battle.isPlaying;
        data["calmOutputPeak"]=await Output(calm);
        calm.time=calm.clip.length-.15f;await Seconds(.55f);
        data["calmLoop"]=calm.isPlaying && calm.time<1;
        data["calmWholeBars"]=Mathf.Abs(calm.clip.length-48)<.0001f;
        game.BeginBossChallenge();
        await Until(()=>battle.isPlaying);
        data["bossStartsAtBeginning"]=battle.time<.2f;
        await Until(()=>!game.IsTransitioning);game.Entrance.SkipPresentation();await Until(()=>game.IsRunning);
        game.PlayerHealth.Invulnerable=true;game.Boss.enabled=false;await Seconds(.9f);
        data["combatOnly"]=battle.isPlaying && !calm.isPlaying;
        data["battleOutputPeak"]=await Output(battle);
        battle.time=battle.clip.length-.15f;await Seconds(.55f);
        data["battleLoop"]=battle.isPlaying && battle.time<1;
        data["battleWholeBars"]=Mathf.Abs(battle.clip.length-64*60f/144)<.0001f;
        float level=mix.CombatLevel,position=battle.time;
        pause.TogglePauseImmediately();await Seconds(.6f);
        data["pauseKeepsOnlyBossMusic"]=battle.isPlaying && !calm.isPlaying && battle.time>position && mix.CombatLevel<level*.32f;
        pause.TogglePauseImmediately();await Seconds(.8f);
        data["resumeMusicLevel"]=mix.CombatLevel>level*.95f;
        game.Boss.Health.Kill();await Until(()=>game.State==PassGameFlow.RunState.Victory && !game.IsTransitioning);await Seconds(1.0f);
        data["victoryStopsMusic"]=!calm.isPlaying && !battle.isPlaying;
        game.RetryCheckpoint();await Until(()=>!game.IsTransitioning);game.Entrance.SkipPresentation();await Until(()=>game.IsRunning);
        game.PlayerHealth.Invulnerable=true;game.Boss.enabled=false;await Seconds(.8f);
        data["retryRestartsBossOnly"]=battle.isPlaying && !calm.isPlaying;
        game.BeginJourney();await Until(()=>game.IsRunning && calm.isPlaying);await Seconds(.5f);
        data["returnToExplorationOnly"]=calm.isPlaying && !battle.isPlaying;
        mix.enabled=false;await Seconds(.1f);
        data["disableStopsMusic"]=!calm.isPlaying && !battle.isPlaying;
        mix.enabled=true;await Seconds(.4f);
        data["reenableNoDuplicates"]=calm.isPlaying && !battle.isPlaying &&
            UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s=>s.name=="Test_ExplorationMusic" || s.name=="Test_CombatMusic")==2;
        data["observedFrames"]=sampledFrames;data["simultaneousMusicFrames"]=simultaneous;
        data["neverOverlap"]=simultaneous==0 && sampledFrames>100;
        data["bothTracksAudible"]=(float)data["calmOutputPeak"]>.0001f && (float)data["battleOutputPeak"]>.0001f;
        var failures=data.Where(p=>p.Value is bool && !(bool)p.Value).Select(p=>p.Key).ToArray();
        data["passed"]=failures.Length==0;data["failures"]=failures;
        File.WriteAllText("Assets/Temp/MusicRefinement~/PlayVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));
        return data;
    }
}
