using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_MusicOverlapProbe
{
    static async Task Until(Func<bool> condition)
    {
        float end=Time.realtimeSinceStartup+10;
        while(!condition()){if(Time.realtimeSinceStartup>end)throw new Exception("Timeout");await Task.Delay(10);}
    }
    public static async Task<object> Run()
    {
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var mix=UnityEngine.Object.FindFirstObjectByType<Test_TraditionalSoundscape>();
        game.BeginJourney();await Until(()=>game.IsRunning);await Task.Delay(800);
        int overlap=0,samples=0;float maxBoth=0;
        game.BeginBossChallenge();
        float end=Time.realtimeSinceStartup+3.5f;
        while(Time.realtimeSinceStartup<end)
        {
            await Task.Delay(10);samples++;
            if(mix.ExplorationLevel>.001f && mix.CombatLevel>.001f) overlap++;
            maxBoth=Mathf.Max(maxBoth,Mathf.Min(mix.ExplorationLevel,mix.CombatLevel));
        }
        var result=new {overlappingSamples=overlap,samples,maximumQuieterTrackLevel=maxBoth};
        File.WriteAllText("Assets/Temp/MusicRefinement~/OverlapBefore.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
