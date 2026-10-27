using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Cheon.Prototype.MountainPass;

public static class Test_ShockwaveVerification
{
    static async Task Until(Func<bool> condition,float seconds=12)
    {
        float limit=Time.realtimeSinceStartup+seconds;
        while(!condition()){if(Time.realtimeSinceStartup>limit)throw new Exception("Timed out");await Task.Delay(10);}
    }
    public static async Task<object> Run()
    {
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        game.BeginBossChallenge();await Until(()=>!game.IsTransitioning);
        game.Entrance.SkipPresentation();await Until(()=>game.IsRunning);
        game.PlayerHealth.Invulnerable=true;
        var boss=(PassBossPatternController)game.Boss;
        var effects=game.Audio.GetComponents<AudioSource>()[0];
        var phases=new List<string>();
        float peak=0;int impacts=0;float impactTime=-100;
        var previous=boss.Phase;
        float end=Time.realtimeSinceStartup+9;
        float[] samples=new float[1024];
        while(Time.realtimeSinceStartup<end)
        {
            await Task.Delay(10);
            if(boss.Phase!=previous)
            {
                previous=boss.Phase;phases.Add(previous.ToString());
                if(previous==PassBossPatternController.PatternPhase.JumpRecovery ||
                    previous==PassBossPatternController.PatternPhase.FallHold ||
                    previous==PassBossPatternController.PatternPhase.CrackActive)
                {impacts++;impactTime=Time.realtimeSinceStartup;}
            }
            if(Time.realtimeSinceStartup-impactTime<.5f)
            {effects.GetOutputData(samples,0);peak=Mathf.Max(peak,samples.Max(Mathf.Abs));}
        }
        var result=new {naturalImpacts=impacts,impactOutputPeak=peak,phases,
            passed=impacts>0 && peak>.001f && peak<.98f};
        File.WriteAllText("Assets/Temp/FoleyRefinement~/ShockwavePlayVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
